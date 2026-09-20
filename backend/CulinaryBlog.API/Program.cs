using CulinaryBlog.Application.Categories.Queries;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Repositories;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
// Application / CQRS
// =========================

builder.Services.AddMediatR(config =>
    config.RegisterServicesFromAssembly(
        typeof(GetCategoriesQuery).Assembly));


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

builder.Services.AddAuthorization();


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
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


var app = builder.Build();


// =========================
// Middleware
// =========================

app.UseCors("AllowFrontend");

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
    // Default pagination
    var currentPage = page ?? 1;
    var currentPageSize = pageSize ?? 12;


    // =========================
    // Validate pagination
    // =========================

    if (currentPage < 1 ||
        currentPageSize < 1 ||
        currentPageSize > 50)
    {
        return Results.BadRequest(new
        {
            error = "VALIDATION_ERROR",
            message =
                "page must be >= 1 and pageSize must be between 1 and 50."
        });
    }


    // =========================
    // Current User
    // =========================

    var userId =
        httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;


    // =========================
    // Check Admin Role
    // =========================

    var isAdmin =
        httpContext.User.IsInRole("Admin");


    // =========================
    // Dispatch Query
    // =========================

    var result = await sender.Send(
        new GetCategoryBySlugQuery(
            slug,
            currentPage,
            currentPageSize,
            userId,
            isAdmin),
        cancellationToken);


    // =========================
    // Category Not Found
    // =========================

    if (result is null)
    {
        return Results.NotFound(new
        {
            error = "CATEGORY_NOT_FOUND",
            message = "Category not found."
        });
    }


    // =========================
    // Success
    // =========================

    return Results.Ok(result);
});


app.Run();