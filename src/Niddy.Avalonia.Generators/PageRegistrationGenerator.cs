using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Niddy.Avalonia.Generators;

/// <summary>
///     Generates an <c>IPageProvider</c> for every assembly with <c>[PageRegistration]</c> pages, marked with an
///     assembly-level <c>[PageProvider]</c> attribute so the page registry finds it when the assembly loads. The
///     provider also includes the providers of referenced assemblies, so their pages are registered even before
///     those assemblies load.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class PageRegistrationGenerator : IIncrementalGenerator
{
    private const string NiddyAvaloniaAssemblyName = "Niddy.Avalonia";
    private const string PageSystemNamespace = "Niddy.Avalonia.PageSystem";
    private const string RegistrationAttributeName = PageSystemNamespace + ".PageRegistrationAttribute";
    private const string IconAttributeName = PageSystemNamespace + ".PageIconAttribute";
    private const string ProviderAttributeName = PageSystemNamespace + ".PageProviderAttribute";
    private const string PageTypeName = PageSystemNamespace + ".Page";
    private const string RegistryTypeName = PageSystemNamespace + ".PageRegistry";
    private const string GeneratedNamespace = "Niddy.Generated";

    private static readonly SymbolDisplayFormat FullyQualified = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var pageClasses = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegistrationAttributeName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, ct) => ReadPageClass(ctx, ct)
        );

        // Icons on classes without [PageRegistration] would be silently ignored.
        var unregisteredIcons = context.SyntaxProvider.ForAttributeWithMetadataName(
            IconAttributeName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => ReadUnregisteredIcon(ctx)
        ).Where(static diagnostic => diagnostic is not null);

        var compilationInfo = context.CompilationProvider.Select(static (compilation, ct) => ReadCompilation(compilation, ct));

        context.RegisterSourceOutput(pageClasses.Collect().Combine(compilationInfo), static (spc, input) => Emit(spc, input.Left, input.Right));
        context.RegisterSourceOutput(unregisteredIcons.Combine(compilationInfo), static (spc, input) =>
        {
            if (input.Right.ReferencesNiddy)
                spc.ReportDiagnostic(input.Left!.ToDiagnostic());
        });
    }

    // ── Reading ─────────────────────────────────────────────────────────────────────

    private static PageClassResult ReadPageClass(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        var type = (INamedTypeSymbol)ctx.TargetSymbol;
        var typeName = type.ToDisplayString(FullyQualified);
        var displayTypeName = type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        var classLocation = LocationInfo.From(((ClassDeclarationSyntax)ctx.TargetNode).Identifier.GetLocation());
        var pages = new List<PageInfo>();
        var diagnostics = new List<DiagnosticInfo>();

        var pageBase = ctx.SemanticModel.Compilation.GetTypeByMetadataName(PageTypeName);
        if (pageBase is null || !DerivesFrom(type, pageBase))
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.NotAPage, classLocation, displayTypeName));
            return Result(pages, diagnostics);
        }

        if (WhyNotCreatable(type) is { } reason)
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.CannotCreatePage, classLocation, displayTypeName, reason));
            return Result(pages, diagnostics);
        }

        // The attribute can't be repeated, and the compiler reports attributes that don't bind.
        var attribute = ctx.Attributes.FirstOrDefault(a => a.AttributeConstructor is not null);
        if (attribute is null)
            return Result(pages, diagnostics);

        var location = LocationInfo.From(attribute.ApplicationSyntaxReference?.GetSyntax(ct)) ?? classLocation;
        var registrationAttribute = ctx.SemanticModel.Compilation.GetTypeByMetadataName(RegistrationAttributeName);
        string displayNameCode = "null", topLevelCode = "null", shortcutCode = "null";
        var keepAlive = false;
        var parents = new List<string>();
        var children = new List<string>();

        foreach (var argument in attribute.NamedArguments)
        {
            ct.ThrowIfCancellationRequested();
            switch (argument.Key)
            {
                case "DisplayName" when argument.Value.Value is string name:
                    displayNameCode = SymbolDisplay.FormatLiteral(name, quote: true);
                    break;
                case "KeepAlive":
                    keepAlive = argument.Value.Value is true;
                    break;
                case "TopLevel":
                    topLevelCode = argument.Value.Value is true ? "true" : "false";
                    break;
                case "Parents":
                    ReadRelatedPages(argument.Value, "parent", parents);
                    break;
                case "Children":
                    ReadRelatedPages(argument.Value, "child", children);
                    break;
                case "Shortcut" when argument.Value.Value is string shortcut:
                    if (ValidateShortcut(shortcut, ctx.SemanticModel.Compilation) is { } problem)
                    {
                        diagnostics.Add(DiagnosticInfo.Create(Diagnostics.InvalidShortcut, location, displayTypeName, shortcut, problem));
                        return Result(pages, diagnostics);
                    }
                    shortcutCode = SymbolDisplay.FormatLiteral(shortcut, quote: true);
                    break;
            }
        }

        var iconCode = ReadIcon(type, displayTypeName, classLocation, diagnostics, ct);

        var sortKey = location is null
            ? typeName
            : $"{location.FilePath}|{location.Span.Start.ToString("D10", CultureInfo.InvariantCulture)}";
        pages.Add(new PageInfo(typeName, displayNameCode, keepAlive, parents.ToEquatableArray(), children.ToEquatableArray(), topLevelCode, iconCode, shortcutCode, location, sortKey));
        return Result(pages, diagnostics);

        void ReadRelatedPages(TypedConstant array, string relation, List<string> related)
        {
            if (array.Kind != TypedConstantKind.Array || array.IsNull)
                return;

            foreach (var item in array.Values)
            {
                // typeof of a type that doesn't exist is reported by the compiler.
                if (item.Value is IErrorTypeSymbol)
                    continue;

                if (item.Value is not INamedTypeSymbol relatedType || relatedType.IsUnboundGenericType || !DerivesFrom(relatedType, pageBase))
                {
                    var name = item.Value is ITypeSymbol other ? other.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat) : "null";
                    diagnostics.Add(DiagnosticInfo.Create(Diagnostics.RelatedTypeNotAPage, location, displayTypeName, name, relation));
                    continue;
                }

                if (!relatedType.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, registrationAttribute)))
                {
                    var name = relatedType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                    diagnostics.Add(DiagnosticInfo.Create(Diagnostics.RelatedPageNotRegistered, location, displayTypeName, name, relation));
                }

                var relatedName = relatedType.ToDisplayString(FullyQualified);
                if (!related.Contains(relatedName))
                    related.Add(relatedName);
            }
        }
    }

    private static readonly string[] ShortcutModifiers = ["primary", "ctrl", "control", "shift", "alt", "meta", "cmd", "win", "⌘"];

    /// <summary>
    ///     Checks a shortcut the way <c>PageShortcut.Parse</c> will at runtime: modifiers joined by <c>+</c>, then a key
    ///     that is a member of <c>Avalonia.Input.Key</c> or a single character.
    /// </summary>
    /// <returns>Why it's invalid, or null if it's valid.</returns>
    internal static string? ValidateShortcut(string shortcut, Compilation compilation)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return "it's empty";

        // A trailing "++" is the plus key.
        var text = shortcut.Trim();
        string key;
        string[] modifiers;
        if (text.EndsWith("++", StringComparison.Ordinal) || text == "+")
        {
            key = "+";
            var rest = text.Substring(0, text.Length - (text == "+" ? 1 : 2));
            modifiers = rest.Length == 0 ? [] : rest.Split('+');
        }
        else
        {
            var parts = text.Split('+');
            key = parts[parts.Length - 1].Trim();
            modifiers = parts.Take(parts.Length - 1).ToArray();
        }

        foreach (var modifier in modifiers)
        {
            if (!ShortcutModifiers.Contains(modifier.Trim().ToLowerInvariant()))
                return $"'{modifier.Trim()}' isn't a modifier (Primary, Ctrl, Shift, Alt or Meta)";
        }

        if (key.Length == 0)
            return "it has no key";
        if (ShortcutModifiers.Contains(key.ToLowerInvariant()))
            return "it has no key after the modifiers";
        if (key.Length == 1)
            return char.IsLetterOrDigit(key[0]) || "+-.,".IndexOf(key[0]) >= 0 ? null : $"'{key}' isn't a key name";
        return IsKeyName(key, compilation) ? null : $"'{key}' isn't a key name";

        static bool IsKeyName(string name, Compilation compilation)
        {
            // Without Avalonia's Key enum there's nothing to check against, so let the runtime decide.
            if (compilation.GetTypeByMetadataName("Avalonia.Input.Key") is not { } keyType)
                return true;
            return keyType.GetMembers().Any(m => m is IFieldSymbol { IsConst: true } && string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Reads the class's <c>[PageIcon]</c> attributes as a <c>PageIcon</c> expression, or <c>null</c> if it has none.</summary>
    private static string ReadIcon(INamedTypeSymbol type, string displayTypeName, LocationInfo? classLocation, List<DiagnosticInfo> diagnostics, CancellationToken ct)
    {
        var sources = new List<string>();
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != IconAttributeName || attribute.AttributeConstructor is null)
                continue;
            ct.ThrowIfCancellationRequested();

            var data = attribute.ConstructorArguments.Length == 1 ? attribute.ConstructorArguments[0].Value as string : null;
            string? source = null, resourceKey = null;
            var kinds = data is null ? 0 : 1;
            var size = 0d;
            foreach (var argument in attribute.NamedArguments)
            {
                switch (argument.Key)
                {
                    case "Data" when argument.Value.Value is string value:
                        kinds += data is null ? 1 : 2;
                        data = value;
                        break;
                    case "Source" when argument.Value.Value is string value:
                        kinds++;
                        source = value;
                        break;
                    case "ResourceKey" when argument.Value.Value is string value:
                        kinds++;
                        resourceKey = value;
                        break;
                    case "Size" when argument.Value.Value is double value:
                        size = value;
                        break;
                }
            }

            var reason = kinds != 1 ? "it must set exactly one of Data, Source or ResourceKey"
                : size < 0 || double.IsNaN(size) || double.IsInfinity(size) ? "its Size must be 0 (any size) or a positive number"
                : source is not null && !Uri.TryCreate(source, UriKind.Absolute, out _) ? $"its Source '{source}' isn't an absolute URI, e.g. avares://MyApp/Assets/icon.png"
                : data is { Length: 0 } || resourceKey is { Length: 0 } ? "its Data or ResourceKey is empty"
                : null;
            if (reason is not null)
            {
                var location = LocationInfo.From(attribute.ApplicationSyntaxReference?.GetSyntax(ct)) ?? classLocation;
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.InvalidPageIcon, location, displayTypeName, reason));
                continue;
            }

            var (factory, text) = data is not null ? ("FromData", data) : source is not null ? ("FromUri", source) : ("FromResource", resourceKey!);
            var sizeCode = size.ToString("R", CultureInfo.InvariantCulture);
            sources.Add($"global::{PageSystemNamespace}.PageIconSource.{factory}({SymbolDisplay.FormatLiteral(text, quote: true)}, {sizeCode})");
        }

        return sources.Count == 0 ? "null" : $"new global::{PageSystemNamespace}.PageIcon({string.Join(", ", sources)})";
    }

    private static DiagnosticInfo? ReadUnregisteredIcon(GeneratorAttributeSyntaxContext ctx)
    {
        var type = (INamedTypeSymbol)ctx.TargetSymbol;
        if (type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == RegistrationAttributeName))
            return null;
        var location = LocationInfo.From(((ClassDeclarationSyntax)ctx.TargetNode).Identifier.GetLocation());
        return DiagnosticInfo.Create(Diagnostics.PageIconWithoutRegistration, location, type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat));
    }

    private static PageClassResult Result(List<PageInfo> pages, List<DiagnosticInfo> diagnostics) =>
        new(pages.ToEquatableArray(), diagnostics.ToEquatableArray());

    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
                return true;
        }

        return false;
    }

    /// <summary>Why the generated code can't create the page with <c>new</c>, or null if it can.</summary>
    private static string? WhyNotCreatable(INamedTypeSymbol type)
    {
        if (type.IsAbstract)
            return "it is abstract";

        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsGenericType)
                return SymbolEqualityComparer.Default.Equals(current, type) ? "it is generic" : $"its containing type '{current.Name}' is generic";

            if (current.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
                return SymbolEqualityComparer.Default.Equals(current, type) ? "it is private or protected" : $"its containing type '{current.Name}' is private or protected";
        }

        var hasConstructor = type.InstanceConstructors.Any(c =>
            c.Parameters.All(p => p.IsOptional || p.IsParams)
            && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal
        );
        return hasConstructor ? null : "it has no public or internal parameterless constructor";
    }

    private static CompilationInfo ReadCompilation(Compilation compilation, CancellationToken ct)
    {
        var assemblyName = compilation.AssemblyName ?? "Assembly";
        var referencesNiddy = assemblyName != NiddyAvaloniaAssemblyName && compilation.GetTypeByMetadataName(RegistryTypeName) is not null;
        var providerName = "Pages_" + SanitizeIdentifier(assemblyName);
        if (!referencesNiddy)
            return new CompilationInfo(false, providerName, default);

        var providers = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            ct.ThrowIfCancellationRequested();

            // Only assemblies built against Niddy.Avalonia can have pages; this skips the framework cheaply.
            if (!assembly.Modules.Any(m => m.ReferencedAssemblies.Any(r => r.Name == NiddyAvaloniaAssemblyName)))
                continue;

            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == ProviderAttributeName
                    && attribute.ConstructorArguments.Length == 1
                    && attribute.ConstructorArguments[0].Value is INamedTypeSymbol provider
                    && compilation.IsSymbolAccessibleWithin(provider, compilation.Assembly))
                {
                    providers.Add(provider.ToDisplayString(FullyQualified));
                }
            }
        }

        return new CompilationInfo(true, providerName, providers.ToEquatableArray());
    }

    private static string SanitizeIdentifier(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
            builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        return builder.ToString();
    }

    // ── Emitting ────────────────────────────────────────────────────────────────────

    private static void Emit(SourceProductionContext context, ImmutableArray<PageClassResult> classes, CompilationInfo compilation)
    {
        if (!compilation.ReferencesNiddy)
            return;

        foreach (var diagnostic in classes.SelectMany(c => c.Diagnostics))
            context.ReportDiagnostic(diagnostic.ToDiagnostic());

        var pages = classes.SelectMany(c => c.Pages).ToList();
        pages.Sort((a, b) => string.CompareOrdinal(a.SortKey, b.SortKey));

        // A cycle would throw when registering, so report it here and leave its pages out.
        var inCycle = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            if (inCycle.Contains(page.PageType) || FindCycle(page.PageType, pages) is not { } cycle)
                continue;
            inCycle.UnionWith(cycle);
            var path = string.Join(" > ", cycle.Select(Unqualified));
            context.ReportDiagnostic(DiagnosticInfo.Create(Diagnostics.PageCycle, page.Location, Unqualified(page.PageType), path).ToDiagnostic());
        }
        pages.RemoveAll(p => inCycle.Contains(p.PageType));

        if (pages.Count == 0 && compilation.ReferencedProviders.Length == 0)
            return;

        context.AddSource($"{compilation.ProviderName}.g.cs", Generate(compilation, pages));
    }

    /// <summary>Finds a path from <paramref name="start"/> through child pages back to itself.</summary>
    private static List<string>? FindCycle(string start, List<PageInfo> pages)
    {
        var path = new List<string> { start };
        var visited = new HashSet<string>(StringComparer.Ordinal);
        return Visit(start) ? path : null;

        bool Visit(string page)
        {
            foreach (var child in ChildrenOf(page))
            {
                path.Add(child);
                if (child == start || (visited.Add(child) && Visit(child)))
                    return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        IEnumerable<string> ChildrenOf(string page) =>
            pages.Where(p => p.PageType == page).SelectMany(p => p.Children)
                .Concat(pages.Where(p => p.Parents.Contains(page)).Select(p => p.PageType))
                .Distinct();
    }

    private static string Unqualified(string typeName) => typeName.Replace("global::", string.Empty);

    private static string TypeArray(EquatableArray<string> types) =>
        types.Length == 0 ? "null" : $"new global::System.Type[] {{ {string.Join(", ", types.Select(t => $"typeof({t})"))} }}";

    private static string Generate(CompilationInfo compilation, List<PageInfo> pages)
    {
        var provider = $"global::{GeneratedNamespace}.{compilation.ProviderName}";
        var registry = $"global::{RegistryTypeName}";
        var version = typeof(PageRegistrationGenerator).Assembly.GetName().Version?.ToString() ?? "1.0.0.0";

        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/>");
        code.AppendLine("#nullable enable");
        code.AppendLine();
        code.AppendLine($"[assembly: global::{ProviderAttributeName}(typeof({provider}))]");
        code.AppendLine();
        code.AppendLine($"namespace {GeneratedNamespace}");
        code.AppendLine("{");
        code.AppendLine("    /// <summary>Registers this assembly's pages, and those of the assemblies it references. Generated by Niddy.Avalonia.Generators.</summary>");
        code.AppendLine("    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        code.AppendLine($"    [global::System.CodeDom.Compiler.GeneratedCode(\"Niddy.Avalonia.Generators\", \"{version}\")]");
        code.AppendLine($"    public sealed class {compilation.ProviderName} : global::{PageSystemNamespace}.IPageProvider");
        code.AppendLine("    {");
        code.AppendLine("        /// <inheritdoc />");
        code.AppendLine("        public void RegisterPages()");
        code.AppendLine("        {");

        foreach (var referenced in compilation.ReferencedProviders)
            code.AppendLine($"            {registry}.Include<{referenced}>();");

        foreach (var page in pages)
        {
            code.Append($"            {registry}.Register<{page.PageType}>(factory: static () => new {page.PageType}()");
            code.Append($", displayName: {page.DisplayName}, keepAlive: {(page.KeepAlive ? "true" : "false")}");
            code.Append($", parents: {TypeArray(page.Parents)}, children: {TypeArray(page.Children)}, topLevel: {page.TopLevel}, icon: {page.Icon}");
            if (page.Shortcut != "null")
                code.Append($", shortcut: {page.Shortcut}");
            code.AppendLine(");");
        }

        code.AppendLine("        }");
        code.AppendLine("    }");
        code.AppendLine("}");
        return code.ToString();
    }
}
