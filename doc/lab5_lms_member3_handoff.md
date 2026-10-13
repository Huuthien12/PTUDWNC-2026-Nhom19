# Báo cáo cá nhân Lab 5

Nguyễn Khiêm Thuận — 2312764 — Member 3

Ngày kiểm tra 13/10/2026 · Hạn nộp 23:59 ngày 13/10/2026

Nhánh `thuan/lab5-recipe-cache` · HEAD `eecd0f3`

Báo cáo đối chiếu 5 yêu cầu LMS do sinh viên cung cấp với code đang có trên nhánh cá nhân. Phần cache theo phân công README đã được giữ nguyên và chốt lỗi quyền xem/revalidation. Nhánh cá nhân **chưa đủ toàn bộ yêu cầu LMS**: còn thiếu Rate Limiting, tài liệu OpenAPI được sinh tự động và cơ chế API versioning. Không coi hoàn tất cache là hoàn tất Lab 5.

## Đối chiếu đúng 5 yêu cầu LMS

Các đường dẫn sau tính từ repo root. “Có” nói về code và kiểm tra nêu cụ thể, không có nghĩa đã demo môi trường thật.

| Yêu cầu LMS | Bằng chứng trên nhánh cá nhân | Trạng thái và kiểm chứng | Cá nhân và phần nhóm |
| --- | --- | --- | --- |
| 1. CORS, Rate Limiting, Authentication/Authorization | `backend/CulinaryBlog.API/Program.cs`: `AddCors("Frontend")`, `UseCors("Frontend")`, JWT Bearer, `UseAuthentication`, `UseAuthorization`, policy Admin. `Recipes/RecipeEndpoints.cs`, `Recipes/RecipeImageEndpoints.cs`: endpoint có `RequireAuthorization`; `Application/Recipes/Commands/UpdateRecipeCommand.cs` và các handler: kiểm tra owner/Admin. | CORS và auth có, áp dụng thật trong TestServer; 12 test tập trung đạt. Không có `AddRateLimiter`, `UseRateLimiter` hoặc policy limiter trên nhánh này; chưa có bằng chứng HTTP 429. | CORS/JWT/endpoint auth là nền tảng đã có của nhóm; không nhận là mới triển khai Lab 5. Member 3 bổ sung 3 case CORS, chạy kiểm chứng JWT/role/ownership và bảo vệ Detail cache. Rate Limiting cần phối hợp tích hợp foundation của Thiện. |
| 2. Sinh tài liệu đặc tả API và API versioning | Route `/api/v1/auth`, `/api/v1/recipes`, `/api/v1/categories` trong `Program.cs` và các endpoint extension. `CulinaryBlog.API.csproj` không có OpenAPI/Scalar/versioning; không có `AddOpenApi`, `MapOpenApi` hoặc đăng ký API versioning trên nhánh này. | Thiếu sinh tài liệu API và versioning có metadata/chọn phiên bản. `/api/v1` chỉ là tiền tố URL; không đủ bằng chứng cơ chế versioning. Không có đường OpenAPI/UI được map trên nhánh cá nhân. | Đây là khoảng thiếu foundation dùng chung; Member 3 đã rà soát, chưa triển khai/chưa nhận hoàn thành. Remote main có OpenAPI/Scalar do Thiện, nhưng chưa tích hợp vào nhánh cá nhân; remote cũng chưa có cơ chế versioning qua rà soát code. |
| 3. Cài đặt môi trường, cấu trúc dự án frontend | `frontend/.env.example`: `NEXT_PUBLIC_API_URL`; `package.json`: dev/start/test/lint/typecheck/build; `tsconfig.json`: strict và alias `@/*`; `next.config.ts`; `src/app`, `components`, `services`, `types`. | Có nền tảng Next.js App Router/TypeScript; lint/typecheck/build đạt trên môi trường hiện tại. Chưa thử cài mới từ máy sạch hoặc trình duyệt thật. | Khởi tạo Next.js/cấu trúc ban đầu thuộc công việc nhóm có sẵn. Member 3 bổ sung route proxy cache, loading/error, empty/retry và tối ưu request vào cấu trúc hiện có. |
| 4. Cài đặt các thư viện cần thiết | `frontend/package.json`, `package-lock.json`: Next 16.3.5, React/React DOM 19.2.8; TypeScript, ESLint, Tailwind CSS 4, PostCSS, typings Node/React. Backend cache: `CulinaryBlog.API.csproj`, `Program.cs`. | Các thư viện frontend đang dùng được: 46 test đạt, lint/typecheck/build đạt. Không cài lại hoặc nâng version. Không có bằng chứng fresh install offline/CI. | Thư viện frontend nền tảng có sẵn của nhóm. Member 3 thêm package Redis Output Cache 10.0.12 và dùng thư viện có sẵn cho cache/UI; không nhận Next/React là mới cài trong lượt này. |
| 5. Các TS types được frontend sử dụng | `src/types/recipe.ts`: RecipeDetail, RecipePage, Create/UpdateRecipeRequest → `recipe-service.ts`, forms, RecipeListClient, detail/dashboard. `types/auth.ts`: AuthResponse/AuthSession → auth-service, auth-session, api-client. `types/category.ts`: Category/CategoryDetail → category-service và UI Category. | Có type được import và dùng trong generic transport, state và props, không chỉ khai báo rời rạc; typecheck/build đạt. Types compile-time không thay cho validation backend. | Member 3 sử dụng types hiện có cho cache/consumer; `auth.ts` có commit cá nhân `2c13679` từ phần auth trước đây. Recipe/Category types có lịch sử đóng góp của nhóm; không nhận toàn bộ types là công việc mới Lab 5. |

## Kết quả kiểm tra tập trung

CORS chỉ cho phép origin `http://localhost:3000`, cho phép credentials, method/header và expose `ETag`; không dùng wildcard origin với credentials. Thứ tự code: GlobalException → CORS → Authentication → Authorization → Output Cache → endpoints. Minimal API dùng routing của framework; không bổ sung lại pipeline. Test preflight kiểm tra origin hợp lệ và origin lạ; test POST chưa đăng nhập xác nhận CORS vẫn áp dụng trên response 401 thực tế. Origin bị từ chối không có CORS allow headers; CORS không thay thế auth và không chặn HTTP client ngoài trình duyệt.

JWT xác minh signing key, issuer, audience, lifetime, ClockSkew zero; không đưa giá trị cấu hình nhạy cảm vào báo cáo. Recipe mutations yêu cầu role Author/Admin; handler kiểm tra owner, không chỉ dựa vào nút UI. Test có sẵn kiểm tra guest/token sai/token hết hạn → 401, role Reader và Author khác owner → 403 và không ghi DB. Category mutations dùng policy Admin; scope mới không sửa contract hay logic quyền của thành viên khác.

Rate Limiting không được đăng ký hoặc áp dụng trên nhánh cá nhân. Nhánh remote `origin/codex/lab5-security-api-foundation`, commit `60757c8` của Huuthien12, đã nằm trong `origin/main`; đây chỉ là bằng chứng đọc code remote. Nó có global fixed window theo IP, 1 phút: auth 10 request, upload image 5, API còn lại 100; queue 0; áp dụng đường `/api/v1`, `UseRateLimiter` trước auth, và handler 429 ProblemDetails/Retry-After. Trong Testing chỉ bật khi `RateLimiting:Enabled=true`. Chưa chạy code remote nên không tuyên bố 429 đã kiểm chứng.

Remote foundation đăng ký `AddOpenApi("v1")`, JWT security scheme và metadata auth, map `MapOpenApi`/`MapScalarApiReference` trong Development/Testing. Đường mặc định dự kiến `/openapi/v1.json` và `/scalar/v1` cần được xác nhận sau tích hợp. `document.Info.Version = "v1"` mô tả tài liệu, không phải cơ chế chọn/kiểm soát phiên bản API. Nhánh cá nhân không có endpoints docs này. Không pull/merge/cherry-pick remote trong lượt bàn giao.

Frontend cấu hình API bằng `NEXT_PUBLIC_API_URL` (mẫu localhost:5062); giá trị public không được chứa secrets. Browser Recipe dùng proxy cùng origin, upstream giữ bearer và If-Match, không forward cookie; SSR transport gọi backend trực tiếp. Thư mục app chứa route/layout, components chứa UI, services chứa transport/nghiệp vụ frontend, types chứa DTO. Script typecheck chạy `next typegen && tsc --noEmit`, test biên dịch TS rồi dùng Node test runner.

## Công việc cá nhân và thiết kế cache cuối cùng

Đã hoàn thành theo phân công Member 3: Redis Detail cache-aside 5 phút absolute; List ASP.NET Output Cache 2 phút, vary mọi query, tag recipes; invalidation sau DB commit cho Recipe, children, Category và profile ảnh hưởng DTO; graceful degradation lỗi cache. Frontend có List Data Cache 120 giây, coalescing request đang chạy, loading/empty/error/retry và revalidation sau mutation. Chi tiết và test từng yêu cầu nằm trong `doc/lab5_cache_member3.md`.

Lượt chốt ngày 13/10 sửa Next Detail thành `no-store`: sau khách đọc Published, owner Unpublish/Archive/Delete qua proxy hoặc gọi backend trực tiếp, GET mới luôn tới backend kiểm tra quyền/trạng thái. Redis chỉ dùng DTO Published sau DB access check và đối chiếu ID/slug/status/RowVersion. Auth/private request và mutation không vào shared Next cache. Không thêm tính năng cache mới.

Sau upstream mutation 2xx, proxy thử từng tag bằng `revalidateTag(tag, { expire: 0 })`; không dùng profile max như hết hạn tức thì. Nếu revalidation throw/reject, proxy vẫn trả status/body/ETag/Location thành công, không tự gửi lại mutation, tag tiếp theo vẫn được thử. Log chỉ thông báo cố định và tag, không có exception message/token/body.

Trong lượt đối chiếu LMS, Member 3 chỉ bổ sung `backend/CulinaryBlog.Tests/Lab5CorsApiTests.cs` và tài liệu bàn giao; không thay production CORS/auth, không viết lại phần foundation có trên remote. Chưa hoàn thành trên nhánh cá nhân: Rate Limiting, API docs runtime, cơ chế API versioning, tích hợp foundation và demo thật. Các phần này cần phối hợp nhóm; không nhận công của thành viên khác.

## Kiểm thử thực tế và giới hạn

| Lượt kiểm chứng 13/10 | Kết quả thực tế | Môi trường và giới hạn |
| --- | --- | --- |
| Backend build và full suite trước test LMS mới | Build 0 warning/0 error; 368 passed, 2 skipped, 0 failed; TRX `TestResults/lab5-final.trx`. | Testing, TestServer, SQLite memory/temp; cache/storage fake. Hai skip cần PostgreSQL FTS và migration backfill. |
| Backend kiểm tra LMS tập trung sau thêm test CORS | 12 passed, 0 skipped/failed; TRX `TestResults/lab5-lms-focused.trx`. Gồm 3 CORS, 6 JWT/create auth, 3 update role/ownership. | Build dependency trong dotnet test đạt. Không cộng 12 vào 368 vì có test chạy lại; chưa chạy lại full suite sau thêm 3 case CORS. |
| Frontend code cache cuối cùng | 46/46 passed; lint exit 0 với 1 warning img cũ; typecheck và production build exit 0. | Proxy fetch/invalidation mock; không chứng minh hit Next runtime hoặc Redis thật. Không sửa frontend thêm trong lượt LMS. |

Chưa chạy Redis/PostgreSQL/Docker thật, API ngoài Testing, migration DB ứng dụng, browser hoặc đo hiệu năng. Không có ảnh demo và không có số liệu tốc độ tự ước lượng. Eviction thất bại/race refill có thể giữ DTO cũ tới TTL; Category/Author thay đổi không luôn đổi RowVersion Recipe. Next List có thể stale khi mutation ngoài proxy hoặc lỗi revalidation; 120 giây là cấu hình revalidation, không phải SLA freshness cứng. UI đang mở không tự nhận thay đổi và request đồng thời mutation có race.

## Kịch bản demo có thể tái hiện

1. Chạy test tập trung CORS/JWT/owner bên dưới. Đọc assertions của `Lab5CorsApiTests`: origin hợp lệ có allow-origin/credentials, origin lạ không có, POST guest vẫn trả 401 kèm CORS hợp lệ.
2. Chạy frontend test: xem hai case `Detail always reaches backend...` và `revalidation throw or rejection...`; fixture mô phỏng 6 nhánh lifecycle và 6 tổ hợp response 200/201/204 với throw/reject. Đây là demo test, chưa phải demo browser.
3. Chạy test cache backend theo tên RecipeDetailCacheTests/RecipeOutputCacheTests/RecipeRelatedCacheTests. Counter/clock fake chứng minh hit/miss, TTL và eviction sau commit, không cần bật Redis.
4. Sau khi nhóm tự tích hợp foundation trên nhánh phù hợp, dùng host Testing bật RateLimiting:Enabled để kiểm tra 429/Retry-After và GET tài liệu. Cần bổ sung kiểm tra versioning thực tế. Bước này chưa thực hiện và không phải kết quả cá nhân đã hoàn thành.

```powershell
$env:RECIPE_TEST_POSTGRES = ''
dotnet test backend/CulinaryBlog.Tests/CulinaryBlog.Tests.csproj --no-restore --filter 'FullyQualifiedName~Lab5CorsApiTests|FullyQualifiedName~Authorization_rejects_without_writing|FullyQualifiedName~Missing_or_invalid_jwt_returns_problem_details_challenge|FullyQualifiedName~Unauthorized_updates_do_not_write'
npm.cmd --prefix frontend run test
npm.cmd --prefix frontend run lint
npm.cmd --prefix frontend run typecheck
npm.cmd --prefix frontend run build
```

## Phụ lục bàn giao Git

Danh sách dưới đây gồm toàn bộ worktree chưa commit, kể cả công việc cache tồn tại trước lượt đối chiếu LMS. Không tự tạo commit/push/PR; không reset/amend/ghi đè, không đổi OCOP hoặc database ứng dụng. Nhóm commit giữ interface cùng implementation/DI/consumer và tests để tránh commit trung gian không build được.

### Snapshot cuối cùng

1. Backend cùng dependency

```text
 M backend/CulinaryBlog.API/CulinaryBlog.API.csproj
 M backend/CulinaryBlog.API/Program.cs
 M backend/CulinaryBlog.API/appsettings.json
 M backend/CulinaryBlog.Application/Categories/Commands/CreateCategoryCommand.cs
 M backend/CulinaryBlog.Application/Categories/Commands/DeleteCategoryCommand.cs
 M backend/CulinaryBlog.Application/Categories/Commands/UpdateCategoryCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/ChangeRecipeLifecycleCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/CreateRecipeCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/DeleteRecipeCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/DeleteRecipeImageCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/RecipeIngredientCommands.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/RecipeStepCommands.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/SetPrimaryRecipeImageCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/UpdateRecipeCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/UploadRecipeImageCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Queries/GetRecipeDetailQuery.cs
 M backend/CulinaryBlog.Application/Recipes/Queries/RecipeDetailCacheOptions.cs
 M backend/CulinaryBlog.Infrastructure/Authentication/IdentityService.cs
 M backend/CulinaryBlog.Infrastructure/Caching/RedisCacheService.cs
 M backend/CulinaryBlog.Tests/AuthApiFactory.cs
 M backend/CulinaryBlog.Tests/CreateRecipeTests.cs
 M backend/CulinaryBlog.Tests/RecipeApiFactory.cs
 M backend/CulinaryBlog.Tests/RecipeDatabaseFixture.cs
 M backend/CulinaryBlog.Tests/RecipeDetailCacheTests.cs
 M backend/CulinaryBlog.Tests/RecipeLifecycleApiTests.cs
?? backend/CulinaryBlog.API/Caching/RecipeCacheInvalidator.cs
?? backend/CulinaryBlog.API/Caching/ResilientOutputCacheStore.cs
?? backend/CulinaryBlog.Application/Common/Interfaces/CacheFailure.cs
?? backend/CulinaryBlog.Application/Common/Interfaces/IRecipeCacheInvalidator.cs
?? backend/CulinaryBlog.Tests/CacheFailurePolicyTests.cs
?? backend/CulinaryBlog.Tests/Lab5CorsApiTests.cs
?? backend/CulinaryBlog.Tests/RecipeOutputCacheTests.cs
?? backend/CulinaryBlog.Tests/RecipeRelatedCacheTests.cs
?? backend/CulinaryBlog.Tests/RecordingRecipeInvalidator.cs
?? backend/CulinaryBlog.Tests/TestOutputCacheStore.cs
```

2. Frontend sau backend

```text
 M frontend/src/app/recipes/[slug]/page.tsx
 M frontend/src/components/recipes/EditRecipeForm.tsx
 M frontend/src/components/recipes/RecipeListClient.tsx
 M frontend/src/services/http-client.ts
 M frontend/src/services/recipe-service.ts
?? frontend/src/app/api/recipe-cache/[...path]/route.ts
?? frontend/src/app/recipes/error.tsx
?? frontend/src/app/recipes/loading.tsx
?? frontend/src/services/recipe-cache-policy.ts
?? frontend/src/services/recipe-cache-proxy.ts
?? frontend/src/services/recipe-requests.ts
?? frontend/tests/recipe-cache.test.ts
?? frontend/tests/recipe-proxy.test.ts
```

3. Tài liệu và ignore sau kiểm chứng

```text
 M .gitignore
 M README.md
?? doc/Lab5_2312764_NguyenKhiemThuan.docx
?? doc/lab5_cache_member3.md
?? doc/lab5_lms_member3_handoff.md
```

`git status --short --untracked-files=all`:

```text
 M .gitignore
 M README.md
 M backend/CulinaryBlog.API/CulinaryBlog.API.csproj
 M backend/CulinaryBlog.API/Program.cs
 M backend/CulinaryBlog.API/appsettings.json
 M backend/CulinaryBlog.Application/Categories/Commands/CreateCategoryCommand.cs
 M backend/CulinaryBlog.Application/Categories/Commands/DeleteCategoryCommand.cs
 M backend/CulinaryBlog.Application/Categories/Commands/UpdateCategoryCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/ChangeRecipeLifecycleCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/CreateRecipeCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/DeleteRecipeCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/DeleteRecipeImageCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/RecipeIngredientCommands.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/RecipeStepCommands.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/SetPrimaryRecipeImageCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/UpdateRecipeCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Commands/UploadRecipeImageCommand.cs
 M backend/CulinaryBlog.Application/Recipes/Queries/GetRecipeDetailQuery.cs
 M backend/CulinaryBlog.Application/Recipes/Queries/RecipeDetailCacheOptions.cs
 M backend/CulinaryBlog.Infrastructure/Authentication/IdentityService.cs
 M backend/CulinaryBlog.Infrastructure/Caching/RedisCacheService.cs
 M backend/CulinaryBlog.Tests/AuthApiFactory.cs
 M backend/CulinaryBlog.Tests/CreateRecipeTests.cs
 M backend/CulinaryBlog.Tests/RecipeApiFactory.cs
 M backend/CulinaryBlog.Tests/RecipeDatabaseFixture.cs
 M backend/CulinaryBlog.Tests/RecipeDetailCacheTests.cs
 M backend/CulinaryBlog.Tests/RecipeLifecycleApiTests.cs
 M frontend/src/app/recipes/[slug]/page.tsx
 M frontend/src/components/recipes/EditRecipeForm.tsx
 M frontend/src/components/recipes/RecipeListClient.tsx
 M frontend/src/services/http-client.ts
 M frontend/src/services/recipe-service.ts
?? backend/CulinaryBlog.API/Caching/RecipeCacheInvalidator.cs
?? backend/CulinaryBlog.API/Caching/ResilientOutputCacheStore.cs
?? backend/CulinaryBlog.Application/Common/Interfaces/CacheFailure.cs
?? backend/CulinaryBlog.Application/Common/Interfaces/IRecipeCacheInvalidator.cs
?? backend/CulinaryBlog.Tests/CacheFailurePolicyTests.cs
?? backend/CulinaryBlog.Tests/Lab5CorsApiTests.cs
?? backend/CulinaryBlog.Tests/RecipeOutputCacheTests.cs
?? backend/CulinaryBlog.Tests/RecipeRelatedCacheTests.cs
?? backend/CulinaryBlog.Tests/RecordingRecipeInvalidator.cs
?? backend/CulinaryBlog.Tests/TestOutputCacheStore.cs
?? doc/Lab5_2312764_NguyenKhiemThuan.docx
?? doc/lab5_cache_member3.md
?? doc/lab5_lms_member3_handoff.md
?? frontend/src/app/api/recipe-cache/[...path]/route.ts
?? frontend/src/app/recipes/error.tsx
?? frontend/src/app/recipes/loading.tsx
?? frontend/src/services/recipe-cache-policy.ts
?? frontend/src/services/recipe-cache-proxy.ts
?? frontend/src/services/recipe-requests.ts
?? frontend/tests/recipe-cache.test.ts
?? frontend/tests/recipe-proxy.test.ts
```

`git diff --stat` (chỉ tracked, không tính file ??):

```text
 .gitignore                                         |  3 +-
 README.md                                          |  8 ++--
 backend/CulinaryBlog.API/CulinaryBlog.API.csproj   |  1 +
 backend/CulinaryBlog.API/Program.cs                | 33 +++++++++++++++-
 backend/CulinaryBlog.API/appsettings.json          |  3 ++
 .../Categories/Commands/CreateCategoryCommand.cs   |  2 +-
 .../Categories/Commands/DeleteCategoryCommand.cs   |  2 +-
 .../Categories/Commands/UpdateCategoryCommand.cs   |  8 +++-
 .../Commands/ChangeRecipeLifecycleCommand.cs       |  7 ++--
 .../Recipes/Commands/CreateRecipeCommand.cs        |  5 ++-
 .../Recipes/Commands/DeleteRecipeCommand.cs        |  7 ++--
 .../Recipes/Commands/DeleteRecipeImageCommand.cs   |  4 +-
 .../Recipes/Commands/RecipeIngredientCommands.cs   |  9 +++--
 .../Recipes/Commands/RecipeStepCommands.cs         |  9 +++--
 .../Commands/SetPrimaryRecipeImageCommand.cs       |  4 +-
 .../Recipes/Commands/UpdateRecipeCommand.cs        |  7 ++--
 .../Recipes/Commands/UploadRecipeImageCommand.cs   |  7 +++-
 .../Recipes/Queries/GetRecipeDetailQuery.cs        | 25 ++++++++++--
 .../Recipes/Queries/RecipeDetailCacheOptions.cs    |  4 +-
 .../Authentication/IdentityService.cs              |  6 ++-
 .../Caching/RedisCacheService.cs                   | 16 +++++---
 backend/CulinaryBlog.Tests/AuthApiFactory.cs       |  4 ++
 backend/CulinaryBlog.Tests/CreateRecipeTests.cs    |  4 +-
 backend/CulinaryBlog.Tests/RecipeApiFactory.cs     | 10 +++++
 .../CulinaryBlog.Tests/RecipeDatabaseFixture.cs    |  3 ++
 .../CulinaryBlog.Tests/RecipeDetailCacheTests.cs   | 46 +++++++++++++++++++++-
 .../CulinaryBlog.Tests/RecipeLifecycleApiTests.cs  | 16 +++-----
 frontend/src/app/recipes/[slug]/page.tsx           |  8 ++--
 frontend/src/components/recipes/EditRecipeForm.tsx | 19 ++++++---
 .../src/components/recipes/RecipeListClient.tsx    | 14 ++++---
 frontend/src/services/http-client.ts               |  8 +++-
 frontend/src/services/recipe-service.ts            | 11 ++++--
 32 files changed, 237 insertions(+), 76 deletions(-)
```

`git diff --check`: exit 0, không có lỗi whitespace.
