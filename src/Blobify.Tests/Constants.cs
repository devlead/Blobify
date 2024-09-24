using Blobify.Tests.Fixture;
using Microsoft.Extensions.Primitives;

namespace Blobify.Tests;

public static class Constants
{
    public static class Request
    {
        public static readonly Uri BaseUri = new ("https://blobify.tests/", UriKind.Absolute);
    }

    public static class MediaType
    {
        public const string Json = "application/json";
    }

    public static class Tenant
    {
        public const string Id = "daea2e9b-847b-4c93-850d-2aa6f2d7af33";
    }
}