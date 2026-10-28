using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CulinaryBlog.Tests;

public sealed class AdminUserApiTests : IAsyncLifetime
{
    private readonly AuthApiFactory _factory = new();
    private HttpClient _anonymous = null!;
    private HttpClient _author = null!;
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        _anonymous = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Author" })
            Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);

        await AddUser(users, "admin", "Admin User", "admin@users.test", ["Admin"]);
        var author = await AddUser(users, "author-anna", "Author Anna", "anna@users.test", ["Author"]);
        await AddUser(users, "author-bella", "Author Bella", "bella@users.test", ["Author"]);
        await AddUser(users, "admin-author-claire", "Author Claire", "claire@users.test", ["Admin", "Author"]);
        await AddUser(users, "reader-dan", "Reader Dan", "dan@users.test", []);

        _author = AuthenticatedClient(author.Id, "Author");
        _admin = AuthenticatedClient("admin-test-user", "Admin");
    }

    public async Task DisposeAsync()
    {
        _anonymous.Dispose();
        _author.Dispose();
        _admin.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task List_requires_an_admin_role()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.GetAsync("/api/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _author.GetAsync("/api/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _admin.GetAsync("/api/v1/admin/users")).StatusCode);
    }

    [Fact]
    public async Task List_paginates_searches_and_filters_roles_without_sensitive_fields()
    {
        var page = await _admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/users?page=1&pageSize=2");
        Assert.Equal(5, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, page.GetProperty("items").GetArrayLength());

        var search = await _admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/users?search=bella");
        Assert.Single(search.GetProperty("items").EnumerateArray());
        Assert.Equal("author-bella", search.GetProperty("items")[0].GetProperty("userName").GetString());

        var role = await _admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/users?role=Author&page=2&pageSize=1&search=author");
        Assert.Equal(3, role.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, role.GetProperty("totalPages").GetInt32());
        Assert.Single(role.GetProperty("items").EnumerateArray());
        Assert.Contains(role.GetProperty("items")[0].GetProperty("roles").EnumerateArray(), value =>
            value.GetString() == "Author");

        var lowerCaseRole = await _admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/users?role=author");
        Assert.Equal(3, lowerCaseRole.GetProperty("totalCount").GetInt32());

        var item = page.GetProperty("items")[0];
        Assert.Equal(["createdAt", "email", "emailConfirmed", "fullName", "id", "roles", "userName"],
            item.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray());
        var payload = page.GetRawText().ToLowerInvariant();
        Assert.DoesNotContain("password", payload);
        Assert.DoesNotContain("securitystamp", payload);
        Assert.DoesNotContain("token", payload);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?role=Guest")]
    public async Task List_rejects_invalid_query_parameters(string query)
    {
        var response = await _admin.GetAsync($"/api/v1/admin/users{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_rejects_an_overlong_search_term()
    {
        var response = await _admin.GetAsync($"/api/v1/admin/users?search={new string('a', 101)}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_loads_roles_in_a_fixed_number_of_queries()
    {
        _factory.SqlCommands.Reset();

        var response = await _admin.GetAsync("/api/v1/admin/users?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.InRange(_factory.SqlCommands.Count, 1, 3);
    }

    private async Task<ApplicationUser> AddUser(UserManager<ApplicationUser> users, string userName, string fullName,
        string email, string[] roles)
    {
        var user = new ApplicationUser { UserName = userName, FullName = fullName, Email = email, CreatedAt = DateTime.UtcNow };
        Assert.True((await users.CreateAsync(user, "Test-only@123456")).Succeeded);
        if (roles.Length > 0) Assert.True((await users.AddToRolesAsync(user, roles)).Succeeded);
        return user;
    }

    private HttpClient AuthenticatedClient(string userId, string role)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role) };
        var token = new JwtSecurityToken("culinary-tests", "culinary-tests", claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthApiFactory.Key)),
                SecurityAlgorithms.HmacSha256));
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
