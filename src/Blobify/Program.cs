using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Configuration;
using Spectre.Console.Cli.Extensions.DependencyInjection;
using Azure.Core;
using Azure.Identity;
using Blobify.Services.Storage;

var serviceCollection = new ServiceCollection()
    .AddCakeCore()
    .AddLogging(configure =>
            configure
                .AddSimpleConsole(opts =>
                {
                    opts.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                })
                .AddConfiguration(
                new ConfigurationBuilder()
                    .Add(new MemoryConfigurationSource
                    {
                        InitialData = new Dictionary<string, string?>
                        {
                            { "LogLevel:System.Net.Http.HttpClient", "Warning" }
                        }
                    })
                    .Build()
            ))
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
    .AddSingleton<TokenService>();

serviceCollection.AddHttpClient();

using var registrar = new DependencyInjectionRegistrar(serviceCollection);
var app = new CommandApp(registrar);

app.Configure(
    config =>
    {
        config.SetApplicationName("blobify");
        config.ValidateExamples();

        config.AddCommand<ArchiveCommand>("archive")
                .WithDescription("Example Archive command.")
                .WithExample(["archive", "inputpath", "storageaccountname"]);

        config.SetExceptionHandler(
            (ex, typeResolver) => AnsiConsole.WriteException(ex, ExceptionFormats.ShowLinks)
            );
    });

return await app.RunAsync(args);