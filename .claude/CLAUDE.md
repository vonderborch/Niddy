# Niddy

Personal .NET library: `Niddy.Core` (UI-independent helpers), `Niddy.Avalonia` (Avalonia app building blocks) and `Niddy.Avalonia.Generators` (a source generator for `[PageRegistration]`, shipped as its own package).

**Read the `## Development` section of the root `README.md` before changing anything.** It holds the layout, build and test commands, the warning policy, code conventions and the documentation rules, and they all apply here.

## Docs: always keep them in sync

Any change to the public API or to documented behavior must update the docs in the same change, following the Documentation rules in `README.md`. Run `/update-docs` when in doubt.

**Anything new gets documented.** Whenever you create a public class, record, struct, interface, enum, delegate, attribute, or any other type, document it in the same change: a `docs/<Project>/<Folder>/<Type>.md` page (or a section on the owning class's page for small supporting types), an entry in `docs/README.md`, and a `docs/AI-GUIDE.md` update if it affects how the library is used. New public members on existing types go on that type's page. Don't finish a task, or report it as done, with an undocumented public type; if you notice an existing one that's undocumented, flag it to the user.

Before documenting a member, read its signature and XML docs. Compile-check non-trivial examples mentally against the real API: generic arguments, parameter names, return types, which thread things run on, what happens on mobile/browser.

## Rules

- Never commit or push without explicit confirmation from the user.
- Don't remove package references from the `.csproj` files unless asked; some are there for upcoming work.
- Don't touch `.claude/settings.local.json`.
- All packages share one version, set by CI from the release tag. Local builds are `0.0.0-dev`.
