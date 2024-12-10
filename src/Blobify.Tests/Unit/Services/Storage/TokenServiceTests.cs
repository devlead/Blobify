using Blobify.Services.Storage;
using System.Runtime.CompilerServices;

namespace Blobify.Tests.Unit.Services.Storage;

public class TokenServiceTests
{
    public Uri BaseUri { get; private set; }= new Uri(Constants.Request.BaseUri, "Services/Storage/TokenServiceTests/");

    private Uri GetUri(
        [CallerMemberName]
        string path = ""
        ) => new (BaseUri, path);

    [Test]
    public async Task GetAsync()
    {
        // Given
        var tokenService = ServiceProviderFixture.GetRequiredService<TokenService>();

        // When
        var result = await tokenService.GetAsync<string>(
            Constants.Tenant.Id,
            GetUri()
            );

        // Then
        await Verify(result);
    }

    [Test]
    public async Task HeadAsync()
    {
        // Given
        var tokenService = ServiceProviderFixture.GetRequiredService<TokenService>();

        // When
        var result = await tokenService.HeadAsync(
            Constants.Tenant.Id,
            GetUri()
            );

        // Then
        await Verify(result);
    }

    [Test]
    public async Task PutAsync()
    {
        // Given
        var tokenService = ServiceProviderFixture.GetRequiredService<TokenService>();

        // When
        var result = await tokenService.PutAsync(
            Constants.Tenant.Id,
            GetUri()
            );

        // Then
        await Verify(result);
    }
}