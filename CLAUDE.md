# Niddy

Personal .NET library: `Niddy.Core` (UI-independent helpers), `Niddy.Avalonia` (Avalonia app building blocks) and `Niddy.Avalonia.Generators` (a source generator for `[PageRegistration]`, shipped as its own package). .NET 10 / C# 14, Avalonia 12, xUnit v3.

## Layout

```
Niddy.slnx                             solution: /src/ and /tests/ solution folders
src/Niddy.Core/<Folder>/*.cs           namespace Niddy.<Folder>            (e.g. Niddy.Settings)
src/Niddy.Avalonia/<Folder>/*.cs       namespace Niddy.Avalonia.<Folder>
src/Niddy.Avalonia.Generators/         netstandard2.0, packed with its .nuspec
tests/<Project>.Tests/                 one test project per package
tests/Fixtures/                        small page libraries the tests reference or load
docs/                                  documentation (see below)
```

A new Avalonia folder whose types are used from XAML also needs an `XmlnsDefinition` line in `src/Niddy.Avalonia/XmlnsDefinitions.cs` (every folder except `Utilities` is mapped to `https://github.com/vonderborch/Niddy`).

## Docs: always keep them in sync

**Any change to the public API or to documented behavior must update the docs in the same change.** Run `/update-docs` when in doubt.

- `docs/<Project>/<Folder>/<Class>.md`: one page per public class, mirroring the source layout under `src/`. Small supporting types (enums, event args, options records, nested types) go on the page of the class that uses them. Internal types are not documented.
- Page shape: `# Name`, a line with `` `Namespace` · Package · [source](../../../src/<Project>/<Folder>/<Class>.cs) ``, a short summary, `## API` tables, `## Example` / `## Examples`, `## See also`.
- `docs/examples/*.md`: walkthroughs that span several classes. Add one when a feature only makes sense combined with others.
- `docs/README.md`: the index. Add, rename or remove pages there too.
- `docs/AI-GUIDE.md`: a single-file guide to the whole library for AI assistants. Update it when a change affects how the library should be used: new features, changed signatures, new gotchas.
- `README.md` (root): packages, a feature list, a short quick start and links into `docs/`. It is packed into every NuGet package, so its links to docs are absolute GitHub URLs. Keep usage detail in `docs/`, not here.

Write docs from the source, not from memory. Before documenting a member, read its signature and XML docs; compile-check non-trivial examples mentally against the real API (generic arguments, parameter names, return types, which thread things run on, what happens on mobile/browser). Relative links must resolve.

## Build and test

```bash
dotnet build
dotnet test
```

Avalonia tests run headless (`[AvaloniaFact]`); call `Dispatcher.UIThread.RunJobs()` to flush posted work. Generator tests run the generator on in-memory compilations. Add tests with every behavior change.

Known, ignorable warnings: CS1591, CS1734, AVLN3001, and CS1574 in `WindowNotification`.

## Code conventions

- File-scoped namespaces; XML doc comments on every public member (they feed IntelliSense and the docs).
- `System.Threading.Lock` (`private readonly Lock _gate = new();`) for locking.
- Inside `Niddy.Avalonia`, refer to Avalonia types as `global::Avalonia.*` where the `Niddy.Avalonia` namespace would shadow them.
- Match the surrounding code's style and comment density.
- Things that must work off desktop (dialogs, toasts, file pickers, navigation) need a mobile/browser path; don't assume windows exist.

## Rules

- Never commit or push without explicit confirmation from the user.
- Don't remove package references from the `.csproj` files unless asked; some are there for upcoming work.
- Don't touch `.claude/settings.local.json`.
- All packages share one version, set by CI from the release tag. Local builds are `0.0.0-dev`.
