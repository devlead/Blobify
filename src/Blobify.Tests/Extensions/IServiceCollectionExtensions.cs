using Cake.Core.Configuration;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core;
using VerifyTests.Http;
using System.Collections.Immutable;
using System.Net;
using Blobify.Tests.Fixture;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace Blobify.Tests.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddCakeFakes(
        this IServiceCollection services,
        Action<FakeFileSystem>? configureFileSystem = null
        )
    {
        var configuration = new FakeConfiguration();

        var environment = FakeEnvironment.CreateUnixEnvironment();

        var fileSystem = new FakeFileSystem(environment);
        configureFileSystem?.Invoke(fileSystem);

        var globber = new Globber(fileSystem, environment);

        var log = new FakeLog();

        var Context = Substitute.For<ICakeContext>();
        Context.Configuration.Returns(configuration);
        Context.Environment.Returns(environment);
        Context.FileSystem.Returns(fileSystem);
        Context.Globber.Returns(globber);
        Context.Log.Returns(log);

        return services.AddSingleton<ICakeConfiguration>(configuration)
                                .AddSingleton<ICakeEnvironment>(environment)
                                .AddSingleton(fileSystem)
                                .AddSingleton<IFileSystem>(fileSystem)
                                .AddSingleton<IGlobber>(globber)
                                .AddSingleton<ICakeLog>(log)
                                .AddSingleton<ICakeRuntime>(environment.Runtime)
                                .AddSingleton(Context);
    }

    public static IServiceCollection AddMockHttpClient(this IServiceCollection services)
    {
        //var routes = new[]
        //        {
        //            new Routes.Route(
        //                new (
        //                    [HttpMethod.Get],
        //                    "/GetAsync/string"
        //                ),
        //                [
        //                new (
        //                    [],
        //                    "\"OK\"",
        //                    Constants.MediaType.Json,
        //                    [],
        //                    HttpStatusCode.OK
        //                )
        //                ],
        //                Constants.Request.Authorization.Authorized
        //            )
        //        };
        //var routesJson = System.Text.Json.JsonSerializer.Serialize(routes, new System.Text.Json.JsonSerializerOptions { WriteIndented = true});


        static MockHttpClient CreateClient()
            => new(Routes.GetResponseBuilder());

        return services
            .AddSingleton<HttpClient>(
            _ => CreateClient()
            )
            .AddSingleton(
             _ =>
             {
                 var httpClientFactory = Substitute.For<IHttpClientFactory>();
                 httpClientFactory
                    .CreateClient(null!)
                    .ReturnsForAnyArgs(_ => CreateClient());

                 httpClientFactory
                    .CreateClient()
                    .Returns(_ => CreateClient());

                 return httpClientFactory;
             }
            );
    }
}

public static class  Resources
{
    private static readonly ConcurrentDictionary<string, string> _stringResources = new();

    public static string? GetString(string filename)
        => _stringResources.GetOrAdd(
            filename, 
            _ => GetResourceString(filename)
            );

    private static readonly ConcurrentDictionary<string, byte[]> _byteResources = new();

    public static byte[] GetBytes(string filename)
        => _byteResources.GetOrAdd(
            filename,
            _ => GetResourceBytes(filename)
            );


    private static byte[] GetResourceBytes(string filename)
    {
        using var stream = GetResourceStream(filename);
        using var targetStream = new MemoryStream();
        stream.CopyTo(targetStream);
        return targetStream.ToArray();
    }

    private static string GetResourceString(string filename, Encoding? encoding = null)
    {
        using var stream = GetResourceStream(filename);
        using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static Stream GetResourceStream(string filename)
    {
        var resourceStream = typeof(BlobifyServiceProviderFixture)
                                .Assembly
                                .GetManifestResourceStream($"Blobify.Tests.Resources.{filename}");

        return resourceStream
            ?? throw new Exception($"Failed to get stream for {filename}.");
    }
}

public class Routes
{
    public static Func<HttpRequestMessage, HttpResponseMessage> GetResponseBuilder()
    {
        var routes = GetRoutes();

        HttpResponseMessage GetResponseBuilder(HttpRequestMessage request)
        {
            if (
                   request.RequestUri?.AbsoluteUri is { } absoluteUri
                   &&
                   routes.TryGetValue(
                   (
                       request.Method,
                       absoluteUri
                   ),
                   out var response
                   )
               )
            {
                return response(request);
            }

            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            };
        }

        return GetResponseBuilder;
    }

    private static ImmutableDictionary<(
                        HttpMethod Method,
                        string AbsoluteUri
                        ),
                        Func<HttpRequestMessage, HttpResponseMessage>
                        >? _routes;

    private static ImmutableDictionary<(
                        HttpMethod Method,
                        string AbsoluteUri
                        ),
                        Func<HttpRequestMessage, HttpResponseMessage>
                        > GetRoutes()
     => _routes ??= InitializeRoutesFromResourse();


    private static ImmutableDictionary<(HttpMethod Method, string PathAndQuery), Func<HttpRequestMessage, HttpResponseMessage>> InitializeRoutesFromResourse()
    {
        var routesJson = Resources.GetString("Routes.json");
        ArgumentException.ThrowIfNullOrEmpty(routesJson);
        
        var routes = System.Text.Json.JsonSerializer.Deserialize<Route[]>(routesJson);
        ArgumentNullException.ThrowIfNull(routes);

        var enableRoute = routes
                .Aggregate(
                    new Dictionary<(
                        HttpMethod Method,
                        string AbsoluteUri
                        ),
                        Action
                        >(),
                    (seed, value) =>
                    {
                        void Enable() => value.Request.Disabled = false;
                        foreach (var method in value.Request.Methods)
                        {
                            seed[(method, value.Request.AbsoluteUri)] = Enable;
                        }

                        return seed;
                    },
                    seed => seed.ToImmutableDictionary()
                    );

        var result =
            routes
            .Aggregate(
                new Dictionary<(
                    HttpMethod Method,
                    string AbsoluteUri
                    ),
                    Func<HttpRequestMessage, HttpResponseMessage>
                    >(),
                (seed, value) =>
                {
                    static HttpResponseMessage AddHeaders(HttpResponseMessage response, Dictionary<string, string[]> headers)
                    {
                        foreach (var (key, value) in headers)
                        {
                            response.Headers.TryAddWithoutValidation(key, value);
                        }

                        return response;
                    }

                    var responseFunc = new Func<HttpRequestMessage, HttpResponseMessage>(
                        request =>
                        {
                            if (value.Request.Disabled)
                            {
                                return new HttpResponseMessage
                                {
                                    StatusCode = HttpStatusCode.NotFound
                                };
                            }

                            if (value.Authorization is { } authorization)
                            {
                                foreach (var header in authorization)
                                {
                                    if (!request.Headers.TryGetValues(header.Key, out var values) || !values.All(value => header.Value.Contains(value)))
                                    {
                                        return new HttpResponseMessage
                                        {
                                            StatusCode = HttpStatusCode.Unauthorized
                                        };
                                    }
                                }
                            }

                            var result = value.Responses.FirstOrDefault(
                                response => response.RequestHeaders.All(
                                    header =>
                                    request.Headers.TryGetValues(header.Key, out var values) && values.All(value => header.Value.Contains(value))
                                    ||
                                    request.Content?.Headers.TryGetValues(header.Key, out var contentValues) == true && contentValues.All(value => header.Value.Contains(value))
                                    )
                            );

                            if (result is { } response)
                            {
                                if (response.EnableRequests.Any())
                                {
                                    foreach (var enableRequest in response.EnableRequests)
                                    {
                                        if (enableRoute.TryGetValue((enableRequest.Method, enableRequest.AbsoluteUri), out var enable))
                                        {
                                            enable();
                                        }
                                    }
                                }

                                return AddHeaders(new HttpResponseMessage()
                                {
                                    Content = !string.IsNullOrWhiteSpace(response.ContentResource) && Resources.GetBytes(response.ContentResource) is { } content
                                                ? new ByteArrayContent(
                                                    content
                                                )
                                                {
                                                    Headers =
                                                    {
                                                        ContentType = MediaTypeHeaderValue.Parse(response.ContentType),
                                                        ContentMD5 = System.Security.Cryptography.MD5.HashData(content)
                                                    }
                                                }
                                                : null,
                                    StatusCode = response.StatusCode
                                },
                                response.ContentHeaders
                                );
                            }

                            return new HttpResponseMessage
                            {
                                StatusCode = HttpStatusCode.BadRequest
                            };
                        }
                    );

                    foreach (var method in value.Request.Methods)
                    {
                        seed[(method, value.Request.AbsoluteUri)] = responseFunc;
                    }

                    return seed;
                },
                seed => seed.ToImmutableDictionary()
                );

        return result;
    }

    public record Route(
        RouteRequest Request,
        RouteResponse[] Responses,
        Dictionary<string, string[]>? Authorization = null
        );

    public record RouteRequest(
        HttpMethod[] Methods,
        string AbsoluteUri
        )
    {
        public bool Disabled { get; set; }
    }

    public record RouteResponse(
        Dictionary<string, string[]> RequestHeaders,
        string? ContentResource,
        string ContentType,
        Dictionary<string, string[]> ContentHeaders,
        HttpStatusCode StatusCode
        )
    {
        public RouteEnableRequest[] EnableRequests { get; init; } = [];
    }

    public record RouteEnableRequest(
        [property:JsonPropertyName("Method")]
        string StringMethod,
        string AbsoluteUri
        )
    {
        internal HttpMethod Method { get; init; } = new(StringMethod);
    }
}