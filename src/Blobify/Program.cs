using Azure.Core;
using Azure.Identity;

public partial class Program
{
    static partial void AddServices(IServiceCollection services)
    {
        services
    .AddCakeCore()
    
    .AddSingleton<AzureTokenService>(
        async (tenantId, scope) =>
        {
            var tokenCredential = new DefaultAzureCredential();
            var accessToken = await tokenCredential.GetTokenAsync(
                new TokenRequestContext(
                    tenantId: tenantId,
                    scopes: [
                        scope ?? "https://storage.azure.com/.default"
                    ]
                    )
            );
            return accessToken;
        }
    )
    .AddSingleton<ArchiveCommand>()
    .AddSingleton<Blobify.Services.Storage.TokenService>();

        services.AddHttpClient();
    }

    // Configure commands
    static partial void ConfigureApp(AppServiceConfig appServiceConfig)
    {
        appServiceConfig.SetApplicationName("blobify");

        appServiceConfig.AddCommand<ArchiveCommand>("archive")
                .WithDescription("Example Archive command.")
                .WithExample(["archive", "inputpath", "storageaccountname"]);
    }
}