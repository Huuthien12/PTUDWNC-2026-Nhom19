using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace CulinaryBlog.Tests;

public sealed class RecipeApiFactory : WebApplicationFactory<Program>
{
    private const string Key = "recipe-tests-only-signing-key-0123456789012345678901234567890123456789";
    public RecipeDatabaseFixture Database { get; } = new();
    public Guid CategoryId { get; private set; }
    public string AuthorId { get; private set; } = "";
    public bool FailWithSlugConstraint { get; set; }
    public RecordingRecipeCache Cache { get; } = new();

    public async Task InitializeAsync()
    {
        var id = await Database.InitializeAsync();
        await using var db = Database.NewContext();
        var recipe = await db.Recipes.SingleAsync(x => x.Id == id);
        CategoryId = recipe.CategoryId;
        AuthorId = recipe.AuthorId;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=127.0.0.1;Port=1;Database=Unused;Username=test");
        builder.UseSetting("Jwt:Key", Key);
        builder.UseSetting("Jwt:Issuer", "recipe-tests");
        builder.UseSetting("Jwt:Audience", "recipe-tests");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddScoped(_ => Database.NewContext());
            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService>(Cache);
            if (FailWithSlugConstraint)
            {
                services.RemoveAll<IUnitOfWork>();
                services.AddScoped<IUnitOfWork, SlugConstraintUnitOfWork>();
            }
        });
    }

    public HttpClient Client(string? role = "Author", bool expired = false, bool withSubject = true, string? userId = null)
    {
        var client = CreateClient();
        if (role is null) return client;
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (withSubject) claims.Add(new(ClaimTypes.NameIdentifier, userId ?? AuthorId));
        var token = new JwtSecurityToken("recipe-tests", "recipe-tests", claims,
            expires: DateTime.UtcNow.AddMinutes(expired ? -5 : 5),
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Database.DisposeAsync();
    }

    // Deterministically exercises the provider error path after the slug precheck succeeds.
    private sealed class SlugConstraintUnitOfWork(ICategoryRepository categories, IRecipeRepository recipes) : IUnitOfWork
    {
        public ICategoryRepository Categories => categories;
        public IRecipeRepository Recipes => recipes;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new DbUpdateException("Concurrent slug insert", new PostgresException(
                "duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
                constraintName: "IX_Recipes_Slug"));
    }
}

public sealed class RecordingRecipeCache : ICacheService
{
    public List<string> RemovedKeys { get; } = [];
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult(default(T));
    public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        RemovedKeys.Add(key);
        return Task.CompletedTask;
    }
}
