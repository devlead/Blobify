using System.Net.Http.Json;
using System.Runtime.CompilerServices;

namespace Blobify.Services.Storage;
public class TokenService(
    IHttpClientFactory httpClientFactory,
    AzureTokenService azureTokenService,
    ILogger<TokenService> logger
    )
{
    static Azure.Core.AccessToken? cachedAccessToken = default;

    private async Task<string> GetAzureToken(string? tenantId)
    {
        if (cachedAccessToken.HasValue && (cachedAccessToken.Value.ExpiresOn - DateTimeOffset.UtcNow).TotalMinutes > 1)
        {
            return cachedAccessToken.Value.Token;
        }

        logger.LogInformation("Getting azure token...");
        try
        {
            var accessToken = await azureTokenService(tenantId);
            cachedAccessToken = accessToken;
            return accessToken.Token;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch Azure Access Token");
            throw;
        }
    }

    private HttpClient GetBearerTokenHttpClient(
        string bearerToken,
        string? accept = "application/json",
        string? contentType = "application/json",
        [CallerMemberName]
        string name = nameof(GetBearerTokenHttpClient)
    )
    {
        var bearerHttpClient = httpClientFactory.CreateClient(name);

        bearerHttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
           "Bearer",
           bearerToken
        );

        bearerHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-ms-version", "2024-05-04");
        bearerHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("x -ms-date", DateTimeOffset.UtcNow.ToString("R"));
        bearerHttpClient.DefaultRequestHeaders.Date = DateTimeOffset.UtcNow;


        if (!string.IsNullOrWhiteSpace(accept))
        {
            bearerHttpClient.DefaultRequestHeaders.Accept.TryParseAdd(accept);
        }

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            bearerHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", contentType);
        }

        return bearerHttpClient;
    }

    public async Task<T> GetAsync<T>(
        string? tenantId,
        Uri url,
        string? accept = "application/json"
    )
    {
        using var httpClient = GetBearerTokenHttpClient(
            await GetAzureToken(tenantId),
            accept,
            null
            );

        return await GetFromJsonAsync<T>(httpClient, url);
    }

    public Task<(HttpStatusCode StatusCode, ILookup<string, string> Headers, byte[]? ContentMD5)> HeadAsync(
         string? tenantId,
         Uri url
    ) => SendAsync(tenantId, url, HttpMethod.Head);

    public Task<(HttpStatusCode StatusCode, ILookup<string, string> Headers, byte[]? ContentMD5)> PutAsync(
         string? tenantId,
         Uri url,
        HttpContent? content = null
    ) => SendAsync(tenantId, url, HttpMethod.Put, content);

    private async Task<(HttpStatusCode StatusCode, ILookup<string, string> Headers, byte[]? ContentMD5)> SendAsync(
        string? tenantId,
        Uri url,
        HttpMethod method,
        HttpContent? content = null
        )
    {
        using var httpClient = GetBearerTokenHttpClient(
            await GetAzureToken(tenantId),
            null,
            null
            );
        var response = await httpClient.SendAsync(
            new HttpRequestMessage(
                method,
                url
            )
            {
                Content = content
            },
            HttpCompletionOption.ResponseHeadersRead
        );
        return (
            response.StatusCode,
            (
            from header in response.Headers.Union(response.Content.Headers)
            from value in header.Value
            select (header.Key, value)
        ).ToLookup(
            x => x.Key,
            x => x.value
        ),
        response.Content.Headers.ContentMD5
        );
    }

    private static async Task<T> GetFromJsonAsync<T>(HttpClient httpClient, Uri url)
    {
        var result = await httpClient.GetFromJsonAsync<T>(url);

        ArgumentNullException.ThrowIfNull(result);

        return result;
    }
}
