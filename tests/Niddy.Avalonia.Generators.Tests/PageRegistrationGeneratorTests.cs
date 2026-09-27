using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Niddy.Avalonia.PageSystem;

namespace Niddy.Avalonia.Generators.Tests;

public class PageRegistrationGeneratorTests
{
    private const string Usings = "using Niddy.Avalonia.PageSystem;\n";

    // Every assembly the tests run with (the framework, Avalonia and Niddy.Avalonia), as compilation references.
    private static readonly ImmutableArray<MetadataReference> References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    private static readonly ImmutableArray<MetadataReference> ReferencesWithoutNiddy =
        [.. References.Where(r => !Path.GetFileName(r.Display!).StartsWith("Niddy.Avalonia", StringComparison.Ordinal))];

    private sealed record Result(Compilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics, string? Source)
    {
        public ImmutableArray<Diagnostic> Errors =>
            [.. Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)];

        public string[] Ids => [.. GeneratorDiagnostics.Select(d => d.Id)];
    }

    private static Result Run(string source, string assemblyName = "App", IEnumerable<MetadataReference>? references = null)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, path: "Pages.cs")],
            references ?? References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
        );

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new PageRegistrationGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var generated = driver.GetRunResult().GeneratedTrees.SingleOrDefault();
        return new Result(output, diagnostics, generated?.GetText().ToString());
    }

    private static MetadataReference Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    [Fact]
    public void RegistersAttributedPagesInDeclarationOrder()
    {
        var result = Run(Usings + """
            namespace App;
            [PageRegistration(DisplayName = "Start")]
            public sealed class HomePage : Page;
            [PageRegistration(KeepAlive = true, Children = [typeof(GeneralPage)])]
            internal sealed class SettingsPage : Page;
            [PageRegistration(Parents = [typeof(HomePage), typeof(SettingsPage)], TopLevel = true)]
            public sealed class GeneralPage : Page { internal GeneralPage() { } }
            [PageRegistration(TopLevel = false)]
            public sealed class HiddenPage : Page;
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.Errors);
        var source = result.Source!;
        Assert.Contains("[assembly: global::Niddy.Avalonia.PageSystem.PageProviderAttribute(typeof(global::Niddy.Generated.Pages_App))]", source);
        Assert.Contains("Register<global::App.HomePage>(factory: static () => new global::App.HomePage(), displayName: \"Start\", keepAlive: false, parents: null, children: null, topLevel: null, icon: null);", source);
        Assert.Contains("Register<global::App.SettingsPage>(factory: static () => new global::App.SettingsPage(), displayName: null, keepAlive: true, parents: null, children: new global::System.Type[] { typeof(global::App.GeneralPage) }, topLevel: null, icon: null);", source);
        Assert.Contains("Register<global::App.GeneralPage>(factory: static () => new global::App.GeneralPage(), displayName: null, keepAlive: false, parents: new global::System.Type[] { typeof(global::App.HomePage), typeof(global::App.SettingsPage) }, children: null, topLevel: true, icon: null);", source);
        Assert.Contains("Register<global::App.HiddenPage>(factory: static () => new global::App.HiddenPage(), displayName: null, keepAlive: false, parents: null, children: null, topLevel: false, icon: null);", source);
        Assert.True(source.IndexOf("HomePage>", StringComparison.Ordinal) < source.IndexOf("SettingsPage>", StringComparison.Ordinal));
        Assert.True(source.IndexOf("SettingsPage>", StringComparison.Ordinal) < source.IndexOf("GeneralPage>", StringComparison.Ordinal));
    }

    [Fact]
    public void CyclesAreReportedAndNotRegistered()
    {
        var result = Run(Usings + """
            namespace App;
            [PageRegistration(Children = [typeof(B)])] public sealed class A : Page;
            [PageRegistration(Children = [typeof(C)])] public sealed class B : Page;
            [PageRegistration(Children = [typeof(A)])] public sealed class C : Page;
            [PageRegistration(Parents = [typeof(A)])] public sealed class D : Page;
            """);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("NIDDY001", diagnostic.Id);
        Assert.Contains("App.A > App.B > App.C > App.A", diagnostic.GetMessage());
        Assert.DoesNotContain("Register<global::App.A>", result.Source);
        Assert.DoesNotContain("Register<global::App.B>", result.Source);
        Assert.DoesNotContain("Register<global::App.C>", result.Source);
        Assert.Contains("Register<global::App.D>", result.Source);
    }

    [Fact]
    public void ClassesThatArentPagesAreReported()
    {
        var result = Run(Usings + """
            [PageRegistration] public sealed class NotAPage;
            """);

        Assert.Equal(["NIDDY002"], result.Ids);
        Assert.Null(result.Source);
    }

    [Theory]
    [InlineData("[PageRegistration] public abstract class P : Page;", "abstract")]
    [InlineData("[PageRegistration] public sealed class P<T> : Page;", "generic")]
    [InlineData("[PageRegistration] public sealed class P : Page { private P() { } }", "constructor")]
    [InlineData("[PageRegistration] public sealed class P : Page { public P(int value) { } }", "constructor")]
    [InlineData("public class Outer { [PageRegistration] private sealed class P : Page; }", "private")]
    public void PagesThatCantBeCreatedAreReported(string declaration, string reason)
    {
        var result = Run(Usings + declaration);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("NIDDY003", diagnostic.Id);
        Assert.Contains(reason, diagnostic.GetMessage());
        Assert.Null(result.Source);
    }

    [Fact]
    public void ParentsAndChildrenThatArentPagesAreReportedAndSkipped()
    {
        var result = Run(Usings + """
            namespace App;
            public sealed class NotAPage;
            [PageRegistration] public sealed class Home : Page;
            [PageRegistration(Parents = [typeof(NotAPage), typeof(Home)], Children = [null!])] public sealed class A : Page;
            """);

        Assert.Equal(["NIDDY004", "NIDDY004"], result.Ids);
        Assert.Contains("'NotAPage' as a parent", result.GeneratorDiagnostics[0].GetMessage());
        Assert.Contains("parents: new global::System.Type[] { typeof(global::App.Home) }, children: null", result.Source);
    }

    [Fact]
    public void UnregisteredParentsAndChildrenAreWarnedAbout()
    {
        var result = Run(Usings + """
            namespace App;
            public sealed class Manual : Page;
            [PageRegistration(Parents = [typeof(Manual)])] public sealed class A : Page;
            """);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("NIDDY005", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("typeof(global::App.Manual)", result.Source);
    }

    [Fact]
    public void IncludesTheProvidersOfReferencedAssemblies()
    {
        var library = Run(Usings + """
            namespace Lib;
            [PageRegistration] public sealed class LibPage : Page;
            """, assemblyName: "My.Lib");
        Assert.Empty(library.Errors);
        var libraryReference = Emit(library.Output);

        var app = Run(Usings + """
            [PageRegistration] public sealed class AppPage : Page;
            """, references: [.. References, libraryReference]);

        Assert.Empty(app.Errors);
        Assert.Contains("PageRegistry.Include<global::Niddy.Generated.Pages_My_Lib>();", app.Source);
    }

    [Fact]
    public void AssemblyWithoutPagesStillIncludesReferencedProviders()
    {
        var library = Run(Usings + """
            [PageRegistration] public sealed class LibPage : Page;
            """, assemblyName: "Lib");
        var libraryReference = Emit(library.Output);

        var app = Run("public static class Program;", references: [.. References, libraryReference]);

        Assert.Empty(app.Errors);
        Assert.Contains("Include<global::Niddy.Generated.Pages_Lib>", app.Source);
        Assert.DoesNotContain("Register<", app.Source);
    }

    [Fact]
    public void GeneratesNothingWithoutPages()
    {
        Assert.Null(Run("public static class Program;").Source);
    }

    [Fact]
    public void GeneratesNothingWithoutNiddyAvalonia()
    {
        var result = Run("public static class Program;", references: ReferencesWithoutNiddy);

        Assert.Null(result.Source);
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void IconsAreRegisteredWithEverySize()
    {
        var result = Run(Usings + """
            namespace App;
            [PageRegistration, PageIcon("M0,0 L16,16", Size = 16), PageIcon(Data = "M0,0 L24,24", Size = 24.5)]
            public sealed class DataPage : Page;
            [PageRegistration, PageIcon(Source = "avares://App/Assets/home.png")]
            public sealed class ImagePage : Page;
            [PageRegistration, PageIcon(ResourceKey = "HomeIcon", Size = 20)]
            public sealed class ResourcePage : Page;
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.Errors);
        const string Icon = "new global::Niddy.Avalonia.PageSystem.PageIcon(";
        const string Source = "global::Niddy.Avalonia.PageSystem.PageIconSource.";
        Assert.Contains($"icon: {Icon}{Source}FromData(\"M0,0 L16,16\", 16), {Source}FromData(\"M0,0 L24,24\", 24.5)));", result.Source);
        Assert.Contains($"icon: {Icon}{Source}FromUri(\"avares://App/Assets/home.png\", 0)));", result.Source);
        Assert.Contains($"icon: {Icon}{Source}FromResource(\"HomeIcon\", 20)));", result.Source);
    }

    [Theory]
    [InlineData("[PageIcon]", "exactly one of Data, Source or ResourceKey")]
    [InlineData("[PageIcon(\"M0,0\", ResourceKey = \"Key\")]", "exactly one of Data, Source or ResourceKey")]
    [InlineData("[PageIcon(\"M0,0\", Size = -1)]", "Size must be 0")]
    [InlineData("[PageIcon(Source = \"Assets/icon.png\")]", "isn't an absolute URI")]
    [InlineData("[PageIcon(\"\")]", "is empty")]
    public void InvalidIconsAreReportedAndSkipped(string attribute, string reason)
    {
        var result = Run(Usings + $$"""
            namespace App;
            [PageRegistration, PageIcon("M0,0 L1,1", Size = 16)]
            {{attribute}}
            public sealed class P : Page;
            """);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("NIDDY006", diagnostic.Id);
        Assert.Contains(reason, diagnostic.GetMessage());
        Assert.Empty(result.Errors);
        Assert.Contains("FromData(\"M0,0 L1,1\", 16)));", result.Source);
    }

    [Fact]
    public void IconsWithoutARegistrationAreWarnedAbout()
    {
        var result = Run(Usings + """
            [PageIcon("M0,0 L1,1")] public sealed class Manual : Page;
            """);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("NIDDY007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Null(result.Source);
    }

    [Fact]
    public void GeneratedProviderRegistersPagesAtRuntime()
    {
        var result = Run(Usings + """
            namespace Runtime;
            [PageRegistration(DisplayName = "Generated"), PageIcon("M0,0 L1,1", Size = 16), PageIcon("M0,0 L2,2")]
            public sealed class GeneratedPage : Page;
            """, assemblyName: "Runtime.Pages");
        Assert.Empty(result.Errors);

        using var stream = new MemoryStream();
        Assert.True(result.Output.Emit(stream, cancellationToken: TestContext.Current.CancellationToken).Success);
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var provider = (IPageProvider)Activator.CreateInstance(assembly.GetType("Niddy.Generated.Pages_Runtime_Pages")!)!;
        provider.RegisterPages();

        var registration = PageRegistry.Find(PageId.Of(assembly.GetType("Runtime.GeneratedPage")!));
        Assert.NotNull(registration);
        Assert.Equal("Generated", registration.DisplayName);
        Assert.Equal(
            new PageIcon(PageIconSource.FromData("M0,0 L1,1", 16), PageIconSource.FromData("M0,0 L2,2")),
            registration.Icon
        );
    }
    [Fact]
    public void ShortcutsAreRegistered()
    {
        var result = Run(Usings + """
            namespace App;
            [PageRegistration(Shortcut = "Primary+1")] public sealed class Home : Page;
            [PageRegistration(Shortcut = "Ctrl+Shift+F5")] public sealed class Settings : Page;
            [PageRegistration] public sealed class About : Page;
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.Errors);
        Assert.Contains(", shortcut: \"Primary+1\");", result.Source);
        Assert.Contains(", shortcut: \"Ctrl+Shift+F5\");", result.Source);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(result.Source!, "shortcut:"));
    }

    [Theory]
    [InlineData("Ctrl+Nope", "Nope")]
    [InlineData("Hyper+S", "Hyper")]
    [InlineData("Ctrl+", "no key")]
    [InlineData("", "empty")]
    public void InvalidShortcutsAreReportedAndSkipped(string shortcut, string reason)
    {
        var result = Run(Usings + $$"""
            namespace App;
            [PageRegistration(Shortcut = "{{shortcut}}")] public sealed class Bad : Page;
            [PageRegistration] public sealed class Good : Page;
            """);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("NIDDY008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains(reason, diagnostic.GetMessage());
        Assert.DoesNotContain("App.Bad", result.Source);
        Assert.Contains("App.Good", result.Source);
    }
}
