using System.Net;
using Acjr3.OpenApi;
using Acjr3.Tests.Http;

namespace Acjr3.Tests.OpenApi;

public sealed class OpenApiServiceTests
{
    [Fact]
    public async Task FetchAsync_UsesConfiguredCachePath_WhenOutPathNotProvided()
    {
        var cachePath = Path.Combine(Path.GetTempPath(), $"acjr3-openapi-cache-{Guid.NewGuid():N}.json");
        var previous = Environment.GetEnvironmentVariable("ACJR3_OPENAPI_CACHE_PATH");
        Environment.SetEnvironmentVariable("ACJR3_OPENAPI_CACHE_PATH", cachePath);

        try
        {
            var handler = new SequenceHttpMessageHandler(
                (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"paths":{"/rest/api/3/project":{"get":{"operationId":"listProjects"}}}}""")
                }));
            var service = new OpenApiService(new TestHttpClientFactory(handler));

            var result = await service.FetchAsync(outPath: null, specUrl: "https://example.test/swagger.json", new TestLogger());

            Assert.True(result.Success);
            Assert.True(File.Exists(cachePath));
            Assert.Contains("OpenAPI spec saved to", result.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ACJR3_OPENAPI_CACHE_PATH", previous);
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }
    }

    [Fact]
    public async Task FetchAsync_ReturnsFail_WhenFetchFails()
    {
        var handler = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var service = new OpenApiService(new TestHttpClientFactory(handler));

        var result = await service.FetchAsync(outPath: null, specUrl: "https://example.test/swagger.json", new TestLogger());

        Assert.False(result.Success);
        Assert.Contains("Unable to fetch OpenAPI spec", result.Message);
    }

    [Fact]
    public void ListPaths_ReturnsOperationLines()
    {
        var file = WriteTempSpec(
            """
            {
              "paths": {
                "/rest/api/3/project": {
                  "get": { "operationId": "listProjects" }
                },
                "/rest/api/3/issue/{issueIdOrKey}": {
                  "get": { "operationId": "getIssue" }
                }
              }
            }
            """);

        try
        {
            var service = new OpenApiService(new TestHttpClientFactory(new SequenceHttpMessageHandler()));
            var result = service.ListPaths("project", file);

            Assert.True(result.Success);
            Assert.Single(result.Lines);
            Assert.Contains("/rest/api/3/project", result.Lines[0]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ShowOperation_ReturnsDetailsForKnownOperation()
    {
        var file = WriteTempSpec(
            """
            {
              "paths": {
                "/rest/api/3/project": {
                  "post": {
                    "operationId": "createProject",
                    "parameters": [
                      { "in": "query", "name": "expand", "required": true }
                    ],
                    "requestBody": {
                      "content": {
                        "application/json": {}
                      }
                    },
                    "responses": {
                      "201": {
                        "content": {
                          "application/json": {}
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        try
        {
            var service = new OpenApiService(new TestHttpClientFactory(new SequenceHttpMessageHandler()));
            var result = service.ShowOperation("POST", "/rest/api/3/project", file);

            Assert.True(result.Success);
            Assert.Contains("operationId: createProject", result.Lines);
            Assert.Contains("Required params: query:expand", result.Lines);
            Assert.Contains("Request content-types: application/json", result.Lines);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ShowOperation_ReturnsFail_WhenPathMissing()
    {
        var file = WriteTempSpec("""{"paths":{"/rest/api/3/project":{"get":{"operationId":"listProjects"}}}}""");

        try
        {
            var service = new OpenApiService(new TestHttpClientFactory(new SequenceHttpMessageHandler()));
            var result = service.ShowOperation("GET", "/rest/api/3/missing", file);

            Assert.False(result.Success);
            Assert.Contains("Path not found", result.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ListPaths_UsesConfiguredCachePath_WhenSpecFileIsNull()
    {
        var cachePath = WriteTempSpec("""{"paths":{"/rest/api/3/project":{"get":{"operationId":"listProjects"}}}}""");
        var previous = Environment.GetEnvironmentVariable("ACJR3_OPENAPI_CACHE_PATH");
        Environment.SetEnvironmentVariable("ACJR3_OPENAPI_CACHE_PATH", cachePath);

        try
        {
            var service = new OpenApiService(new TestHttpClientFactory(new SequenceHttpMessageHandler()));
            var result = service.ListPaths(filter: null, specFile: null);

            Assert.True(result.Success);
            Assert.Contains("/rest/api/3/project", result.Lines[0]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ACJR3_OPENAPI_CACHE_PATH", previous);
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }
    }

    private static string WriteTempSpec(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"acjr3-openapi-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content);
        return path;
    }
}
