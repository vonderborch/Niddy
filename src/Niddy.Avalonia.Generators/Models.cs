using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Niddy.Avalonia.Generators;

/// <summary>
///     An immutable array compared by its elements, so the models holding it are cached by the incremental pipeline.
/// </summary>
internal readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items = items;

    public ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    public int Length => Items.Length;

    public bool Equals(EquatableArray<T> other) => Items.SequenceEqual(other.Items);

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in Items)
            hash = unchecked(hash * 31 + (item?.GetHashCode() ?? 0));
        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal static class EquatableArray
{
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> items) where T : IEquatable<T> =>
        new(items.ToImmutableArray());
}

/// <summary>A source location without the syntax tree, so it can be cached.</summary>
internal sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public static LocationInfo? From(SyntaxNode? node) => node is null ? null : From(node.GetLocation());

    public static LocationInfo? From(Location location) =>
        location.SourceTree is null ? null : new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);

    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
}

/// <summary>A diagnostic to report, with its message arguments as strings so it can be cached.</summary>
internal sealed record DiagnosticInfo(DiagnosticDescriptor Descriptor, LocationInfo? Location, EquatableArray<string> Arguments)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] arguments) =>
        new(descriptor, location, arguments.ToEquatableArray());

    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(Descriptor, Location?.ToLocation(), Arguments.Items.ToArray<object?>());
}

/// <summary>A registered page class.</summary>
/// <param name="PageType">The page's fully qualified type name, e.g. <c>global::MyApp.HomePage</c>.</param>
/// <param name="DisplayName">The display name as a C# expression, or <c>null</c>.</param>
/// <param name="KeepAlive">Whether the page is kept alive.</param>
/// <param name="Parents">The fully qualified type names of the pages it names as parents.</param>
/// <param name="Children">The fully qualified type names of the pages it names as children.</param>
/// <param name="TopLevel">Whether it is top-level as a C# expression: <c>true</c>, <c>false</c> or <c>null</c>.</param>
/// <param name="Icon">The page's icon as a C# expression, or <c>null</c>.</param>
/// <param name="Shortcut">The shortcut as a C# string literal, or <c>null</c>.</param>
/// <param name="Location">The attribute's location, for diagnostics.</param>
/// <param name="SortKey">Where the attribute is declared, so pages are registered in declaration order.</param>
internal sealed record PageInfo(
    string PageType,
    string DisplayName,
    bool KeepAlive,
    EquatableArray<string> Parents,
    EquatableArray<string> Children,
    string TopLevel,
    string Icon,
    string Shortcut,
    LocationInfo? Location,
    string SortKey
);

/// <summary>What the generator found on one page class: its valid registrations and any problems.</summary>
internal sealed record PageClassResult(EquatableArray<PageInfo> Pages, EquatableArray<DiagnosticInfo> Diagnostics);

/// <summary>What the generator needs to know about the compilation itself.</summary>
/// <param name="ReferencesNiddy">Whether the compilation references Niddy.Avalonia, without which nothing is generated.</param>
/// <param name="ProviderName">The name of the provider class to generate, unique to the assembly.</param>
/// <param name="ReferencedProviders">The fully qualified provider types of referenced assemblies with pages.</param>
internal sealed record CompilationInfo(bool ReferencesNiddy, string ProviderName, EquatableArray<string> ReferencedProviders);
