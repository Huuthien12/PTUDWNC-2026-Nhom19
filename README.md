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


