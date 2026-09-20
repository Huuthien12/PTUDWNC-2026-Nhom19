using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Application.Categories.Queries;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Infrastructure.Repositories;
using MediatR;

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


// Map Controller endpoints
app.MapControllers();


// =========================
// Category Endpoints
// =========================

var categories = app.MapGroup("/api/v1/categories");

categories.MapGet("/", async (
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var result = await sender.Send(
        new GetCategoriesQuery(),
        cancellationToken);

    return Results.Ok(result);
});


app.Run();