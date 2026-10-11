namespace Niddy.Core.GitHub;

/// <summary>
/// The answer from <see cref="GitHubReleases.CheckForUpdateAsync(string, string, GitHubReleaseOptions?, HttpClient?, CancellationToken)"/>:
/// whether the latest release is newer than the running version, and which release that is.
/// </summary>
/// <param name="IsUpdateAvailable">Whether <paramref name="Latest"/> is a newer version than <paramref name="CurrentVersion"/>.</param>
/// <param name="CurrentVersion">The version that was checked, as given.</param>
/// <param name="Latest">The latest release, newer or not.</param>
public sealed record GitHubUpdateCheck(bool IsUpdateAvailable, string CurrentVersion, GitHubRelease Latest);
