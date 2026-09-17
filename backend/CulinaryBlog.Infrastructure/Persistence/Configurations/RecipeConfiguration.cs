using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("Recipes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Slug)
            .HasMaxLength(220)
            .IsRequired();

        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.Property(x => x.Description)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Instructions)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Difficulty)
            .HasConversion<short>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<short>()
            .IsRequired();

        builder.Property(x => x.AuthorId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.OwnsOne(x => x.Nutrition, nutrition =>
        {
            nutrition.Property(x => x.Calories)
                .HasColumnName("Nutrition_Calories");

            nutrition.Property(x => x.Protein)
                .HasColumnName("Nutrition_Protein");

            nutrition.Property(x => x.Carbs)
                .HasColumnName("Nutrition_Carbs");

            nutrition.Property(x => x.Fat)
                .HasColumnName("Nutrition_Fat");
        });

        builder.Navigation(x => x.Nutrition)
            .IsRequired();
    }
}