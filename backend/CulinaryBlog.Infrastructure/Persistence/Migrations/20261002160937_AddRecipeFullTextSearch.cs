using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace CulinaryBlog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Recipes",
                type: "tsvector",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Recipes"
                SET "SearchVector" = to_tsvector('simple', unaccent(coalesce("Title", '') || ' ' || coalesce("Description", '')));
                """);
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION update_recipe_search_vector()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    NEW."SearchVector" := to_tsvector('simple', unaccent(coalesce(NEW."Title", '') || ' ' || coalesce(NEW."Description", '')));
                    RETURN NEW;
                END $$;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER recipe_search_vector_update
                BEFORE INSERT OR UPDATE OF "Title", "Description" ON "Recipes"
                FOR EACH ROW EXECUTE FUNCTION update_recipe_search_vector();
                """);

            migrationBuilder.CreateIndex(
                name: "IDX_Recipe_Search",
                table: "Recipes",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS recipe_search_vector_update ON \"Recipes\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_recipe_search_vector();");

            migrationBuilder.DropIndex(
                name: "IDX_Recipe_Search",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Recipes");
        }
    }
}
