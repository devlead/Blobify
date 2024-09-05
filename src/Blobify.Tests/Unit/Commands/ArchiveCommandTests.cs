using Blobify.Commands;
using Blobify.Commands.Settings;
using Blobify.Tests.Fixture;

namespace Blobify.Tests.Unit.Commands;
public class ArchiveCommandTests
{
    public class NoFiles
    {

        [Test]
        public async Task ExecuteAsync()
        {
            // Given
            var (archiveCommand, settings) = BlobifyServiceProviderFixture.GetRequiredService<ArchiveCommand, ArchiveSettings>();
            

            // When
            var result = await archiveCommand.ExecuteAsync(
                null!,
                settings
                );

            // Then
            await Verify(result);
        }
    }

    public class ExistingFiles
    {

        [TestCase("\"ExistingFile\"")]
        [TestCase("\"ExistingFileWrongHash\"")]
        public async Task ExecuteAsync(string content)
        {
            // Given
            var (archiveCommand, settings, fileSystem) = BlobifyServiceProviderFixture.GetRequiredService<ArchiveCommand, ArchiveSettings, FakeFileSystem>();
            var file = fileSystem.CreateFile("/Working/InputPath/ExistingFile.json").SetContent(content);

            // When
            var result = await archiveCommand.ExecuteAsync(
                null!,
                settings
                );

            // Then
            await Verify(
                new
                {
                    ExitCode = result,
                    FileExists = file.Exists
                }
                );
        }
    }

    public class NewFiles
    {

        [Test]
        public async Task ExecuteAsync()
        {
            // Given
            var (archiveCommand, settings, fileSystem) = BlobifyServiceProviderFixture.GetRequiredService<ArchiveCommand, ArchiveSettings, FakeFileSystem>();
            var file = fileSystem.CreateFile("/Working/InputPath/NewFile.json").SetContent("\"NewFile\"");

            // When
            var result = await archiveCommand.ExecuteAsync(
                null!,
                settings
                );

            // Then
            await Verify(
                new
                {
                    ExitCode = result,
                    FileExists = file.Exists
                }
                );
        }
    }
}
