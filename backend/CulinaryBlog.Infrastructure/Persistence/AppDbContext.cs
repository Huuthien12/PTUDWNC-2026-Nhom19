using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // =========================
    // DbSets
    // =========================

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Recipe> Recipes => Set<Recipe>();

    public DbSet<RecipeIngredient> RecipeIngredients
        => Set<RecipeIngredient>();

    public DbSet<RecipeStep> RecipeSteps
        => Set<RecipeStep>();

    public DbSet<RecipeImage> RecipeImages
        => Set<RecipeImage>();


    // =========================
    // Model Configuration
    // =========================

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Nạp toàn bộ Fluent API configurations
        // hiện có trong Infrastructure
        builder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly
        );


        // =========================
        // ASP.NET Core Identity
        // =========================
        // SRS: User Id dùng string và Recipe.AuthorId
        // là varchar(450), FK -> AspNetUsers.Id.

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.Id)
                .HasMaxLength(450);
        });

        builder.Entity<IdentityRole>(entity =>
        {
            entity.Property(role => role.Id)
                .HasMaxLength(450);
        });


        // =========================
        // RefreshToken
        // =========================

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);

            entity.Property(rt => rt.UserId)
                .HasMaxLength(450);

            entity.HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        // =========================
        // Recipe - Author
        // =========================

        builder.Entity<Recipe>(entity =>
        {
            entity.Property(r => r.AuthorId)
                .HasMaxLength(450);

            entity.HasOne(r => r.Author)
                .WithMany(u => u.Recipes)
                .HasForeignKey(r => r.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}