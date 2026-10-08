---
description: Bring docs/ (and README.md, AI-GUIDE.md) in sync with the source
argument-hint: "[class, folder or project to focus on; defaults to what changed]"
---

Sync the Niddy documentation with the source code. Follow the Documentation rules in the `## Development` section of the root `README.md`, and `.claude/CLAUDE.md`.

Scope: $ARGUMENTS
If no scope is given, use what changed: `git status`, `git diff` and `git diff --cached` (or, with no git history, compare the public types in the source against the pages in `docs/`).

**Every public type must be documented: classes, records, structs, interfaces, enums, delegates, attributes, anything.** No type is too small or too "helper"-like to skip. A run of this command isn't done while any public type in scope lacks docs.

1. **Find the public API in scope.** For each public type, read its source and XML docs. Note new, removed and renamed types and members, changed signatures and defaults, and changed behavior (threading, platform differences, exceptions).
2. **Check for undocumented types.** List every public type in scope and confirm each one has a page, or a section on its owner's page. With no scope given, check the whole source, not just what changed, e.g.:
   ```bash
   grep -rhoE 'public (sealed |static |abstract |partial |readonly |ref )*(class|record|struct|interface|enum|delegate [^ ]+) [A-Za-z0-9_]+' src | awk '{print $NF}' | sort -u
   ```
   then search `docs/` for each name, as a whole word (and without the `Attribute` suffix for attributes, which docs usually write as `[Name]`). Document every gap you find, whether or not it was part of this change.
3. **Update type pages** in `docs/<Project>/<Folder>/<Type>.md` (folders nest like the namespaces, e.g. `docs/Niddy.Core/IO/Archives/Archive.md`; a type in a project's root namespace goes in `docs/<Project>/`):
   - Create a page for each new public type, using the existing page shape (header line with namespace, package and source link; summary; `## API`; examples; `## See also`). Small supporting types (enums, event args, options records, nested types) go on their owner's page as a `###` section.
   - Add new public members of existing types to that type's page. C# 14 extension members go on their `<Target>Extensions` class's page, written the way they're called (`Directory.DeleteWithRetry(...)`, `stream.ReadToBytes()`); the type grep above doesn't list them, so check the `extension(...)` blocks too.
   - Fix API tables and examples for changed members. Delete pages for removed types, and fix every link to them.
4. **Update walkthroughs** in `docs/examples/` that use anything that changed. Add a new walkthrough if a new feature spans several classes.
5. **Update `docs/README.md`** (the index) for any added, removed or renamed page. Every new page gets an entry.
6. **Update `docs/AI-GUIDE.md`** for any new type that changes how the library is used, and if usage, signatures, defaults or gotchas changed.
7. **Update the root `README.md`** only if the feature list or the quick start is affected (a new feature area usually is). Keep its docs links absolute (`https://github.com/vonderborch/Niddy/blob/main/docs/...`).
8. **Verify.**
   - Every public type in scope has a page or a section on its owner's page, and every page is in the index.
   - Every code example matches the real API: names, generic arguments, parameter names, return types, `async` usage, namespaces in `using` lines.
   - Every relative link under `docs/` resolves to an existing file, e.g.:
     ```bash
     grep -rhoE '\]\([^)#]+\.md' docs | sed 's/](//' | sort -u
     ```
     and check each target relative to the page it's on.
   - `dotnet build` still succeeds if any XML docs changed.
9. **Report** what changed, which previously undocumented types you documented, and anything in the source that looked wrong or inconsistent while you were reading it (e.g. stale XML docs). Don't commit.
