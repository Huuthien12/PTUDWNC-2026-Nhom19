using CulinaryBlog.Application.Authentication.Commands.Login;
using CulinaryBlog.Application.Authentication.Commands.Register;
using CulinaryBlog.Application.Categories.Commands;
using CulinaryBlog.Application.Authentication.DTOs;
using CulinaryBlog.API.Authentication;
using CulinaryBlog.API.Recipes;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Application.Categories.Queries;
using CulinaryBlog.Application.Recipes.Queries;
using CulinaryBlog.Application.Common.Behaviors;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Infrastructure.Caching;
using CulinaryBlog.Infrastructure.Persistence.Seed;
using CulinaryBlog.Infrastructure.Authentication;
using CulinaryBlog.Infrastructure.BackgroundJobs;
using CulinaryBlog.Infrastructure.Email;
using CulinaryBlog.Infrastructure.Storage;
using CulinaryBlog.API.Health;
using CulinaryBlog.API.Observability;
using CulinaryBlog.Application.Authentication.Commands;
using Hangfire;
using Hangfire.PostgreSql;
using System.Text.Json;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.API.Caching;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Threading.RateLimiting;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Net;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .CreateBootstrapLogger();

try
{
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();

    if (context.HostingEnvironment.IsEnvironment("Testing"))
        configuration.MinimumLevel.Warning();
    else if (context.HostingEnvironment.IsDevelopment())
        configuration.MinimumLevel.Override("CulinaryBlog", LogEventLevel.Debug);
});

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");


// =========================
// Database
// =========================

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));


// =========================
// Redis Cache
// =========================

var redisConnection =
    builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379";

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
});

builder.Services.AddScoped<ICacheService, RedisCacheService>();
builder.Services.AddOutputCache(options => options.AddPolicy("RecipeList", policy => policy
    .Expire(TimeSpan.FromMinutes(2))
    .SetVaryByQuery("*")
    .Tag("recipes")
    .With(context => context.HttpContext.User.Identity?.IsAuthenticated != true &&
        !context.HttpContext.Request.Headers.ContainsKey("Authorization") &&
        !context.HttpContext.Request.Headers.ContainsKey("Cookie"))));
// Production uses the Redis-native Output Cache store, not IDistributedCache.
// Test hosts deliberately use the built-in in-memory store with no network connection.
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddStackExchangeRedisOutputCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "culinary:output:";
    });
var outputStore = builder.Services.Last(service => service.ServiceType == typeof(IOutputCacheStore));
builder.Services.RemoveAll<IOutputCacheStore>();
builder.Services.AddSingleton<IOutputCacheStore>(services =>
{
    var inner = outputStore.ImplementationInstance as IOutputCacheStore
        ?? (IOutputCacheStore)(outputStore.ImplementationFactory?.Invoke(services)
            ?? ActivatorUtilities.CreateInstance(services, outputStore.ImplementationType!));
    return new ResilientOutputCacheStore(inner, services.GetRequiredService<ILogger<ResilientOutputCacheStore>>());
});
builder.Services.AddScoped<IRecipeCacheInvalidator, RecipeCacheInvalidator>();
builder.Services.AddOptions<RecipeDetailCacheOptions>()
    .Bind(builder.Configuration.GetSection(RecipeDetailCacheOptions.SectionName));


// =========================
// Application / CQRS
// =========================

builder.Services.AddMediatR(config =>
    config.RegisterServicesFromAssembly(
        typeof(GetCategoriesQuery).Assembly));


// =========================
// FluentValidation
// =========================

builder.Services.AddValidatorsFromAssembly(
    typeof(GetCategoriesQuery).Assembly);

builder.Services.AddTransient(
    typeof(IPipelineBehavior<,>),
    typeof(LoggingBehavior<,>));
builder.Services.AddTransient(
    typeof(IPipelineBehavior<,>),
    typeof(ValidationBehavior<,>));


// =========================
// Repositories
// =========================

builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
builder.Services.AddScoped<ISearchHistoryRepository, SearchHistoryRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
builder.Services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
builder.Services.AddSingleton<IWelcomeEmailQueue, HangfireWelcomeEmailQueue>();
builder.Services.AddSingleton<IImageCleanupQueue, HangfireImageCleanupQueue>();
builder.Services.AddSingleton<IImageProcessingQueue, HangfireImageProcessingQueue>();
builder.Services.AddSingleton<PasswordResetEmailQueue>();
builder.Services.AddSingleton<IPasswordResetEmailQueue>(services =>
    services.GetRequiredService<PasswordResetEmailQueue>());
builder.Services.AddHostedService<PasswordResetEmailWorker>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddSingleton<ImageFileValidator>();
builder.Services.AddSingleton<RecipeImageResizer>();
builder.Services.AddSingleton<IFileStorageService, MinioFileStorageService>();
builder.Services.AddHttpClient("google-oauth", client => client.BaseAddress = new Uri("https://oauth2.googleapis.com/"));
builder.Services.AddScoped<IGoogleCredentialVerifier, GoogleCredentialVerifier>();
builder.Services.AddOptions<GoogleAuthOptions>().Bind(builder.Configuration.GetSection("Authentication:Google"));
builder.Services.AddOptions<MinioOptions>().Bind(builder.Configuration.GetSection("Minio"));
builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection("Email:Smtp"))
    .Validate(options => builder.Environment.IsDevelopment() || options.EnableSsl,
        "SMTP TLS is required outside Development.")
    .ValidateOnStart();
builder.Services.AddOptions<PasswordResetOptions>()
    .Bind(builder.Configuration.GetSection(PasswordResetOptions.SectionName))
    .Validate(options => Uri.TryCreate(options.FrontendBaseUrl, UriKind.Absolute, out _),
        "PasswordReset:FrontendBaseUrl must be an absolute URL.")
    .Validate(options => builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ||
        Uri.TryCreate(options.FrontendBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
        "Password reset links must use HTTPS outside Development.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddCulinaryHealthChecks();
builder.Services.AddCulinaryBlogOpenTelemetry(builder.Configuration, builder.Environment);
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Version = "v1";
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
            });
        }
        return Task.CompletedTask;
    });
});

if (!builder.Environment.IsEnvironment("Testing") || builder.Configuration.GetValue<bool>("RateLimiting:Enabled"))
{
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var path = context.Request.Path;
            if (!path.StartsWithSegments("/api/v1"))
                return RateLimitPartition.GetNoLimiter("outside-api");

            var bucket = path.StartsWithSegments("/api/v1/auth") ? "auth" :
                HttpMethods.IsPost(context.Request.Method) &&
                path.StartsWithSegments("/api/v1/recipes") &&
                path.Value?.EndsWith("/images", StringComparison.OrdinalIgnoreCase) == true
                    ? "upload" : "api";
            var limit = bucket == "auth" ? 10 : bucket == "upload" ? 5 : 100;
            var ip = context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter($"{bucket}:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
        });
        options.OnRejected = async (rejection, cancellationToken) =>
        {
            var context = rejection.HttpContext;
            var seconds = rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 60;
            context.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(context.Response.Body, new ProblemDetails
            {
                Type = "RATE_LIMIT_EXCEEDED",
                Title = "Too many requests.",
                Status = StatusCodes.Status429TooManyRequests,
                Detail = "Please retry after the indicated delay.",
                Instance = context.Request.Path
            }, cancellationToken: cancellationToken);
        };
    });
}

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHangfire(configuration => configuration
        .UseFilter(new HangfireJobMetricsFilter())
        .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(connectionString)));
    builder.Services.AddHangfireServer();
}

// =========================
// ASP.NET Core Identity
// =========================

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        options.User.RequireUniqueEmail = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// =========================
// JWT Authentication
// =========================

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwtKey =
            builder.Configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException(
                "JWT Key is not configured.");
        }

        var jwtIssuer =
            builder.Configuration["Jwt:Issuer"];

        if (string.IsNullOrWhiteSpace(jwtIssuer))
        {
            throw new InvalidOperationException(
                "JWT Issuer is not configured.");
        }

        var jwtAudience =
            builder.Configuration["Jwt:Audience"];

        if (string.IsNullOrWhiteSpace(jwtAudience))
        {
            throw new InvalidOperationException(
                "JWT Audience is not configured.");
        }

        options.TokenValidationParameters =
            new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(jwtKey)),

                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                var response = context.Response;
                response.StatusCode = StatusCodes.Status401Unauthorized;
                response.ContentType = "application/problem+json";
                var problem = new ProblemDetails
                {
                    Type = "AUTH_TOKEN_INVALID",
                    Title = "Unauthorized.",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = "Authentication is required.",
                    Instance = context.Request.Path
                };
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                await JsonSerializer.SerializeAsync(response.Body, problem,
                    cancellationToken: context.HttpContext.RequestAborted);
            }
        };
    });

// =========================
// Authorization
// =========================

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "Admin",
        policy => policy.RequireRole("Admin"));
});


// =========================
// Cookie behavior for API
// =========================

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode =
            StatusCodes.Status401Unauthorized;

        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode =
            StatusCodes.Status403Forbidden;

        return Task.CompletedTask;
    };
});


// =========================
// Controllers
// =========================




// =========================
// CORS
// =========================

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            // The new Recipe RowVersion of a 204 image delete is only delivered in the ETag header.
            .WithExposedHeaders("ETag")
            .AllowCredentials();
    });
});


var app = builder.Build();

var knownProxy = builder.Configuration["ForwardedHeaders:KnownProxy"];
if (IPAddress.TryParse(knownProxy, out var knownProxyAddress))
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto |
            ForwardedHeaders.XForwardedHost,
        ForwardLimit = 1,
        KnownProxies = { knownProxyAddress }
    });
}

// =========================
// Database Migration & Seed
// =========================

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    var dbContext =
        services.GetRequiredService<AppDbContext>();

    var userManager =
        services.GetRequiredService<UserManager<ApplicationUser>>();

    var roleManager =
        services.GetRequiredService<RoleManager<IdentityRole>>();

    await dbContext.Database.MigrateAsync();

    await DataSeeder.SeedAsync(
        dbContext,
        userManager,
        roleManager);

    services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<OrphanImageCleanupJob>(
        "orphan-image-cleanup", job => job.RunAsync(), "30 3 * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<OrphanImageCleanupJob>(
        "pending-image-recovery", job => job.RecoverPendingAsync(), "*/5 * * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
}

// =========================
// Middleware
// =========================
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("Frontend");
if (!app.Environment.IsEnvironment("Testing") || app.Configuration.GetValue<bool>("RateLimiting:Enabled"))
    app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();


// =========================
app.MapCulinaryHealthChecks();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}


// =========================
// Controller Endpoints
// =========================

// =========================
// Authentication Endpoints
// =========================

var auth =
    app.MapGroup("/api/v1/auth");

auth.MapTokenEndpoints();
auth.MapAccountEndpoints();

// =========================
// FR-RCP-001
// GET /api/v1/recipes?page=1&pageSize=12
// =========================

var recipes = app.MapGroup("/api/v1/recipes");

recipes.MapGet("/", async (
    HttpContext context,
    int? page,
    int? pageSize,
    Guid? categoryId,
    RecipeDifficulty? difficulty,
    int? maxCookTime,
    int? minServings,
    string? sortBy,
    string? sortOrder,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var userId =
        context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;

    var isAdmin = context.User.IsInRole("Admin");

    var result = await sender.Send(
        new GetRecipesQuery(
            page ?? 1,
            pageSize ?? 12,
            userId,
            isAdmin,
            categoryId,
            difficulty,
            maxCookTime,
            minServings,
            string.IsNullOrWhiteSpace(sortBy) ? "createdAt" : sortBy,
            string.IsNullOrWhiteSpace(sortOrder) ? "desc" : sortOrder),
        cancellationToken);

    return Results.Ok(result);
}).CacheOutput("RecipeList");

recipes.MapGet("/search", async (
    string? q, int? page, int? pageSize, ISender sender,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await sender.Send(
        new SearchRecipesQuery(q ?? string.Empty, page ?? 1, pageSize ?? 12),
         cancellationToken));
});
// =========================
// Login
// POST /api/v1/auth/login
// =========================

auth.MapPost("/login", async (
    LoginRequest request,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.Problem(
            type: "VALIDATION_ERROR",
            title: "Validation failed.",
            statusCode: StatusCodes.Status400BadRequest,
            detail:
                "Email and password are required.");
    }

    var result = await sender.Send(
        new LoginCommand(
            request.Email,
            request.Password),
        cancellationToken);

    if (!result.Succeeded)
    {
        if (result.ErrorCode == "AUTH_ACCOUNT_LOCKED")
            return Results.Problem(type: result.ErrorCode, title: "Account locked.",
                statusCode: StatusCodes.Status423Locked,
                detail: "Too many failed sign-in attempts. Try again after the lockout ends.",
                extensions: new Dictionary<string, object?> { ["lockedUntil"] = result.LockoutEnd });
        return Results.Problem(
            type:
                result.ErrorCode
                ?? "INVALID_CREDENTIALS",
            title: "Login failed.",
            statusCode:
                StatusCodes.Status401Unauthorized,
            detail:
                "Email or password is incorrect.");
    }

    return Results.Ok(
        new AuthResponseDto(
            result.AccessToken!,
            result.TokenType,
            result.RefreshToken!,
            result.ExpiresAt!.Value,
            result.User!));
});

// =========================
// Register
// POST /api/v1/auth/register
// =========================

auth.MapPost("/register", async (
    RegisterCommand request,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await sender.Send(request, cancellationToken);

        if (result.Tokens is null)
        {
            if (result.ErrorCode == "AUTH_EMAIL_EXISTS")
            {
                return Results.Problem(
                    type: "AUTH_EMAIL_EXISTS",
                    title: "Email already exists.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            return Results.Problem(
                type: result.ErrorCode ?? "REGISTRATION_FAILED",
                title: "Registration failed.",
                statusCode: result.ErrorCode == "VALIDATION_ERROR"
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status500InternalServerError);
        }

        return Results.Created(
            "/api/v1/auth/register",
            result.Tokens);
    }
    catch (ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error => error.ErrorMessage)
                    .ToArray());

        return Results.ValidationProblem(
            errors,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation failed.",
            type: "VALIDATION_ERROR");
    }
});

// =========================
// Recipe Endpoints
// =========================

// =========================
// FR-RCP-002
// GET /api/v1/recipes/{slug}
// Recipe Detail
// =========================

recipes.MapGet("/{slug}", async (
    string slug,
    HttpContext httpContext,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var userId =
        httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;

    var isAdmin =
        httpContext.User.IsInRole("Admin");

    var result = await sender.Send(
        new GetRecipeDetailQuery(slug, userId, isAdmin),
        cancellationToken);

    return Results.Ok(result);
});


// =========================
// Category Endpoints
// =========================

var categories =
    app.MapGroup("/api/v1/categories");


// =========================
// FR-CAT-001
// GET /api/v1/categories
// =========================

categories.MapGet("/", async (
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(
        new GetCategoriesQuery(),
        cancellationToken);

    return Results.Ok(result);
});


// =========================
// FR-CAT-002
// GET /api/v1/categories/{slug}
// =========================

categories.MapGet("/{slug}", async (
    string slug,
    int? page,
    int? pageSize,
    HttpContext httpContext,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var currentPage = page ?? 1;
    var currentPageSize = pageSize ?? 12;

    // Validate pagination
    if (currentPage < 1 ||
        currentPageSize < 1 ||
        currentPageSize > 50)
    {
        return Results.Problem(
            type: "VALIDATION_ERROR",
            title: "Validation failed.",
            statusCode: StatusCodes.Status400BadRequest,
            detail:
                "page must be >= 1 and pageSize must be between 1 and 50.");
    }

    // Get current authenticated user
    var userId =
        httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;

    // Check Admin role
    var isAdmin =
        httpContext.User.IsInRole("Admin");

    // Dispatch query
    var result = await sender.Send(
        new GetCategoryBySlugQuery(
            slug,
            currentPage,
            currentPageSize,
            userId,
            isAdmin),
        cancellationToken);

    // Category not found
    if (result is null)
    {
        return Results.Problem(
            type: "CATEGORY_NOT_FOUND",
            title: "Category not found.",
            statusCode: StatusCodes.Status404NotFound,
            detail:
                "The requested category does not exist.");
    }

    return Results.Ok(result);
});


// =========================
// FR-CAT-003
// POST /api/v1/categories
// Admin creates Category
// =========================

categories.MapPost("/", async (
    CreateCategoryRequest request,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(
        new CreateCategoryCommand(
            request.Name,
            request.Description),
        cancellationToken);

    return Results.Created(
        $"/api/v1/categories/{result.Slug}",
        result);
})
.RequireAuthorization("Admin");


// =========================
// FR-CAT-004
// PUT /api/v1/categories/{id}
// Admin updates Category
// Slug does NOT change
// =========================

categories.MapPut("/{id:guid}", async (
    Guid id,
    UpdateCategoryRequest request,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(
        new UpdateCategoryCommand(
            id,
            request.Name,
            request.Description),
        cancellationToken);

    if (result is null)
    {
        return Results.Problem(
            type: "CATEGORY_NOT_FOUND",
            title: "Category not found.",
            statusCode: StatusCodes.Status404NotFound,
            detail:
                "The requested category does not exist.");
    }

    return Results.Ok(result);
})
.RequireAuthorization("Admin");


// =========================
// FR-CAT-005
// DELETE /api/v1/categories/{id}
// Admin deletes Category
// =========================

categories.MapDelete("/{id:guid}", async (
    Guid id,
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(
        new DeleteCategoryCommand(id),
        cancellationToken);

    // Category does not exist
    if (!result.Found)
    {
        return Results.Problem(
            type: "CATEGORY_NOT_FOUND",
            title: "Category not found.",
            statusCode: StatusCodes.Status404NotFound,
            detail:
                "The requested category does not exist.");
    }

    // Category still contains Recipes
    if (result.RecipeCount > 0)
    {
        return Results.Problem(
            type: "CATEGORY_DELETE_HAS_RECIPES",
            title: "Category still contains recipes.",
            statusCode: StatusCodes.Status409Conflict,
            detail:
                $"Danh mục còn chứa {result.RecipeCount} công thức.",
            extensions: new Dictionary<string, object?>
            {
                ["recipeCount"] = result.RecipeCount
            });
    }

    return Results.NoContent();
})
.RequireAuthorization("Admin");


// =========================
// Run Application
// =========================

app.MapRecipeEndpoints();
app.MapRecipeImageEndpoints();
app.MapAdminUserEndpoints();

app.Run();
}
catch (Exception exception)
{
    Log.Fatal(
        "CulinaryBlog API terminated unexpectedly during startup ({ExceptionType}).",
        exception.GetType().Name);
    throw;
}
finally
{
    Log.CloseAndFlush();
}


// =========================
// Request Models
// =========================

public sealed record CreateCategoryRequest(
    string Name,
    string? Description);

public sealed record UpdateCategoryRequest(
    string Name,
    string? Description);

public sealed record LoginRequest(
    string Email,
    string Password);

public partial class Program { }
