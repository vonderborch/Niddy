---
description: Bring docs/ (and README.md, AI-GUIDE.md) in sync with the source
argument-hint: "[class, folder or project to focus on; defaults to what changed]"
---

Sync the Niddy documentation with the source code. Follow the docs rules in CLAUDE.md.

Scope: $ARGUMENTS
If no scope is given, use what changed: `git status`, `git diff` and `git diff --cached` (or, with no git history, compare the public types in the source against the pages in `docs/`).

1. **Find the public API in scope.** For each public type, read its source and XML docs. Note new, removed and renamed types and members, changed signatures and defaults, and changed behavior (threading, platform differences, exceptions).
2. **Update class pages** in `docs/<Project>/<Folder>/<Class>.md`:
   - Create a page for each new public class, using the existing page shape (header line with namespace, package and source link; summary; `## API`; examples; `## See also`). Small supporting types go on their owner's page.
   - Fix API tables and examples for changed members. Delete pages for removed types, and fix every link to them.
3. **Update walkthroughs** in `docs/examples/` that use anything that changed. Add a new walkthrough if a new feature spans several classes.
4. **Update `docs/README.md`** (the index) for any added, removed or renamed page.
5. **Update `docs/AI-GUIDE.md`** if usage, signatures, defaults or gotchas changed.
6. **Update the root `README.md`** only if the feature list or the quick start is affected. Keep its docs links absolute (`https://github.com/vonderborch/Niddy/blob/main/docs/...`).
7. **Verify.**
   - Every code example matches the real API: names, generic arguments, parameter names, return types, `async` usage, namespaces in `using` lines.
   - Every relative link under `docs/` resolves to an existing file, e.g.:
     ```bash
     grep -rhoE '\]\([^)#]+\.md' docs | sed 's/](//' | sort -u
     ```
     and check each target relative to the page it's on.
   - `dotnet build` still succeeds if any XML docs changed.
8. **Report** what changed, and anything in the source that looked wrong or inconsistent while you were reading it (e.g. stale XML docs). Don't commit.
