# SRS – Culinary Blog v1.0.2

<!-- PAGE 1 -->

GIÁO TRÌNH PHÁT TRI ỂN ỨNG DỤNG WEB NÂNG CAO 
Phiên b ản V4  ·  .NET 10 + Next.js App Router  
TÀI LIỆU ĐẶC TẢ YÊU CẦU PHẦN MỀM 
Software Requirements Specification (SRS)  
Tiêu chuẩn IEEE 830 / ISO/IEC/IEEE 29148:2018 
Dự án: Blog Ẩm thực và Nấu ăn 
Culinary Blog 
Phiên bản tài li ệu 1.0.2 
Ngày phát hành 16/09/2026 
Trạng thái Đã duy ệt (Approved)  
Công nghệ Backend .NET 10 Minimal APIs, C#  
Công nghệ Frontend Next.js App Router, TypeScript  
Cơ sở dữ liệu PostgreSQL 16  
Object Storage MinIO (S3-Compatible)  
Cache Redis 7 
 
Tài liệu này được biên soạn theo tiêu chuẩn IEEE 830 / ISO/IEC/IEEE 29148:2018.


---

<!-- PAGE 2 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao  •  Trang 2 / 71 
LỊCH SỬ THAY ĐỔI TÀI LIỆU 
 
Phiên 
bản Ngày Tác giả / Vai trò Nội dung thay đ ổi Trạng 
thái 
1.0.0 04/06/2026 Senior BA / Architect  Phát hành l ần đầu – Bản hoàn 
chỉnh theo IEEE 830 / ISO 29148.  Approved  
1.0.1 16/09/2026 Senior BA / Architect  Đồng bộ hóa FR/NFR/API/Data Model; hoàn thiện đầy đủ FR-CAT đến FR-OBS; chuẩn hóa Redis, sorting, HTTP status, publish rule, refresh token và Google OAuth flow.  Approved
1.0.2 16/09/2026 Senior BA / Architect  Hoàn thiện FR-SRCH đến FR-OBS; đồng bộ Google OAuth Frontend→Backend verification; chuẩn hóa Output Cache 2 phút, Next.js cache invalidation và Optimistic Concurrency cho Recipe aggregate.  Approved
0.9.0 20/05/2026 Senior BA Bổ sung Chương 7 (Data Model), 
Chương 8 (API Spec) và Ph ụ lục. 
Under 
Review 
0.8.0 05/05/2026 Senior BA Hoàn thiện Chương 3 (FR), b ổ 
sung FR-FILE, FR-JOB, FR -OBS. Draft  
0.5.0 15/04/2026 Senior BA Phác thảo ban đ ầu: Chương 1–4 
(skeleton). Draft  
 
Phê duyệt tài liệu: Tài liệu phiên bản 1.0.2 đã được xem xét và phê duyệt bởi Trưởng nhóm 
Kiến trúc Hệ thống (Lead Systems Architect). Mọi thay đổi từ phiên bản 1.0.1 trở đi đều phải 
thông qua quy trình Change Request (CR) và được cập nhật vào bảng này.


---

<!-- PAGE 3 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao  •  Trang 3 / 71 
MỤC LỤC 
LỊCH SỬ THAY ĐỔI TÀI LIỆU .................................................................................................. 2 
MỤC LỤC ................................................................................................................................... 3 
CHƯƠNG 1. GIỚI THIỆU ......................................................................................................... 6 
1.1. Mục đích Tài liệu ............................................................................................................. 6 
1.2. Phạm vi Sản phẩm.......................................................................................................... 6 
1.2.1. Tên và Định danh ..................................................................................................... 6 
1.2.2. Mô tả Sản phẩm ....................................................................................................... 6 
1.2.3. Những gì KHÔNG thuộc phạm vi............................................................................. 7 
1.3. Định nghĩa, Từ viết tắt và Ký hiệu .................................................................................. 7 
1.4. Tài liệu Tham chiếu......................................................................................................... 8 
1.5. Tổng quan Tài liệu ........................................................................................................ 10 
CHƯƠNG 2. MÔ TẢ TỔNG QUAN HỆ THỐNG .................................................................... 11 
2.1. Bối cảnh Sản phẩm....................................................................................................... 11 
2.1.1. Vị trí trong Hệ sinh thái ........................................................................................... 11 
2.1.2. Quan hệ với Hệ thống Ngoài.................................................................................. 11 
2.2. Chức năng Sản phẩm Tổng quát ................................................................................. 12 
2.3. Các Lớp Người dùng và Đặc điểm............................................................................... 12 
2.4. Môi trường Vận hành .................................................................................................... 13 
2.4.1. Môi trường Server (Production) ............................................................................. 13 
2.4.2. Môi trường Phát triển (Development) .................................................................... 13 
2.4.3. Yêu cầu Trình duyệt Client..................................................................................... 14 
2.5. Ràng buộc Thiết kế và Hiện thực ................................................................................. 14 
2.6. Giả định và Phụ thuộc................................................................................................... 15 
2.6.1. Giả định................................................................................................................... 15 
2.6.2. Phụ thuộc Bên ngoài .............................................................................................. 15 
CHƯƠNG 3. YÊU CẦU CHỨC NĂNG CHI TIẾT ................................................................... 17 
3.1. Module Xác thực và Quản lý Người dùng (FR-AUTH) ................................................ 17 
FR-AUTH-001: Đăng ký Tài khoản (User Registration)................................................... 17 
FR-AUTH-002: Đăng nhập bằng Email/Mật khẩu (Local Login) ..................................... 18 
FR-AUTH-003: Đăng nhập bằng Google OAuth 2.0 ....................................................... 19 
FR-AUTH-004: Làm mới Access Token (Token Refresh) ............................................... 20 
FR-AUTH-005: Đăng xuất (Logout / Token Revocation) ................................................. 21 
FR-AUTH-006: Xem Hồ sơ Cá nhân (View Profile)......................................................... 22 
FR-AUTH-007: Cập nhật Hồ sơ Cá nhân (Update Profile) ............................................. 23 
3.2. Module Quản lý Danh mục (FR-CAT) .......................................................................... 23 
FR-CAT-001: Xem Danh sách Danh mục........................................................................ 23 
FR-CAT-002: Xem Chi tiết Danh mục và Công thức ....................................................... 24 
FR-CAT-003: Tạo Danh mục Mới [Admin]....................................................................... 25


---

<!-- PAGE 4 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao  •  Trang 4 / 71 
FR-CAT-004: Cập nhật Danh mục [Admin] ..................................................................... 26 
FR-CAT-005: Xóa Danh mục [Admin] .............................................................................. 26 
3.3. Module Quản lý Công thức Nấu ăn (FR-RCP)............................................................. 27 
FR-RCP-001: Xem Danh sách Công thức (Paginated + Filtered + Sorted) ................... 27 
FR-RCP-002: Xem Chi tiết Công thức ............................................................................. 28 
FR-RCP-003: Tạo Công thức Nấu ăn Mới [Author/Admin] ............................................. 29 
FR-RCP-004: Cập nhật Công thức [Author-Owner/Admin] ............................................. 30 
FR-RCP-005: Xuất bản / Hủy Xuất bản Công thức ......................................................... 31 
FR-RCP-006: Lưu trữ Công thức (Archive) ..................................................................... 32 
FR-RCP-007: Xóa Công thức [Author-Owner/Admin] ..................................................... 32 
FR-RCP-008: Quản lý Ảnh Công thức (Upload / Set Primary / Delete) .......................... 33 
FR-RCP-009: Quản lý Nguyên liệu (CRUD RecipeIngredient) ....................................... 34 
FR-RCP-010: Quản lý Các bước Thực hiện (CRUD RecipeStep).................................. 35 
3.4. Module Tìm kiếm và Phân trang (FR-SRCH) ............................................................... 36 
FR-SRCH-001: Tìm kiếm Toàn văn bản (Full-Text Search)............................................ 36 
FR-SRCH-002/003/004: Lọc, Sắp xếp và Phân trang (Tóm tắt) ..................................... 37 
3.5. Module Quản lý Tệp tin (FR-FILE) ............................................................................... 37 
3.6. Module Background Jobs (FR-JOB)............................................................................. 38 
3.7. Module Quan sát Hệ thống (FR-OBS).......................................................................... 39 
4. Yêu cầu Phi Chức năng (NFR)............................................................................................ 40 
4.1. Hiệu năng (NFR-PERF) ................................................................................................ 40 
4.2. Bảo mật (NFR-SEC) ..................................................................................................... 41 
4.3. Khả năng Sử dụng (NFR-USE) .................................................................................... 42 
4.4. Độ tin cậy (NFR-REL) ................................................................................................... 42 
4.5. Khả năng Bảo trì (NFR-MAINT).................................................................................... 43 
4.6. Khả năng Mở rộng (NFR-SCALE) ................................................................................ 44 
4.7. Tối ưu SEO (NFR-SEO) ............................................................................................... 44 
5. Yêu cầu Giao diện Ngoài ..................................................................................................... 46 
5.1. Giao diện Người dùng (UI) ........................................................................................... 46 
5.2. Giao diện Phần mềm – REST API ............................................................................... 47 
5.3. Giao diện Dịch vụ Bên thứ ba....................................................................................... 47 
5.4. Giao diện Phần cứng .................................................................................................... 48 
6. Kiến trúc Hệ thống ............................................................................................................... 50 
6.1. Tổng quan Kiến trúc...................................................................................................... 50 
6.2. Kiến trúc Backend – Clean Architecture....................................................................... 50 
6.3. CQRS + MediatR Pipeline ............................................................................................ 51 
6.4. Mô hình Quan hệ Thực thể (ERD tóm tắt) ................................................................... 52 
6.5. Triển khai – Docker Compose ...................................................................................... 52 
7. Mô hình Dữ liệu ................................................................................................................... 54


---

<!-- PAGE 5 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao  •  Trang 5 / 71 
7.1. BaseEntity (Abstract) .................................................................................................... 54 
7.2. Recipe ........................................................................................................................... 54 
7.2.1. RecipeNutrition (Owned Entity — cột trong bảng Recipes) .................................. 56 
7.3. RecipeStep .................................................................................................................... 57 
7.4. RecipeIngredient ........................................................................................................... 57 
7.5. RecipeImage ................................................................................................................. 58 
7.6. Category ........................................................................................................................ 58 
7.7. ApplicationUser (extends IdentityUser) ........................................................................ 58 
7.8. RefreshToken................................................................................................................ 59 
8. Đặc tả REST API ................................................................................................................. 61 
8.1. Authentication Module (/auth) ....................................................................................... 61 
8.2. Categories Module (/categories)................................................................................... 62 
8.3. Recipes Module (/recipes) ............................................................................................ 63 
8.4. Recipe Images (/recipes/{id}/images) ........................................................................... 64 
8.5. Recipe Steps (/recipes/{id}/steps) ................................................................................ 64 
8.6. Recipe Ingredients (/recipes/{id}/ingredients)............................................................... 65 
8.7. Health Check Endpoints ............................................................................................... 65 
Phụ lục A – HTTP Status Codes ............................................................................................. 67 
Phụ lục B – Application Error Codes ....................................................................................... 67 
Phụ lục C – Từ điển Thuật ngữ ............................................................................................... 69


---

<!-- PAGE 6 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 6 / 71 
CHƯƠNG 1. GIỚI THIỆU 
 
1.1. Mục đích Tài liệu 
Tài liệu Đặc tả Yêu cầu Phần mềm (Software Requirements Specification – SRS) này được 
biên soạn theo tiêu chuẩn IEEE 830-1998 và ISO/IEC/IEEE 29148:2018 nhằm mô tả đầy đủ, 
chính xác và nhất quán toàn bộ yêu cầu chức năng (Functional Requirements) và yêu cầu phi 
chức năng (Non-Functional Requirements) của dự án ứng dụng web Blog Ẩm thực và Nấu 
ăn (Culinary Blog). 
Tài liệu này phục vụ các đối tượng sau: 
• Nhóm phát triển Backend (.NET 10/C#): Căn cứ thiết kế API, domain model, và 
business rules. 
• Nhóm phát triển Frontend (Next.js/TypeScript): Căn cứ thiết kế giao diện, luồng 
người dùng và tích hợp API. 
• Kỹ sư Kiểm thử (QA/QC): Cơ sở xây dựng test cases, kiểm thử chấp nhận 
(acceptance testing). 
• Kiến trúc sư Hệ thống: Tham chiếu khi đưa ra quyết định kiến trúc (architecture 
decisions). 
• Giảng viên và Sinh viên: Tài liệu học thuật mẫu cho dự án thực hành xuyên suốt 
giáo trình. 
• Stakeholder / Product Owner: Phê duyệt phạm vi và ưu tiên tính năng. 
 
Phạm vi hiệu lực: Tài liệu này có hiệu lực từ phiên bản 1.0.2 và là tài liệu nền tảng (baseline) 
cho toàn bộ vòng đời phát triển dự án. Mọi thay đổi yêu cầu sau khi tài liệu được phê duyệt 
phải tuân theo quy trình quản lý thay đổi (Change Management Process). 
1.2. Phạm vi Sản phẩm 
1.2.1. Tên và Định danh 
Thuộc tính Giá trị 
Tên sản phẩm Culinary Blog – Blog Ẩm thực và Nấu ăn 
Định danh d ự án CULINARY-BLOG-V1 
Loại hệ thống Ứng dụng Web Full-Stack (API-Driven Architecture)  
Phiên b ản sản phẩm 1.0.0 
Môi trư ờng đích Cloud/On-premise (Docker Compose + Nginx)  
 
1.2.2. Mô tả Sản phẩm 
Culinary Blog là một nền tảng web cho phép người dùng chia sẻ, khám phá và lưu trữ các 
công thức nấu ăn từ nhiều nền ẩm thực khác nhau. Ứng dụng cung cấp hệ sinh thái hoàn 
chỉnh bao gồm: 
• Nền tảng chia sẻ công thức: Tác giả (Author) đăng tải công thức với hình ảnh, 
danh sách nguyên liệu chi tiết, hướng dẫn từng bước thực hiện và thông tin dinh 
dưỡng.


---

<!-- PAGE 7 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 7 / 71 
• Tổ chức nội dung: Phân loại công thức theo danh mục (Category), độ khó 
(Difficulty Level), thời gian chuẩn bị và nấu. 
• Tìm kiếm thông minh: Full-Text Search tiếng Việt sử dụng PostgreSQL 
tsvector/tsquery với unaccent extension. 
• Bảo mật đa lớp: Xác thực JWT stateless, phân quyền theo vai trò (RBAC) và theo 
tài nguyên (Resource-Based Authorization), đăng nhập Google OAuth 2.0. 
• Tối ưu hiệu năng và SEO: Redis distributed cache, Next.js ISR, Open Graph 
Protocol, JSON-LD Schema.org Recipe markup. 
• Quan sát hệ thống: Structured logging (Serilog), distributed tracing 
(OpenTelemetry), health check endpoints. 
 
1.2.3. Những gì KHÔNG thuộc phạm vi 
Các tính năng sau đây nằm ngoài phạm vi phiên bản 1.0.0: 
• Hệ thống bình luận (Comment System) và đánh giá sao (Rating System). 
• Tính năng lưu/đánh dấu công thức yêu thích (Bookmark/Favorite). 
• Thông báo real-time (SignalR/WebSocket). 
• Ứng dụng di động native (iOS/Android). 
• Thanh toán / Tính năng thương mại điện tử. 
• Hệ thống nhắn tin trực tiếp giữa người dùng. 
• GraphQL API (định hướng sau khóa học). 
 
1.3. Định nghĩa, Từ viết tắt và Ký hiệu 
Thuật ngữ / Viết tắt Định nghĩa đ ầy đủ 
SRS Software Requirements Specification – Đặc tả Yêu cầu Phần 
mềm. 
FR Functional Requirement – Yêu c ầu chức năng. 
NFR Non-Functional Requirement – Yêu c ầu phi chức năng. 
API Application Programming Interface – Giao di ện lập trình ứng dụng. 
REST Representational State Transfer – Kiểu kiến trúc API ph ổ biến 
nhất. 
JWT JSON Web Token – Chuẩn token xác th ực stateless (RFC 7519). 
RBAC Role-Based Access Control – Kiểm soát truy c ập d ựa trên vai trò.  
CQRS Command Query Responsibility Segregation – Pattern tách bi ệt 
lệnh và truy v ấn. 
DDD  Domain-Driven Design – Phương pháp thi ết kế phần mềm lấy 
domain làm trung tâm.  
ORM Object-Relational Mapper – Công c ụ ánh xạ object -database (EF 
Core). 
FTS Full-Text Search – Tìm kiếm toàn văn b ản. 
ISR Incremental Static Regeneration – Kỹ thuật tái t ạo trang tĩnh c ủa 
Next.js.


---

<!-- PAGE 8 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 8 / 71 
Thuật ngữ / Viết tắt Định nghĩa đ ầy đủ 
LCP Largest Contentful Paint – Core Web Vital đo t ốc độ tải nội dung 
lớn nhất. 
CLS Cumulative Layout Shift – Core Web Vital đo đ ộ ổn định bố cục 
trang. 
INP Interaction to Next Paint – Core Web Vital đo th ời gian ph ản hồi 
tương tác.  
CI/CD Continuous Integration / Continuous Delivery – Tích hợp và tri ển 
khai liên t ục. 
DXA Device-independent pixel unit used in OOXML (1 inch = 1440 
DXA). 
TTL Time-To-Live – Thời gian s ống của dữ liệu trong cache.  
SSR Server-Side Rendering – Render HTML trên server.  
SSG Static Site Generation – Tạo trang tĩnh lúc build time.  
MoSCoW Must Have / Should Have / Could Have / Won't Have – Mô hình 
phân loại ưu tiên. 
RFC Request For Comments – Tài liệu tiêu chuẩn kỹ thuật (e.g., RFC 
7807). 
ERD Entity Relationship Diagram – Sơ đ ồ quan hệ thực thể. 
PBKDF2 Password -Based Key Derivation Function 2 – Thuật toán hash m ật 
khẩu an toàn.  
CDN Content Delivery Network – Mạng phân ph ối nội dung.  
MIME Multipurpose Internet Mail Extensions – Chuẩn đ ịnh dạng tệp trên 
Internet. 
JSON-LD JavaScript Object Notation for Linked Data – Định dạng dữ liệu có 
cấu trúc cho SEO.  
 
1.4. Tài liệu Tham chiếu 
STT Tài liệu / Tiêu 
chuẩn Nguồn / URL 
1 
IEEE Std 830 -
1998 – 
Recommended 
Practice for 
Software 
Requirements 
Specifications  
https://ieeexplore.ieee.org/document/720574  
2 
ISO/IEC/IEEE 
29148:2018 – 
Requirements 
Engineering 
https://www.iso.org/standard/72089.html  
3 OWASP Top 
10:2021 – Top 10 https://owasp.org/www -project -top -ten/


---

<!-- PAGE 9 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 9 / 71 
STT Tài liệu / Tiêu 
chuẩn Nguồn / URL 
Web Application 
Security Risks  
4 
RFC 7807 – 
Problem Details 
for HTTP APIs  
https://datatracker.ietf.org/doc/html/rfc7807  
5 
RFC 7519 – 
JSON Web Token 
(JWT) 
https://datatracker.ietf.org/doc/html/rfc7519  
6 
RFC 6749 – The 
OAuth 2.0 
Authorization 
Framework 
https://datatracker.ietf.org/doc/html/rfc6749  
7 
.NET 10 Minimal 
APIs – Microsoft 
Learn 
https://learn.microsoft.com/aspnet/core/fundamentals/minimal -apis 
8 
ASP.NET Core 
Identity – 
Microsoft Learn  
https://learn.microsoft.com/aspnet/core/security/authentication/identity  
9 
Entity Framework 
Core 10 
Documentation  
https://learn.microsoft.com/ef/core/  
10 
Next.js 15 App 
Router 
Documentation  
https://nextjs.org/docs  
11 
PostgreSQL 16 
Documentation – 
Full-Text Search  
https://www.postgresql.org/docs/16/textsearch.html  
12 Redis 7 
Documentation  https://redis.io/docs/  
13 
MinIO S3-
Compatible 
Object Storage  
https://min.io/docs/  
14 
Google Web 
Vitals – Core Web 
Vitals 
https://web.dev/explore/learn -core-web-vitals 
15 
Schema.org 
Recipe – 
Structured Data  
https://schema.org/Recipe  
16 
OpenTelemetry 
.NET 
Documentation  
https://opentelemetry.io/docs/languages/dotnet/  
17 Serilog 
Documentation  https://serilog.net/  
18 Hangfire 
Documentation  https://docs.hangfire.io/


---

<!-- PAGE 10 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 10 / 71 
STT Tài liệu / Tiêu 
chuẩn Nguồn / URL 
19 FluentValidation 
Documentation  https://docs.fluentvalidation.net/  
20 
Giáo trình Phát 
triển Ứng dụng 
Web Nâng cao V4 
– Nội bộ 
N/A (tài liệu nội bộ) 
 
1.5. Tổng quan Tài liệu 
Tài liệu SRS này được tổ chức thành 8 chương chính và 3 phụ lục, theo cấu trúc từ tổng quan 
đến chi tiết: 
• Chương 2 – Mô tả Tổng quan: Bối cảnh sản phẩm, chức năng tóm tắt, các lớp 
người dùng, môi trường vận hành và ràng buộc thiết kế. 
• Chương 3 – Yêu cầu Chức năng: 27 FR được đặc tả chi tiết theo format chuẩn, 
nhóm thành 7 module chức năng. 
• Chương 4 – Yêu cầu Phi chức năng: Hiệu năng, bảo mật, khả năng sử dụng, độ 
tin cậy, khả năng bảo trì/mở rộng và SEO. 
• Chương 5 – Giao diện Ngoài: Tích hợp với các hệ thống và dịch vụ ngoài (Google 
OAuth, MinIO, Redis, SendGrid). 
• Chương 6 – Kiến trúc Hệ thống: Clean Architecture Backend, Next.js App Router 
Frontend, chiến lược caching và deployment. 
• Chương 7 – Mô hình Dữ liệu: ERD mô tả văn bản và bảng định nghĩa chi tiết từng 
entity/table. 
• Chương 8 – Đặc tả API REST: Quy ước, chuẩn lỗi RFC 7807, và bảng tổng hợp tất 
cả ~30 endpoint. 
• Phụ lục A-C: HTTP Status Codes, Application Error Codes, và Từ điển thuật ngữ.


---

<!-- PAGE 11 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 11 / 71 
CHƯƠNG 2. MÔ TẢ TỔNG QUAN HỆ THỐNG 
 
2.1. Bối cảnh Sản phẩm 
2.1.1. Vị trí trong Hệ sinh thái 
Culinary Blog vận hành theo mô hình API-Driven Architecture, trong đó Backend (.NET 10) 
và Frontend (Next.js) là hai hệ thống độc lập giao tiếp hoàn toàn qua HTTP/JSON RESTful 
API. Không có server -side rendering truy ền thống (MVC Razor/Blazor) hay shared view 
engine giữa hai tầng. 
 
Sơ đồ bối cảnh hệ thống (Context Diagram): 
 
┌─────────────────────────────────────────────────────────────────┐ 
│                    CULINARY BLOG SYSTEM                         │ 
│                                                                 │ 
│   ┌──────────────────┐        ┌───────────────────────────────┐  │ 
│   │  NEXT.JS FRONTEND│◄──────►│    .NET 10 BACKEND API        │  │ 
│   │  (App Router)    │  REST  │    (Minimal APIs + Clean Arch) │  │ 
│   │  Port: 3000      │  JSON  │    Port: 5000                 │  │ 
│   └──────────────────┘        └──────────────┬────────────────┘  │ 
│                                             │                  │ 
│   ┌──────┐ ┌────────┐  ┌────────┐  ┌────────┐ ┌───────────┐   │ 
│   │ Pgsql│ │ Redis  │  │ MinIO  │  │Hangfire│ │Google Auth│   │ 
│   │:5432 │ │:6379   │  │:9000   │  │ Jobs   │ │ OAuth2.0  │   │ 
│   └──────┘ └────────┘  └────────┘  └────────┘ └───────────┘   │ 
└─────────────────────────────────────────────────────────────────┘ 
 
Hình 2.1. Sơ đồ bối cảnh hệ thống Culinary Blog 
 
2.1.2. Quan hệ với Hệ thống Ngoài 
Hệ thống Ngoài Vai trò Giao thức / Chu ẩn Hướng tích h ợp 
PostgreSQL 16  Hệ quản trị CSDL quan 
hệ chính (RDBMS) 
TCP + Npgsql Driver 
(EF Core) Backend → PostgreSQL  
Redis 7 Distributed Cache & 
Session Store  
TCP + 
StackExchange.Redis  Backend → Redis  
MinIO (S3) Object Storage cho ảnh 
công th ức 
HTTP/S3 API + 
MinIO .NET SDK  Backend → MinIO 
Google OAuth 
2.0 
Đăng nhập bên th ứ ba 
(Identity Provider)  
HTTPS + OpenID 
Connect 
Client ↔ Google ↔ 
Backend 
Hangfire Background Job 
Processing (embedded)  In-process (.NET) Backend (internal) 
Serilog / Seq  
Structured Log 
Aggregation 
(development)  
HTTP Sink → Seq  Backend → Seq  
OpenTelemetry 
Collector 
Distributed Tracing & 
Metrics (production)  OTLP / gRPC  Backend → Collector


---

<!-- PAGE 12 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 12 / 71 
Hệ thống Ngoài Vai trò Giao thức / Chu ẩn Hướng tích h ợp 
Nginx (Reverse 
Proxy) 
SSL termination, load 
balancing, static serving  HTTP/HTTPS Client → Nginx → 
Services 
 
2.2. Chức năng Sản phẩm Tổng quát 
Culinary Blog cung c ấp 7 nhóm ch ức năng chính, đư ợc hiện thực hóa qua 27 Functional 
Requirements chi tiết tại Chương 3: 
 
Nhóm chức năng  Mã nhóm Số 
FR Mô tả tóm tắt 
Xác thực & Quản lý 
Người dùng FR-AUTH 7 Đăng ký, đăng nh ập (email + Google), JWT 
refresh token, logout, qu ản lý profile.  
Quản lý Danh m ục FR-CAT 5 CRUD danh m ục công th ức (Category) – phân 
quyền Admin. 
Quản lý Công th ức nấu 
ăn FR-RCP 10 CRUD recipe, publish/archive, qu ản lý 
ảnh/bước/nguyên li ệu. 
Tìm kiếm & Phân trang FR-SRCH 4 Full-Text Search (PostgreSQL), filter, sort, 
offset pagination.  
Quản lý Tệp tin FR-FILE 2 Upload/Delete ảnh trên MinIO S3 -compatible.  
Background Jobs  FR-JOB 3 Email chào m ừng, thumbnail generation, 
sitemap XML (Hangfire).  
Quan sát Hệ thống FR-OBS 3 Health checks, structured logging, distributed 
tracing. 
 
2.3. Các Lớp Người dùng và Đặc điểm 
Hệ thống định nghĩa 3 loại tác nhân (Actor) với quyền hạn khác nhau: 
 
Vai trò Mô tả Điều kiện Quyền hạn chính 
Ưu tiên 
phục 
vụ 
Khách (Guest 
/ Anonymous)  
Người dùng chưa 
xác thực, truy c ập 
ứng dụng mà 
không có tài 
khoản. 
Không c ần tài 
khoản 
Xem danh sách & chi ti ết 
recipe (Published), xem 
danh mục, tìm ki ếm. 
KHÔNG đư ợc tạo/sửa/xóa. 
Cao 
(đây là 
đại đa 
số 
người 
dùng) 
Tác giả 
(Author) 
Người dùng đã 
đăng ký và xác 
thực thành công. 
Được tự động gán 
khi đăng ký.  
Có tài kho ản & 
JWT hợp lệ 
+ Tất cả quyền của Guest. 
+ Tạo/sửa/xóa recipe C ỦA 
MÌNH. + Upload ảnh, quản 
lý steps/ingredients. + 
Publish/Archive recipe c ủa 
mình. 
Cao 
(nhà 
sản 
xuất nội 
dung) 
Quản trị viên 
(Admin) 
Người quản lý hệ 
thống với quyền 
cao nhất. Được 
Có tài kho ản & 
role Admin  
+ Tất cả quyền của Author. 
+ Quản lý (CRUD) danh 
mục. + S ửa/xóa b ất kỳ 
Trung 
bình (số


---

<!-- PAGE 13 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 13 / 71 
Vai trò Mô tả Điều kiện Quyền hạn chính 
Ưu tiên 
phục 
vụ 
gán thủ công qua 
database seeding.  
recipe của bất kỳ Author. + 
Truy cập Hangfire 
Dashboard. + Xem 
structured logs.  
lượng 
ít) 
 
Ghi chú v ề phân quy ền: Hệ thống tri ển khai 3 t ầng phân quy ền. (1) Role -Based 
Authorization: phân bi ệt quy ền d ựa trên role (Guest/Author/Admin). (2) Resource -Based 
Authorization: Author chỉ sửa/xóa được recipe của chính mình (AuthorId == currentUserId). 
(3) Policy-Based Authorization: Policy "VerifiedAuthor" yêu cầu email đã xác nhận. Admin có 
quyền bypass resource ownership check. 
 
2.4. Môi trường Vận hành 
2.4.1. Môi trường Server (Production) 
Thành phần Yêu cầu tối thiểu Khuyến ngh ị Ghi chú 
Hệ điều hành Linux Ubuntu 
22.04 LTS 
Ubuntu 22.04 
LTS / Debian 12  Docker ph ải được cài đ ặt 
.NET Runtime .NET 10.0 Runtime 
(aspnet) 
.NET 10.0.x 
latest patch  
Cung c ấp qua Docker image 
mcr.microsoft.com/dotnet/aspnet:10.0  
Node.js  Node.js 20 LTS 
(build only)  Node.js 22 LTS Chỉ cần lúc build Next.js; production 
dùng standalone output  
PostgreSQL  PostgreSQL 16.x  PostgreSQL 
16.x 
Extensions: unaccent, pg_trgm b ắt 
buộc 
Redis Redis 7.x  Redis 7.2.x  Persistent mode v ới AOF 
MinIO MinIO 
RELEASE.2024+ 
MinIO latest 
stable 
Bucket policy: public -read cho recipe 
images 
Docker  Docker Engine 
24.x 
Docker Engine 
27.x + Compose 
v2 
Docker Compose cho local dev và 
staging  
Nginx Nginx 1.24+ Nginx 1.26+ 
(stable) Reverse proxy, SSL termination  
RAM 4 GB minimum 8 GB+ RAM cần tăng n ếu Redis cache l ớn 
CPU 2 vCPU minimum  4 vCPU+ CPU-intensive: FTS indexing, image 
processing  
Disk 20 GB SSD 
minimum 50 GB+ SSD  MinIO object storage t ốn nhiều disk  
 
2.4.2. Môi trường Phát triển (Development) 
Thành phần Yêu cầu 
.NET 10 SDK  dotnet SDK 10.0.x (bao g ồm CLI và runtime)


---

<!-- PAGE 14 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 14 / 71 
Thành phần Yêu cầu 
Node.js  Node.js 20+ LTS v ới npm 10+ 
Docker Desktop  Docker Desktop 4.x+ (Windows/macOS) ho ặc Docker Engine (Linux) – 
để chạy PostgreSQL, Redis, MinIO local  
IDE / Editor  Visual Studio 2022 v17.12+ / Rider 2024+ / VS Code v ới C# Dev Kit 
extension 
Git Git 2.40+ v ới Git LFS (nếu lưu asset l ớn) 
Postman / Scalar  Postman ho ặc Scalar UI (tích h ợp sẵn, chạy tại /scalar) đ ể test API 
 
2.4.3. Yêu cầu Trình duyệt Client 
Trình duyệt Phiên bản tối thiểu Ghi chú 
Google Chrome  90+ Khuyến nghị chính – tốt nhất cho 
Developer Tools  
Mozilla Firefox  88+ Hỗ trợ đầy đủ 
Microsoft Edge  90+ (Chromium) Hỗ trợ đầy đủ (Chromium-based) 
Safari 14+ (macOS 11+)  Hỗ trợ đầy đủ; Safari 13 tr ở xuống 
KHÔNG đ ảm bảo 
Mobile Chrome (Android)  90+ Responsive design, touch -friendly  
Mobile Safari (iOS)  iOS 14+ Hỗ trợ đầy đủ 
Internet Explorer  Mọi phiên b ản KHÔNG hỗ trợ (EOL) 
 
2.5. Ràng buộc Thiết kế và Hiện thực 
Các ràng buộc sau đây là bắt buộc và không thể thương lượng trong suốt quá trình phát triển: 
 
Mã ràng 
buộc Loại Mô tả ràng buộc 
CONS-001 Kiến trúc 
Backend PH ẢI tuân thủ Clean Architecture v ới 4 tầng riêng bi ệt: 
Domain, Application, Infrastructure, Presentation. T ầng Domain 
không đư ợc phụ thuộc bất kỳ thư viện ngoài nào.  
CONS-002 Pattern 
CQRS với MediatR là pattern b ắt buộc cho t ầng Application. M ỗi 
use case đư ợc hiện thực dư ới dạng Command ho ặc Query 
Handler riêng bi ệt. 
CONS-003 Ngôn ng ữ / 
Framework 
Backend: .NET 10 Minimal APIs (không dùng MVC Controllers). 
Frontend: Next.js App Router (không dùng Pages Router).  
CONS-004 Bảo mật 
Xác thực PHẢI sử dụng JWT stateless (access token 15 phút, 
refresh token 7 ngày). M ật khẩu PHẢI đư ợc hash với PBKDF2 qua 
ASP.NET Core Identity.  
CONS-005 API Design  
API PHẢI tuân th ủ RESTful design. Ph ản hồi lỗi PHẢI theo RFC 
7807 (application/problem+json). API versioning qua URL path 
(/api/v1/).


---

<!-- PAGE 15 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 15 / 71 
Mã ràng 
buộc Loại Mô tả ràng buộc 
CONS-006 Database 
PostgreSQL là DBMS duy nh ất. Migrations qua EF Core Code -
First. Không vi ết raw SQL trực tiếp (dùng LINQ ho ặc Raw SQL có 
parameterization qua EF Core).  
CONS-007 File Upload  
Kích thước tệp tải lên tối đa 5 MB. Đ ịnh dạng chỉ chấp nhận: 
image/jpeg, image/png, image/webp, image/avif. Ki ểm tra MIME 
type (không ch ỉ extension). 
CONS-008 Validation  Input validation PH ẢI qua FluentValidation k ết hợp MediatR 
Pipeline Behavior. Không validation trong Endpoint handler.  
CONS-009 Container 
Ứng dụng PHẢI được đóng gói Docker. Dockerfile multi -stage 
build (SDK → aspnet runtime). Docker Compose cho local 
development.  
CONS-010 Logging  Structured logging v ới Serilog là b ắt buộc. Mọi log entry PH ẢI có 
CorrelationId, RequestPath, UserId (khi đã xác th ực). 
 
2.6. Giả định và Phụ thuộc 
2.6.1. Giả định 
• Môi trường development có kết nối Internet để pull Docker images và package 
NuGet/npm. 
• PostgreSQL, Redis và MinIO được cung cấp qua Docker Compose trong 
development và dưới dạng managed service (hoặc VPS) trong production. 
• Người dùng cuối có trình duyệt hiện đại và kết nối Internet đủ ổn định để load ảnh từ 
MinIO. 
• Dữ liệu test (seed) được tạo bằng thư viện Bogus với 50 recipe mẫu và 5 tác giả 
mẫu. 
• Email service (SendGrid hoặc SMTP) được cấu hình sẵn khi triển khai production để 
gửi email chào mừng. 
• Giới hạn dữ liệu kỳ vọng (initial scale): ≤ 10,000 công thức, ≤ 5,000 người dùng, 
≤ 50 danh mục – phù hợp với single-server deployment. 
 
2.6.2. Phụ thuộc Bên ngoài 
Phụ thuộc Phiên bản Mức độ ảnh hưởng 
nếu không khả dụng Kế hoạch dự phòng 
Google OAuth 
2.0 API 
v2 (OpenID 
Connect) 
Cao – Mất chức năng 
đăng nhập Google  
Vẫn có đăng nh ập 
email/password. Hi ển thị thông 
báo "Google login t ạm thời 
không kh ả dụng". 
MinIO / S3 MinIO 
RELEASE.2024+ 
Cao – Không 
upload/xem đư ợc ảnh 
Fallback v ề local FileSystem 
storage (development only). 
Production c ần MinIO. 
Redis 7.x Trung bình – Mất 
cache, hiệu năng gi ảm 
Hệ thống tiếp tục hoạt động 
nhưng mọi request đ ều query 
database. Cache miss graceful 
degradation.


---

<!-- PAGE 16 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 16 / 71 
Phụ thuộc Phiên bản Mức độ ảnh hưởng 
nếu không khả dụng Kế hoạch dự phòng 
PostgreSQL  16.x Rất cao – Toàn bộ hệ 
thống ngừng 
Backup đ ịnh kỳ (pg_dump). 
Readiness probe s ẽ fail, Nginx 
trả 503. 
Hangfire (in-
process)  v1.8+ Thấp – Background 
jobs không ch ạy 
Fire-and-forget jobs s ẽ bị mất; 
Recurring jobs b ỏ qua chu kỳ. 
Không ảnh hưởng core 
functionality.


---

<!-- PAGE 17 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 17 / 71 
CHƯƠNG 3. YÊU CẦU CHỨC NĂNG CHI TIẾT 
 
Chương này đặc tả chi tiết 27 Functional Requirements (FR) đư ợc nhóm thành 7 module 
chức năng. Mỗi FR được mô tả theo template chuẩn bao gồm: Mã yêu cầu, Tên, Nhóm chức 
năng, Tác nhân, Mức ưu tiên (MoSCoW), Mô tả, Điều kiện tiên quyết, Luồng chính, Luồng 
thay thế/Ngoại lệ, HTTP Endpoint, Kết quả mong đợi và HTTP Status Code. 
 
Quy ước mức ưu tiên MoSCoW: M (Must Have – Bắt buộc), S (Should Have – Nên có), C 
(Could Have – Có thể có), W (Won't Have – Không trong scope hiện tại). 
 
3.1. Module Xác thực và Quản lý Người dùng (FR-AUTH) 
Module này quản lý toàn bộ vòng đời xác thực người dùng: từ đăng ký, đăng nhập đa phương 
thức, duy trì phiên làm việc với cơ chế token rotation, đến quản lý hồ sơ cá nhân. Backend 
sử dụng ASP.NET Core Identity kết hợp JWT và OAuth 2.0. 
 
FR-AUTH-001: Đăng ký Tài khoản (User Registration) 
Mã yêu cầu FR-AUTH-001 
Tên yêu cầu Đăng ký Tài kho ản Mới 
Nhóm chức năng  Module Xác th ực và Quản lý Ngư ời dùng (FR-AUTH) 
Tác nhân Khách (Guest / Anonymous User)  
Mức ưu tiên 
(MoSCoW) 
M – Must Have (Bắt bu ộc) 
Mô tả 
Hệ thống cho phép ngư ời dùng chưa có tài kho ản tạo một tài kho ản 
mới bằng cách cung cấp thông tin cơ b ản. Sau khi đăng ký thành công, 
người dùng t ự động đư ợc gán role "Author" và nh ận bộ token đ ể truy 
cập ngay l ập tức (auto-login sau đăng ký). H ệ thống kích ho ạt job g ửi 
email chào m ừng bất đồng bộ qua Hangfire.  
Điều kiện tiên quyết 
1. Người dùng chưa đăng nh ập vào h ệ thống. 2. Endpoint POST 
/api/v1/auth/register đang ho ạt động. 3. PostgreSQL database đang k ết 
nối thành công.  
Luồng chính (Happy 
Path) 
1. Người dùng (client) g ửi HTTP POST đ ến /api/v1/auth/register v ới 
JSON body: { "fullName": "...", "email": "...", "userName": "...", 
"password": "..." }.  
2. RegisterCommand đư ợc tạo và dispatch đ ến MediatR. 
3. ValidationBehavior ch ạy RegisterCommandValidator: ki ểm tra 
fullName không r ỗng, email đúng format, userName không ch ứa ký t ự 
đặc biệt, password t ối thiểu 8 ký t ự (1 chữ hoa, 1 chữ số, 1 ký t ự đặc 
biệt). 
4. RegisterCommandHandler ki ểm tra email chưa t ồn tại trong 
database (UserManager.FindByEmailAsync).  
5. Tạo ApplicationUser m ới qua factory method 
ApplicationUser.Create(fullName, email, userName).  
6. UserManager.CreateAsync(user, password) – ASP.NET Core 
Identity t ự hash password v ới PBKDF2.


---

<!-- PAGE 18 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 18 / 71 
7. UserManager.AddToRoleAsync(user, "Author") – gán role mặc định. 
8. JwtService.GenerateAccessToken() – tạo JWT access token 
(HS256, 15 phút).  
9. JwtService.GenerateRefreshToken() – tạo refresh token ng ẫu nhiên 
(128-bit, 7 ngày).  
10. Lưu RefreshToken vào b ảng refresh_tokens trong database.  
11. BackgroundJob.Enqueue<WelcomeEmailJob>() – đẩy job g ửi 
email chào m ừng vào Hangfire queue (fire -and-forget).  
12. Trả về HTTP 201 Created v ới AuthResponseDto: { accessToken, 
refreshToken, expiresAt, user: { id, fullName, email, userName, 
avatarUrl, roles } }.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – Email đã t ồn tại: Tại bư ớc 4, nếu email đã đư ợc đăng ký → Throw 
ConflictException → GlobalExceptionMiddleware tr ả về HTTP 409 
Conflict v ới RFC 7807 body.  
A2 – Password không đ ủ mạnh: Tại bước 3 hoặc 6, 
UserManager.CreateAsync tr ả về IdentityError → Throw 
ValidationException → HTTP 400 Bad Request v ới danh sách 
lỗi chi ti ết. 
A3 – Dữ liệu đ ầu vào không h ợp lệ: Tại bước 3, FluentValidation fail → 
HTTP 400 với từng field l ỗi (theo RFC 7807 ValidationProblemDetails).  
A4 – Database không k ết nối: EF Core ném DbUpdateException → 
HTTP 500 Internal Server Error (GlobalExceptionMiddleware log l ỗi, 
không expose stack trace).  
HTTP Method & 
Endpoint 
POST  /api/v1/auth/register  
Kết quả mong đợi 
Tài khoản mới được tạo trong database, role "Author" đư ợc gán, 
refresh token đư ợc persist, email chào m ừng đư ợc đẩy vào Hangfire 
queue. Client nhận đư ợc access token và refresh token.  
HTTP Status Code tr ả 
về 
201 Created – Đăng ký thành công. 409 Conflict – Email đã t ồn tại. 422 
Unprocessable Entity – Dữ liệu không h ợp lệ. 500 Internal Server Error 
– Lỗi hệ thống. 
 
FR-AUTH-002: Đăng nhập bằng Email/Mật khẩu (Local Login) 
Mã yêu cầu FR-AUTH-002 
Tên yêu cầu Đăng nhập b ằng Email và M ật khẩu 
Nhóm chức năng  Module Xác th ực và Quản lý Ngư ời dùng (FR-AUTH) 
Tác nhân Tác giả đã đăng ký (Author) ho ặc Quản trị viên (Admin) 
Mức ưu tiên 
(MoSCoW) 
M – Must Have (Bắt bu ộc) 
Mô tả 
Hệ thống cho phép ngư ời dùng đã có tài kho ản đăng nhập b ằng email 
và mật khẩu. Mỗi lần đăng nhập thành công t ạo ra một cặp access 
token m ới (JWT, 15 phút) và refresh token m ới (7 ngày). Cơ ch ế Token 
Rotation: refresh token cũ KHÔNG b ị xóa ngay mà đư ợc đánh d ấu đã 
sử dụng (để phát hiện token reuse attack).  
Điều kiện tiên quyết 
1. Người dùng đã có tài kho ản hợp lệ trong hệ thống. 2. Tài kho ản 
chưa bị khóa (LockoutEnabled = false ho ặc chưa đ ến lockout 
deadline).


---

<!-- PAGE 19 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 19 / 71 
Luồng chính (Happy 
Path) 
1. Client g ửi POST /api/v1/auth/login v ới body: { "email": "...", 
"password": "..." }.  
2. LoginCommand đư ợc dispatch qua MediatR.  
3. ValidationBehavior ki ểm tra email format và password không r ỗng. 
4. LoginCommandHandler tìm user: 
UserManager.FindByEmailAsync(email).  
5. Xác minh m ật khẩu: UserManager.CheckPasswordAsync(user, 
password) – so sánh v ới PBKDF2 hash.  
6. Kiểm tra tài kho ản không b ị lockout: 
UserManager.IsLockedOutAsync(user).  
7. Tạo access token m ới: JwtService.GenerateAccessToken(user, 
roles). 
8. Tạo refresh token m ới: JwtService.GenerateRefreshToken(userId). 
9. Lưu refresh token m ới vào database.  
10. Ghi nhận đăng nh ập thành công: 
UserManager.ResetAccessFailedCountAsync(user).  
11. Trả về HTTP 200 OK v ới AuthResponseDto.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – Tài khoản không t ồn tại hoặc mật khẩu sai: HTTP 401 
Unauthorized v ới message generic "Email ho ặc mật khẩu không đúng" 
(KHÔNG ti ết lộ tài khoản có t ồn tại hay không – tránh User 
Enumeration Attack).  
A2 – Tài khoản bị lockout: HTTP 423 Locked v ới thông báo th ời gian 
unlock còn l ại. 
A3 – Vượt quá s ố lần thử sai (5 lần): AccessFailedCount tăng lên, sau 
5 lần → tài kho ản bị lockout 15 phút (c ấu hình qua LockoutOptions).  
HTTP Method & 
Endpoint 
POST  /api/v1/auth/login  
Kết quả mong đợi Access token và refresh token m ới được tạo và tr ả về. Refresh token 
được lưu vào database.  
HTTP Status Code tr ả 
về 
200 OK – Đăng nhập thành công. 401 Unauthorized – Sai email/mật 
khẩu. 400 Bad Request – Dữ liệu không h ợp lệ. 423 Locked – 
Tài khoản bị khóa. 
 
FR-AUTH-003: Đăng nhập bằng Google OAuth 2.0 
Mã yêu cầu FR-AUTH-003 
Tên yêu cầu Đăng nhập / Đăng ký b ằng Google OAuth 2.0  
Nhóm chức năng  Module Xác th ực và Quản lý Ngư ời dùng (FR-AUTH) 
Tác nhân Khách (Guest) – lần đầu / Người dùng đã đăng ký trư ớc đó qua Google  
Mức ưu tiên 
(MoSCoW) 
S – Should Have 
Mô tả 
Hệ thống hỗ trợ đăng nhập qua tài kho ản Google s ử dụng OAuth 2.0 
Authorization Code Flow v ới PKCE. Nếu đây là l ần đăng nhập Google 
đầu tiên, hệ thống tự động tạo tài khoản mới từ thông tin Google profile 
(email, display name, avatar URL) và gán role "Author". N ếu email đã 
tồn tại từ đăng ký th ủ công trước đó, hệ thống liên kết Google login v ới 
tài khoản hiện có.


---

<!-- PAGE 20 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 20 / 71 
Điều kiện tiên quyết 
1. Google OAuth 2.0 Credentials (ClientId, ClientSecret) đã đư ợc cấu 
hình trong appsettings. 2. Redirect URI đã đư ợc đăng ký trong Google 
Cloud Console. 3. Ngư ời dùng có tài kho ản Google h ợp lệ. 
Luồng chính (Happy 
Path) 
1. Frontend (Next.js) redirect ngư ời dùng đ ến Google Authorization 
Endpoint v ới scopes: openid, email, profile.  
2. Người dùng xác nh ận cấp quy ền trên Google Consent Screen.  
3. Google redirect v ề callback URL (Next.js) v ới Authorization Code.  
4. Google trả kết quả về Google Popup/callback do Next.js quản lý; Frontend nhận Authorization Code (PKCE) hoặc Google ID Token theo cấu hình Google SDK. Frontend không tạo session server-side.

5. Frontend gửi Authorization Code / ID Token tới POST /api/v1/auth/google. Backend .NET 10 nhận token/code, gọi Google token verification endpoint để xác minh trực tiếp với Google Server (issuer, audience/clientId, signature, expiry, nonce nếu áp dụng), sau đó lấy Google profile cần thiết và phát hành JWT/Refresh Token của hệ thống.
6. GoogleLoginCommandHandler tìm user b ằng 
UserManager.FindByLoginAsync("Google", providerKey).  
7. Nếu chưa có tài kho ản: kiểm tra email → nếu email chưa t ồn tại thì 
tạo ApplicationUser m ới từ Google profile, gán role "Author" → 
AddLoginAsync.  
8. Nếu email đã t ồn tại (đã đăng ký th ủ công): liên k ết Google login → 
AddLoginAsync v ới tài kho ản hiện có.  
9. Tạo access token và refresh token, lưu vào database.  
10. Trả về HTTP 200 OK v ới AuthResponseDto.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – Google token không h ợp lệ hoặc hết hạn: HTTP 401 
Unauthorized. 
A2 – Email Google b ị revoke quy ền: HTTP 400 Bad Request.  
A3 – Google API không kh ả dụng: HTTP 502 Bad Gateway v ới 
message thích hợp. 
HTTP Method & 
Endpoint 
POST  /api/v1/auth/google  
Kết quả mong đợi Người dùng đư ợc đăng nh ập (hoặc tự động đăng ký), nh ận 
AuthResponseDto. Tài kho ản mới (nếu có) đư ợc tạo với role "Author".  
HTTP Status Code tr ả 
về 
200 OK – Đăng nhập/đăng ký thành công. 401 Unauthorized – Token 
Google không h ợp lệ. 400 Bad Request – Thiếu thông tin Google 
profile.  
 
FR-AUTH-004: Làm mới Access Token (Token Refresh)  
Mã yêu cầu FR-AUTH-004 
Tên yêu cầu Làm mới Access Token b ằng Refresh Token  
Nhóm chức năng  Module Xác th ực và Quản lý Ngư ời dùng (FR-AUTH) 
Tác nhân Tác giả (Author) / Quản trị viên (Admin) – có refresh token h ợp lệ 
Mức ưu tiên 
(MoSCoW) 
M – Must Have (Bắt bu ộc) 
Mô tả 
Khi access token h ết hạn (sau 15 phút), client s ử dụng refresh token 
còn hiệu lực để lấy cặp token m ới mà không c ần người dùng đăng 
nhập lại. Cơ chế Token Rotation b ắt buộc: mỗi lần refresh, refresh 
token cũ b ị vô hiệu hóa (IsRevoked = true, RevokedAt = 
DateTime.UtcNow) và m ột refresh token M ỚI được tạo ra. Đây là bi ện 
pháp ch ống Refresh Token Reuse Attack.


---

<!-- PAGE 21 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 21 / 71 
Điều kiện tiên quyết 
1. Client có refresh token h ợp lệ (chưa hết hạn, chưa b ị revoke, chưa 
bị thay thế). 2. Người dùng tương ứng vẫn còn t ồn tại trong database 
và chưa b ị khóa. 
Luồng chính (Happy 
Path) 
1. Client g ửi POST /api/v1/auth/refresh v ới body: { "refreshToken": "..." 
}. 
2. RefreshTokenCommand dispatch qua MediatR.  
3. Handler tìm refresh token trong database: bao g ồm User navigation 
property.  
4. Kiểm tra: token t ồn tại, IsRevoked == false, ExpiresAt > 
DateTime.UtcNow, user v ẫn active.  
5. Đánh dấu token cũ: IsRevoked = true, ReplacedByToken = 
newToken, RevokedAt = DateTime.UtcNow.  
6. Tạo access token m ới cho user.  
7. Tạo refresh token m ới, lưu vào database.  
8. Trả về HTTP 200 OK v ới AuthResponseDto ch ứa cặp token m ới. 
Luồng thay th ế / 
Ngoại l ệ 
A1 – Refresh token không tìm th ấy trong database: HTTP 401 
Unauthorized. 
A2 – Refresh token đã h ết hạn: HTTP 401 Unauthorized, client ph ải 
đăng nhập lại. 
A3 – Refresh token đã b ị revoke (Reuse Attack detected): HTTP 401 
Unauthorized. LOG SECURITY ALERT v ới mức WARNING. Có th ể 
kích hoạt revoke toàn b ộ refresh tokens c ủa user đó (paranoid mode).  
A4 – User bị xóa ho ặc bị khóa sau khi token đư ợc cấp: HTTP 401 
Unauthorized. 
HTTP Method & 
Endpoint 
POST  /api/v1/auth/refresh  
Kết quả mong đợi Refresh token cũ b ị invalidate. Access token m ới (15 phút) và refresh 
token m ới (7 ngày) đư ợc tạo và tr ả về. 
HTTP Status Code tr ả 
về 
200 OK – Refresh thành công. 401 Unauthorized – Token không h ợp 
lệ, hết hạn hoặc đã b ị revoke. 
 
FR-AUTH-005: Đăng xuất (Logout / Token Revocation) 
Mã yêu cầu FR-AUTH-005 
Tên yêu cầu Đăng xuất và Thu hồi Refresh Token 
Nhóm chức năng  Module Xác th ực và Quản lý Ngư ời dùng (FR-AUTH) 
Tác nhân Tác giả (Author) / Quản trị viên (Admin) đang đăng nh ập 
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Người dùng đăng xu ất khỏi hệ thống. Vì JWT access token là stateless 
(không th ể revoke trực tiếp trước khi hết hạn), hành động logout ch ủ 
yếu là revoke refresh token tương ứng trong database. Client có trách 
nhiệm xóa access token kh ỏi bộ nhớ (localStorage/cookie) phía client.  
Điều kiện tiên quyết 1. Người dùng đang đăng nh ập với access token h ợp lệ trong 
Authorization header. 2. Client g ửi refresh token mu ốn revoke. 
Luồng chính (Happy 
Path) 
1. Client g ửi POST /api/v1/auth/logout v ới Authorization: Bearer 
{accessToken} header và body: { "refreshToken": "..." }.


---

<!-- PAGE 22 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 22 / 71 
2. Middleware xác th ực JWT (UseAuthentication) xác minh access 
token. 
3. LogoutCommandHandler tìm refresh token trong database.  
4. Nếu tìm thấy và thuộc về user hiện tại: đánh d ấu IsRevoked = true, 
RevokedAt = DateTime.UtcNow.  
5. Lưu thay đ ổi vào database.  
6. Trả về HTTP 204 No Content.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – Refresh token không tìm th ấy: Vẫn trả về HTTP 204 (idempotent 
– không ti ết lộ trạng thái). 
A2 – Access token đã h ết hạn: Vẫn cho phép logout n ếu refresh token 
hợp lệ; hoặc HTTP 401 nếu không cung c ấp refresh token.  
HTTP Method & 
Endpoint 
POST  /api/v1/auth/logout  
Kết quả mong đợi Refresh token b ị đánh dấu IsRevoked = true trong database. Các l ần 
refresh tiếp theo v ới token này s ẽ thất bại. 
HTTP Status Code tr ả 
về 
204 No Content – Đăng xuất thành công (ho ặc token không t ồn tại – 
idempotent). 401 Unauthorized – Access token không h ợp lệ. 
 
FR-AUTH-006: Xem Hồ sơ Cá nhân (View Profile) 
Mã yêu cầu FR-AUTH-006 
Tên yêu cầu Xem Hồ sơ Cá nhân 
Nhóm chức năng  Module Xác th ực và Quản lý Ngư ời dùng (FR-AUTH) 
Tác nhân Tác giả (Author) / Quản trị viên (Admin) đang đăng nh ập 
Mức ưu tiên 
(MoSCoW) 
S – Should Have 
Mô tả 
Trả về thông tin h ồ sơ c ủa người dùng hi ện đang đăng nh ập, d ựa trên 
UserId đư ợc trích xuất từ JWT claims. Không bao gi ờ trả về 
PasswordHash ho ặc SecurityStamp.  
Điều kiện tiên quyết 1. Người dùng đang đăng nh ập với access token h ợp lệ. 
Luồng chính (Happy 
Path) 
1. Client g ửi GET /api/v1/auth/me v ới Authorization: Bearer 
{accessToken}. 
2. Middleware xác th ực JWT, trích xuất UserId t ừ claim NameIdentifier.  
3. GetCurrentUserQuery dispatch qua MediatR.  
4. Handler tìm user: UserManager.FindByIdAsync(userId).  
5. Map sang UserProfileDto: { id, fullName, email, userName, 
avatarUrl, roles, emailConfirmed, createdAt }.  
6. Trả về HTTP 200 OK v ới UserProfileDto.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – User đã b ị xóa khỏi database sau khi token đư ợc cấp: HTTP 404 
Not Found.  
HTTP Method & 
Endpoint 
GET  /api/v1/auth/me  
Kết quả mong đợi Trả về thông tin hồ sơ đầy đủ của người dùng (không có thông tin nh ạy 
cảm như password hash).


---

<!-- PAGE 23 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 23 / 71 
HTTP Status Code tr ả 
về 
200 OK – Thành công. 401 Unauthorized – Chưa đăng nh ập. 404 Not 
Found – User không t ồn tại. 
 
FR-AUTH-007: Cập nhật Hồ sơ Cá nhân (Update Profile) 
Mã yêu cầu FR-AUTH-007 
Tên yêu cầu Cập nhật Hồ sơ Cá nhân 
Nhóm chức năng  Module Xác th ực và Quản lý Ngư ời dùng (FR-AUTH) 
Tác nhân Tác giả (Author) / Quản trị viên (Admin) đang đăng nh ập 
Mức ưu tiên 
(MoSCoW) 
S – Should Have 
Mô tả 
Người dùng có th ể cập nhật FullName và AvatarUrl c ủa mình. Email và 
UserName không th ể thay đổi qua endpoint này (đây là quy trình riêng 
có xác nh ận OTP). Sử dụng PATCH (partial update) đ ể chỉ cập nhật 
các field đư ợc cung c ấp. 
Điều kiện tiên quyết 1. Người dùng đang đăng nh ập. 2. D ữ liệu mới phải hợp lệ (FullName 
không rỗng, AvatarUrl là URL h ợp lệ nếu cung c ấp). 
Luồng chính (Happy 
Path) 
1. Client g ửi PATCH /api/v1/auth/me v ới body: { "fullName": "...", 
"avatarUrl": "..." }.  
2. UpdateProfileCommand dispatch qua MediatR, UserId l ấy từ JWT 
claims. 
3. ValidationBehavior ki ểm tra: fullName 2 –100 ký t ự, avatarUrl là URL 
hợp lệ (nếu cung c ấp). 
4. Handler tìm user, c ập nhật FullName và/ho ặc AvatarUrl. 
5. UserManager.UpdateAsync(user).  
6. Trả về HTTP 200 OK v ới UserProfileDto đã c ập nhật. 
Luồng thay th ế / 
Ngoại l ệ 
A1 – Dữ liệu không h ợp lệ: HTTP 400 Bad Request.  
HTTP Method & 
Endpoint 
PATCH  /api/v1/auth/me  
Kết quả mong đợi Hồ sơ ngư ời dùng đư ợc cập nhật trong database. Tr ả về hồ sơ mới. 
HTTP Status Code tr ả 
về 
200 OK – Cập nhật thành công. 401 Unauthorized – Chưa đăng nh ập. 
400 Bad Request – Dữ liệu không h ợp lệ. 
 
3.2. Module Quản lý Danh mục (FR-CAT) 
Module quản lý danh mục (Category) phân loại công thức nấu ăn. Danh mục được tạo và duy 
trì bởi Admin; Author và Guest ch ỉ có quyền đọc. Mỗi danh mục có Slug duy nhất phục vụ 
URL thân thiện SEO. Danh m ục được cache với Redis (TTL 30 phút) vì thay đ ổi ít 
thường xuyên. 
 
FR-CAT-001: Xem Danh sách Danh mục 
Mã yêu cầu FR-CAT-001 
Tên yêu cầu Xem Danh sách T ất c ả Danh mục


---

<!-- PAGE 24 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 24 / 71 
Nhóm chức năng  Module Quản lý Danh m ục (FR-CAT) 
Tác nhân Tất c ả (Guest / Author / Admin)  
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Trả về danh sách t ất cả danh mục công th ức hiện có trong h ệ thống, 
kèm số lượng công th ức đã xu ất bản (Published) trong m ỗi danh mục. 
Kết quả được cache v ới Redis (TTL 30 phút) và s ắp xếp theo 
Name tăng d ần. 
Điều kiện tiên quyết 1. Ít nhất m ột danh mục tồn tại trong database (ho ặc trả về mảng rỗng). 
2. Không yêu c ầu xác thực. 
Luồng chính (Happy 
Path) 
1. Client g ửi GET /api/v1/categories.  
2. GetCategoriesQuery dispatch qua MediatR.  
3. Handler ki ểm tra Redis v ới key "categories:all".  
4. Cache hit: tr ả về dữ liệu từ cache. 
5. Cache miss: query database 
(IUnitOfWork.Categories.GetAllWithRecipeCount()), map sang 
CategoryDto[].  
6. Lưu vào Redis v ới TTL 30 phút (sliding expiration).  
7. Trả về HTTP 200 OK v ới CategoryDto[].  
Luồng thay th ế / 
Ngoại l ệ 
A1 – Không có danh m ục nào: HTTP 200 OK v ới mảng rỗng [].  
HTTP Method & 
Endpoint 
GET  /api/v1/categories  
Kết quả mong đợi Mảng CategoryDto[] v ới các field: { id, name, slug, description, 
recipeCount }. K ết quả được serve t ừ cache khi có.  
HTTP Status Code tr ả 
về 
200 OK – Thành công (k ể cả khi trống). 
 
FR-CAT-002: Xem Chi tiết Danh mục và Công thức 
Mã yêu cầu FR-CAT-002 
Tên yêu cầu Xem Chi ti ết Danh mục và Danh sách Công th ức thuộc Danh mục 
Nhóm chức năng  Module Quản lý Danh m ục (FR-CAT) 
Tác nhân Tất c ả (Guest / Author / Admin)  
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Trả về thông tin chi ti ết của một danh mục cụ thể (theo Slug) kèm danh 
sách phân trang các công th ức đã xu ất bản (Published) thu ộc danh 
mục đó. Guest ch ỉ thấy Published recipes; Author th ấy thêm Draft 
recipes c ủa chính mình trong danh m ục. 
Điều kiện tiên quyết 1. Danh mục với slug tương ứng phải tồn tại. 2. Không yêu c ầu xác 
thực. 
Luồng chính (Happy 
Path) 
1. Client g ửi GET /api/v1/categories/{slug}?page=1&pageSize=12.  
2. GetCategoryBySlugQuery dispatch qua MediatR.


---

<!-- PAGE 25 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 25 / 71 
3. Handler tìm category theo slug: 
_unitOfWork.Categories.GetBySlugAsync(slug).  
4. Query recipes thu ộc category v ới Status == Published (+ Draft c ủa 
currentUser nếu đã đăng nh ập). 
5. Apply pagination (OFFSET -based: SKIP (page -1)*pageSize TAKE 
pageSize). 
6. Map sang CategoryDetailDto kèm 
PagedResult<RecipeSummaryDto>.  
7. Trả về HTTP 200 OK.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – Slug không t ồn tại: HTTP 404 Not Found v ới RFC 7807 body.  
HTTP Method & 
Endpoint 
GET  /api/v1/categories/{slug}?page={n}&pageSize={n}  
Kết quả mong đợi { category: CategoryDto, recipes: { items: RecipeSummaryDto[], 
totalCount, page, pageSize, totalPages } }  
HTTP Status Code tr ả 
về 
200 OK – Thành công. 404 Not Found – Slug không t ồn tại. 
 
FR-CAT-003: Tạo Danh mục Mới [Admin] 
Mã yêu cầu FR-CAT-003 
Tên yêu cầu Tạo Danh m ục Công th ức Mới 
Nhóm chức năng  Module Quản lý Danh m ục (FR-CAT) 
Tác nhân Quản trị viên (Admin) 
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Admin t ạo danh m ục công th ức mới. Slug đư ợc tự động sinh t ừ Name 
(slugify: chuy ển sang chữ thường, bỏ dấu, thay kho ảng trắng bằng "-"). 
Nếu Slug đã t ồn tại, hệ thống thêm suffix s ố (e.g., "mon -chinh-2"). Sau 
khi tạo, cache danh m ục (Redis key "categories:all") b ị 
invalidate. 
Điều kiện tiên quyết 1. Người dùng đang đăng nh ập với role Admin. 2. Name chưa t ồn tại 
trong database.  
Luồng chính (Happy 
Path) 
1. Admin g ửi POST /api/v1/categories v ới Authorization: Bearer 
{adminJwt} và body: { "name": "...", "description": "..." }.  
2. RequireAuthorization("Admin") middleware ki ểm tra role. 
3. CreateCategoryCommand dispatch qua MediatR.  
4. ValidationBehavior: name 2 –50 ký t ự, không ch ứa HTML. 
5. SlugHelper.Generate(name) t ạo slug.  
6. Kiểm tra slug chưa t ồn tại. Nếu trùng, thêm " -2", "-3",... cho đ ến khi 
unique. 
7. Category.Create(name, slug, description) t ạo entity.  
8. _unitOfWork.Categories.AddAsync(entity).  
9. _unitOfWork.SaveChangesAsync().  
10. Redis key "categories:all" được invalidate sau mutation.  
11. Trả về HTTP 201 Created v ới CategoryDto và Location header.


---

<!-- PAGE 26 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 26 / 71 
Luồng thay th ế / 
Ngoại l ệ 
A1 – Thiếu role Admin: HTTP 403 Forbidden.  
A2 – Dữ liệu không h ợp lệ: HTTP 400. 
HTTP Method & 
Endpoint 
POST  /api/v1/categories  
Kết quả mong đợi Danh mục mới được tạo trong database. Cache danh m ục bị xóa. 
Location header tr ỏ đến /api/v1/categories/{newSlug}.  
HTTP Status Code tr ả 
về 
201 Created – Tạo thành công. 403 Forbidden – Không có quy ền 
Admin. 409 Conflict – Name đã tồn tại. 400 Bad Request – Dữ 
liệu không h ợp lệ. 
 
FR-CAT-004: Cập nhật Danh mục [Admin] 
Mã yêu cầu FR-CAT-004 
Tên yêu cầu Cập nhật Thông tin Danh m ục 
Nhóm chức năng  Module Quản lý Danh m ục (FR-CAT) 
Tác nhân Quản trị viên (Admin) 
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Admin c ập nhật Name và/ho ặc Description c ủa danh mục. Slug 
KHÔNG thay đ ổi khi đ ổi tên (để tránh broken links). Sau khi c ập nhật, 
cache bị invalidate. 
Điều kiện tiên quyết 1. Admin đang đăng nh ập. 2. Danh m ục với ID tương ứng tồn tại. 
Luồng chính (Happy 
Path) 
1. Admin g ửi PUT /api/v1/categories/{id} v ới body: { "name": "...", 
"description": "..." }.  
2. Kiểm tra role Admin.  
3. UpdateCategoryCommand dispatch qua MediatR.  
4. Tìm category theo ID, c ập nhật Name và Description.  
5. Lưu thay đ ổi, invalidate cache.  
6. Trả về HTTP 200 OK v ới CategoryDto đã c ập nhật. 
Luồng thay th ế / 
Ngoại l ệ 
A1 – ID không t ồn tại: HTTP 404. 
A2 – Thiếu role Admin: HTTP 403.  
HTTP Method & 
Endpoint 
PUT  /api/v1/categories/{id:guid}  
Kết quả mong đợi Thông tin danh m ục đư ợc cập nhật. Cache invalidated.  
HTTP Status Code tr ả 
về 
200 OK – Cập nhật thành công. 403 Forbidden. 404 Not Found. 422 
Unprocessable Entity.  
 
FR-CAT-005: Xóa Danh mục [Admin] 
Mã yêu cầu FR-CAT-005 
Tên yêu cầu Xóa Danh m ục 
Nhóm chức năng  Module Quản lý Danh m ục (FR-CAT) 
Tác nhân Quản trị viên (Admin)


---

<!-- PAGE 27 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 27 / 71 
Mức ưu tiên 
(MoSCoW) 
S – Should Have 
Mô tả 
Admin xóa m ột danh mục. Quy t ắc nghiệp vụ: KHÔNG được xóa danh 
mục còn ch ứa công th ức (dù là Published hay Draft). Admin ph ải 
chuyển tất cả công thức sang danh m ục khác trư ớc khi xóa. Đây là soft 
constraint đ ể bảo vệ toàn vẹn dữ liệu. 
Điều kiện tiên quyết 1. Admin đang đăng nh ập. 2. Danh m ục tồn tại và không còn công th ức 
nào. 
Luồng chính (Happy 
Path) 
1. Admin g ửi DELETE /api/v1/categories/{id}.  
2. Kiểm tra role Admin.  
3. DeleteCategoryCommand dispatch.  
4. Đếm s ố recipe trong category: n ếu > 0 → Throw 
ConflictException("Danh m ục còn ch ứa {count} công th ức."). 
5. Xóa entity, lưu thay đ ổi, invalidate cache.  
6. Trả về HTTP 204 No Content.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – Danh mục có recipe: HTTP 409 Conflict v ới thông báo s ố lượng 
recipe. 
A2 – ID không t ồn tại: HTTP 404. 
HTTP Method & 
Endpoint 
DELETE  /api/v1/categories/{id:guid}  
Kết quả mong đợi Danh mục bị xóa khỏi database. HTTP 204 đư ợc trả về. 
HTTP Status Code tr ả 
về 
204 No Content – Xóa thành công. 403 Forbidden. 404 Not Found. 409 
Conflict – Danh mục còn recipe.  
 
3.3. Module Quản lý Công thức Nấu ăn (FR-RCP) 
Module cốt lõi c ủa hệ thống. Recipe là aggregate root ch ứa các child entity: RecipeStep, 
RecipeIngredient, RecipeImage và Owned Entity RecipeNutrition. T ất c ả mutation 
(Create/Update/Delete) đi qua UnitOfWork đ ể đảm b ảo tính nh ất quán transaction. 
Concurrency được xử lý qua RowVersion (Timestamp) để phát hiện lost update khi hai Author 
cùng sửa một recipe. 
 
FR-RCP-001: Xem Danh sách Công thức (Paginated + Filtered + Sorted) 
Mã yêu 
cầu 
FR-RCP-001 
Tên yêu 
cầu 
Xem Danh sách Công th ức Nấu ăn với Phân trang, Lọc và S ắp xếp 
Nhóm 
chức 
năng 
Module Quản lý Công th ức Nấu ăn (FR-RCP) 
Tác nhân Tất c ả (Guest / Author / Admin)  
Mức ưu 
tiên 
(MoSCoW) 
M – Must Have


---

<!-- PAGE 28 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 28 / 71 
Mô tả 
Trả về danh sách phân trang các công th ức. Guest và Author khác ch ỉ thấy Status == Published. Author th ấy 
thêm Draft/Archived c ủa chính mình. Admin th ấy tất cả trạng thái. Hỗ trợ lọc theo CategoryId, DifficultyLevel, 
thời gian nấu; sắp xếp theo createdAt, title, cookTime. K ết quả được cache v ới Output Cache (.NET 10) theo 
policy "RecipeList" (TTL 2 phút, vary by query string).  
Điều kiện 
tiên quyết 
1. Không yêu c ầu xác thực (endpoint public cho Published recipes). 2. Tham s ố page >= 1, pageSize trong [1, 
50]. 
Luồng 
chính 
(Happy 
Path) 
1. Client g ửi GET 
/api/v1/recipes?page=1&pageSize=12&categoryId={guid}&difficulty=Easy&maxCookTime=30&sortBy=createdAt&sortOrder=desc. 
2. GetRecipesQuery dispatch qua MediatR.  
3. Handler xây d ựng IQueryable v ới filters t ừ query params.  
4. Áp d ụng Authorization filter: n ếu Guest → chỉ Published; n ếu Author → Published OR (Draft AND AuthorId 
== userId); nếu Admin → t ất cả. 
5. Apply sorting: sortBy=createdAt&sortOrder=desc → ORDER BY CreatedAt DESC; sortBy=title&sortOrder=asc → ORDER BY Title ASC.  
6. COUNT total trư ớc khi pagination.  
7. Apply OFFSET -LIMIT pagination.  
8. Map sang PagedResult<RecipeSummaryDto>.  
9. Trả về HTTP 200 OK. Redis lưu response theo key = {path}?{queryString} với TTL 1 phút.  
Luồng 
thay thế / 
Ngoại l ệ 
A1 – page ho ặc pageSize không h ợp lệ: HTTP 400. A2 – categoryId không t ồn tại: HTTP 200 với items rỗng 
(không throw 404). 
HTTP 
Method & 
Endpoint 
GET  
/api/v1/recipes?page={n}&pageSize={n}&categoryId={guid}&difficulty={level}&maxCookTime={min}&sortBy={field}&sortOrder={asc|desc}  
Kết quả 
mong đợi 
PagedResult<RecipeSummaryDto>: { items[], totalCount, page, pageSize, totalPages, hasNextPage, 
hasPreviousPage }.  
HTTP 
Status 
Code trả 
về 
200 OK – Thành công (k ể cả items rỗng). 400 Bad Request – Tham số không hợp lệ. 
 
FR-RCP-002: Xem Chi tiết Công thức 
Mã yêu 
cầu 
FR-RCP-002 
Tên yêu 
cầu 
Xem Chi ti ết Công th ức Nấu ăn 
Nhóm 
chức 
năng 
Module Quản lý Công th ức Nấu ăn (FR-RCP) 
Tác nhân Tất c ả (Guest / Author / Admin)  
Mức ưu 
tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Trả về toàn b ộ thông tin chi ti ết của một công th ức cụ thể, bao g ồm: thông tin cơ b ản, danh sách nguyên li ệu 
(RecipeIngredient[]) s ắp xếp theo SortOrder, các bư ớc thực hiện (RecipeStep[]) s ắp xếp theo StepNumber, ảnh 
minh họa (RecipeImage[]), thông tin dinh dư ỡng (RecipeNutrition), thông tin danh m ục và tác gi ả. Recipe Draft 
chỉ được xem b ởi tác gi ả sở hữu hoặc Admin. Endpoint đư ợc cache v ới Redis cache key "recipe:{slug}" với TTL 5 phút (cache-aside pattern); invalidate khi Recipe Create/Update/Delete.


---

<!-- PAGE 29 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 29 / 71 
Điều kiện 
tiên quyết 
1. Recipe v ới slug tương ứng tồn tại. 2. Nếu Recipe ở trạng thái Draft/Archived: ngư ời yêu cầu phải là tác gi ả 
hoặc Admin.  
Luồng 
chính 
(Happy 
Path) 
1. Client g ửi GET /api/v1/recipes/{slug}.  
2. GetRecipeBySlugQuery dispatch qua MediatR.  
3. Handler query Recipe v ới Eager Loading: 
Include(Steps).Include(Ingredients).Include(Images).Include(Category).Include(Author).IncludeOwned(Nutrition).  
4. Kiểm tra null → NotFoundException n ếu không tìm th ấy. 
5. Kiểm tra Status: n ếu Draft/Archived → ch ỉ tác gi ả hoặc Admin m ới được xem (Authorization check).  
6. Map sang RecipeDetailDto (bao g ồm tất cả nested collections).  
7. Trả về HTTP 200 OK. Tag output cache entry v ới ["recipes", $"recipe:{slug}"].  
Luồng 
thay thế / 
Ngoại l ệ 
A1 – Slug không t ồn tại: HTTP 404 Not Found.  
A2 – Recipe Draft/Archived, ngư ời dùng không có quy ền: HTTP 403 Forbidden. 
HTTP 
Method & 
Endpoint 
GET  /api/v1/recipes/{slug}  
Kết quả 
mong đợi 
RecipeDetailDto đ ầy đủ gồm tất cả nested data (steps, ingredients, images, nutrition, category, author).  
HTTP 
Status 
Code trả 
về 
200 OK – Thành công. 403 Forbidden – Không có quy ền xem Draft. 404 Not Found – Slug không t ồn tại. 
 
FR-RCP-003: Tạo Công thức Nấu ăn Mới [Author/Admin] 
Mã yêu cầu FR-RCP-003 
Tên yêu cầu Tạo Công th ức Nấu ăn Mới 
Nhóm chức năng  Module Quản lý Công th ức Nấu ăn (FR-RCP) 
Tác nhân Tác giả (Author) / Quản trị viên (Admin) 
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Author hoặc Admin t ạo mới một công th ức nấu ăn. Trạng thái ban đ ầu 
luôn là Draft (chưa công khai). Slug đư ợc tự động sinh t ừ Title. Steps 
và Ingredients có th ể được tạo cùng lúc (trong cùng request) ho ặc 
thêm riêng l ẻ sau qua FR-RCP-009/010. 
Điều kiện tiên quyết 1. Người dùng đang đăng nh ập với role Author ho ặc Admin. 2. 
CategoryId tham chi ếu đ ến danh mục đã t ồn tại. 
Luồng chính (Happy 
Path) 
1. Author g ửi POST /api/v1/recipes v ới body: { title, description, 
categoryId, prepTimeMinutes, cookTimeMinutes, servings, difficulty, 
instructions?, nutrition?: {...}, steps?: [...], ingredients?: [...] }.  
2. Kiểm tra xác th ực (RequireAuthorization). 
3. CreateRecipeCommand dispatch.  
4. ValidationBehavior: title 5 –200 ký t ự, prepTime/cookTime/servings > 
0, categoryId valid Guid.  
5. SlugHelper.Generate(title), ki ểm tra slug unique.  
6. Recipe.Create(title, description, categoryId, authorId, prepTime, 
cookTime, servings, difficulty).


---

<!-- PAGE 30 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 30 / 71 
7. Nếu có steps: thêm t ừng RecipeStep.Create() vào recipe.Steps.  
8. Nếu có ingredients: thêm t ừng RecipeIngredient.Create() vào 
recipe.Ingredients.  
9. Nếu có nutrition: recipe.SetNutrition(calories, protein, carbs, fat).  
10. _unitOfWork.Recipes.AddAsync(recipe), SaveChangesAsync().  
11. Invalidate Redis keys liên quan đến recipe list/search và recipe detail.  
12. Trả về HTTP 201 Created v ới RecipeDto.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – Không có quy ền Author/Admin: HTTP 401/403. 
A2 – CategoryId không t ồn tại: HTTP 400 với lỗi "Category không h ợp 
lệ.". 
A3 – Slug đã t ồn tại (title trùng): HTTP 409 Conflict.  
HTTP Method & 
Endpoint 
POST  /api/v1/recipes  
Kết quả mong đợi Recipe mới được tạo với Status = Draft, Slug đư ợc sinh t ự động. 
Cache "recipes" b ị invalidate. 
HTTP Status Code tr ả 
về 
201 Created – Tạo thành công. 401/403 – Chưa đăng nh ập / Không có 
quyền. 409 Conflict – Slug đã t ồn tại. 422 Unprocessable Entity – Dữ 
liệu không h ợp lệ. 
 
FR-RCP-004: Cập nhật Công thức [Author-Owner/Admin] 
Mã yêu cầu FR-RCP-004 
Tên yêu cầu Cập nhật Thông tin Công th ức Nấu ăn 
Nhóm chức năng  Module Quản lý Công th ức Nấu ăn (FR-RCP) 
Tác nhân Tác giả sở hữu (Author – Owner) / Quản trị viên (Admin) 
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Cập nhật thông tin c ủa một công th ức. Resource-Based Authorization 
được áp d ụng: chỉ Author sở hữu recipe (AuthorId == currentUserId) 
hoặc Admin đư ợc phép. Concurrency control qua RowVersion (ETag 
pattern): client ph ải gửi RowVersion hi ện tại trong If -Match header; nếu 
mismatch → conflict.  
Điều kiện tiên quyết 
1. Author/Admin đang đăng nh ập. 2. Recipe v ới ID tương ứng tồn tại. 
3. Client cung c ấp RowVersion h ợp lệ trong If -Match header (hoặc 
trong request body).  
Luồng chính (Happy 
Path) 
1. Author g ửi PUT /api/v1/recipes/{id} v ới body: { title, description, 
categoryId, prepTime, cookTime, servings, difficulty, instructions, 
nutrition? }.  
2. Kiểm tra xác th ực. 
3. UpdateRecipeCommand dispatch.  
4. Lấy recipe t ừ database theo ID.  
5. IAuthorizationService.AuthorizeAsync(user, recipe, 
Operations.Update) – kiểm tra resource-based auth.  
6. Kiểm tra RowVersion: DbContext s ẽ ném 
DbUpdateConcurrencyException n ếu RowVersion mismatch.  
7. Update các field c ủa recipe entity qua domain method 
recipe.Update(...).


---

<!-- PAGE 31 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 31 / 71 
8. Cập nhật Nutrition n ếu có.  
9. SaveChangesAsync() – nếu RowVersion mismatch t ại đây → ném 
ConcurrencyException → HTTP 409.  
10. Invalidate cache: EvictByTagAsync("recipes"), 
EvictByTagAsync($"recipe:{slug}").  
11. Trả về HTTP 200 OK v ới RecipeDto đã c ập nhật. 
Luồng thay th ế / 
Ngoại l ệ 
A1 – Không ph ải owner (Author khác): HTTP 403 Forbidden.  
A2 – Concurrency conflict (RowVersion mismatch): HTTP 409 Conflict 
– "Dữ liệu đã b ị thay đổi bởi người dùng khác."  
A3 – ID không t ồn tại: HTTP 404. 
HTTP Method & 
Endpoint 
PUT  /api/v1/recipes/{id:guid}  
Kết quả mong đợi Recipe đư ợc cập nhật, cache b ị invalidate, tr ả về RecipeDto m ới nhất. 
HTTP Status Code tr ả 
về 
200 OK. 403 Forbidden – Không ph ải owner. 404 Not Found. 409 
Conflict – Concurrency hoặc slug trùng. 400 Bad Request.  
 
FR-RCP-005: Xuất bản / Hủy Xuất bản Công thức 
Mã yêu cầu FR-RCP-005 
Tên yêu cầu Xuất bản (Publish) / Hủy Xuất bản (Unpublish) Công th ức 
Nhóm chức năng  Module Quản lý Công th ức Nấu ăn (FR-RCP) 
Tác nhân Tác giả sở hữu (Author – Owner) / Quản trị viên (Admin) 
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Thay đổi trạng thái công th ức: Draft → Published (xu ất bản) hoặc 
Published → Draft (h ủy xuất bản). Business rule: KHÔNG th ể publish 
nếu recipe không có ít nh ất 1 ingredient và ít nh ất 1 bước thực hiện (Ingredients.Count >= 1 AND Steps.Count >= 1). Khi 
publish, Recipe tr ở nên công khai và đư ợc đưa vào index tìm ki ếm. 
Điều kiện tiên quyết 1. Recipe t ồn tại, người dùng là owner ho ặc Admin. 2. Đ ể publish: 
recipe phải có ít nh ất 1 RecipeIngredient và ít nh ất 1 RecipeStep.  
Luồng chính (Happy 
Path) 
1. Author g ửi PATCH /api/v1/recipes/{id}/publish (đ ể xuất bản) hoặc 
PATCH /api/v1/recipes/{id}/unpublish.  
2. PublishRecipeCommand dispatch v ới isPublish = true/false.  
3. Kiểm tra resource-based authorization.  
4. Gọi domain method: recipe.Publish() ho ặc recipe.Unpublish().  
5. recipe.Publish() ki ểm tra: Ingredients.Count == 0 OR Steps.Count == 0 → Throw 
DomainException("Recipe ph ải có ít nh ất 1 ingredient và 1 bư ớc thực hiện."). 
6. Set Status = Published/Draft, UpdatedAt = DateTime.UtcNow.  
7. SaveChangesAsync(), invalidate cache.  
8. Trả về HTTP 200 OK v ới RecipeDto.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – Recipe thiếu ingredient hoặc bư ớc thực hiện: HTTP 400 với 
DomainException message.  
A2 – Recipe đã ở trạng thái mong mu ốn: Idempotent, tr ả về HTTP 200 
OK.


---

<!-- PAGE 32 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 32 / 71 
HTTP Method & 
Endpoint 
PATCH  /api/v1/recipes/{id:guid}/publish  |  PATCH  
/api/v1/recipes/{id:guid}/unpublish  
Kết quả mong đợi Status recipe đư ợc thay đ ổi thành Published ho ặc Draft. Cache b ị 
invalidate. 
HTTP Status Code tr ả 
về 
200 OK – Thành công. 403 Forbidden. 404 Not Found. 400 
Unprocessable Entity – Thiếu steps.  
 
FR-RCP-006: Lưu trữ Công thức (Archive) 
Mã yêu cầu FR-RCP-006 
Tên yêu cầu Lưu trữ Công thức (Archive / Unarchive) 
Nhóm chức năng  Module Quản lý Công th ức Nấu ăn (FR-RCP) 
Tác nhân Tác giả sở hữu / Quản trị viên (Admin) 
Mức ưu tiên 
(MoSCoW) 
S – Should Have 
Mô tả 
Chuyển Recipe sang tr ạng thái Archived. Recipe Archived không hi ển 
thị trong danh sách công khai nhưng không b ị xóa khỏi database (soft 
hide). Hữu ích đ ể ẩn recipe cũ không còn phù h ợp mà không m ất dữ 
liệu. 
Điều kiện tiên quyết 1. Recipe t ồn tại, người dùng có quy ền. 
Luồng chính (Happy 
Path) 
1. Author g ửi PATCH /api/v1/recipes/{id}/archive.  
2. Kiểm tra authorization.  
3. recipe.Archive() → Status = Archived.  
4. SaveChangesAsync(), invalidate cache.  
5. HTTP 200 OK.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – ID không t ồn tại: HTTP 404. A2 – Không có quy ền: HTTP 403. 
HTTP Method & 
Endpoint 
PATCH  /api/v1/recipes/{id:guid}/archive  
Kết quả mong đợi Status = Archived. Recipe không còn xu ất hiện trong public listing.  
HTTP Status Code tr ả 
về 
200 OK. 403 Forbidden. 404 Not Found.  
 
FR-RCP-007: Xóa Công thức [Author-Owner/Admin] 
Mã yêu cầu FR-RCP-007 
Tên yêu cầu Xóa Vĩnh vi ễn Công thức Nấu ăn 
Nhóm chức năng  Module Quản lý Công th ức Nấu ăn (FR-RCP) 
Tác nhân Tác giả sở hữu (Author – Owner) / Quản trị viên (Admin) 
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả Xóa vĩnh viễn một công th ức và t ất cả dữ liệu liên quan (cascade 
delete: Steps, Ingredients, Images). Các file ảnh trên MinIO đư ợc xóa


---

<!-- PAGE 33 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 33 / 71 
bất đồng bộ qua Hangfire fire -and-forget job đ ể tránh blocking HTTP 
response. Đây là hard delete (không dùng soft delete pattern cho 
recipe). 
Điều kiện tiên quyết 1. Recipe t ồn tại. 2. Ngư ời dùng là owner ho ặc Admin.  
Luồng chính (Happy 
Path) 
1. Author/Admin g ửi DELETE /api/v1/recipes/{id}.  
2. Kiểm tra xác th ực và resource -based authorization.  
3. Lấy danh sách URL ảnh từ recipe.Images.  
4. _unitOfWork.Recipes.Remove(recipe), SaveChangesAsync() – 
cascade delete Steps, Ingredients, Images trong database.  
5. Với mỗi imageUrl: 
BackgroundJob.Enqueue<IFileStorageService>(svc => 
svc.DeleteAsync(url)) – xóa ảnh trên MinIO b ất đồng bộ. 
6. EvictByTagAsync("recipes"), 
EvictByTagAsync($"recipe:{recipe.Slug}") – invalidate cache.  
7. Trả về HTTP 204 No Content.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – ID không t ồn tại: HTTP 404. 
A2 – Không ph ải owner: HTTP 403.  
A3 – Xóa MinIO file th ất bại (job retry): Hangfire t ự động retry 3 lần. 
Nếu vẫn fail, log error nhưng không ảnh hưởng response đã tr ả về. 
HTTP Method & 
Endpoint 
DELETE  /api/v1/recipes/{id:guid}  
Kết quả mong đợi Recipe được đánh dấu IsDeleted=true; child entities không bị xóa vật lý. Ảnh trên MinIO 
được lên lịch xóa qua Hangfire.  
HTTP Status Code tr ả 
về 
204 No Content – Xóa thành công. 403 Forbidden. 404 Not Found.  
 
FR-RCP-008: Quản lý Ảnh Công thức (Upload / Set Primary / Delete) 
Mã yêu cầu FR-RCP-008 
Tên yêu cầu Upload Ảnh, Đặt Ảnh Chính, Xóa Ảnh Công thức 
Nhóm chức năng  Module Quản lý Công th ức Nấu ăn (FR-RCP) 
Tác nhân Tác giả sở hữu / Quản trị viên (Admin) 
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Author quản lý ảnh minh họa cho công th ức của mình. Upload dùng 
multipart/form -data. Ảnh được lưu trên MinIO với path: 
recipes/{recipeId}/{uuid}.{ext}. Ảnh đầu tiên tự động đư ợc đặt làm ảnh 
chính (IsPrimary = true). H ỗ trợ 3 thao tác: Upload (POST), đ ặt ảnh 
chính (PATCH primary), Xóa (DELETE). Validation b ắt buộc: MIME 
type (image/jpeg, image/png, image/webp, image/avif) và kích thư ớc tối 
đa 5MB. 
Điều kiện tiên quyết 1. Author/Admin đang đăng nh ập. 2. Recipe t ồn tại và ngư ời dùng có 
quyền. 
Luồng chính (Happy 
Path) 
--- UPLOAD --- 
1. POST /api/v1/recipes/{id}/images v ới multipart/form -data chứa field 
"file".


---

<!-- PAGE 34 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 34 / 71 
2. Validate MIME type: ch ỉ chấp nhận image/jpeg, image/png, 
image/webp, image/avif.  
3. Validate kích thư ớc: file.Length <= 5*1024*1024 bytes (5MB).  
4. Validate magic bytes: đ ọc 4 bytes đ ầu để xác nhận định dạng thực 
sự (JPEG: FF D8 FF; PNG: 89 50 4E 47).  
5. IFileStorageService.UploadAsync(file, "recipes/{id}") → tr ả về URL 
công khai.  
6. RecipeImage.Create(url, altText, isPrimary: !recipe.Images.Any()) → 
thêm vào recipe.  
7. SaveChangesAsync(), invalidate cache.  
8. HTTP 201 Created v ới { url, isPrimary }.  
 
--- SET PRIMARY IMAGE --- 
9. PATCH /api/v1/recipes/{id}/images/{imageId}/primary.  
10. Tìm image theo imageId, đ ặt IsPrimary = true, đ ặt tất cả ảnh khác 
IsPrimary = false.  
11. HTTP 200 OK.  
 
--- DELETE IMAGE --- 
12. DELETE /api/v1/recipes/{id}/images/{imageId}.  
13. Xóa entity kh ỏi database.  
14. BackgroundJob.Enqueue xóa file trên MinIO.  
15. Nếu ảnh bị xóa là IsPrimary và còn ảnh khác: tự động đặt ảnh đầu 
tiên còn l ại làm primary.  
16. HTTP 204 No Content.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – MIME type không h ợp lệ: HTTP 400 Bad Request. 
A2 – File vượt quá 5MB: HTTP 400 v ới message "Kích thư ớc file vư ợt 
quá giới hạn 5MB.". 
A3 – Magic bytes không kh ớp MIME type: HTTP 400 "File không h ợp 
lệ.". 
A4 – MinIO không kh ả dụng: HTTP 503 Service Unavailable.  
HTTP Method & 
Endpoint 
POST /api/v1/recipes/{id}/images  |  PATCH 
/api/v1/recipes/{id}/images/{imgId}/primary  |  DELETE 
/api/v1/recipes/{id}/images/{imgId}  
Kết quả mong đợi Ảnh được upload lên MinIO, URL lưu vào database. IsPrimary đư ợc 
quản lý chính xác.  
HTTP Status Code tr ả 
về 
Upload: 201 Created. Set Primary: 200 OK. Delete: 204 No Content. 
400 Bad Request – File không h ợp lệ. 403/404 – Lỗi quyền/không tìm 
thấy. 
 
FR-RCP-009: Quản lý Nguyên liệu (CRUD RecipeIngredient) 
Mã yêu cầu FR-RCP-009 
Tên yêu cầu Thêm / Cập nhật / Xóa Nguyên li ệu Công thức 
Nhóm chức năng  Module Quản lý Công th ức Nấu ăn (FR-RCP) 
Tác nhân Tác giả sở hữu / Quản trị viên (Admin)


---

<!-- PAGE 35 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 35 / 71 
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Author quản lý danh sách nguyên li ệu (RecipeIngredient) c ủa công 
thức. Mỗi nguyên liệu có: Name (tên), Quantity (s ố lượng), Unit (đơn v ị: 
gram/ml/muỗng/cái/c ủ...), Notes (ghi chú tu ỳ chọn), SortOrder (thứ tự 
hiển thị). Endpoint h ỗ trợ thêm mới (POST), cập nhật (PUT), xóa 
(DELETE) từng nguyên li ệu riêng l ẻ. 
Điều kiện tiên quyết 1. Recipe t ồn tại và ngư ời dùng có quy ền. 2. Quantity > 0, Unit không 
rỗng, Name 1–100 ký t ự. 
Luồng chính (Happy 
Path) 
--- THÊM NGUYÊN LIỆU --- 
1. POST /api/v1/recipes/{id}/ingredients v ới body: { name, quantity, unit, 
notes?, sortOrder? }.  
2. Validate, t ạo RecipeIngredient.Create(recipeId, name, qty, unit, 
notes, sortOrder).  
3. _unitOfWork.Recipes (qua navigation) thêm ingredient, 
SaveChangesAsync().  
4. HTTP 201 Created.  
 
--- CẬP NHẬT NGUYÊN LI ỆU --- 
5. PUT /api/v1/recipes/{id}/ingredients/{ingId} v ới body fields c ần cập 
nhật. 
6. Tìm ingredient, c ập nhật, SaveChangesAsync(). HTTP 200 OK.  
 
--- XÓA NGUYÊN LI ỆU --- 
7. DELETE /api/v1/recipes/{id}/ingredients/{ingId}.  
8. Xóa entity, SaveChangesAsync(). HTTP 204 No Content.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – Recipe/Ingredient không t ồn tại: HTTP 404. A2 – Không có quy ền: 
HTTP 403. A3 – Dữ liệu không h ợp lệ: HTTP 400. 
HTTP Method & 
Endpoint 
POST/PUT/DELETE  /api/v1/recipes/{id}/ingredients/{ingId?}  
Kết quả mong đợi Danh sách nguyên li ệu được cập nhật chính xác. Cache b ị invalidate. 
HTTP Status Code tr ả 
về 
201/200/204 – Thành công. 403/404/400 – Lỗi tương ứng. 
 
FR-RCP-010: Quản lý Các bước Thực hiện (CRUD RecipeStep) 
Mã yêu cầu FR-RCP-010 
Tên yêu cầu Thêm / Cập nhật / Xóa Bư ớc Thực hiện Công thức 
Nhóm chức năng  Module Quản lý Công th ức Nấu ăn (FR-RCP) 
Tác nhân Tác giả sở hữu / Quản trị viên (Admin) 
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Author quản lý các bư ớc thực hiện (RecipeStep) c ủa công th ức. Mọi POST/PUT/DELETE child phải gửi RowVersion hiện tại của Recipe (If-Match hoặc body) và được commit cùng transaction của Recipe aggregate. Mỗi 
bước có: StepNumber (th ứ tự, tự động tăng), Description (mô t ả bước), 
DurationMinutes (th ời gian ước tính cho bư ớc, tùy ch ọn), ImageUrl 
(ảnh minh h ọa cho bư ớc riêng, tùy ch ọn). Khi xóa m ột bư ớc, hệ thống


---

<!-- PAGE 36 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2 
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 36 / 71 
tự động renumber các bư ớc còn l ại để đảm bảo StepNumber liên t ục 
(1, 2, 3...).  
Điều kiện tiên quyết 1. Recipe t ồn tại, người dùng có quy ền. 2. Description không r ỗng, tối 
đa 2000 ký t ự. 
Luồng chính (Happy 
Path) 
1. POST /api/v1/recipes/{id}/steps v ới body: { description, 
durationMinutes?, imageUrl? }.  
2. StepNumber = recipe.Steps.Max(s => s.StepNumber) + 1 (ho ặc 1 
nếu chưa có bư ớc nào). 
3. RecipeStep.Create(recipeId, stepNumber, description, 
durationMinutes). 
4. SaveChangesAsync(). HTTP 201 Created.  
 
--- XÓA BƯỚC --- 
5. DELETE /api/v1/recipes/{id}/steps/{stepId}.  
6. Xóa step, sau đó renumber: c ập nhật StepNumber c ủa tất cả steps 
còn lại theo thứ tự. 
7. SaveChangesAsync(). HTTP 204 No Content.  
Luồng thay th ế / 
Ngoại l ệ 
A1 – Recipe không t ồn tại: HTTP 404. A2 – Không có quy ền: HTTP 
403. 
HTTP Method & 
Endpoint 
POST/PUT/DELETE  /api/v1/recipes/{id}/steps/{stepId?}  
Kết quả mong đợi Danh sách steps đư ợc cập nhật với StepNumber liên t ục. Cache b ị 
invalidate. 
HTTP Status Code tr ả 
về 
201/200/204 – Thành công. 403/404/400 – Lỗi. 
 
3.4. Module Tìm kiếm và Phân trang (FR-SRCH) 
 
FR-SRCH-001: Tìm kiếm Toàn văn bản (Full-Text Search) 
Mã yêu cầu FR-SRCH-001 
Tên yêu cầu Tìm kiếm Toàn văn b ản Công thức (Full-Text Search) 
Nhóm chức năng  Module Tìm ki ếm và Phân trang (FR -SRCH) 
Tác nhân Tất c ả (Guest / Author / Admin)  
Mức ưu tiên 
(MoSCoW) 
M – Must Have 
Mô tả 
Hệ thống cung c ấp tính năng tìm ki ếm toàn văn b ản (FTS) cho công 
thức sử dụng PostgreSQL tsvector/tsquery v ới cấu hình tiếng Việt. 
Trường SearchVector (computed column) đư ợc tự động cập nhật bởi 
PostgreSQL trigger khi Title ho ặc Description thay đ ổi. Kết quả được 
xếp hạng bởi ts_rank(). Hỗ trợ tìm kiếm gần đúng v ới unaccent 
extension (b ỏ dấu tiếng Việt: "pho" tìm đư ợc "ph ở"). 
Điều kiện tiên quyết 
1. PostgreSQL extensions unaccent và pg_trgm đã đư ợc install. 2. GIN 
index trên c ột SearchVector đã đư ợc tạo. 3. Tham s ố q không r ỗng, tối 
thiểu 2 ký t ự.


---

<!-- PAGE 37 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2  
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 37 / 78

FR-SRCH-001: Tìm kiếm Toàn văn bản (Full-Text Search)

| Thuộc tính | Nội dung |
|---|---|
| Mã yêu cầu | FR-SRCH-001 |
| Tên yêu cầu | Tìm kiếm Toàn văn bản Công thức (PostgreSQL Full-Text Search) |
| Nhóm chức năng | Module Tìm kiếm và Phân trang (FR-SRCH) |
| Tác nhân | Guest / Author / Admin |
| Mức ưu tiên | M – Must Have |

**Mô tả**  
Hệ thống cung cấp tìm kiếm toàn văn bản trên Recipe bằng PostgreSQL `tsvector/tsquery`. Cột `SearchVector` được duy trì tự động từ `Title` và `Description`; PostgreSQL `unaccent` được sử dụng để hỗ trợ tìm kiếm tiếng Việt không dấu. GIN index trên `SearchVector` phục vụ truy vấn nhanh. Kết quả được xếp hạng bằng `ts_rank()` và chỉ trả về Recipe `Published` đối với public search. Search term được chuẩn hóa, tokenize và tạo prefix query để hỗ trợ các truy vấn như `pho bo` tìm được `phở bò`.

**Tiền điều kiện**  
1. PostgreSQL 16 đang hoạt động.
2. Extensions `unaccent` và `pg_trgm` đã được cài đặt.
3. Cột `Recipes.SearchVector` và GIN index `IDX_Recipe_Search` đã tồn tại.
4. Trigger cập nhật `SearchVector` khi `Title` hoặc `Description` thay đổi.
5. Query `q` có tối thiểu 2 ký tự sau khi trim.

**Luồng chính**  
1. Client gửi `GET /api/v1/recipes/search?q=pho+bo&page=1&pageSize=10`.
2. `SearchRecipesQuery` được dispatch qua MediatR.
3. `ValidationBehavior` kiểm tra `q`, `page`, `pageSize`.
4. Handler chuẩn hóa chuỗi tìm kiếm: trim, normalize Unicode, loại bỏ ký tự điều khiển và token không hợp lệ.
5. Handler tạo `tsquery` theo prefix matching, ví dụ `pho:* & bo:*`.
6. Query dùng EF Core/Npgsql với điều kiện tương đương `SearchVector @@ to_tsquery(...)` và `unaccent` cho input.
7. Filter bắt buộc `Status = Published` và `IsDeleted = false` đối với public search.
8. Tính `relevanceScore = ts_rank(SearchVector, query)`.
9. Sắp xếp `relevanceScore DESC`, sau đó `CreatedAt DESC` để ổn định thứ tự.
10. Áp dụng offset pagination.
11. Map kết quả sang `PagedResult<RecipeSummaryDto>`.
12. Kết quả search được cache Redis với TTL 1 phút theo normalized query + filter + pagination.
13. Trả HTTP 200 với danh sách kết quả.

**Luồng ngoại lệ**  
- A1 – `q` rỗng hoặc < 2 ký tự → HTTP 400, error code `VALIDATION_ERROR`.
- A2 – Không có kết quả → HTTP 200, `items=[]`, kèm thông tin gợi ý nếu có.
- A3 – Query chứa toán tử tsquery không hợp lệ → hệ thống sanitize và coi phần không hợp lệ là literal search term; không phát sinh raw SQL.
- A4 – PostgreSQL unavailable → HTTP 503 nếu readiness dependency fail; lỗi được ghi Serilog.

**Validation**  
- `q`: 2–100 ký tự sau trim.
- `page >= 1`.
- `pageSize` mặc định 12, tối đa 50.
- Không nhận raw SQL hoặc raw `tsquery` expression từ client.
- Chỉ search recipe được phép hiển thị theo authorization scope.

**HTTP Method & Endpoint**  
`GET /api/v1/recipes/search?q={searchTerm}&page={n}&pageSize={n}`

**Kết quả mong đợi**  
`PagedResult<RecipeSummaryDto>` có `items`, `totalCount`, `page`, `pageSize`, `totalPages`, `hasNextPage`, `hasPreviousPage`, `relevanceScore`.

**HTTP Status Code**  
200 OK – Thành công; 400 Bad Request – query không hợp lệ; 429 Too Many Requests – vượt rate limit; 500 Internal Server Error; 503 Service Unavailable – dependency unavailable.

---

FR-SRCH-002: Lọc Công thức (Recipe Filtering)

| Thuộc tính | Nội dung |
|---|---|
| Mã yêu cầu | FR-SRCH-002 |
| Tên yêu cầu | Lọc Công thức theo nhiều tiêu chí |
| Nhóm chức năng | Module Tìm kiếm và Phân trang (FR-SRCH) |
| Tác nhân | Guest / Author / Admin |
| Mức ưu tiên | M – Must Have |

**Mô tả**  
Cho phép client lọc danh sách Recipe theo `categoryId`, `difficulty`, `maxCookTime`, `minServings` và các tiêu chí được hỗ trợ trong API. Nhiều filter được kết hợp bằng logic AND. Filter được áp dụng trước sort và pagination.

**Tiền điều kiện**  
1. Endpoint `/api/v1/recipes` khả dụng.
2. User không cần đăng nhập đối với Published recipes.
3. Với Author/Admin, authorization scope được xác định từ JWT nếu request có authentication.

**Luồng chính**  
1. Client gửi `GET /api/v1/recipes?categoryId={guid}&difficulty=Easy&maxCookTime=30&minServings=2`.
2. `GetRecipesQuery` đi qua MediatR.
3. Validator kiểm tra kiểu và phạm vi từng filter.
4. Handler xây dựng `IQueryable<Recipe>`.
5. Áp dụng từng filter theo AND logic.
6. Áp dụng authorization: Guest chỉ Published; Author thấy Published và recipe của mình; Admin thấy theo policy quản trị.
7. Áp dụng sorting và pagination.
8. Trả `PagedResult<RecipeSummaryDto>`.
9. Với public list, Output Cache policy `RecipeList` TTL 2 phút, vary theo query string.

**Luồng ngoại lệ**  
- A1 – `categoryId` sai GUID → HTTP 400.
- A2 – `difficulty` không thuộc enum → HTTP 400.
- A3 – `maxCookTime < 0` hoặc `minServings < 1` → HTTP 400.
- A4 – Category không tồn tại → HTTP 200 với kết quả rỗng, không coi đây là lỗi resource detail.

**Validation**  
`page >= 1`; `pageSize ∈ [1,50]`; `maxCookTime >= 0`; `minServings >= 1`; enum difficulty chỉ nhận `Easy|Medium|Hard|Expert`.

**HTTP Method & Endpoint**  
`GET /api/v1/recipes?categoryId={guid}&difficulty={level}&maxCookTime={minutes}&minServings={n}&page={n}&pageSize={n}&sortBy={field}&sortOrder={asc|desc}`

**HTTP Status Code**  
200 OK; 400 Bad Request; 429 Too Many Requests; 500 Internal Server Error.

---

FR-SRCH-003: Gợi ý Từ khóa (Search Suggestions)

| Thuộc tính | Nội dung |
|---|---|
| Mã yêu cầu | FR-SRCH-003 |
| Tên yêu cầu | Gợi ý từ khóa và tên công thức |
| Nhóm chức năng | Module Tìm kiếm và Phân trang (FR-SRCH) |
| Tác nhân | Guest / Author / Admin |
| Mức ưu tiên | S – Should Have |

**Mô tả**  
Hệ thống cung cấp autocomplete khi người dùng nhập từ khóa. Suggestions được lấy từ title/category đã được chuẩn hóa; ưu tiên cụm từ phổ biến và recipe Published. Kết quả được cache Redis trong thời gian ngắn để giảm tải PostgreSQL.

**Tiền điều kiện**  
1. Endpoint suggestions hoạt động.
2. Query có tối thiểu 2 ký tự.
3. PostgreSQL và Redis sẵn sàng.

**Luồng chính**  
1. Client gửi `GET /api/v1/recipes/suggestions?q=pho`.
2. Validator chuẩn hóa query.
3. Handler kiểm tra Redis key `search:suggest:{normalizedQ}`.
4. Cache hit → trả ngay suggestions.
5. Cache miss → query title/category bằng `pg_trgm` hoặc prefix matching trên dữ liệu Published.
6. Loại duplicate, sắp xếp theo relevance rồi title.
7. Lưu Redis TTL 1 phút.
8. Trả HTTP 200.

**Luồng ngoại lệ**  
- A1 – Query < 2 ký tự → HTTP 400.
- A2 – Không có suggestion → HTTP 200 với `items=[]`.
- A3 – Redis down → thực hiện database query trực tiếp và không làm request thất bại nếu DB vẫn sẵn sàng.

**Validation**  
`q`: 2–100 ký tự; tối đa 10 suggestions; chỉ trả dữ liệu public; không expose Draft/Archived của user khác.

**HTTP Method & Endpoint**  
`GET /api/v1/recipes/suggestions?q={prefix}`

**Kết quả mong đợi**  
`{ items: [{ text, type, slug? }] }`.

**HTTP Status Code**  
200 OK; 400 Bad Request; 429 Too Many Requests; 500 Internal Server Error.

---

FR-SRCH-004: Lịch sử Tìm kiếm (Search History)

| Thuộc tính | Nội dung |
|---|---|
| Mã yêu cầu | FR-SRCH-004 |
| Tên yêu cầu | Lưu và quản lý lịch sử tìm kiếm của người dùng |
| Nhóm chức năng | Module Tìm kiếm và Phân trang (FR-SRCH) |
| Tác nhân | Author / Admin đã đăng nhập |
| Mức ưu tiên | S – Should Have |

**Mô tả**  
Người dùng đã xác thực có thể lưu các truy vấn tìm kiếm gần đây, xem danh sách và xóa lịch sử. Guest có thể sử dụng search nhưng không có search history server-side. Lịch sử được giới hạn số bản ghi để kiểm soát storage.

**Tiền điều kiện**  
1. User có JWT hợp lệ.
2. Database khả dụng.

**Luồng chính**  
1. Sau khi search thành công, client có thể gửi `POST /api/v1/search-history` với `{ query }`.
2. Handler chuẩn hóa query và kiểm tra duplicate gần nhất.
3. Tạo bản ghi `SearchHistory(UserId, Query, SearchedAt)` hoặc cập nhật timestamp bản ghi tương ứng.
4. Giới hạn tối đa 20 mục/user; bản ghi cũ nhất bị loại khi vượt giới hạn.
5. `GET /api/v1/search-history` trả tối đa 20 mục theo `SearchedAt DESC`.
6. `DELETE /api/v1/search-history/{id}` xóa mục thuộc user hiện tại.
7. `DELETE /api/v1/search-history` xóa toàn bộ lịch sử của user.

**Luồng ngoại lệ**  
- A1 – Không đăng nhập/invalid JWT → HTTP 401.
- A2 – History id không thuộc user → HTTP 404 để không tiết lộ dữ liệu user khác.
- A3 – Query rỗng → HTTP 400.

**Validation**  
`query`: 2–100 ký tự; trim whitespace; không lưu access token, password hoặc dữ liệu nhạy cảm trong query history.

**HTTP Method & Endpoint**  
`GET/POST/DELETE /api/v1/search-history` và `DELETE /api/v1/search-history/{id}`.

**HTTP Status Code**  
200 OK; 201 Created; 204 No Content; 400 Bad Request; 401 Unauthorized; 404 Not Found.

---

<!-- PAGE 38 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2  
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 38 / 78

## 3.5. Module Quản lý Tệp tin (FR-FILE)

FR-FILE-001: Upload Ảnh lên MinIO

| Thuộc tính | Nội dung |
|---|---|
| Mã yêu cầu | FR-FILE-001 |
| Tên yêu cầu | Upload ảnh công thức lên MinIO S3-Compatible |
| Tác nhân | Author-Owner / Admin |
| Mức ưu tiên | M – Must Have |

**Mô tả**  
Hệ thống nhận multipart/form-data, kiểm tra an toàn file trước khi ghi Object Storage. Backend sử dụng `IFileStorageService` để tách Application khỏi MinIO. Object key được sinh bằng GUID theo dạng `recipes/{recipeId}/{guid}.{ext}`; không sử dụng filename do người dùng cung cấp để tránh path traversal và collision.

**Tiền điều kiện**  
1. User đã xác thực và có quyền trên Recipe.
2. Recipe tồn tại và chưa bị soft-delete.
3. MinIO bucket `culinary-blog` khả dụng.

**Luồng chính**  
1. Client gửi `POST /api/v1/recipes/{id}/images` với field `file`.
2. Endpoint chuyển request thành Command và qua ValidationBehavior.
3. Kiểm tra size trước khi đọc toàn bộ stream.
4. Kiểm tra MIME và magic bytes.
5. Sinh object key duy nhất bằng GUID.
6. Upload stream tới MinIO qua S3 SDK.
7. Tạo `RecipeImage` với `OriginalUrl` và metadata.
8. Nếu là ảnh đầu tiên, đặt `IsPrimary=true`.
9. Commit DB trong transaction.
10. Enqueue `ImageResizeJob` qua Hangfire để tạo medium/thumbnail.
11. Invalidate recipe cache và trigger Next.js `revalidateTag` thông qua backend/webhook hoặc Server Action integration.
12. Trả HTTP 201.

**Luồng ngoại lệ**  
- A1 – Recipe không tồn tại → 404.
- A2 – Không có quyền → 403.
- A3 – MIME/size/magic bytes invalid → 400.
- A4 – MinIO unavailable → 503; không tạo bản ghi DB nếu upload chưa thành công.
- A5 – DB commit fail sau upload → job cleanup object orphan được enqueue.

**Validation**  
- Max 5 MB.
- MIME: JPEG, PNG, WebP, AVIF.
- Magic bytes phải khớp định dạng.
- File không được có executable content.
- Filename gốc không được dùng làm object key.

**HTTP Method & Endpoint**  
`POST /api/v1/recipes/{id}/images`.

**HTTP Status Code**  
201 Created; 400 Bad Request; 401 Unauthorized; 403 Forbidden; 404 Not Found; 503 Service Unavailable.

---

FR-FILE-002: Validate / Resize Ảnh

| Thuộc tính | Nội dung |
|---|---|
| Mã yêu cầu | FR-FILE-002 |
| Tên yêu cầu | Validate, chuẩn hóa và tạo phiên bản ảnh |
| Tác nhân | System / Hangfire Worker |
| Mức ưu tiên | M – Must Have |

**Mô tả**  
Sau upload, hệ thống xử lý ảnh bất đồng bộ. Ảnh gốc được giữ nguyên; worker tạo `medium` 800×600 và `thumbnail` 300×300, giữ tỷ lệ phù hợp và cập nhật URL vào `RecipeImage`.

**Tiền điều kiện**  
1. Original object tồn tại trên MinIO.
2. RecipeImage đã được tạo.
3. Hangfire worker hoạt động.

**Luồng chính**  
1. Hangfire lấy job từ queue.
2. Worker download stream ảnh gốc từ MinIO.
3. Kiểm tra MIME/magic bytes lần hai.
4. Decode ảnh bằng thư viện xử lý ảnh được phê duyệt.
5. Resize theo bounded dimensions, không upscale ảnh nhỏ.
6. Encode output theo MIME an toàn.
7. Upload medium và thumbnail bằng object keys riêng.
8. Update `MediumUrl`, `ThumbnailUrl`.
9. Commit transaction.
10. Log jobId, recipeId, imageId và elapsed time.

**Luồng ngoại lệ**  
- A1 – Corrupt image → job Failed, log Error; giữ original.
- A2 – MinIO timeout → retry tối đa 3 lần với exponential backoff.
- A3 – DB conflict → retry job; không ghi đè dữ liệu mới hơn.

**Validation**  
Input phải thuộc MIME whitelist và <=5MB; output không vượt kích thước mục tiêu; memory usage phải được giới hạn; không tin extension filename.

**HTTP Method & Endpoint**  
Không có HTTP endpoint trực tiếp; được kích hoạt bởi FR-FILE-001/FR-RCP-008 và Hangfire.

**Kết quả mong đợi**  
`RecipeImage` có Original/Medium/Thumbnail URL phù hợp; nếu resize thất bại, original vẫn sử dụng được.

---

<!-- PAGE 39 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2  
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 39 / 78

## 3.6. Module Background Jobs (FR-JOB)

FR-JOB-001: Cấu hình Hangfire và Job Processing

**Mô tả**  
Hệ thống sử dụng Hangfire để xử lý fire-and-forget, delayed và recurring jobs. PostgreSQL là persistent storage của Hangfire; queue phải survive API restart. Dashboard `/hangfire` chỉ Admin được truy cập.

**Tiền điều kiện**  
1. PostgreSQL connection string hợp lệ.
2. Hangfire schema được migration/provision.
3. Worker được đăng ký khi API startup.

**Luồng chính**  
1. `AddHangfire()` cấu hình PostgreSQL storage.
2. `AddHangfireServer()` khởi tạo worker.
3. Đăng ký queues và retry policy mặc định.
4. Đăng ký recurring jobs bằng cron UTC.
5. Bảo vệ Dashboard bằng Admin policy.
6. Job state được persist: Enqueued → Processing → Succeeded/Failed.
7. Failed jobs được giữ lại theo retention policy để điều tra.

**Luồng ngoại lệ**  
- A1 – PostgreSQL unavailable → worker không xử lý job; readiness có thể fail.
- A2 – Job throw exception → retry theo policy.
- A3 – Duplicate enqueue → job phải idempotent; không tạo dữ liệu duplicate.

**Validation**  
Job payload không chứa password/access token/secret; retry count và timeout phải cấu hình; recurring job không được đăng ký trùng.

**Kết quả mong đợi**  
Jobs được thực thi bất đồng bộ, có retry, persistence và monitoring qua Dashboard.

---

FR-JOB-002: Gửi Email Bất đồng bộ

**Mô tả**  
Email chào mừng và các email hệ thống được gửi ngoài HTTP request cycle. Application chỉ enqueue job; worker gọi `IEmailService`/SMTP provider. Production hỗ trợ SendGrid hoặc SMTP.

**Tiền điều kiện**  
1. User registration thành công.
2. Email provider đã cấu hình.
3. Hangfire worker hoạt động.

**Luồng chính**  
1. FR-AUTH-001 tạo user.
2. `BackgroundJob.Enqueue<WelcomeEmailJob>(...)` được gọi.
3. Job load template và dữ liệu recipient tối thiểu.
4. Render HTML email.
5. Gửi qua SendGrid API hoặc SMTP/TLS.
6. Log kết quả delivery với correlation/job id, không log nội dung nhạy cảm.
7. Mark job succeeded.

**Luồng ngoại lệ**  
- A1 – Provider timeout/5xx → retry 3 lần theo exponential backoff.
- A2 – Email address invalid → Failed state, không retry vô hạn.
- A3 – Provider unavailable → Failed/Delayed tùy policy, alert operator.

**Validation**  
Recipient phải là email hợp lệ; template không cho phép HTML injection từ profile; secret provider không được log; SMTP production bắt buộc TLS.

**Kết quả mong đợi**  
HTTP registration response không bị block bởi email; email được xử lý độc lập bởi Hangfire.

---

FR-JOB-003: Cleanup File Rác

**Mô tả**  
Recurring job định kỳ quét MinIO để tìm object orphan: file đã upload nhưng không có `RecipeImage`, file tạm sau upload thất bại hoặc biến thể resize không còn tham chiếu. Job xóa theo grace period để tránh race condition.

**Tiền điều kiện**  
1. MinIO và PostgreSQL khả dụng.
2. Job có quyền read/list/delete bucket.
3. Grace period tối thiểu 24 giờ.

**Luồng chính**  
1. Chạy hàng ngày, ví dụ 03:30 UTC.
2. Liệt kê object theo prefix `recipes/`.
3. Đối chiếu object key với các URL được tham chiếu trong `RecipeImage`.
4. Bỏ qua object mới hơn grace period.
5. Xóa orphan objects theo batch.
6. Ghi số object scanned/deleted/skipped.
7. Không xóa object thuộc Recipe chưa soft-delete nếu vẫn có reference.

**Luồng ngoại lệ**  
- A1 – MinIO unavailable → retry 3 lần.
- A2 – DB unavailable → không xóa object vì không thể xác định reference.
- A3 – Một object delete fail → tiếp tục batch và log object cụ thể.

**Validation**  
Không xóa object khi chưa qua grace period; chỉ xóa trong bucket/prefix được cấu hình; job phải idempotent.

---

<!-- PAGE 40 -->

Culinary Blog – Tài liệu Đặc tả Yêu cầu Phần mềm (SRS) v1.0.2  
CONFIDENTIAL  •  Phát triển Ứng dụng Web Nâng cao V4  •  Trang 40 / 78

## 3.7. Module Quan sát Hệ thống (FR-OBS)

FR-OBS-001: Structured Logging với Serilog

**Mô tả**  
Mọi HTTP request, Command/Query và background job phải tạo structured log. Log tối thiểu gồm Timestamp, Level, Message, CorrelationId, TraceId, RequestPath, HTTP method/status, elapsed time và UserId khi authenticated. Production không log secret, password, access token hoặc refresh token.

**Tiền điều kiện**  
1. Serilog được cấu hình.
2. CorrelationId middleware chạy trước endpoint.
3. Sink phù hợp đã được cấu hình.

**Luồng chính**  
1. Request đến Nginx/API.
2. CorrelationId được lấy từ `X-Correlation-ID` hoặc sinh mới.
3. Serilog enrich log context.
4. LoggingBehavior log Command/Query type và duration.
5. Response log status/duration.
6. Exception middleware ghi Error với exception metadata.
7. Production sink ghi JSON structured logs; development có thể gửi Seq.

**Luồng ngoại lệ**  
- A1 – Correlation header invalid → sinh correlation ID mới.
- A2 – Logging sink unavailable → fallback console; không làm request fail.

**Validation**  
PII và secrets phải được masking/redaction; request body chỉ log field allowlist; request >500ms tạo Warning; error 5xx tạo Error.

---

FR-OBS-002: Health Checks

**Mô tả**  
Hệ thống cung cấp liveness, readiness và aggregate health endpoints để orchestrator/Nginx biết process có sống và dependency có sẵn sàng hay không.

**Tiền điều kiện**  
Health check services được đăng ký cho PostgreSQL, Redis và MinIO.

**Luồng chính**  
1. `GET /health/live`: chỉ kiểm tra process, trả 200 nếu process đang chạy.
2. `GET /health/ready`: kiểm tra PostgreSQL và Redis; fail nếu dependency bắt buộc không sẵn sàng.
3. `GET /health`: tổng hợp DB, Redis, MinIO và trả trạng thái từng component.
4. Health response không expose connection string hoặc secret.
5. Nginx/orchestrator ngừng route traffic tới instance không Ready.

**Luồng ngoại lệ**  
- A1 – DB down → readiness 503.
- A2 – Redis down → readiness 503 theo dependency policy; application có thể graceful-degrade cho cache trong request thường nhưng instance không Ready theo baseline production.
- A3 – MinIO down → aggregate health Unhealthy; readiness xử lý theo policy storage bắt buộc.

**Validation**  
Health endpoints không yêu cầu JWT; timeout mỗi dependency phải ngắn hơn timeout của health probe; response format ổn định.

---

FR-OBS-003: Distributed Tracing và Metrics

**Mô tả**  
OpenTelemetry thu thập traces và metrics xuyên suốt Nginx/API, MediatR, EF Core, HttpClient, MinIO/SMTP integration và Hangfire. Trace context được liên kết với CorrelationId/TraceId.

**Tiền điều kiện**  
1. OpenTelemetry .NET SDK được cấu hình.
2. OTLP exporter endpoint hợp lệ.
3. Production collector/Tempo/Jaeger đã sẵn sàng.

**Luồng chính**  
1. Tạo Activity cho incoming HTTP request.
2. Propagate W3C Trace Context tới downstream HTTP calls.
3. EF Core tạo database spans.
4. Business handler có thể tạo custom spans cho FTS, image processing và external provider calls.
5. Thu metrics request count, duration histogram, error count/rate, job duration và job failure.
6. Export OTLP đến collector.
7. Dashboard hiển thị p50/p95/p99, error rate và dependency latency.

**Luồng ngoại lệ**  
- A1 – Collector unavailable → application tiếp tục chạy; exporter dùng batching/drop policy để không block request.
- A2 – Instrumentation error → log Warning, không làm business operation fail.

**Validation**  
Không đưa secrets/PII vào span attributes; metric labels phải có cardinality kiểm soát; TraceId phải xuất hiện trong structured log để correlation.

---

# CHƯƠNG 4. YÊU CẦU PHI CHỨC NĂNG (NFR)

Các NFR là ràng buộc chất lượng bắt buộc đối với production baseline. SLA hiệu năng API mục tiêu là **< 200ms cho p95 của các GET API cache-warm thông thường**, đồng thời duy trì các ngưỡng p99 và write-operation đã nêu dưới đây.

## 4.1. Hiệu năng (NFR-PERF)

| Mã | Yêu cầu | Tiêu chí chấp nhận |
|---|---|---|
| NFR-PERF-001 | API response time | p50 ≤ 150ms; p95 < 200ms cho GET cache-warm thông thường; p95 ≤ 500ms cho write/uncached; p99 ≤ 1s. |
| NFR-PERF-002 | Throughput | ≥100 concurrent users trên baseline 2 vCPU/4GB RAM mà không vượt SLA đã định. |
| NFR-PERF-003 | Cache | Category Redis TTL 30m; Recipe detail Redis TTL 5m; Search Redis TTL 1m; Recipe list Output Cache TTL 2m. |
| NFR-PERF-004 | Database | Không N+1; B-tree cho equality/range/order; GIN cho FTS; slow query >100ms phải được cảnh báo. |
| NFR-PERF-005 | Frontend | LCP ≤2.5s, CLS ≤0.1, INP ≤200ms; sử dụng Next.js App Router, ISR và `next/image`. |

### Chiến lược Cache và Invalidation

1. **Backend Output Cache – Recipe List:** TTL 2 phút, vary by query string.
2. **Redis:** Category 30 phút; Recipe detail 5 phút; Search/Suggestions 1 phút.
3. **Next.js App Router:** các route public dùng `revalidate` phù hợp và gắn `next: { tags: [...] }` cho fetch server-side.
4. Khi Create/Update/Delete/Publish/Archive Recipe thành công, Backend phát tín hiệu mutation thành công; Next.js Server Action gọi `revalidateTag('recipes')` và tag chi tiết `recipe:{slug}` khi có khả năng truy cập cùng deployment. Với API-driven deployment tách biệt, frontend gọi endpoint revalidation nội bộ có secret bảo vệ hoặc webhook nội bộ.
5. Khi Category thay đổi, gọi `revalidateTag('categories')` và các tag category liên quan.
6. Không dùng stale cache sau mutation trong cùng user flow; mutation response trả resource mới nhất để client cập nhật TanStack Query cache.

## 4.2. Bảo mật (NFR-SEC)

Hệ thống tuân thủ các nguyên tắc OWASP Top 10, đặc biệt Injection, Broken Access Control, Identification/Authentication Failures, Security Misconfiguration, Vulnerable Components, Logging/Monitoring và SSRF.

| Mã | Yêu cầu | Tiêu chí |
|---|---|---|
| NFR-SEC-001 | Password | ASP.NET Core Identity PBKDF2; không plaintext; tối thiểu 8 ký tự và policy complexity. |
| NFR-SEC-002 | JWT/Refresh | Access JWT 15 phút; Refresh 7 ngày; refresh token 128-bit CSPRNG, SHA-256 trước DB, rotation + reuse detection. |
| NFR-SEC-003 | Rate Limiting | Auth 10 req/phút/IP; API 100 req/phút/IP; upload 5 req/phút/IP; HTTP 429 + Retry-After. |
| NFR-SEC-004 | Input/File | FluentValidation; parameterized EF Core; MIME + magic bytes; max 5MB; GUID object key; CSP. |
| NFR-SEC-005 | Transport | HTTPS/TLS 1.2+; HSTS; CORS allowlist; secrets qua env/user secrets. |
| NFR-SEC-006 | Authorization | RBAC + resource ownership + policy-based authorization; kiểm tra ownership ở Application Layer. |
| NFR-SEC-007 | Google OAuth | Frontend nhận code/token từ Google Popup; Backend verify trực tiếp với Google Server trước khi phát hành system JWT. |

## 4.3. Khả năng Sử dụng (NFR-USE)

- Responsive: mobile 320–767px, tablet 768–1199px, desktop ≥1200px.
- WCAG 2.1 AA: semantic HTML, keyboard navigation, ARIA, contrast ≥4.5:1 cho text.
- Error message actionable, dùng Application Error Code thay vì phụ thuộc message text.
- Mọi async operation có loading/skeleton/progress feedback phù hợp.

## 4.4. Độ tin cậy (NFR-REL)

- Uptime mục tiêu ≥99.5%.
- Global exception handling trả RFC 7807 và không expose stack trace.
- Redis failure có graceful degradation cho cache.
- Hangfire retry job tối đa 3 lần theo exponential backoff trừ job có policy riêng.
- PostgreSQL WAL + daily backup; retention 30 ngày.
- Recipe dùng Soft Delete (`IsDeleted=true`) thay vì hard delete.
- MinIO dùng persistent volume và orphan cleanup job.

## 4.5. Khả năng Bảo trì (NFR-MAINT)

- Clean Architecture 4 layer: Domain, Application, Infrastructure, Presentation.
- CQRS: Command và Query Handler tách biệt.
- FluentValidation qua MediatR Pipeline.
- Unit test ≥80% line coverage cho Application layer; integration test cho API; E2E cho register/login/create/publish/search.
- CI phải pass build, static analysis, tests và security scan.
- API/OpenAPI và ADR phải được cập nhật cùng release.

## 4.6. Khả năng Mở rộng (NFR-SCALE)

Backend stateless, Redis là distributed cache, Hangfire dùng PostgreSQL shared storage, Nginx hỗ trợ load balancing. PostgreSQL có chiến lược index B-tree/GIN và có thể mở rộng read replica/partitioning khi scale lớn.

## 4.7. SEO (NFR-SEO)

Recipe Published phải có canonical slug, JSON-LD Schema.org Recipe, Open Graph, robots metadata và sitemap XML. Draft/Archived không được index công khai.

---

# CHƯƠNG 5. YÊU CẦU GIAO DIỆN NGOÀI

## 5.1. Giao diện Người dùng

Frontend dùng Next.js 15 App Router, TypeScript, Tailwind CSS, TanStack Query v5 và React Hook Form + Zod. Public pages ưu tiên SSR/ISR; dashboard dùng CSR khi cần tương tác cao.

## 5.2. Giao diện Phần mềm – REST API

- Base path: `/api/v1`.
- Resource-oriented URL, plural nouns: `/recipes`, `/categories`.
- HTTP methods: GET, POST, PUT, PATCH, DELETE.
- Pagination: `page`, `pageSize`.
- Sorting: `sortBy`, `sortOrder=asc|desc`.
- Authentication: `Authorization: Bearer <access_token>`.
- Content type JSON; upload dùng multipart/form-data.
- Error format: `application/problem+json` theo RFC 7807.
- Versioning bằng URL path.
- `X-Correlation-ID` được nhận/generate và trả lại ở response.

## 5.3. MinIO S3-Compatible Interface

Backend sử dụng AWS SDK for .NET (`AWSSDK.S3`) hoặc abstraction tương đương qua `IFileStorageService`. Configuration gồm endpoint, access key, secret key, bucket. Object key do server sinh. Public-read chỉ áp dụng nếu policy sản phẩm cho phép; bucket production nên ưu tiên private + presigned URL nếu yêu cầu bảo mật ảnh.

## 5.4. Email – SendGrid / SMTP

`IEmailService` là abstraction. Production có thể dùng SendGrid API hoặc SMTP/TLS. Configuration: provider, host, port, username, password/API key, from address, timeout và retry policy. Development dùng MailHog. Secrets không commit vào Git.

## 5.5. Google OAuth 2.0

1. Next.js mở Google OAuth Popup/authorization flow.
2. Google trả Authorization Code hoặc ID Token về frontend callback.
3. Frontend gửi code/token tới `POST /api/v1/auth/google`.
4. Backend gọi Google verification endpoint để xác minh trực tiếp với Google Server.
5. Backend map Google subject/email vào ApplicationUser.
6. Backend phát hành system JWT + Refresh Token.
7. Không sử dụng Auth.js server-side session làm nguồn xác thực của hệ thống.

---

# CHƯƠNG 6. KIẾN TRÚC HỆ THỐNG

## 6.1. Tổng quan

`Browser → Nginx → Next.js → .NET 10 Minimal API → Application → Domain/Infrastructure → PostgreSQL/Redis/MinIO/External Services`.

## 6.2. Clean Architecture

### Domain
Entities, value objects, enums, aggregate rules và repository interfaces. Domain không phụ thuộc Infrastructure.

### Application
CQRS commands/queries, handlers, DTOs, validators, pipeline behaviors và service interfaces.

### Infrastructure
EF Core/Npgsql, repositories, Redis, MinIO S3, email provider, Hangfire, Serilog/OpenTelemetry exporters.

### Presentation
Minimal API endpoint groups, authentication/authorization, middleware, exception handling, rate limiting và OpenAPI/Scalar.

## 6.3. CQRS + MediatR Pipeline

Pipeline chuẩn: `LoggingBehavior → ValidationBehavior → CachingBehavior (Query) → Handler → CacheInvalidationBehavior (Command)`. Query không mutate state; Command thay đổi state và phải thực hiện cache invalidation sau commit thành công.

## 6.4. Optimistic Concurrency – Recipe Aggregate Root

Recipe là Aggregate Root. `Recipe.RowVersion` là concurrency token.

**Quy tắc bắt buộc:**
1. Mọi Recipe CRUD phải đọc RowVersion hiện tại.
2. Mọi RecipeStep/RecipeIngredient/RecipeImage mutation phải thuộc transaction của Recipe aggregate.
3. Client gửi `If-Match: "<rowVersion>"` hoặc `rowVersion` trong body.
4. Handler kiểm tra authorization trước khi mutation.
5. EF Core dùng concurrency predicate trên RowVersion khi commit.
6. Nếu RowVersion đã thay đổi → `DbUpdateConcurrencyException`.
7. Global handler map lỗi này thành HTTP 422 + `RECIPE_CONCURRENCY_CONFLICT`.
8. Client phải reload aggregate và áp dụng lại thay đổi; hệ thống không tự động merge child collection.
9. Sau commit thành công, RowVersion mới được trả về DTO/ETag.

## 6.5. Deployment – Docker Compose

Services: Nginx, frontend, API, PostgreSQL 16, Redis 7, MinIO, Seq (development), MailHog (development). Production secrets được inject qua environment/secret store; volumes của PostgreSQL/Redis/MinIO phải persistent.

---

# CHƯƠNG 7. MÔ HÌNH DỮ LIỆU

## 7.1. Nguyên tắc

PostgreSQL 16, EF Core 10 Code First. UUID làm PK cho domain entities; Identity dùng string key. BaseEntity cung cấp `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `RowVersion`. Global Query Filter loại bản ghi `IsDeleted=true` khỏi query thông thường.

## 7.2. Bảng `Recipes`

| Cột | Kiểu | Ràng buộc | Index / Mô tả |
|---|---|---|---|
| Id | uuid | PK | B-tree PK |
| Title | varchar(200) | NOT NULL | B-tree/GIN trigram tùy query |
| Slug | varchar(220) | UNIQUE NOT NULL | UNIQUE B-tree |
| Description | text | NOT NULL | SearchVector nguồn |
| Instructions | text | NOT NULL | Legacy/general instructions |
| PrepTime | int | >0 | filter/range |
| CookTime | int | ≥0 | B-tree |
| Servings | int | >0 | B-tree nếu filter thường xuyên |
| Difficulty | smallint | NOT NULL | B-tree |
| Status | smallint | NOT NULL | B-tree |
| CategoryId | uuid | FK → Categories.Id, RESTRICT | B-tree |
| AuthorId | varchar(450) | FK → AspNetUsers.Id | B-tree |
| SearchVector | tsvector | nullable/maintained | **GIN** |
| PublishedAt | timestamptz | nullable | B-tree |
| Nutrition_* | numeric | nullable | Owned entity columns |
| IsDeleted | boolean | NOT NULL | Partial index `WHERE IsDeleted=false` |
| RowVersion | bytea | NOT NULL | Optimistic concurrency token |

**FTS index:** `CREATE INDEX ... ON Recipes USING GIN (SearchVector);`. Trigger dùng `to_tsvector` và `unaccent` để đồng bộ Title/Description.

## 7.3. Bảng `RecipeSteps`

`Id uuid PK`, `RecipeId uuid FK → Recipes.Id ON DELETE CASCADE`, `StepNumber int CHECK >0`, `Title varchar(200)`, `Description text`, `TimerMinutes int nullable`, `ImageUrl varchar(500) nullable`. Unique composite `(RecipeId, StepNumber)`. B-tree index trên `RecipeId`.

## 7.4. Bảng `RecipeIngredients`

`Id uuid PK`, `RecipeId uuid FK → Recipes.Id ON DELETE CASCADE`, `Name varchar(200)`, `Quantity numeric(10,3) nullable`, `Unit varchar(50) nullable`, `Notes varchar(500) nullable`, `OrderIndex int`. B-tree `(RecipeId, OrderIndex)`.

## 7.5. Bảng `RecipeImages`

`Id uuid PK`, `RecipeId uuid FK → Recipes.Id ON DELETE CASCADE`, `OriginalUrl varchar(500)`, `MediumUrl varchar(500) nullable`, `ThumbnailUrl varchar(500) nullable`, `AltText varchar(200) nullable`, `IsPrimary boolean`, `OrderIndex int`. Partial unique index đảm bảo tối đa một ảnh primary/recipe.

## 7.6. Bảng `Categories`

`Id uuid PK`, `Name varchar(100) UNIQUE`, `Slug varchar(120) UNIQUE`, `Description text nullable`, `ImageUrl varchar(500) nullable`, `OrderIndex int`. Unique B-tree trên Name/Slug.

## 7.7. Bảng `AspNetUsers`

ASP.NET Core Identity `IdentityUser<string>` mở rộng với FullName, AvatarUrl, IsActive, CreatedAt và các fields nghiệp vụ cần thiết. Identity tables giữ các PK/FK chuẩn của framework.

## 7.8. Bảng `RefreshTokens`

`Id uuid PK`, `UserId string FK`, `TokenHash varchar(64) UNIQUE`, `ExpiresAt timestamptz`, `CreatedAt`, `RevokedAt nullable`, `IsRevoked`, `ReplacedByToken nullable`, `CreatedByIp nullable`. Không lưu raw refresh token.

## 7.9. Bảng `SearchHistories`

`Id uuid PK`, `UserId string FK → AspNetUsers.Id ON DELETE CASCADE`, `Query varchar(100)`, `SearchedAt timestamptz`. Index `(UserId, SearchedAt DESC)` để đọc history nhanh. Có thể unique logic theo `(UserId, lower(Query))` nếu muốn chống duplicate.

## 7.10. Quan hệ ERD

- Category `1:N` Recipe.
- ApplicationUser `1:N` Recipe.
- Recipe `1:N` RecipeStep.
- Recipe `1:N` RecipeIngredient.
- Recipe `1:N` RecipeImage.
- Recipe `1:1` RecipeNutrition (owned columns).
- ApplicationUser `1:N` RefreshToken.
- ApplicationUser `1:N` SearchHistory.

## 7.11. Index Strategy

- **B-tree:** PK, UNIQUE, FK, status/category/author, dates, sort fields.
- **GIN:** `Recipes.SearchVector` cho FTS.
- **GIN/pg_trgm:** Title nếu autocomplete/fuzzy search yêu cầu.
- **Partial index:** Published + not deleted hoặc IsPrimary=true để tối ưu public queries.
- Index phải được đánh giá bằng `EXPLAIN ANALYZE` trước release.

---

# CHƯƠNG 8. ĐẶC TẢ REST API

## 8.1. Quy ước chung

Base URL: `/api/v1`. JSON UTF-8. Public GET không cần JWT nếu chỉ truy cập Published content. Protected mutations yêu cầu Bearer JWT và resource ownership/policy check.

### Pagination / Sorting

Ví dụ: `GET /api/v1/recipes?page=1&pageSize=10&sortBy=createdAt&sortOrder=desc`.

### Success Envelope

```json
{
  "data": {},
  "meta": {
    "page": 1,
    "pageSize": 10,
    "total": 100
  }
}
```

## 8.2. Authentication API

| Method | Endpoint | Auth | Response |
|---|---|---|---|
| POST | `/auth/register` | Public | 201 AuthResponseDto |
| POST | `/auth/login` | Public | 200 AuthResponseDto |
| POST | `/auth/google` | Public | 200 AuthResponseDto |
| POST | `/auth/refresh` | Refresh token | 200 AuthResponseDto |
| POST | `/auth/logout` | Bearer | 204 |
| GET | `/auth/me` | Bearer | 200 UserProfileDto |
| PATCH | `/auth/me` | Bearer | 200 UserProfileDto |

## 8.3. Categories API

| Method | Endpoint | Auth | Response |
|---|---|---|---|
| GET | `/categories` | Public | 200 |
| GET | `/categories/{slug}` | Public | 200/404 |
| POST | `/categories` | Admin | 201 |
| PUT | `/categories/{id}` | Admin | 200/404/422 |
| DELETE | `/categories/{id}` | Admin | 204/404/409 |

## 8.4. Recipes API

| Method | Endpoint | Auth | Response |
|---|---|---|---|
| GET | `/recipes` | Public | 200 |
| GET | `/recipes/{slug}` | Public/Owner/Admin | 200/403/404 |
| POST | `/recipes` | Author/Admin | 201 |
| PUT | `/recipes/{id}` | Owner/Admin | 200/403/404/422 |
| PATCH | `/recipes/{id}/publish` | Owner/Admin | 200/403/404/422 |
| PATCH | `/recipes/{id}/unpublish` | Owner/Admin | 200 |
| PATCH | `/recipes/{id}/archive` | Owner/Admin | 200 |
| DELETE | `/recipes/{id}` | Owner/Admin | 204/403/404 |

**Recipe DELETE:** Soft Delete. `IsDeleted=true`; child data remains recoverable according to retention policy. MinIO cleanup is asynchronous.

## 8.5. Recipe Images

| Method | Endpoint | Auth | Response |
|---|---|---|---|
| POST | `/recipes/{id}/images` | Owner/Admin | 201 |
| PATCH | `/recipes/{id}/images/{imageId}/primary` | Owner/Admin | 200 |
| DELETE | `/recipes/{id}/images/{imageId}` | Owner/Admin | 204 |

## 8.6. Recipe Steps / Ingredients

All child mutation endpoints require current Recipe RowVersion via `If-Match` or request body. A stale RowVersion returns 422 `RECIPE_CONCURRENCY_CONFLICT`.

| Method | Endpoint | Auth | Response |
|---|---|---|---|
| POST | `/recipes/{id}/steps` | Owner/Admin | 201 |
| PUT | `/recipes/{id}/steps/{stepId}` | Owner/Admin | 200 |
| DELETE | `/recipes/{id}/steps/{stepId}` | Owner/Admin | 204 |
| POST | `/recipes/{id}/ingredients` | Owner/Admin | 201 |
| PUT | `/recipes/{id}/ingredients/{ingId}` | Owner/Admin | 200 |
| DELETE | `/recipes/{id}/ingredients/{ingId}` | Owner/Admin | 204 |

## 8.7. Search API

| Method | Endpoint | Auth | Response |
|---|---|---|---|
| GET | `/recipes/search` | Public | 200 |
| GET | `/recipes/suggestions` | Public | 200 |
| GET | `/search-history` | Bearer | 200 |
| POST | `/search-history` | Bearer | 201 |
| DELETE | `/search-history/{id}` | Bearer | 204 |
| DELETE | `/search-history` | Bearer | 204 |

## 8.8. File / Job / Observability

File operations are exposed through Recipe Image endpoints. Hangfire jobs are internal and not public APIs. Health endpoints are unauthenticated and intended for infrastructure probes: `/health`, `/health/live`, `/health/ready`.

## 8.9. RFC 7807 Error Response

Content-Type: `application/problem+json`.

```json
{
  "type": "RECIPE_CONCURRENCY_CONFLICT",
  "title": "Optimistic concurrency conflict",
  "status": 422,
  "detail": "Recipe đã được cập nhật bởi request khác. Vui lòng tải lại dữ liệu.",
  "instance": "/api/v1/recipes/9d4...",
  "errors": {},
  "traceId": "00-..."
}
```

`type` chứa Application Error Code; `errors` dùng cho field validation. Không trả stack trace trong production.

---

# PHỤ LỤC A – HTTP STATUS CODES

| Code | Status | Ngữ cảnh chuẩn |
|---:|---|---|
| 200 | OK | GET/PATCH thành công; login thành công |
| 201 | Created | POST tạo resource thành công |
| 204 | No Content | DELETE/logout thành công |
| 400 | Bad Request | Validation, malformed request, business rule vi phạm |
| 401 | Unauthorized | Thiếu/invalid access token; refresh token invalid/expired/revoked |
| 403 | Forbidden | Đã xác thực nhưng không có quyền |
| 404 | Not Found | Resource không tồn tại hoặc đã soft-delete theo public scope |
| 409 | Conflict | Unique conflict, category còn recipe, duplicate resource conflict |
| 422 | Unprocessable Entity | **Chỉ dùng cho Optimistic Concurrency / RowVersion conflict** theo baseline này |
| 429 | Too Many Requests | Rate limit vượt ngưỡng; kèm Retry-After |
| 500 | Internal Server Error | Unhandled server error; RFC 7807 + Serilog |
| 503 | Service Unavailable | Dependency/health failure hoặc service unavailable |

---

# PHỤ LỤC B – APPLICATION ERROR CODES

| Error Code | HTTP | Mô tả |
|---|---:|---|
| AUTH_EMAIL_EXISTS | 409 | Email đã đăng ký |
| AUTH_INVALID_CREDENTIALS | 401 | Credential không hợp lệ |
| AUTH_TOKEN_EXPIRED | 401 | Access token hết hạn |
| AUTH_TOKEN_INVALID | 401 | Access token invalid |
| AUTH_REFRESH_TOKEN_EXPIRED | 401 | Refresh token hết hạn |
| AUTH_REFRESH_TOKEN_REVOKED | 401 | Refresh token đã revoke/reuse |
| AUTH_GOOGLE_TOKEN_INVALID | 401 | Google token/code không xác minh được |
| AUTH_ACCOUNT_DISABLED | 403 | Account disabled |
| RECIPE_NOT_FOUND | 404 | Recipe không tồn tại/đã xóa |
| RECIPE_SLUG_EXISTS | 409 | Slug conflict |
| RECIPE_PUBLISH_INCOMPLETE | 400 | Thiếu ít nhất 1 ingredient hoặc 1 step |
| RECIPE_FORBIDDEN | 403 | Không phải owner/Admin |
| RECIPE_CONCURRENCY_CONFLICT | 422 | RowVersion stale |
| CATEGORY_NOT_FOUND | 404 | Category không tồn tại |
| CATEGORY_NAME_EXISTS | 409 | Category name conflict |
| CATEGORY_DELETE_HAS_RECIPES | 409 | Category còn recipe |
| FILE_SIZE_EXCEEDED | 400 | File >5MB |
| FILE_MIME_INVALID | 400 | MIME/magic bytes invalid |
| VALIDATION_ERROR | 400 | FluentValidation failed |
| RATE_LIMIT_EXCEEDED | 429 | Rate limit exceeded |

---

# PHỤ LỤC C – TỪ ĐIỂN THUẬT NGỮ

| Thuật ngữ | Định nghĩa |
|---|---|
| Aggregate Root | Entity gốc kiểm soát invariant và transaction boundary của một aggregate; trong hệ thống này là Recipe. |
| App Router | Kiến trúc routing của Next.js dùng thư mục `app/`. |
| Authorization Code | Mã do Google cấp trong OAuth flow, được frontend chuyển tới backend để xác minh/đổi token theo flow cấu hình. |
| CQRS | Command Query Responsibility Segregation – tách luồng đọc và ghi. |
| FTS | Full-Text Search – tìm kiếm toàn văn bản bằng PostgreSQL tsvector/tsquery. |
| GIN | Generalized Inverted Index – index PostgreSQL phù hợp cho tsvector và full-text search. |
| Hangfire | Framework .NET cho background jobs. |
| Optimistic Concurrency | Kiểm soát concurrent update bằng concurrency token thay vì lock dài hạn. |
| Output Cache | Cơ chế cache response HTTP ở ASP.NET Core; Recipe list TTL 2 phút. |
| Redis | Distributed in-memory data store dùng cho cache. |
| RowVersion | Concurrency token của Recipe dùng để phát hiện lost update. |
| RFC 7807 | Chuẩn Problem Details cho HTTP API errors. |
| S3-Compatible | API object storage tương thích giao thức Amazon S3; MinIO thuộc nhóm này. |
| SearchVector | Cột PostgreSQL `tsvector` dùng cho FTS, có GIN index. |
| Soft Delete | Đánh dấu `IsDeleted=true` thay vì physical delete. |
| TTL | Time-To-Live – thời gian dữ liệu tồn tại trong cache. |
| Token Rotation | Mỗi lần refresh thành công sẽ revoke refresh token cũ và cấp token mới. |
| TraceId | Định danh distributed trace, dùng để liên kết request giữa nhiều service. |
| Unaccent | PostgreSQL extension loại bỏ dấu trong text để hỗ trợ tìm kiếm không dấu. |
| Next.js revalidateTag | Cơ chế invalidation/revalidation theo tag trong Next.js App Router. |

---

**Kết thúc tài liệu SRS – Culinary Blog v1.0.2**
