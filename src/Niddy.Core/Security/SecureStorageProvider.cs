namespace Niddy.Core.Security;

/// <summary>
/// Creates an <see cref="ISecureStorage"/> for the current environment, or returns null to let the next provider try.
/// Register one with <see cref="SecureStorageFactory.Register"/>.
/// </summary>
/// <param name="context">The base directory and service name passed to <see cref="SecureStorageFactory.Create"/>.</param>
/// <returns>The storage to use, or null if this provider doesn't apply here.</returns>
public delegate ISecureStorage? SecureStorageProvider(SecureStorageContext context);
