using Niddy.Core.SystemInfo;

namespace Niddy.Core.Security;

/// <summary>
/// The arguments passed to <see cref="SecureStorageFactory.Create"/>, handed to each <see cref="SecureStorageProvider"/>.
/// </summary>
/// <param name="BaseDirectory">The base directory for file-backed storage.</param>
/// <param name="ServiceName">The application identifier for keychain/credential entries.</param>
/// <param name="OS">
/// The operating system to choose storage for. <see cref="SecureStorageFactory.Create"/> sets it to
/// <see cref="Platform.Info"/>'s <see cref="Platform.OS"/>.
/// </param>
public sealed record SecureStorageContext(string BaseDirectory, string ServiceName, OS OS);
