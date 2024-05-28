using System.ComponentModel;

namespace Blobify.Commands.Settings;

public class ArchiveSettings : CommandSettings
{
    [CommandArgument(0, "<inputpath>")]
    [ValidatePath]
    [Description("Input path")]
    public required DirectoryPath InputPath { get; set; }

    [CommandArgument(1, "<azureStorageAccount>")]
    [ValidateString]
    [Description("Azure Storage Account Name")]
    public required string AzureStorageAccount { get; set; }

    [CommandArgument(2, "<azureStorageAccountContainer>")]
    [ValidateString]
    [Description("Azure Storage Account Container Name")]
    public required string AzureStorageAccountContainer { get; set; }


    [CommandOption("--azure-tenant-id")]
    [Description("Azure Tentant ID to sign into")]
    public string? AzureTenantId { get; set; } = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");


    [CommandOption("--file-pattern")]
    [Description("Local file pattern to match")]
    public string FilePattern { get; set; } = "**/*.*";

    public string AzureStorageBlobSuffix { get; set; } = "blob.core.windows.net";

    public Uri GetAzureStorageBlobUrl() => new (
                                                $"https://{AzureStorageAccount}.{AzureStorageBlobSuffix}/{AzureStorageAccountContainer}/",
                                                UriKind.Absolute
                                            );
}