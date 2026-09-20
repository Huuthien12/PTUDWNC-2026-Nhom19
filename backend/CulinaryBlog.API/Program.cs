using CulinaryBlog.Application.Categories.Commands;
using CulinaryBlog.Application.Categories.Queries;
using CulinaryBlog.Application.Common.Behaviors;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Infrastructure.Caching;

var builder = WebApplication.CreateBuilder(args);

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
    typeof(ValidationBehavior<,>));


// =========================
// Repositories
// =========================

builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();


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
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();


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

builder.Services.AddControllers();


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
            .AllowCredentials();
    });
});


var app = builder.Build();


// =========================
// Middleware
// =========================
app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();


// =========================
// Health Check
// =========================

app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "Healthy",
        message = "Backend .NET 10 is running!"
    }));


// =========================
// Controller Endpoints
// =========================

app.MapControllers();


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
    try
    {
        var result = await sender.Send(
            new CreateCategoryCommand(
                request.Name,
                request.Description),
            cancellationToken);

        return Results.Created(
            $"/api/v1/categories/{result.Slug}",
            result);
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
    catch (InvalidOperationException exception)
        when (exception.Message == "CATEGORY_NAME_EXISTS")
    {
        return Results.Problem(
            type: "CATEGORY_NAME_EXISTS",
            title: "Category name already exists.",
            statusCode: StatusCodes.Status409Conflict,
            detail:
                "A category with this name already exists.");
    }
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
    try
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
    catch (InvalidOperationException exception)
        when (exception.Message == "CATEGORY_NAME_EXISTS")
    {
        return Results.Problem(
            type: "CATEGORY_NAME_EXISTS",
            title: "Category name already exists.",
            statusCode: StatusCodes.Status409Conflict,
            detail:
                "A category with this name already exists.");
    }
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

app.Run();


// =========================
// Request Models
// =========================

public sealed record CreateCategoryRequest(
    string Name,
    string? Description);

public sealed record UpdateCategoryRequest(
    string Name,
    string? Description);