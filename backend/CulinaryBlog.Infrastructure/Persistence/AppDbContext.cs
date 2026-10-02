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

    public DbSet<Category> Categories => Set<Category>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareRecipeVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareRecipeVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareRecipeVersions()
{
    ChangeTracker.DetectChanges();

    foreach (var recipe in ChangeTracker.Entries<Recipe>().ToList())
    {
        var nutrition = recipe.Reference(x => x.Nutrition).TargetEntry;

        var nutritionChanged =
            nutrition?.State is EntityState.Added
                or EntityState.Modified
                or EntityState.Deleted;

        if (recipe.State is not (EntityState.Added or EntityState.Modified)
            && !nutritionChanged)
        {
            continue;
        }

        // Recipe.RowVersion is the single concurrency token
        // for the Recipes table. Nutrition shares the same table,
        // so it must not have a second concurrency property mapped
        // to the same physical column.
        if (recipe.State == EntityState.Unchanged)
        {
            recipe.State = EntityState.Modified;
        }

        var version = recipe.Property(x => x.RowVersion);
        var nextVersion = Guid.NewGuid().ToByteArray();

        version.CurrentValue = nextVersion;

        if (recipe.State != EntityState.Added)
        {
            version.IsModified = true;
        }
    }
}

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

            entity.Property(rt => rt.Token)
                .HasMaxLength(64);

            entity.Property(rt => rt.ReplacedByToken)
                .HasMaxLength(64);

            entity.HasIndex(rt => rt.Token)
                .IsUnique();

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
