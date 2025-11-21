
using Blobify.Services.Storage;
using Cake.Common.IO;
using Cake.Common.Security;
using System.Net.Http.Headers;

namespace Blobify.Commands;

public class ArchiveCommand(
        ICakeContext cakeContext,
        ILogger<ArchiveCommand> logger,
        TokenService tokenService
) : AsyncCommand<ArchiveSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ArchiveSettings settings, CancellationToken cancellationToken)
    {
        if (settings.InputPath.IsRelative)
        {
            logger.LogInformation("Relative inputpath {InputPath} making absolute...", settings.InputPath);
            settings.InputPath = settings.InputPath.MakeAbsolute(cakeContext.Environment);
        }
        logger.LogInformation("InputPath: {InputPath}", settings.InputPath);
        logger.LogInformation("AzureStorageAccount: {AzureStorageAccount}", settings.AzureStorageAccount);
        logger.LogInformation("AzureStorageAccountContainer: {AzureStorageAccountContainer}", settings.AzureStorageAccountContainer);

        var searchPattern = settings.InputPath.CombineWithFilePath(settings.FilePattern).FullPath;
        logger.LogInformation("Looking for files ({SearchPattern})...", searchPattern);

        var files = cakeContext.GetFiles(searchPattern);

        logger.LogInformation("Found {FileCount} files.", files.Count);

        if(files.Count == 0)
        {
            logger.LogInformation("No files found, exiting...");
            return 0;
        }

        var containerUrl = settings.GetAzureStorageBlobUrl();

        switch (await tokenService.HeadAsync(settings.AzureTenantId, new Uri(containerUrl, "?restype=container")))
        {
            case (HttpStatusCode.NotFound, _, _):
                logger.LogInformation("Container {AzureStorageAccount}/{AzureStorageAccountContainer} not found, creating...", settings.AzureStorageAccount, settings.AzureStorageAccountContainer);
                await tokenService.PutAsync(settings.AzureTenantId, new Uri(containerUrl, "?restype=container"));
                break;
            case (HttpStatusCode.OK, _, _):
                logger.LogInformation("Container {AzureStorageAccount}/{AzureStorageAccountContainer} found", settings.AzureStorageAccount, settings.AzureStorageAccountContainer);
                break;
            default:
                logger.LogError("Failed to check container {ContainerUrl}", containerUrl);
                return 1;
        }

        await Parallel.ForEachAsync(
            files,
            async (filePath, ct) =>
            {
                var targetPath = settings.InputPath.GetRelativePath(filePath);
                var targetUri = new Uri(containerUrl, targetPath.FullPath);
                var file = cakeContext.FileSystem.GetFile(filePath);
                var contentType = MimeTypes.TryGetMimeType(
                                    filePath.FullPath,
                                    out var mimeType
                                )
                                    ? mimeType
                                    : "application/octet-stream";

                var hash = cakeContext.CalculateFileHash(filePath, HashAlgorithm.MD5);

                switch (await tokenService.HeadAsync(settings.AzureTenantId, targetUri, ct))
                {
                    case (HttpStatusCode.NotFound, _, _):
                        {
                            logger.LogInformation("Blob {File} not found, uploading...", targetPath.FullPath);
                            await using var stream = file.OpenRead();
                            using var content = new StreamContent(stream) { 
                                Headers = {
                                    ContentType = new MediaTypeHeaderValue(contentType),
                                    ContentLength = file.Length,
                                    ContentMD5 = hash.ComputedHash
                                }
                            };
                            content.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");

                            switch (await tokenService.PutAsync(settings.AzureTenantId, targetUri, ct, content))
                            {
                                case (HttpStatusCode.Created, _, _):
                                    logger.LogInformation("Blob {File} uploaded", targetPath.FullPath);
                                    break;
                                default:
                                    throw new Exception($"Failed to upload blob {targetUri}");
                            }
                            break;
                        }
                    case (HttpStatusCode.OK, _, byte[] md5Hash):
                        if (md5Hash.SequenceEqual(hash.ComputedHash))
                        {
                            logger.LogInformation("Blob {File} found and hash match deleting...", targetPath.FullPath);
                            file.Delete();
                        }
                        else
                        {
                            logger.LogInformation("Blob {File} found, skipping...", targetPath.FullPath);
                        }
                        return;
                    default:
                        throw new Exception($"Failed to check blob {targetUri}");
                }

                switch (await tokenService.HeadAsync(settings.AzureTenantId, targetUri, ct))
                {
                    case (HttpStatusCode.OK, _, byte[] md5Hash):
                        if (md5Hash.SequenceEqual(hash.ComputedHash))
                        {
                            logger.LogInformation("Blob {File} found and hash match deleting...", targetPath.FullPath);
                            file.Delete();
                        }
                        else
                        {
                            throw new Exception($"Blob {targetPath.FullPath} found blob but hash mismatch, won't delete.");
                        }

                        return;
                }
            }
        );


        return 0;
    }
}