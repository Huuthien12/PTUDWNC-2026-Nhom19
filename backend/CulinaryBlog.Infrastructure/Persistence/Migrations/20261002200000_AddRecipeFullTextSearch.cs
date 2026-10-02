using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Persistence.Migrations;

[Migration("20261002200000_AddRecipeFullTextSearch")]
public partial class AddRecipeFullTextSearch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""CREATE EXTENSION IF NOT EXISTS unaccent;""");
        migrationBuilder.Sql("""CREATE EXTENSION IF NOT EXISTS pg_trgm;""");
        migrationBuilder.AddColumn<string>(
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
                NEW."SearchVector" :=
                    to_tsvector('simple', unaccent(coalesce(NEW."Title", '') || ' ' || coalesce(NEW."Description", '')));
                RETURN NEW;
            END $$;
            """);
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS recipe_search_vector_update ON "Recipes";
            CREATE TRIGGER recipe_search_vector_update
            BEFORE INSERT OR UPDATE OF "Title", "Description" ON "Recipes"
            FOR EACH ROW EXECUTE FUNCTION update_recipe_search_vector();
            """);
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IDX_Recipe_Search"
            ON "Recipes" USING GIN ("SearchVector");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP INDEX IF EXISTS "IDX_Recipe_Search";""");
        migrationBuilder.Sql("""DROP TRIGGER IF EXISTS recipe_search_vector_update ON "Recipes";""");
        migrationBuilder.Sql("""DROP FUNCTION IF EXISTS update_recipe_search_vector();""");
        migrationBuilder.DropColumn(name: "SearchVector", table: "Recipes");
    }
}
