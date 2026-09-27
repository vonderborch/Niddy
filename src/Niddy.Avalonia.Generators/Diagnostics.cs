using Microsoft.CodeAnalysis;

namespace Niddy.Avalonia.Generators;

internal static class Diagnostics
{
    private const string Category = "Niddy.PageSystem";

    public static readonly DiagnosticDescriptor PageCycle = new(
        "NIDDY001",
        "Page is its own ancestor",
        "Page '{0}' is its own ancestor ({1}); none of these pages are registered",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Parents and Children of pages must form a tree without cycles."
    );

    public static readonly DiagnosticDescriptor NotAPage = new(
        "NIDDY002",
        "Registered class isn't a page",
        "'{0}' has [PageRegistration] but doesn't derive from Niddy.Avalonia.PageSystem.Page, so it isn't registered",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor CannotCreatePage = new(
        "NIDDY003",
        "Page can't be created",
        "Page '{0}' can't be registered automatically because {1}. Register it with PageRegistry.Register and a factory instead.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor RelatedTypeNotAPage = new(
        "NIDDY004",
        "Parent or child isn't a page",
        "'{0}' lists '{1}' as a {2}, but it isn't a page",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor RelatedPageNotRegistered = new(
        "NIDDY005",
        "Parent or child page isn't registered",
        "'{0}' lists '{1}' as a {2}, but '{1}' has no [PageRegistration]; the link only takes effect if '{1}' is registered with PageRegistry.Register",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor InvalidPageIcon = new(
        "NIDDY006",
        "Invalid page icon",
        "[PageIcon] on '{0}' is ignored because {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor PageIconWithoutRegistration = new(
        "NIDDY007",
        "Page icon on an unregistered class",
        "'{0}' has [PageIcon] but no [PageRegistration], so the icon isn't used; pass it to PageRegistry.Register instead",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor InvalidShortcut = new(
        "NIDDY008",
        "Invalid page shortcut",
        "The Shortcut '{1}' of page '{0}' isn't a valid key gesture because {2}, so the page isn't registered (use a gesture like \"Primary+1\" or \"Ctrl+Shift+S\")",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
