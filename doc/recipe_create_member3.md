# Member 3 — đợt 3: FR-RCP-003 Create Recipe

`POST /api/v1/recipes` yêu cầu Bearer JWT có role `Author` hoặc `Admin`.
AuthorId lấy từ claim NameIdentifier theo JwtTokenService hiện tại; token thiếu
user identifier trả 401. Endpoint dùng CreateRecipeRequest đã commit, command
validator tái sử dụng CreateRecipeRequestValidator qua ValidationBehavior.

## Request và response

Ví dụ request (CategoryId phải tồn tại):

```json
{
  "title": "Canh chua cá",
  "description": "Canh chua",
  "categoryId": "11111111-1111-1111-1111-111111111111",
  "prepTime": 10,
  "cookTime": 20,
  "servings": 2,
  "difficulty": 1,
  "instructions": "Nấu chín cá",
  "nutrition": { "calories": 150, "protein": 12.5, "carbs": null, "fat": 0 }
}
```

201, `Location: /api/v1/recipes/canh-chua-ca`, DTO trực tiếp theo API hiện tại
(không bọc `data`). Ví dụ minh họa; ID, timestamps và RowVersion sinh tại server:

```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "title": "Canh chua cá",
  "slug": "canh-chua-ca",
  "description": "Canh chua",
  "instructions": "Nấu chín cá",
  "categoryId": "11111111-1111-1111-1111-111111111111",
  "authorId": "jwt-user-id",
  "prepTime": 10,
  "cookTime": 20,
  "servings": 2,
  "difficulty": 1,
  "status": 1,
  "nutrition": { "calories": 150, "protein": 12.5, "carbs": null, "fat": 0 },
  "rowVersion": "AQIDBAUGBwgJCgsMDQ4PEA==",
  "createdAt": "2026-09-27T00:00:00Z",
  "updatedAt": "2026-09-27T00:00:00Z",
  "publishedAt": null
}
```

| Trường hợp | Status / body |
|---|---|
| Thành công | 201 RecipeDto, RowVersion Base64 từ bản ghi đã lưu |
| DTO không hợp lệ, Category không tồn tại, title sinh slug rỗng | 400 ProblemDetails, `type: VALIDATION_ERROR`, `errors` theo field |
| JSON/GUID sai cú pháp | 400 từ Minimal API binding; không đảm bảo body ProblemDetails |
| Không có JWT hoặc JWT hết hạn/không hợp lệ | 401 từ JWT middleware |
| JWT hợp lệ nhưng thiếu Author/Admin | 403 từ authorization middleware |
| JWT có role nhưng thiếu user identifier | 401 ProblemDetails, `type: AUTH_TOKEN_INVALID` |
| Slug trùng, kể cả bản ghi soft-delete hoặc race unique constraint | 409 ProblemDetails, `type: RECIPE_SLUG_EXISTS` |

Ví dụ lỗi slug:

```json
{"type":"RECIPE_SLUG_EXISTS","title":"Recipe slug already exists.","status":409}
```

## Phạm vi và persistence

- Status luôn Draft; AuthorId, Slug, RowVersion không bind từ body.
- Instructions bỏ qua/null thành chuỗi rỗng. Nutrition bỏ qua/null có object
  response với bốn giá trị null; phân biệt giá trị 0 với null.
- Contract đã chọn thêm ingredients/steps riêng sau. Endpoint này không tạo
  children. JSON field lạ vẫn bị serializer bỏ qua, kể cả children và field
  server quản lý; không thay đổi serializer toàn API.
- Root và owned Nutrition lưu bằng một SaveChanges atomic; dùng repository và
  cơ chế RowVersion hiện có. Không thêm migration.
- Slug dùng SlugHelper hiện có, không tự thêm suffix; slug rỗng trả 400.
  Catch race chỉ nhận PostgreSQL `23505` với constraint `IX_Recipes_Slug`;
  không chuyển mọi lỗi database thành 409.
- Recipe list/search/detail và cache keys tương ứng chưa được triển khai trong
  repository hiện tại. Chưa thêm invalidation cho các key giả định. Cache Category
  hiện đếm Published; Create Draft không làm thay đổi count này.
- Location chỉ định URI theo slug; GET Recipe detail chưa thuộc đợt này.

## Kiểm chứng và giới hạn

Đã chạy `dotnet build backend/CulinaryBlog.slnx --no-restore` thành công, không
warning/error; `dotnet test backend/CulinaryBlog.slnx --no-restore` build lại và
cho kết quả **104 passed, 1 skipped, 0 failed**.

RecipeApiFactory riêng dùng RecipeDatabaseFixture với toàn bộ production model,
JWT được ký thật và middleware thật; không dùng AuthApiFactory. Environment
`Testing` bỏ startup migrate/seed; connection mặc định trỏ cổng không sử dụng
trước khi DI thay bằng fixture. Không chạy API production hoặc migrate vào
`CulinaryBlogDb_Thuan_Main`; chưa kiểm tra migration history của database đó.

Test mới kiểm tra HTTP 201/400/401/403/409, persisted Draft/owner/slug/token,
Nutrition, instructions, chống ghi đè field server, children không được lưu,
slug của bản ghi soft-delete. Test hai context cùng precheck slug rồi ghi lần
lượt chứng minh unique constraint chặn writer thứ hai và rollback toàn save,
kể cả thay đổi Nutrition khác trong cùng save.

Lần chạy này dùng SQLite relational. Nhánh HTTP PostgreSQL unique violation
được kiểm tra bằng DbUpdateException/PostgresException mô phỏng sau precheck;
chưa xác minh race HTTP end-to-end trên PostgreSQL. Test migration PostgreSQL
hiện có bị skip vì chưa cấu hình `RECIPE_TEST_POSTGRES`.
Có thể chạy lại trên server test với biến này: fixture tự thay database thành
`recipe_tests_<guid>`, tạo/drop riêng database đó (cần quyền CREATE DATABASE).
Không dùng database chính để chạy test.

## File thay đổi

- `backend/CulinaryBlog.API/Program.cs`: đăng ký endpoint Recipe.
- `backend/CulinaryBlog.API/Recipes/RecipeEndpoints.cs`: HTTP/auth/error mapping.
- `backend/CulinaryBlog.Application/Recipes/Commands/CreateRecipeCommand.cs`: command, validator, handler.
- `backend/CulinaryBlog.Tests/RecipeApiFactory.cs`: fixture API riêng.
- `backend/CulinaryBlog.Tests/CreateRecipeTests.cs`: HTTP và persistence tests.
- `doc/recipe_create_member3.md`: báo cáo này.

Không sửa Category CRUD, frontend, OCOP; không commit/push.
