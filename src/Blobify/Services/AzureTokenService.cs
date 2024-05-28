namespace Blobify.Services;

public delegate Task<Azure.Core.AccessToken> AzureTokenService(string? tenantId, string? scope = null);