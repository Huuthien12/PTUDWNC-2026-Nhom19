# Recipe concurrency

`AppDbContext.SaveChanges[Async]` generates a new 16-byte token for added/modified
Recipes and for changes to their owned Nutrition. Both entities map the same
`Recipes.RowVersion` bytea column. Other entities retain their existing behavior.
Unchanged saves do not rotate the token. Direct SQL/ExecuteUpdate bypass this
pipeline and must implement their own token rotation and comparison.

For future update handlers: load with `IRecipeRepository.GetByIdAsync`, map allowed
fields, then call `Update(recipe, Convert.FromBase64String(request.RowVersion))`
and save through the unit of work. Return the saved token with
`Convert.ToBase64String(recipe.RowVersion)`. Handle `DbUpdateConcurrencyException`
as a conflict; do not retry with the latest token without client review.
The repository intentionally requires a tracked root and does not mark related
Author/Category/collections modified. Slug checks include soft-deleted rows because
the unique database index includes them; the index remains the final race guard.

## Migration review

`20260927131847_EnableRecipeRowVersion` adds no columns and changes no column types.
Its only data operation fills empty Recipe tokens with 16-byte values using built-in
PostgreSQL functions. Existing non-empty tokens and every other column are retained.
Down deliberately does not restore empty tokens, which could re-enable stale writes.

Before applying to an actual application database, check its migration history and
affected row count (the implementation run did not access that database):

```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
SELECT count(*) AS total,
       count(*) FILTER (WHERE octet_length("RowVersion") = 0) AS backfill_rows
FROM "Recipes";
```

Backfill updates lock affected rows and generate WAL; plan an appropriate deployment
window for a large table. Deploy all writers together: older application instances
do not rotate versions. Clients with cached empty tokens must reload after backfill.
The PostgreSQL integration test migrates from the previous migration with both empty
and non-empty tokens and compares all Recipe content before/after the new migration.

## Tests

Default tests use a dedicated SQLite relational fixture with the complete AppDbContext
model, without AuthApiFactory. To run the same concurrency tests plus the migration
data-preservation test on PostgreSQL, set `RECIPE_TEST_POSTGRES` to a disposable test
server connection. The account needs CREATE DATABASE permission. Each fixture creates
and drops only its own randomly named `recipe_tests_*` database.

```powershell
dotnet build backend/CulinaryBlog.slnx
dotnet test backend/CulinaryBlog.Tests/CulinaryBlog.Tests.csproj
# With RECIPE_TEST_POSTGRES set:
dotnet test backend/CulinaryBlog.Tests/CulinaryBlog.Tests.csproj --filter FullyQualifiedName~RecipeConcurrencyTests
```
