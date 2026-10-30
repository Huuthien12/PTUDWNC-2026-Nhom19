# Culinary Blog - Dự án Phát triển Ứng dụng Web Nâng cao (Nhóm 19)

Hệ thống nền tảng Blog Ẩm thực và Nấu ăn xây dựng theo kiến trúc
API-Driven: - **Backend:** .NET 10 Minimal APIs (Clean Architecture +
CQRS) - **Frontend:** Next.js 16.4.3 App Router (TypeScript, Tailwind CSS) -
**Infrastructure:** PostgreSQL 16, Redis 7, MinIO, Mailhog (Docker
Compose)

------------------------------------------------------------------------

## 🛠️ Yêu cầu Môi trường (Prerequisites)

Trước khi khởi chạy dự án, máy tính cần cài đặt:

1.  **Docker Desktop**.
2.  **.NET 10 SDK**.
3.  **Node.js 24** và **npm**.
4.  **Git**.

Kiểm tra:

``` powershell
docker --version
docker compose version
dotnet --version
node --version
npm --version
git --version
```

------------------------------------------------------------------------

## 🚀 Hướng dẫn Khởi chạy Môi trường Phát triển (Local Setup)

> **Lưu ý:** Connection String, JWT Key và thông tin nhạy cảm không được
> commit lên Git. Mỗi thành viên cần cấu hình **User Secrets** trên máy
> của mình trước khi chạy Backend lần đầu.

### 1. Clone hoặc cập nhật source code

Clone lần đầu:

``` powershell
git clone <repository-url>
cd PTUDWNC-2026-Nhom19
```

Nếu đã có project:

``` powershell
git switch main
git pull --ff-only origin main
```

### 2. Khởi chạy hạ tầng bằng Docker

Tại thư mục gốc:

``` powershell
docker compose up -d
docker compose ps
```

Các service development:

-   **PostgreSQL 16:** `localhost:5432`
-   **Redis 7:** `localhost:6379`
-   **MinIO Console:** `http://localhost:9001`
-   **Mailhog UI:** `http://localhost:8025`
### Docker Compose full-stack

Sao chép `.env.example` thành `.env`, thay toàn bộ placeholder bằng secret local, rồi chạy:

```powershell
docker compose up --build -d
docker compose ps
```

Compose chạy PostgreSQL, Redis, MinIO, Mailhog, API, frontend, Nginx, Hangfire và OTLP Collector. Nginx là entrypoint HTTP duy nhất: mở `http://localhost:3000`; health endpoints qua proxy là `/health/live`, `/health/ready` và `/health`. MinIO console mặc định ở `http://localhost:9001`; Mailhog ở `http://localhost:8025`.

`API_URL=http://api:8080` chỉ dành cho server-side trong mạng Compose. Browser dùng same-origin qua Nginx; frontend image được build với API URL rỗng để gọi `/api/...` cùng host. `BROWSER_API_URL` vẫn là URL công khai được dùng bởi các consumer ngoài browser. `MINIO_PUBLIC_BASE_URL` phải được đặt thành URL host/public thực tế khi truy cập từ máy khác; `localhost` chỉ đúng trên máy đang chạy Docker. Nginx chỉ tin cậy cho forwarded headers trên network Compose; API không tin header forwarded từ client trực tiếp.

Collector hiện dùng debug exporter để quan sát local; chưa có backend lưu trữ telemetry. Không coi GitHub Actions là pass trước khi workflow chạy trên GitHub.

### Kiểm thử

```powershell
dotnet test backend/CulinaryBlog.Tests/CulinaryBlog.Tests.csproj
cd frontend
npm ci
npm test
npm run lint
npm run typecheck
npm run build
```

Nếu Windows báo `ENOTEMPTY` trong `node_modules` khi `npm ci`, đóng tiến trình Node/Next đang dùng đúng worktree và chạy trong checkout sạch hoặc container Node; không dùng `npm audit fix --force`.

### Release checklist

- Sau khi pull an toàn bằng `git pull --ff-only origin main`, chạy `npm ci` khi lockfile đổi; dùng `dotnet restore` khi project/package đổi.
- Dùng `docker compose up --build -d` sau Dockerfile, Compose hoặc frontend build-time environment thay đổi; các lần khác dùng `docker compose up -d` để tái sử dụng PostgreSQL, Redis và MinIO.
- Xác minh `docker compose ps`, `/health/live`, `/health/ready`, đăng nhập, role Admin/Author, dashboard và giới hạn upload. Không dùng `docker compose down -v`: lệnh đó xóa dữ liệu chung trong named volumes.
- Nginx nhận frontend tại `/`, API tại `/api/`, và giữ health endpoints ở root. Multipart lớn hơn policy bị Nginx chặn trước API; API vẫn kiểm tra chính xác giới hạn upload sau khi request được chuyển tiếp.
- Collector debug chỉ chứng minh nhận telemetry cục bộ; cần backend observability riêng để lưu và truy vấn trace/metric lâu dài. Browser responsive smoke cần nghiệm thu thủ công nếu automation không khả dụng.

Thông tin PostgreSQL phải khớp với cấu hình development trong
`docker-compose.yml`.

### 3. Cấu hình Backend User Secrets

Kiểm tra:

``` powershell
dotnet user-secrets list --project backend/CulinaryBlog.API
```

Nếu hiển thị `No secrets configured for this application`, cấu hình các
giá trị sau.

#### 3.1. PostgreSQL Connection String

``` powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=CulinaryBlogDb;Username=postgres;Password=<POSTGRES_PASSWORD>" --project backend/CulinaryBlog.API
```

Thay `<POSTGRES_PASSWORD>` bằng mật khẩu PostgreSQL của môi trường
development đang được nhóm sử dụng.

#### 3.2. JWT Key

``` powershell
dotnet user-secrets set "Jwt:Key" "<YOUR_LOCAL_DEVELOPMENT_JWT_KEY>" --project backend/CulinaryBlog.API
```

Kiểm tra lại:

``` powershell
dotnet user-secrets list --project backend/CulinaryBlog.API
```

Cần có tối thiểu:

``` text
ConnectionStrings:DefaultConnection = ...
Jwt:Key = ...
```

> **Không commit Connection String, JWT Key hoặc mật khẩu thật lên
> Git.**

### 4. Restore và Build Backend

``` powershell
dotnet restore backend/CulinaryBlog.slnx
dotnet build backend/CulinaryBlog.slnx
```

Kết quả mong đợi: `Build succeeded.`

### 5. Chạy Backend (.NET 10 API)

``` powershell
dotnet run --project backend/CulinaryBlog.API
```

Backend API: `http://localhost:5062`

Health Check: `http://localhost:5062/health`

### 6. Cài đặt và chạy Frontend

Mở Terminal khác:

``` powershell
cd frontend
npm install
npm run dev
```

Các lần chạy sau nếu dependencies không thay đổi:

``` powershell
cd frontend
npm run dev
```

Frontend: `http://localhost:3000`

### 7. Tài khoản Development

Tài khoản **Author** được seed để phục vụ phát triển/test:

``` text
Email: seed.author@culinaryblog.local
Password: Seed@123456
```

Tài khoản Admin chỉ sử dụng khi database development đã được seed Admin
theo cấu hình chung của nhóm.

### 8. Lỗi thường gặp

#### PostgreSQL: `28P01: password authentication failed for user "postgres"`

Connection String đang dùng password không khớp với PostgreSQL đang
chạy.

``` powershell
docker compose ps
dotnet user-secrets list --project backend/CulinaryBlog.API
```

Đảm bảo password trong `ConnectionStrings:DefaultConnection` khớp với
PostgreSQL development.

#### `JWT Key is not configured`

``` powershell
dotnet user-secrets set "Jwt:Key" "<YOUR_LOCAL_DEVELOPMENT_JWT_KEY>" --project backend/CulinaryBlog.API
```

#### Port PostgreSQL `5432` đã được sử dụng

``` powershell
netstat -ano | findstr :5432
docker compose ps
```

Nếu máy đã cài PostgreSQL trực tiếp, service local có thể đang chiếm
port `5432` của PostgreSQL Docker.

------------------------------------------------------------------------

## 📁 Cấu trúc Thư mục Dự án

``` text
PTUDWNC-2026-Nhom19/
├── backend/                  # .NET 10 Minimal APIs (Clean Architecture)
│   ├── CulinaryBlog.Domain/        # Domain Entities & Value Objects
│   ├── CulinaryBlog.Application/   # CQRS Handlers, DTOs & Interfaces
│   ├── CulinaryBlog.Infrastructure/# EF Core, MinIO, Redis & Services
│   └── CulinaryBlog.API/           # Endpoints, Middlewares & Configs
├── frontend/                 # Next.js 15 App Router Project
├── docker-compose.yml        # Định nghĩa các container hạ tầng
└── README.md                 # Tài liệu hướng dẫn sử dụng
```

# 👥 PHÂN CÔNG CÔNG VIỆC -- CULINARY BLOG

> Dựa trên SRS Culinary Blog v1.0.2\
> Backend: .NET 10 Minimal APIs\
> Frontend: Next.js App Router + TypeScript\
> Database: PostgreSQL 16\
> Cache: Redis 7\
> Object Storage: MinIO\
> Background Jobs: Hangfire

------------------------------------------------------------------------

## 👨‍💻 Thành viên nhóm

| Member | MSSV | Họ và tên |
|:---:|:---:|---|
| Member 1 | 2312794 | Nguyễn Thục Uyên |
| Member 2 | 2312745 | Nguyễn Quốc Thái |
| Member 3 | 2312764 | Nguyễn Khiêm Thuận |
| Member 4 | 2312753 | Lương Hữu Thiện |

------------------------------------------------------------------------

# 📌 Nguyên tắc phân công

-   Không chia cố định một người chỉ làm Frontend, Backend hoặc
    Database.
-   Cả 4 thành viên đều phải tham gia Database, Backend và Frontend.
-   Mỗi thành viên chịu trách nhiệm chính một phần nhưng phải review
    code của thành viên khác.
-   Các chức năng phải bám theo SRS v1.0.2.
-   Mỗi chức năng hoàn thành phải được kiểm thử trước khi merge vào
    `main`.
-   Mỗi thành viên phát triển trên branch riêng hoặc feature branch.
-   Pull Request phải được ít nhất một thành viên khác review trước khi
    merge.
-   Công việc được chia thành 7 buổi theo thứ tự từ nền tảng → chức năng
    → tích hợp → kiểm thử.

------------------------------------------------------------------------

# 🗓️ BUỔI 1 – KHỞI TẠO DỰ ÁN & THIẾT KẾ DATABASE

### Mục tiêu
Xây dựng nền tảng chung để cả nhóm có thể bắt đầu phát triển.

### Member 1 – Nguyễn Thục Uyên

**Database**
- Thiết kế `ApplicationUser`.
- Thiết kế `RefreshToken`.
- Kiểm tra schema ASP.NET Core Identity.
- Xác định quan hệ User → Recipe và User → RefreshToken.

**Backend**
- Khởi tạo solution .NET 10.
- Tạo cấu trúc Clean Architecture:
  - Domain
  - Application
  - Infrastructure
  - Presentation

**Frontend**
- Khởi tạo Next.js App Router.
- Thiết lập TypeScript và cấu trúc thư mục frontend.
- Rà soát UI library hiện có trước khi chọn shadcn/ui hoặc bộ tương đương; thống nhất theme, design tokens, typography và accessibility cơ bản.
- Chuẩn hóa Button, Input, Label, FormField, Dialog, Toast/Alert và các trạng thái validation, loading, disabled, error/success để tái sử dụng.

### Member 2 – Nguyễn Quốc Thái

**Database**
- Thiết kế bảng `Recipe`.
- Thiết kế `RecipeNutrition` (Owned Entity).
- Xác định `RowVersion` cho Optimistic Concurrency.

**Backend**
- Tạo BaseEntity.
- Tạo Recipe aggregate root.
- Tạo RecipeNutrition.

**Frontend**
- Tạo Global Layout.
- Tạo Header / Navigation cơ bản.
- Hoàn thiện điều hướng mobile và menu theo trạng thái xác thực; thống nhất contract auth state với Thiện.
- Thiết kế layout responsive với grid, sidebar slot và breadcrumbs nếu cần; phối hợp tích hợp app/layout.tsx để tránh sửa chồng.

### Member 3 – Nguyễn Khiêm Thuận

**Database**
- Thiết kế `Category`.
- Thiết kế quan hệ Category ↔ Recipe.
- Xác định index phục vụ Category và Search.

**Backend**
- Cấu hình EF Core + PostgreSQL.
- Tạo DbContext.
- Cấu hình Entity Mapping.

**Frontend**
- Tạo Footer.
- Tạo cấu trúc component dùng chung.
- Xây Category Sidebar/Card/List, Search Bar mock, Pagination và các component loading skeleton, empty/error, not-found dùng chung với props nhất quán.

### Member 4 – Lương Hữu Thiện

**Database**
- Thiết kế:
  - RecipeIngredient
  - RecipeStep
  - RecipeImage
- Xác định FK và cascade behavior.

**Backend**
- Tạo các entity:
  - RecipeIngredient
  - RecipeStep
  - RecipeImage
- Cấu hình quan hệ với Recipe.

**Frontend**
- Tạo API client/service cơ bản.
- Thiết lập biến môi trường frontend.
- Chuẩn hóa API client/fetch wrapper: auth header, abort/timeout, lỗi mạng và RFC 7807 ProblemDetails cho HTTP 400/401/403/404/409/422/423/429.
- Chuẩn hóa auth store, selectors/actions, refresh/logout; phân biệt client auth state với TanStack Query server cache và bảo vệ refresh token.
- Đối chiếu TypeScript types Auth, Profile, Category, Recipe, Ingredient, Step, Image, PaginatedResult và ApiProblem với backend contract.
- Tích hợp providers và global styles sau khi nhận UI kit/layout; tránh trùng component và kiểm tra import, typecheck, build.

### Cả nhóm
- Review ERD.
- Thống nhất naming convention.
- Tạo Migration đầu tiên.
- Chạy PostgreSQL bằng Docker.
- Kiểm tra database được tạo thành công.
- Thống nhất Git workflow và branch convention.

**Kết quả buổi 1:**  
Project Backend + Frontend chạy được, PostgreSQL kết nối thành công và Database Schema cơ bản hoàn chỉnh.

---

# 🗓️ BUỔI 2 – AUTHENTICATION, USER & CATEGORY

### Member 1 – Nguyễn Thục Uyên

**Backend**
- FR-AUTH-001: Register.
- Validation đăng ký.
- Gán role Author mặc định.
- Phối hợp chốt endpoint, DTO và chính sách change/forgot/reset password; reset token một lần, có TTL, giới hạn lạm dụng và phản hồi chống dò email.

**Frontend**
- Trang Register.
- Form validation.
- Tích hợp API Register.
- Thiết kế các trang /login, /forgot-password, /reset-password và /change-password cùng /register; hỗ trợ trạng thái disabled/loading/error/success, hiện/ẩn mật khẩu, bàn phím và responsive.
- Tích hợp Register/Login/Logout/Change Password/Forgot Password/Reset Password với API thật; xử lý validation, session và HTTP 401/403/423.

### Member 2 – Nguyễn Quốc Thái

**Backend**
- FR-AUTH-002: Login.
- JWT Access Token.
- Xử lý credential không hợp lệ.

**Frontend**
- Trang Login.
- Lưu trạng thái đăng nhập.
- Xử lý lỗi đăng nhập.

### Member 3 – Nguyễn Khiêm Thuận

**Backend**
- FR-AUTH-004: Refresh Token.
- FR-AUTH-005: Logout / Token Revocation.
- Token Rotation.

**Frontend**
- Tự động refresh access token.
- Logout.
- Route Protection cơ bản.

### Member 4 – Lương Hữu Thiện

**Backend**
- FR-CAT-001 → FR-CAT-005.
- Xem danh mục.
- Xem chi tiết danh mục.
- Admin Create / Update / Delete Category.
- Rà soát phân quyền owner/Admin và lỗi 409 cho thao tác quản lý Category.

**Frontend**
- Trang danh sách Category.
- Component Category.
- UI quản lý Category cho Admin.
- Đồng bộ auth store/session lifecycle, refresh rotation/reuse, logout/revocation và route protection; không log token hoặc lưu refresh token thiếu an toàn.
- Kết nối UI Category Admin CRUD với API thật, validation, quyền và hiển thị lỗi khi trùng dữ liệu.

### Cả nhóm
- Seed Roles: Admin, Author.
- Seed tài khoản Admin.
- Review authorization.
- Test Auth + Category.
- Kiểm tra HTTP 400 / 401 / 403 / 404 / 409.

**Kết quả buổi 2:**  
Người dùng có thể đăng ký, đăng nhập, refresh token, logout và hệ thống có Category hoạt động.

---

# 🗓️ BUỔI 3 – RECIPE CORE

### Member 1 – Nguyễn Thục Uyên

**Backend**
- FR-RCP-001: Danh sách Recipe.
- Pagination.
- Authorization filter theo Guest / Author / Admin.

**Frontend**
- Trang danh sách công thức.
- Recipe Card.
- Pagination UI.

### Member 2 – Nguyễn Quốc Thái

**Backend**
- FR-RCP-002: Chi tiết Recipe.
- Load Ingredients, Steps, Images, Nutrition, Category, Author.

**Frontend**
- Trang chi tiết Recipe.
- Hiển thị nguyên liệu.
- Hiển thị các bước nấu.
- Hiển thị thông tin dinh dưỡng.
- Hoàn thiện Recipe Card/List/Grid và Recipe Detail skeleton, loading/empty/no-image states; bảo đảm accessibility và responsive.
- Tích hợp Recipe Detail với API thật: hiển thị Ingredients, Steps, Images, Nutrition, Category, Author; xử lý Published/Draft/Deleted theo quyền, 404, lỗi tải và ảnh placeholder.

### Member 3 – Nguyễn Khiêm Thuận

**Backend**
- FR-RCP-003: Create Recipe.
- FR-RCP-004: Update Recipe.
- FluentValidation.

**Frontend**
- Form tạo Recipe.
- Form chỉnh sửa Recipe.
- Form Nutrition.

### Member 4 – Lương Hữu Thiện

**Backend**
- FR-RCP-005: Publish / Unpublish.
- FR-RCP-006: Archive.
- FR-RCP-007: Delete.
- Resource-Based Authorization.
- Áp dụng điều kiện Publish khi có ít nhất một Ingredient và một Step hợp lệ; xác minh quyền Author-owner/Admin, Soft Delete bằng IsDeleted và RowVersion Base64 (stale update trả HTTP 422).

**Frontend**
- Author Recipe Dashboard.
- Các nút:
- Chuẩn bị giao diện Profile, Author Dashboard, Admin User List và error/unauthorized, tái sử dụng Profile UI có sẵn.
- Hoàn thiện Author Dashboard với xác nhận thao tác, toast và refresh danh sách sau Draft/Publish/Unpublish/Archive/Soft Delete.
  - Edit
  - Publish
  - Unpublish
  - Archive
  - Delete

### Cả nhóm
- Kiểm tra Recipe Aggregate.
- Hoàn thiện `RowVersion`.
- Xử lý Optimistic Concurrency.
- Trả `422` khi xảy ra concurrency conflict.
- Review quyền Author Owner / Admin.

**Kết quả buổi 3:**  
Hoàn thành luồng CRUD và vòng đời chính của Recipe.

---

# 🗓️ BUỔI 4 – INGREDIENT, STEP, IMAGE & MINIO

### Member 1 – Nguyễn Thục Uyên

**Backend**
- FR-RCP-009: CRUD RecipeIngredient.

**Frontend**
- Component thêm/xóa/sửa nguyên liệu.
- Sắp xếp nguyên liệu.
- Rà soát Ingredient Editor đã có trước khi tích hợp; kết nối CRUD/reorder vào Recipe Create/Edit, đồng bộ validation và RowVersion, kiểm thử thêm/sửa/xóa/sắp xếp.

### Member 2 – Nguyễn Quốc Thái

**Backend**
- FR-RCP-010: CRUD RecipeStep.
- Kiểm tra Step CRUD/reorder và thứ tự Step, RowVersion Base64, stale update HTTP 422 và quyền Author-owner/Admin.

**Frontend**
- Component quản lý các bước nấu.
- Thêm/xóa/sắp xếp Step.
- Hoàn thiện Recipe Create/Edit với React Hook Form, Zod và API thật: nutrition, category, Ingredient/Step editor; không làm mất dữ liệu form khi API lỗi.
- Phối hợp Thuận gắn upload ảnh vào Recipe Form; kiểm thử chuỗi create → edit → add Ingredient/Step/Image.

### Member 3 – Nguyễn Khiêm Thuận

**Backend**
- FR-RCP-008: Upload RecipeImage.
- Set Primary Image.
- Delete Image.
- Kiểm tra quyền upload/set primary/delete và validation file ở backend theo MIME/magic bytes, dung lượng, quyền truy cập.

**Frontend**
- Component Upload ảnh.
- Preview ảnh.
- Chọn ảnh Primary.
- Hoàn thiện upload/preview/progress/loading/retry/error và chọn ảnh chính/xóa ảnh; validate trước upload tối đa 5 MiB, JPEG/PNG/WebP/AVIF, xử lý file sai.

### Member 4 – Lương Hữu Thiện

**Backend / Infrastructure**
- FR-FILE-001/002.
- Tích hợp MinIO.
- Validate:
  - File <= 5 MB.
  - JPEG.
  - PNG.
  - WebP.
  - AVIF.
  - MIME type.
  - Magic bytes.
- Chuẩn bị xử lý resize ảnh.
- Giới hạn kích thước multipart request trước khi đọc form (5 MiB file và phần envelope cho phép); từ chối file không hợp lệ trước storage, không tạo image DB ngoài ý muốn.
- Kiểm thử authorization, RowVersion, MIME/magic bytes và quyền truy cập file với MinIO.

**Frontend**
- Validate file trước upload.
- Hiển thị lỗi file.
- Loading / upload state.

### Cả nhóm
- Tích hợp Recipe Form hoàn chỉnh.
- Kiểm tra Create Recipe → Ingredient → Step → Image.
- Kiểm tra quyền sửa/xóa.
- Kiểm tra upload file không hợp lệ.

**Kết quả buổi 4:**  
Có thể tạo một công thức hoàn chỉnh gồm thông tin, dinh dưỡng, nguyên liệu, bước nấu và hình ảnh.

---

# 🗓️ BUỔI 5 – SEARCH, FILTER, CACHE, GOOGLE OAUTH & PROFILE

### Member 1 – Nguyễn Thục Uyên

**Backend**
- FR-SRCH-001: PostgreSQL Full-Text Search.
- `tsvector/tsquery`.
- `unaccent`.
- GIN Index.

**Frontend**
- Search Bar.
- Debounce.
- Trang kết quả tìm kiếm.

### Member 2 – Nguyễn Quốc Thái

**Backend**
- FR-SRCH-002/003/004.
- Filter.
- Sort.
- Offset Pagination.

**Frontend**
- Bộ lọc Category.
- Difficulty.
- Cook Time.
- Sorting.
- Thống nhất RecipeCard và DTO pagination/filter/sort với Thuận; dùng next/image, placeholder và xử lý ảnh lỗi trên mobile.

### Member 3 – Nguyễn Khiêm Thuận

**Backend**
- Redis Distributed Cache.
- Output Cache Recipe List TTL 2 phút.
- Cache Recipe Detail.
- Cache invalidation khi Recipe/Category thay đổi.
- Kiểm chứng Category Redis cache TTL 30 phút, Recipe List output cache 2 phút, Recipe Detail cache và invalidation khi Recipe/Category thay đổi; không cache lộ công thức riêng tư.

**Frontend**
- Next.js cache/revalidation.
- Loading / Empty State.
- Tối ưu request dữ liệu.
- Hoàn thiện Homepage, Search Results, Category List/Detail và filter/sort panel, đồng bộ URL state, loading/empty/error và responsive.
- Kết nối Homepage/Search/Category với API thật; chỉ hiện Published Recipe, tìm kiếm tiếng Việt có/không dấu, debounce, sortBy/sortOrder, filter, offset pagination và Category 404.

Tài liệu triển khai và kiểm chứng Member 3: [Buổi 5 — Recipe cache](doc/lab5_cache_member3.md).

### Member 4 – Lương Hữu Thiện

**Backend**
- FR-AUTH-003: Google OAuth 2.0.
- FR-AUTH-006: View Profile.
- FR-AUTH-007: Update Profile.
- Backend verification Google credential/code.
- Kiểm chứng Profile GET/PATCH, 401/403 và Google OAuth credential/code được xác minh ở backend; không tin ID token chưa verify.

**Frontend**
- Login with Google.
- Trang Profile.
- Form Update Profile.
- Tích hợp Profile View/Update với React Hook Form, Zod, TanStack Query; validation và reload phải giữ dữ liệu.
- Hoàn thiện Google Login callback, error handling và liên kết trạng thái đăng nhập frontend với backend.

### Cả nhóm
- Test Search tiếng Việt.
- Test Filter + Sort + Pagination kết hợp.
- Kiểm tra Redis.
- Kiểm tra cache invalidation.
- Kiểm tra Google OAuth Frontend → Backend.

**Kết quả buổi 5:**  
Search, Filter, Sort, Cache, Google Login và Profile hoạt động.

---

# 🗓️ BUỔI 6 – BACKGROUND JOBS, OBSERVABILITY, SEO & DEVOPS

### Member 1 – Nguyễn Thục Uyên

**Backend**
- Welcome Email Background Job.
- Hangfire retry.
- Email provider configuration.
- Kiểm thử welcome email qua Mailhog/Hangfire: retry khi lỗi, idempotency, tránh gửi trùng và cấu hình secrets ngoài Git.
- Chốt API và quyền Admin User List: chỉ Admin, DTO whitelist không lộ dữ liệu nhạy cảm, phân trang/tìm kiếm, 401/403; Author list chỉ triển khai khi đã thống nhất dữ liệu được phép hiển thị.

**Frontend**
- Hoàn thiện UI Auth.
- Error/Success notification.
- Triển khai Admin User List theo API/role contract, có tìm kiếm, phân trang và trạng thái loading/empty/error.

### Member 2 – Nguyễn Quốc Thái

**Backend**
- Background Image Processing.
- Resize:
  - Medium 800×600.
  - Thumbnail 300×300.
- Cleanup orphan files.
- Kiểm thử xử lý ảnh qua Hangfire: resize/error/retry, cleanup orphan an toàn và không xóa MinIO object vẫn được tham chiếu.

**Frontend**
- Tối ưu hiển thị ảnh.
- `next/image`.
- Responsive Image.

### Member 3 – Nguyễn Khiêm Thuận

**Backend / Frontend**
- Sitemap.
- Open Graph.
- Schema.org Recipe JSON-LD.
- Metadata.
- ISR / Revalidation.
- Tối ưu SEO và Core Web Vitals.
- Đồng bộ Redis/Next.js cache invalidation và ISR/revalidation khi Recipe/Category thay đổi; không công khai nội dung Draft/Archived/Deleted.
- Chỉ phát hành sitemap, canonical, Open Graph và Schema.org Recipe JSON-LD cho công thức Published; sitemap phải phân trang đầy đủ và không trùng URL.

### Member 4 – Lương Hữu Thiện

**Backend / DevOps**
- Serilog Structured Logging.
- CorrelationId.
- Health Checks:
  - `/health/live`
  - `/health/ready`
  - `/health`
- OpenTelemetry.
- Dockerfile Backend.
- Dockerfile Frontend.
- Bảo đảm Serilog/CorrelationId không ghi password, JWT/token hoặc PII nhạy cảm; chuẩn hóa error mapping và ProblemDetails.
- Theo dõi OpenTelemetry traces/metrics qua OTLP Collector và kiểm tra health PostgreSQL/Redis/MinIO.
- Tích hợp/kiểm chứng Compose full-stack và Nginx reverse proxy: /api/ đến API, / đến Next.js; giới hạn upload và trusted forwarded headers, không tin forwarded headers từ client trực tiếp.

### Cả nhóm
- Hoàn thiện `docker-compose.yml`.
- Kết nối:
  - Next.js
  - .NET API
  - PostgreSQL
  - Redis
  - MinIO
  - Hangfire
  - Nginx
- Kiểm tra toàn bộ service chạy cùng nhau.

**Kết quả buổi 6:**  
Hệ thống Full-Stack chạy bằng Docker, có background jobs, monitoring, health checks và SEO.

---

# 🗓️ BUỔI 7 – TESTING, TÍCH HỢP & HOÀN THIỆN

### Member 1 – Nguyễn Thục Uyên

**Testing**
- Auth:
  - Register.
  - Login.
  - Google Login.
  - Refresh Token.
  - Logout.
  - Profile.
- Kiểm tra JWT / Authorization.
- Unit/component tests Auth UI và ảnh chụp desktop/mobile; regression Register → Login → Logout, Forgot → email → Reset → Login, Change Password làm mật khẩu cũ hết hiệu lực.
- Kiểm thử lockout sau 5 lần sai (HTTP 423), Auth/Profile/Admin User List 401/403, phân quyền và DTO không lộ dữ liệu nhạy cảm; ghi screenshot/video minh chứng.

**Frontend**
- Fix UI Auth/Profile.
- Responsive.

### Member 2 – Nguyễn Quốc Thái

**Testing**
- Recipe CRUD.
- Ingredient.
- Step.
- Publish / Archive / Delete.
- Optimistic Concurrency.
- Kiểm thử Recipe Detail Guest/Author/Admin và điều hướng từ Home/Search; create → edit → Ingredient/Step/Image, stale RowVersion 422, quyền và API failure.
- Kiểm tra component Recipe/loading/empty/no-image desktop/mobile, accessibility và screenshots.

**Frontend**
- Fix Recipe Form.
- Fix Author Dashboard.

### Member 3 – Nguyễn Khiêm Thuận

**Testing**
- Search.
- Filter.
- Sort.
- Pagination.
- Redis Cache.
- Cache Invalidation.
- SEO.
- Regression tìm kiếm tiếng Việt có/không dấu, query invalid, filter+sort+pagination, Category, stale cache và SEO; lưu minh chứng sitemap/metadata/JSON-LD.
- Kiểm tra accessibility/responsive và screenshot Homepage, Category List, Search/Filter.

**Frontend**
- Fix Homepage.
- Recipe List.
- Search UI.

### Member 4 – Lương Hữu Thiện

**Testing**
- Upload Image.
- MinIO.
- Hangfire.
- Health Checks.
- Logging.
- Docker Compose.
- Review API password/error/rate-limit contract cùng Uyên; smoke tích hợp Auth/Profile/Home/Detail/Search và kiểm thử quyền theo vai trò.
- Kiểm chứng API bằng Postman/Scalar, Docker/MinIO/OTLP, browser desktop/mobile, Author/Admin/Guest, và clean-clone khi có môi trường phù hợp; ghi rõ các bước chưa chạy.

**Frontend / DevOps**
- Fix Recipe Detail.
- Error Pages.
- Kiểm tra môi trường Docker.
- Duy trì docs/lab6/ui-inventory.md về route/component owner và checklist demo.
- Hoàn thiện GitHub Actions CI (.NET 10, Node 24), .env.example, seed/demo data, release checklist và tài liệu chạy/bàn giao.

### Cả nhóm
- Integration Test Frontend ↔ Backend.
- Test toàn bộ API bằng Postman/Scalar.
- Kiểm tra HTTP Status Code theo SRS.
- Kiểm tra RFC 7807 ProblemDetails.
- Kiểm tra Responsive.
- Kiểm tra lỗi 404 / 500.
- Review code chéo.
- Xóa code dư.
- Chuẩn hóa `.env.example`.
- Kiểm tra README.
- Merge các feature branch.
- Chạy lại toàn bộ hệ thống từ repository sạch.

**Kết quả buổi 7:**  
Hệ thống hoàn chỉnh, có thể clone repository → cấu hình `.env` → Docker Compose → chạy và demo.
