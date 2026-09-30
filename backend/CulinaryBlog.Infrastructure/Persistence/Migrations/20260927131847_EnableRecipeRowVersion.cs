using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableRecipeRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep existing non-empty tokens and all recipe content unchanged.
            // Built-in PostgreSQL functions only; no extension or column conversion required.
            migrationBuilder.Sql("""
                UPDATE "Recipes"
                SET "RowVersion" = decode(md5("Id"::text || random()::text || clock_timestamp()::text), 'hex')
                WHERE octet_length("RowVersion") = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Tokens cannot be restored safely: doing so could allow stale clients to write.
        }
    }
}
