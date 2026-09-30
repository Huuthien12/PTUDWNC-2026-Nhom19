# Member 3 — đợt 4: FR-RCP-004 Update Recipe

## Contract đã triển khai

`PUT /api/v1/recipes/{id:guid}` yêu cầu Bearer JWT role Author hoặc Admin.
Author chỉ sửa recipe của mình; Admin sửa được recipe của người khác. UserId và
IsAdmin lấy từ claims, không bind từ JSON. Handler load tracked root rồi kiểm tra
owner/Admin trước validation và trước mutation. Recipe missing/soft-deleted trả 404.
JSON binding và role middleware vẫn chạy trước handler.

- **Giữ nguyên slug khi title đổi**, giữ URL ổn định. Title có thể trùng title
  recipe khác. Không nhận slug từ body; PUT này không có nhánh slug conflict 409.
- Nutrition bỏ qua hoặc null **giữ nguyên**. Object được gửi thay thế bốn giá trị;
  field không gửi trong object thành null; `{}` hoặc bốn null xóa các giá trị.
  Giá trị 0 vẫn là giá trị thật.
- Cập nhật Title, Description, Instructions, CategoryId, PrepTime, CookTime,
  Servings, Difficulty và Nutrition nếu có. Category phải tồn tại, nếu không trả
  400 VALIDATION_ERROR với field CategoryId.
- ID chỉ lấy từ route. Không đổi AuthorId, Status, PublishedAt, CreatedAt, Slug,
  IsDeleted hoặc children. Field JSON lạ bị bỏ qua theo serializer hiện tại.
- UpdateRecipeRequestValidator đã commit được gọi sau resource authorization:
  cookTime >= 0; instructions không null, được rỗng. Không thêm alias field.
- RowVersion bắt buộc trong body, Base64 chuẩn không rỗng. Chưa nhận If-Match.
  Repository.Update nhận token **của client** làm OriginalValue; SaveChanges kiểm
  tra concurrency tại database, không thay bằng token vừa đọc và không tự retry.
- Mỗi PUT thành công cập nhật UpdatedAt và sinh version mới, kể cả Nutrition-only
  hoặc gửi lại cùng nội dung. Root và owned Nutrition lưu atomic trong một save.
- Response DTO trực tiếp, không envelope, theo API Create hiện tại.

## HTTP / JSON

Ví dụ body (dùng CategoryId và RowVersion thật đã đọc từ server):

```json
{
  "title": "Canh chua cập nhật",
  "description": "Mô tả mới",
  "categoryId": "11111111-1111-1111-1111-111111111111",
  "prepTime": 10,
  "cookTime": 0,
  "servings": 2,
  "difficulty": 1,
  "instructions": "Hướng dẫn mới",
  "rowVersion": "AQIDBAUGBwgJCgsMDQ4PEA==",
  "nutrition": { "protein": 12.5, "fat": 0 }
}
```

| Trường hợp | Kết quả |
|---|---|
| Owner Author hoặc Admin cập nhật thành công | 200 RecipeDto, RowVersion mới dạng Base64 |
| Thiếu/invalid/expired JWT hoặc thiếu user identifier | 401 |
| Role không phù hợp | 403 từ middleware |
| Author không sở hữu recipe | 403 `RECIPE_FORBIDDEN` |
| Missing hoặc soft-deleted recipe | 404 `RECIPE_NOT_FOUND` |
| Validation, Category không tồn tại | 400 `VALIDATION_ERROR`, `errors` theo field |
| JSON/GUID body sai cú pháp | 400 từ binding, không đảm bảo ProblemDetails |
| RowVersion hợp lệ về encoding nhưng stale | 422 `RECIPE_CONCURRENCY_CONFLICT` |

Ví dụ stale response (`application/problem+json`):

```json
{
  "type": "RECIPE_CONCURRENCY_CONFLICT",
  "title": "Recipe has changed. Reload it before updating.",
  "status": 422
}
```

422 theo contract và baseline SRS chương 8/phụ lục, thay cho 409 concurrency còn
ghi trong đoạn FR-RCP-004 cũ. Malformed route Guid không match route nên trả 404.

## Cache

Đã rà soát Program, ICacheService, RedisCacheService và các query hiện tại.
Cache thực sự đang có là `categories:all`, chứa số Published Recipe theo category.
Sau save thành công, nếu Recipe Published đổi CategoryId, handler xóa key này
để count cả category cũ/mới được tính lại. Stale, forbidden, validation và missing
không invalidate. Draft/Archived đổi category không đổi Published count.

Category detail hiện query database trực tiếp. Recipe list/search/detail cache,
OutputCache tags `recipes` / `recipe:{slug}` chưa tồn tại; chưa triển khai hạ tầng
cache buổi 5 hoặc xóa các key giả định. Khi bổ sung cache đó, cần nối invalidation
sau save Update, đặc biệt với Published content.

Invalidation sử dụng ICacheService hiện có, sau database commit, giống convention
Category. Nếu Redis lỗi ở bước này, database đã lưu nhưng request có thể lỗi;
chưa có outbox/retry bền vững. Test xác nhận lời gọi cache bằng recording fake,
chưa chạy Redis thực hoặc kiểm tra cache refill đồng thời.

## Kiểm chứng

- `dotnet build backend/CulinaryBlog.slnx --no-restore`: thành công, 0 warning/error.
- `dotnet test backend/CulinaryBlog.slnx --no-restore --filter FullyQualifiedName~UpdateRecipeTests`:
  21 passed sau khi sửa seed child entities thành Added trong fixture.
- `dotnet test backend/CulinaryBlog.slnx --no-build --no-restore`:
  **125 passed, 1 skipped, 0 failed**.
- `git diff --check`: không lỗi whitespace (Git chỉ cảnh báo chuẩn hóa LF/CRLF).

Test dùng RecipeApiFactory riêng, JWT được ký và middleware thật, full model và
SQLite relational persistence. Bao phủ owner, Admin khác owner, Reader, Author
khác, anonymous; permission trước validation/mutation; missing/soft-delete;
validation và Category; stale không ghi đè root/Nutrition/token/timestamp;
Nutrition-only, omitted/null/empty; cookTime=0; bảo toàn status Draft/Published/
Archived, owner, ID, slug, timestamps lifecycle và child IDs/content; cache count.

Chưa chạy Update trên PostgreSQL thật. Một test migration PostgreSQL hiện có bị
skip vì chưa cấu hình RECIPE_TEST_POSTGRES. Fixture hỗ trợ biến này cho server
test và database ngẫu nhiên `recipe_tests_*`; không dùng database ứng dụng.
Không tạo/áp migration, không chạy API ngoài Testing.

## File thay đổi

- `backend/CulinaryBlog.Application/Recipes/Commands/UpdateRecipeCommand.cs`
- `backend/CulinaryBlog.API/Recipes/RecipeEndpoints.cs`
- `backend/CulinaryBlog.Tests/RecipeApiFactory.cs`
- `backend/CulinaryBlog.Tests/UpdateRecipeTests.cs`
- `doc/recipe_contract_member3.md`
- `doc/recipe_update_member3.md`

Không sửa Category CRUD, frontend hoặc OCOP. Không commit/push.
