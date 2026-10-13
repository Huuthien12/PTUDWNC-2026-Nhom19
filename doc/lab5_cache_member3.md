# Buổi 5 — Member 3: Recipe cache

Nguyễn Khiêm Thuận · 2312764 · `thuan/lab5-recipe-cache` · chốt lại 13/10/2026.

Đối chiếu bổ sung đề LMS chính thức và snapshot Git mới nhất:
[Bàn giao Lab 5 theo 5 yêu cầu LMS](lab5_lms_member3_handoff.md).
Cache hoàn tất không đồng nghĩa toàn bộ Lab 5 đã đủ; các khoảng thiếu foundation
và việc phối hợp nhóm được ghi rõ trong báo cáo bổ sung.

## Phạm vi và trạng thái Git

Đối chiếu README buổi 5 và SRS v1.0.2: Redis Distributed Cache; Recipe List
Output Cache 2 phút; Recipe Detail 5 phút; invalidation Recipe/Category và dữ liệu
liên quan; Next.js cache/revalidation, Loading/Empty/Error và giảm request trùng.
NFR-PERF-003 và chính sách cache mục 6 thống nhất List 2 phút; đoạn luồng cũ
FR-RCP-001 còn ghi 1 phút không được dùng làm TTL triển khai.

Kế hoạch đã thực hiện: đọc transaction và luồng frontend → bổ sung eviction sau
commit và xử lý lỗi cache → Output Cache Redis → Next.js cache và mutation
revalidation → kiểm thử cô lập → bàn giao. Không mở rộng Search/Suggestions,
auth nghiệp vụ, màn hình mới, OCOP hay schema/migration.

Mốc 07/10 bắt đầu từ working tree sạch. Lượt 13/10 xác nhận đúng nhánh, HEAD
`eecd0f3`, có nhiều file sửa/mới chưa commit; không ghi đè công việc đó.
Đã fetch origin: `origin/main` từ `5cd1ec5` lên `f1848ad`; không pull/merge.
Đã commit trước lượt này:

| Commit | Nội dung |
| --- | --- |
| `577e1a5` | Absolute expiration tùy chọn, mặc định sliding |
| `ad14c40` | Truy vấn nhẹ kiểm tra quyền/trạng thái/RowVersion |
| `78d17fe` | Detail cache-aside, mặc định tắt tại thời điểm commit |
| `90fc21e` | Update invalidation |
| `cc280f7` | Lifecycle invalidation |
| `9bcf2bf` | Delete invalidation |
| `eecd0f3` | Create invalidation |

Phần bổ sung trong lượt này chưa commit/push/PR; không reset/amend/rebase.
Đã đọc `frontend/AGENTS.md` và tài liệu Next.js **cục bộ 16.3.5**:
`node_modules/next/dist/docs/01-app/03-api-reference/04-functions/fetch.md` và
`revalidateTag.md`. Không dùng API `revalidateTag(tag)` một tham số đã deprecated.

## Checklist yêu cầu → triển khai → bằng chứng

Các đường dẫn backend dưới đây tương đối với `backend/`; frontend với `frontend/`.

| Yêu cầu | File triển khai | Kiểm chứng |
| --- | --- | --- |
| Redis Distributed Cache, sliding/absolute | `CulinaryBlog.Infrastructure/Caching/RedisCacheService.cs`, `CulinaryBlog.API/Program.cs` | `RedisCacheServiceTests`: sliding 30 phút, absolute 5 phút, không trộn hai loại |
| Detail cache-aside Published 5 phút, quyền/RowVersion | `CulinaryBlog.Application/Recipes/Queries/GetRecipeDetailQuery.cs`, `RecipeDetailCacheOptions.cs`, repository truy vấn access đã commit | `RecipeDetailCacheTests`, `RecipeDetailVisibilityTests`: hit/miss/version sai, trạng thái/quyền, fallback DB, cấu hình tắt, TTL absolute |
| Create/Update/lifecycle/Delete | `CulinaryBlog.Application/Recipes/Commands/{CreateRecipe,UpdateRecipe,ChangeRecipeLifecycle,DeleteRecipe}Command.cs` | `CreateRecipeTests`, `UpdateRecipeTests`, `RecipeLifecycleApiTests`: commit/validation/quyền/concurrency, eviction lỗi vẫn trả thành công |
| Ingredients add/update/delete | `Recipes/Commands/RecipeIngredientCommands.cs` | `RecipeRelatedCacheTests`: đủ 3 thao tác; stale/quyền không eviction; lỗi cache vẫn thử tag |
| Steps create/update/delete và renumber | `Recipes/Commands/RecipeStepCommands.cs` | `RecipeRelatedCacheTests`, `RecipeStepTests`: đủ 3 thao tác, renumber sau delete; lỗi save thứ hai rollback, không eviction |
| Images upload/primary/delete | `Recipes/Commands/{UploadRecipeImage,SetPrimaryRecipeImage,DeleteRecipeImage}Command.cs` | `RecipeRelatedCacheTests`, `RecipeImageApiTests`: đủ 3 thao tác; transaction 2 save; cleanup vẫn sau commit |
| Category/Author DTO thay đổi | `Categories/Commands/UpdateCategoryCommand.cs`, `CulinaryBlog.Infrastructure/Authentication/IdentityService.cs`, `CulinaryBlog.API/Caching/RecipeCacheInvalidator.cs` | `RecipeRelatedCacheTests`: Category name và Author name/avatar, nhiều recipe, một key lỗi không chặn key/tag tiếp theo |
| Chính sách lỗi cache | `Common/Interfaces/CacheFailure.cs`, query Detail, Redis adapter, `CulinaryBlog.API/Caching/ResilientOutputCacheStore.cs` | `RecipeDetailCacheTests`, `CacheFailurePolicyTests`, test mutation: read/write/eviction lỗi; DB/quyền/cancellation/lỗi lập trình không bị nuốt; log không chứa message provider |
| Output Cache List 2 phút, distributed, query variants | `CulinaryBlog.API/Program.cs`, package `Microsoft.AspNetCore.OutputCaching.StackExchangeRedis` **10.0.12** | `RecipeOutputCacheTests`: hit, tất cả filter/sort/page có hỗ trợ, TTL/expiry bằng clock fake, cookie/auth bypass, lỗi response không cache, lifecycle evict và refill, lỗi read/write |
| Next public cache, revalidation sau mutation | `src/app/api/recipe-cache/[...path]/route.ts`, `src/services/recipe-cache-{policy,proxy}.ts`, `http-client.ts` | `tests/recipe-proxy.test.ts`: URL/query, public không cookie, private no-store, mutation thành công mới invalidate, 204/ETag/If-Match, auth/origin/path guard |
| Loading/Empty/Error và retry | `src/app/recipes/{loading,error}.tsx`, `[slug]/page.tsx`, `components/recipes/{RecipeListClient,EditRecipeForm}.tsx` | Lint/typecheck/build; review effect theo page/slug và retry bằng attempt; UI/browser chưa kiểm thử thực |
| Giảm request trùng | `src/services/recipe-requests.ts`, `recipe-service.ts` | `tests/recipe-cache.test.ts`: concurrent public reads dùng chung promise, khác page độc lập, không giữ result/rejection, mutation xóa pending |

## Key, tag, cấu hình và điều kiện cache

| Lớp | Key/tag | TTL / điều kiện |
| --- | --- | --- |
| Distributed Detail | `recipe:{slug}` | Absolute 5 phút; chỉ DTO Published, sau DB access check; ID/slug/status/RowVersion phải khớp |
| Category đã tồn tại | `categories:all` | Sliding 30 phút như implementation nhóm; eviction khi thay đổi số đếm Published hoặc Category |
| ASP.NET Output Cache List | Policy `RecipeList`, tag `recipes`, Redis prefix `culinary:output:` | 2 phút; framework tạo key từ request, vary **mọi query**; chỉ GET ẩn danh, không Authorization/Cookie; default policy không lưu non-200/Set-Cookie |
| Next.js Data Cache List | Tag `recipes`, URL đầy đủ query | Server fetch `force-cache`, `next.revalidate: 120`; browser response `no-store` |
| Next.js Detail | Không lưu Data Cache | Server fetch `no-store`, không `next.revalidate/tags`; mọi lượt đọc mới tới backend kiểm tra quyền/trạng thái, Redis Detail vẫn absolute 5 phút |
| Private/mutation frontend | Không có shared cache | Bearer forward, `no-store`; không forward cookie, không cache token/response auth |

`Cache:RecipeDetail:Enabled` và giá trị Options mặc định hiện là **true** sau khi
đủ invalidation và suite cô lập đạt. Có thể tắt bằng
`Cache__RecipeDetail__Enabled=false`. Eviction vẫn chạy khi tắt để không giữ key
cũ lúc bật lại. Không sửa connection strings/credentials: cả hai store Redis
dùng cấu hình `ConnectionStrings:Redis` hiện có. `AddStackExchangeRedisCache`
phục vụ `ICacheService`; `AddStackExchangeRedisOutputCache` phục vụ
`IOutputCacheStore`, không coi chúng là cùng cơ chế.

List endpoint hỗ trợ `page`, `pageSize`, `categoryId`, `difficulty`,
`maxCookTime`, `minServings`, `sortBy`, `sortOrder`; tất cả tham gia key.
`sortBy` hiện chỉ có `createdAt`/`title`, giữ contract nhóm; Search ở endpoint
riêng, không tự thêm cache Search hoặc sorting chưa được hỗ trợ.

## Invalidation và transaction

- Recipe Create/Update/lifecycle/Delete: sau `SaveChangesAsync` thành công,
  xóa Detail và tag List; Category count như điều kiện Published hiện có.
- Ingredients, create/update Step, upload Image: sau atomic SaveChanges.
- Delete Step: sau **cả transaction** soft-delete + renumber commit; repository
  không có endpoint reorder riêng, không tự thêm nghiệp vụ đó.
- Primary/Delete Image: sau transaction demote/delete + promote và RowVersion
  commit; delete file queue vẫn sau DB commit và trước cache eviction.
- Update Category: sau SaveChanges, relational query chỉ lấy slug Recipe của
  Category bị ảnh hưởng rồi xóa Detail và tag List.
- Update profile: chỉ sau `UserManager.UpdateAsync` thành công (Identity đã
  SaveChanges); query Recipe theo AuthorId rồi xóa Detail/tag List. Không thay
  đổi contract hay validation auth.
- Không dùng FLUSHDB/FLUSHALL/KEYS. Validation, quyền, concurrency và save/commit
  thất bại không đến eviction. Test probe kiểm tra `CurrentTransaction == null`
  tại lúc eviction, đồng thời xác minh DB đã lưu RowVersion mới.

Frontend browser dùng proxy `/api/recipe-cache/api/v1/...` qua transport hiện có,
giữ bearer và auth refresh/retry hiện tại. Proxy không phải endpoint cho phép
người dùng tùy ý xóa tag: mutation cần Authorization, Origin nếu có phải cùng
origin, backend kiểm tra quyền và nghiệp vụ; chỉ upstream 2xx mới gọi
`revalidateTag('recipes', { expire: 0 })`. Không dùng profile `"max"` vì đó là
stale-while-revalidate, không hết hạn tức thì. Detail không cache tại Next. Category
mutation xóa thêm `categories`. Mỗi tag được thử độc lập, await và bắt lỗi
revalidation sau upstream thành công: giữ status/body/ETag/Location, không gửi lại
mutation. Log chỉ thông báo cố định và tag cố định, không ghi exception/token/body.
Không cache response mutation. Query/filter
không bị bỏ khỏi upstream URL; ETag, If-Match, Location và status được giữ.

List/Detail public được coalesce **chỉ trong lúc request đang chạy**, không có
cache DTO lâu dài trong browser. Private request không coalesce dùng chung.
Mutation thành công xóa pending public reads để navigation mới không dùng lại
promise từ trước mutation. Dashboard/edit giữ kết quả mutation mới nhất;
không cần một GET detail phụ để lấy RowVersion sau mỗi mutation.

## Chính sách lỗi và giới hạn

Guard chỉ bao quanh cache operation, phân loại IO/socket/timeout, exception của
StackExchange.Redis và JSON cache hỏng. Read lỗi → DB query có kiểm tra quyền;
write lỗi → vẫn trả DTO hợp lệ; eviction lỗi sau commit → vẫn trả kết quả đã
lưu. Mỗi key/tag thử độc lập. Log có lớp thao tác/error type, không exception
message/payload/connection string/token; Detail có debug hit/miss với RecipeId
Published. DB, nghiệp vụ, quyền, cancellation và lỗi lập trình không bị nuốt.

**Không bảo đảm nhất quán tuyệt đối.** Eviction lỗi có thể giữ dữ liệu cũ tới TTL,
đặc biệt Category/Author đổi không làm Recipe RowVersion đổi. Race giữa đọc,
refill và mutation cũng có thể tái đưa dữ liệu cũ vào cache. Không có outbox,
retry bền vững hay fencing; phạm vi buổi 5 không xây hệ thống đó.

Backend và Next là hai lớp độc lập. Detail luôn `no-store` tại Next nên sau
Unpublish/Archive/Delete qua proxy hoặc trực tiếp backend, lượt GET mới tới
kiểm tra access/status hiện tại; không dùng DTO Published đã cache ở Next.
Request đang chạy đồng thời với mutation và màn hình đã hiển thị vẫn có race;
không có cơ chế tự đẩy thay đổi vào UI đang mở.
Mutation gọi trực tiếp API ngoài proxy
(Postman/ứng dụng khác) chưa có webhook để invalidate Next List; List có thể stale tới
revalidation. Next TTL có semantics revalidation, không cam kết giới hạn stale
cứng khi refresh thất bại. Các TTL chồng nhau không tạo SLA freshness tuyệt đối.
Nếu backend eviction thất bại, lần refill Next cũng có thể nhận dữ liệu cũ.

Chưa chạy Redis/PostgreSQL/Docker thật, readiness Redis, multi-instance,
trình duyệt thật, hiệu năng p95 hay runtime Next tag cache trên deployment thật.
Dashboard hiện list + GET detail từng item để có owner/RowVersion vì Summary DTO
không có các trường đó; không đổi contract của nhóm chỉ để loại bỏ N+1 này.
Lint còn **1 warning `<img>` đã tồn tại** ở trang detail, không có lỗi lint.

## Bằng chứng kiểm thử cô lập

Kết quả cuối: backend build **0 warning, 0 error**; suite **368 passed,
2 skipped, 0 failed / 370**. Hai skip: migration backfill và PostgreSQL FTS,
đều cần `RECIPE_TEST_POSTGRES`. SQLite fixture `Data Source=:memory:`;
auth fixture dùng file temp tên GUID riêng. Lượt chạy đã đặt biến PostgreSQL
test rỗng; không chạy migration/seed DB ứng dụng. API host dùng `Testing`,
TestServer nội bộ; Output Cache in-memory/probe fake; cache/file storage dùng
fake. Không khởi động Docker/Redis/PostgreSQL/API bên ngoài.

Lượt đầu lỗi Windows Event Log `Cannot open log for source '.NET Runtime'`.
`Program.cs` chỉ ClearProviders trong `Testing`, không đổi production logging.
Sau sửa môi trường test, suite mới là bằng chứng nghiệp vụ; không tính lượt lỗi
Event Log là test code đã đạt.

Frontend lượt 13/10: **46 passed / 46**, lint exit 0 (1 warning cũ), typecheck exit 0,
production build exit 0. Test proxy chạy với fetch/invalidation dependencies fake;
chứng minh routing và thời điểm gọi invalidation, chưa chứng minh hit Next thật.
Hai test mới kiểm tra 6 tình huống Detail (Unpublish/Archive/Delete, qua proxy
hoặc thay đổi backend trực tiếp giả lập), và 6 trường hợp revalidation throw/reject
với response 200/201/204: body, ETag, Location giữ nguyên, một lần mutation,
tag sau vẫn được thử, log không lộ message provider. Backend build/test phía trên
đã chạy lại trên code hiện tại ngày 13/10, không sửa backend trong lượt này.

Lệnh tái hiện PowerShell (repo root):

```powershell
$env:RECIPE_TEST_POSTGRES = ''
dotnet build backend/CulinaryBlog.slnx --no-restore
dotnet test backend/CulinaryBlog.Tests/CulinaryBlog.Tests.csproj --no-restore --logger 'trx;LogFileName=lab5-final.trx' --logger 'console;verbosity=minimal'
npm.cmd --prefix frontend run lint
npm.cmd --prefix frontend run typecheck
npm.cmd --prefix frontend run test
npm.cmd --prefix frontend run build
```

TRX cục bộ: `backend/CulinaryBlog.Tests/TestResults/lab5-final.trx` (generated,
không cần commit). Dùng `npm.cmd` để tránh PowerShell ExecutionPolicy chặn
`npm.ps1`; không thay ExecutionPolicy. Restore NuGet đã được cấp quyền đọc cấu
hình ngoài sandbox; không in cấu hình hay credentials.

## Demo ngắn và chứng minh hit/miss

1. **Ngay trong fixture, không cần Redis:** chạy filter
   `FullyQualifiedName~RecipeDetailCacheTests|FullyQualifiedName~RecipeOutputCacheTests|FullyQualifiedName~RecipeRelatedCacheTests`.
   Detail hit có `DetailCalls == 0`, miss có full DB query và TTL 5 phút;
   List request thứ hai có `TestOutputCacheStore.Hits == 1`, đổi query tạo entry
   mới, fake clock +2 phút làm miss. Đây là bằng chứng counter, không đo cảm giác.
2. **Mutation:** test lifecycle publish làm anonymous List từ rỗng thành có item
   sau tag eviction; test child có commit probe rồi key/tag eviction; lỗi save
   thứ hai giữ cả DB cũ và không có eviction. Test eviction lỗi vẫn giữ HTTP
   thành công và đủ lần thử key/tag.
3. **Khi nhóm chủ động chuẩn bị Redis/API:** bật Debug riêng cho
   `CulinaryBlog.Application.Recipes.Queries.GetRecipeDetailQueryHandler` và
   `Microsoft.AspNetCore.OutputCaching`. Gọi trực tiếp API Detail Published
   hai lần để thấy miss/hit; List cùng query để thấy middleware cache hit;
   mutate rồi GET để thấy refill. Không dùng MONITOR/log bearer hay secrets.
   Kiểm tra TTL một key Detail đã biết; không quét KEYS. Đây là kịch bản **chưa
   được thực hiện trong lượt này**.
4. **Khi nhóm chuẩn bị browser/deployment:** anonymous GET qua proxy, theo dõi
   server/upstream request count giữa hai lần đọc; login và dùng mutation đã có,
   sau thành công đọc public lại phải fetch/refill. Kiểm tra private request
   no-store và UI loading/empty/retry. Không suy ra Next hit chỉ từ Network
   browser: browser vẫn gọi proxy, cache nằm trên server.

## Trả lời giảng viên

- **Cache-aside:** handler thử cache sau quyền/status DB, miss thì đọc DB đầy đủ,
  ghi DTO Published; mutation xóa key sau commit để lần đọc sau refill.
- **Absolute/sliding:** absolute hết hạn tính từ lần ghi, hit không gia hạn;
  sliding gia hạn khi truy cập. Detail dùng absolute 5 phút; Category giữ sliding
  30 phút theo implementation nhóm.
- **Output Cache:** middleware cache cả HTTP response List, có policy/query/tag;
  Redis Output Store riêng, không phải chỉ gọi `ICacheService` trong handler.
- **Quyền xem:** Detail luôn DB access check trước cache; Draft/Archived không
  vào shared cache. List authenticated bypass Output Cache; Next bearer no-store.
- **Sau commit:** SaveChanges chưa phải commit cuối nếu còn outer transaction;
  xóa cache sớm có thể refill dữ liệu sai trước rollback. Eviction nằm sau await
  transaction kết thúc; test lỗi save thứ hai xác nhận không eviction.
- **Cache lỗi:** coi cache là tối ưu tùy chọn; DB thành công vẫn trả thành công.
  Không nuốt DB/cancellation. Eviction lỗi có giới hạn stale đã nêu, không hứa
  nhất quán tuyệt đối.

Nguồn framework: [ASP.NET Core 10 Output Cache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output?view=aspnetcore-10.0)
xác nhận Redis cần store/package Output Cache riêng. Next API được đối chiếu
với tài liệu package cục bộ đã nêu, đúng phiên bản repo.

## Nhóm commit đề xuất (đủ file sửa và mới)

1. `[2312764] feat(cache): hoàn thiện Redis Detail, Output Cache và invalidation sau commit`
Giữ toàn bộ backend trong một commit: interface, implementation, DI, consumer, cấu hình và fixtures/tests phụ thuộc nhau. Commit này dùng frontend hiện tại trước nhóm 2.

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
?? backend/CulinaryBlog.Tests/RecipeOutputCacheTests.cs
?? backend/CulinaryBlog.Tests/RecipeRelatedCacheTests.cs
?? backend/CulinaryBlog.Tests/RecordingRecipeInvalidator.cs
?? backend/CulinaryBlog.Tests/TestOutputCacheStore.cs
```

2. `[2312764] feat(frontend): List cache, Detail no-store và bảo toàn response mutation`
Gom proxy, policy, transport, consumer UI và tests cùng nhau; phụ thuộc backend nhóm 1.

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

3. `[2312764] docs(lab5): bàn giao Member 3 và ignore kết quả generated`
README, báo cáo và .gitignore; phụ thuộc kết quả kiểm chứng hai nhóm trước.

```text
 M .gitignore
 M README.md
?? doc/lab5_cache_member3.md
```

M = file đã tracked sửa; ?? = file mới cần git add. Không có commit/push/PR được tạo.

## Snapshot bàn giao Git 13/10/2026

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
?? backend/CulinaryBlog.Tests/RecipeOutputCacheTests.cs
?? backend/CulinaryBlog.Tests/RecipeRelatedCacheTests.cs
?? backend/CulinaryBlog.Tests/RecordingRecipeInvalidator.cs
?? backend/CulinaryBlog.Tests/TestOutputCacheStore.cs
?? doc/lab5_cache_member3.md
?? frontend/src/app/api/recipe-cache/[...path]/route.ts
?? frontend/src/app/recipes/error.tsx
?? frontend/src/app/recipes/loading.tsx
?? frontend/src/services/recipe-cache-policy.ts
?? frontend/src/services/recipe-cache-proxy.ts
?? frontend/src/services/recipe-requests.ts
?? frontend/tests/recipe-cache.test.ts
?? frontend/tests/recipe-proxy.test.ts
```

`git diff --stat` (chỉ tracked; không tính các file ?? bên trên):

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

`git diff --check`: exit 0, không có lỗi whitespace. Git có cảnh báo chuyển LF sang CRLF theo cấu hình máy.

Không thay đổi OCOP, schema/migration hoặc database ứng dụng. Chưa demo Redis/Next cache runtime/browser thật.
