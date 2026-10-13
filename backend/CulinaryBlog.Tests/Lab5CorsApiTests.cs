using System.Net;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class Lab5CorsApiTests
{
    [Theory]
    [InlineData("http://localhost:3000", true)]
    [InlineData("https://untrusted.test", false)]
    public async Task Preflight_applies_the_frontend_origin_policy(string origin, bool allowed)
    {
        await using var factory = new RecipeApiFactory();
        await factory.InitializeAsync();
        using var client = factory.Client(role: null);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/recipes");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,if-match");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Credentials"));
        if (allowed)
        {
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
            Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods").SelectMany(value => value.Split(',')).Select(value => value.Trim()));
            var headers = response.Headers.GetValues("Access-Control-Allow-Headers").SelectMany(value => value.Split(',')).Select(value => value.Trim()).ToArray();
            Assert.Contains("authorization", headers);
            Assert.Contains("if-match", headers);
        }
        Assert.Empty(factory.Cache.RemovedKeys);
    }

    [Fact]
    public async Task Cors_headers_are_applied_to_the_real_authentication_challenge()
    {
        await using var factory = new RecipeApiFactory();
        await factory.InitializeAsync();
        using var client = factory.Client(role: null);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/recipes");
        request.Headers.Add("Origin", "http://localhost:3000");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("http://localhost:3000", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.Contains("ETag", response.Headers.GetValues("Access-Control-Expose-Headers"));
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(factory.Cache.RemovedKeys);
    }
}
