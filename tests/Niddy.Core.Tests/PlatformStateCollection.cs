namespace Niddy.Core.Tests;

/// <summary>
/// Test classes that change or read static platform state (<c>Platform</c>'s supported-OS declaration,
/// <c>SecureStorageFactory</c>'s providers). Classes in one collection run one at a time.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PlatformStateCollection
{
    public const string Name = "Platform state";
}
