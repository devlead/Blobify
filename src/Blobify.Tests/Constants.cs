namespace Blobify.Tests;

public class Constants
{
    public static class Request
    {
        public static readonly Uri BaseUri = new ("https://blobify.tests/", UriKind.Absolute);
    }

    public static class Tenant
    {
        public const string Id = "daea2e9b-847b-4c93-850d-2aa6f2d7af33";
    }
}