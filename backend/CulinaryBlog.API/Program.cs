var builder = WebApplication.CreateBuilder(args);

// Bật CORS cho phép Frontend Next.js gọi API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowFrontend");

// Endpoint Health Check test kết nối
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", message = "Backend .NET 10 is running!" }));

app.Run();