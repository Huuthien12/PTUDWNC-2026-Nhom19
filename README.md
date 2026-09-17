# Culinary Blog - Dự án Phát triển Ứng dụng Web Nâng cao (Nhóm 19)

Hệ thống nền tảng Blog Ẩm thực và Nấu ăn xây dựng theo kiến trúc API-Driven:
- **Backend:** .NET 10 Minimal APIs (Clean Architecture + CQRS)
- **Frontend:** Next.js 15 App Router (TypeScript, Tailwind CSS)
- **Infrastructure:** PostgreSQL 16, Redis 7, MinIO, Mailhog (Docker Compose)

---

## 🛠️ Yêu cầu Môi trường (Prerequisites)

Trước khi khởi chạy dự án, máy tính cần cài đặt sẵn các công cụ sau:
1. **Docker Desktop** (bắt buộc phải kích hoạt WSL 2 trên Windows).
2. **.NET 10.0 SDK** (phiên bản 10.x trở lên).
3. **Node.js** (phiên bản v20 LTS trở lên) & **npm**.
4. **Git**.

---

## 🚀 Hướng dẫn Khởi chạy Môi trường Phát triển (Local Setup)

### 1. Khởi chạy các dịch vụ Hạ tầng (Database, Cache, Object Storage)
Mở Terminal tại thư mục gốc dự án và chạy lệnh:
```
docker compose up -d

```
Sau khi chạy thành công, các dịch vụ sẽ hoạt động tại:

PostgreSQL 16: localhost:5432 (Database: CulinaryBlogDb, User: postgres, Password: YourPassword123!)

Redis 7: localhost:6379

MinIO Console: http://localhost:9001 (User: minioadmin, Pass: minioadminpassword)

Mailhog UI (Test Email): http://localhost:8025

2. Khởi chạy Backend (.NET 10 API)
Mở cửa sổ Terminal thứ nhất:
```
cd backend/CulinaryBlog.API
dotnet run

```
Backend API sẽ lắng nghe tại: http://localhost:5062

Endpoint Health Check test: http://localhost:5062/health

3. Khởi chạy Frontend (Next.js 15)
Mở cửa sổ Terminal thứ hai:
```
cd frontend
npm run dev

```
Giao diện Frontend sẽ truy cập tại: http://localhost:3000

## 📁 Cấu trúc Thư mục Dự án

```text
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
# 👥 PHÂN CÔNG CÔNG VIỆC – CULINARY BLOG

> Dựa trên SRS Culinary Blog v1.0.2  
> Backend: .NET 10 Minimal APIs  
> Frontend: Next.js App Router + TypeScript  
> Database: PostgreSQL 16  
> Cache: Redis 7  
> Object Storage: MinIO  
> Background Jobs: Hangfire

---

## 👨‍💻 Thành viên nhóm

| Member | MSSV | Họ và tên |
|:---:|:---:|---|
| Member 1 | 2312794 | Nguyễn Thục Uyên |
| Member 2 | 2312745 | Nguyễn Quốc Thái |
| Member 3 | 2312764 | Nguyễn Khiêm Thuận |
| Member 4 | 2312753 | Lương Hữu Thiện |

---

# 📌 Nguyên tắc phân công

- Không chia cố định một người chỉ làm Frontend, Backend hoặc Database.
- Cả 4 thành viên đều phải tham gia Database, Backend và Frontend.
- Mỗi thành viên chịu trách nhiệm chính một phần nhưng phải review code của thành viên khác.
- Các chức năng phải bám theo SRS v1.0.2.
- Mỗi chức năng hoàn thành phải được kiểm thử trước khi merge vào `main`.
- Mỗi thành viên phát triển trên branch riêng hoặc feature branch.
- Pull Request phải được ít nhất một thành viên khác review trước khi merge.
- Công việc được chia thành 7 buổi theo thứ tự từ nền tảng → chức năng → tích hợp → kiểm thử.

---

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

**Frontend**
- Trang Register.
- Form validation.
- Tích hợp API Register.

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

**Frontend**
- Trang danh sách Category.
- Component Category.
- UI quản lý Category cho Admin.

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

**Frontend**
- Author Recipe Dashboard.
- Các nút:
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

### Member 2 – Nguyễn Quốc Thái

**Backend**
- FR-RCP-010: CRUD RecipeStep.

**Frontend**
- Component quản lý các bước nấu.
- Thêm/xóa/sắp xếp Step.

### Member 3 – Nguyễn Khiêm Thuận

**Backend**
- FR-RCP-008: Upload RecipeImage.
- Set Primary Image.
- Delete Image.

**Frontend**
- Component Upload ảnh.
- Preview ảnh.
- Chọn ảnh Primary.

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

### Member 3 – Nguyễn Khiêm Thuận

**Backend**
- Redis Distributed Cache.
- Output Cache Recipe List TTL 2 phút.
- Cache Recipe Detail.
- Cache invalidation khi Recipe/Category thay đổi.

**Frontend**
- Next.js cache/revalidation.
- Loading / Empty State.
- Tối ưu request dữ liệu.

### Member 4 – Lương Hữu Thiện

**Backend**
- FR-AUTH-003: Google OAuth 2.0.
- FR-AUTH-006: View Profile.
- FR-AUTH-007: Update Profile.
- Backend verification Google credential/code.

**Frontend**
- Login with Google.
- Trang Profile.
- Form Update Profile.

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

**Frontend**
- Hoàn thiện UI Auth.
- Error/Success notification.

### Member 2 – Nguyễn Quốc Thái

**Backend**
- Background Image Processing.
- Resize:
  - Medium 800×600.
  - Thumbnail 300×300.
- Cleanup orphan files.

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

**Frontend / DevOps**
- Fix Recipe Detail.
- Error Pages.
- Kiểm tra môi trường Docker.

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
