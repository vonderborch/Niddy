# PathHelpers

`Niddy.Helpers` · Niddy.Core · [source](../../../src/Niddy.Core/Helpers/PathHelpers.cs)

Matches a path against a list of relative paths or simple patterns, e.g. ignore lists.

## API

```csharp
static bool PathIsInList(
    string path,                    // the path to test
    string rootPath,                // entries are relative to this
    IReadOnlyList<string> paths,    // the entries
    bool checkWithWildcard = false, // "*name" matches a file name anywhere, "dir/*" matches a prefix
    bool checkEntryName = false)    // also match when the file name equals an entry
```

Trailing slashes on entries are ignored.

## Example

```csharp
using Niddy.Helpers;

string[] ignore = ["bin", "obj", "*.user", "docs/generated*"];

foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
{
    if (PathHelpers.PathIsInList(file, root, ignore, checkWithWildcard: true, checkEntryName: true))
        continue;
    Process(file);
}
```
