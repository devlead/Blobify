using Blobify.Services.Storage;
using Blobify.Services;
using Azure.Core;
using Blobify.Tests.Extensions;
using Blobify.Commands;
using Blobify.Commands.Settings;
using Devlead.Testing.MockHttp;
using Blobify.Tests;

public static partial class ServiceProviderFixture
{
    static partial void InitServiceProvider(IServiceCollection services)
    {
        services
            .AddLogging()
            .AddCakeFakes()
            .AddSingleton<AzureTokenService>(
                (_, _) => Task.FromResult(new AccessToken(nameof(AccessToken), DateTimeOffset.UtcNow.AddDays(1)))
            )
            .AddSingleton<TokenService>()
            .AddSingleton<ArchiveCommand>()
            .AddTransient(
                _ => new ArchiveSettings
                {
                    InputPath = "InputPath",
                    AzureStorageAccount = "AzureStorageAccount",
                    AzureStorageAccountContainer = "AzureStorageAccountContainer",
                    AzureTenantId = Constants.Tenant.Id
                }
            )
            .AddMockHttpClient<Constants>();
    }
}