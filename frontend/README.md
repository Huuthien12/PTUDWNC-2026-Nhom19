This is a [Next.js](https://nextjs.org) project bootstrapped with [`create-next-app`](https://nextjs.org/docs/app/api-reference/cli/create-next-app).

## Authentication — buổi 2 / Member 3

Theo contract `../doc/backend_auth_member3.md`, Login/Refresh trả
`accessToken`, `tokenType`, `refreshToken`, `expiresAt` và `user`.
`auth-service.login()` lưu toàn bộ response trong một entry localStorage
`culinary.auth.v1` cùng ID phiên; rotation thay cả cặp token, expiry và profile/roles
bằng một lần ghi. Hai khóa `accessToken`/`tokenType` cũ được bỏ khi login/logout.
Phiên cũ chỉ có access token không được phục hồi; cần đăng nhập lại.

- `apiClient(path, { auth: true, ...options })` gắn Bearer vào request riêng tư.
  Trước request, refresh nếu `expiresAt` đã qua. HTTP 401 refresh rồi retry tối đa
  **một lần**; HTTP 403, lỗi mạng hoặc lỗi nghiệp vụ không tự retry.
  Request body hiện tại là JSON có thể gửi lại; không dùng wrapper retry này cho
  ReadableStream/upload không thể replay. Public requests mặc định `auth: false`,
  kể cả SSR, không đính kèm token và không làm thay đổi phiên.
- `AuthLifecycle` kiểm tra hạn khi khôi phục trang, tới hạn, focus hoặc visibility
  thay đổi. Refresh dùng promise chung trong một tab và Web Locks giữa các tab;
  đọc lại localStorage sau khi lấy lock để không gửi lại token đã rotation.
  Cần trình duyệt có Web Locks trong secure context (HTTPS hoặc localhost).
  Khi thiếu Web Locks, từ chối thao tác thay vì chạy refresh không có khóa.
- Refresh trả lỗi, response sai contract/đã hết hạn, hoặc retry vẫn 401: xóa phiên
  và đưa về `/login?reason=session-expired`. Không log token hoặc đặt token vào URL.
  Login/refresh/logout dùng raw transport riêng, không gọi vòng lặp refresh.
- Logout chờ refresh đang chạy, lấy cặp token mới nhất, xóa phiên local và gửi
  `POST /api/v1/auth/logout` với `{ refreshToken }` + Bearer access token. Vẫn gửi
  được access đã hết hạn theo contract backend. Nếu mạng lỗi, vẫn xóa local và
  thông báo chưa xác nhận thu hồi server. Các tab khác nhận storage event.
  Network timeout của auth requests là 15 giây.
- Kết quả request thuộc phiên cũ không được ghi đè/xóa một login mới. Logout
  và refresh dùng cùng lock; không để refresh đến muộn tự phục hồi phiên đã xóa.
- Header hiển thị user, nút Logout và link quản trị cho Admin. `/admin/*` dùng
  `RequireAuth` để chặn render cho guest, kiểm tra hạn và role Admin; Author thấy
  thông báo không có quyền. `/`, `/categories`, `/categories/[slug]`, `/login`
  vẫn public. Return URL sau Login chỉ cho phép `/admin/categories` hoặc `/`.
  Các route profile/dashboard chưa có trong repo nên chưa tạo thêm.
- Các thao tác create/update/delete Category đã dùng Bearer + refresh/retry.

Route guard là kiểm tra UI phía client, không phải hàng rào bảo mật server.
Backend vẫn xác minh JWT và policy Admin. Không truyền private data qua Server
Component rồi dựa vào guard để bảo mật. Trang Admin tải dữ liệu sau khi guard cho
phép render; public category pages vẫn dùng cấu trúc SSR hiện có.

Storage hiện kế thừa mô hình browser-token của Login cũ; localStorage không có
bảo vệ HttpOnly và chịu rủi ro XSS. Chưa triển khai BFF/cookie HttpOnly trong phạm
vi này; API backend không thay đổi.

### Kiểm tra

```sh
npm run lint
npm run typecheck
npm test
npm run build
```

Repo trước thay đổi chưa có frontend test runner. `npm test` hiện dùng TypeScript
có sẵn để compile test vào `.test-build` (gitignored), rồi chạy Node test runner;
không thêm dependency. Tests giả lập fetch/localStorage/Web Locks, kiểm tra
rotation đồng thời, retry một lần, lỗi refresh, logout race, phiên đến muộn,
đồng bộ storage và chính sách role/return URL. Đây là kiểm thử tự động trong repo, không thay thế test UI trình duyệt hay E2E với backend thật.

Kết quả đã thực hiện: **22 frontend test và 31 backend test pass**; backend và frontend build đạt.

### Kiểm thử tích hợp local đã thực hiện

Các kiểm tra sau dùng API/PostgreSQL thật và Edge, không thuộc bộ test tự động trong repo:

- Migration AddRefreshTokenRevokedAt đã áp thành công trên CulinaryBlogDb_Thuan_Main; database cũ CulinaryBlogDb không bị thay đổi.
- Login **200** → Refresh **200** → dùng lại token cũ **401** → Logout **204** → refresh sau logout **401**.
- Hai request refresh đồng thời cùng token cho **một 200 và một 401**.
- CORS cho phép http://localhost:3000, không cấp quyền cho origin ngoài danh sách.
- Trên Edge: guest chuyển về Login; user không có role Admin bị chặn; hai tab đồng bộ refresh/logout; refresh thất bại xóa phiên.

Giới hạn: chưa thử luồng Admin thành công vì database chưa có role Admin; chưa có E2E tự động trong repo; chưa kiểm thử PostgreSQL lock timeout/rollback thực tế. Các test rollback dùng SQLite không chứng minh rollback trên PostgreSQL.

## Getting Started

First, run the development server:

```bash
npm run dev
# or
yarn dev
# or
pnpm dev
# or
bun dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the result.

You can start editing the page by modifying `app/page.tsx`. The page auto-updates as you edit the file.

This project uses [`next/font`](https://nextjs.org/docs/app/building-your-application/optimizing/fonts) to automatically optimize and load [Geist](https://vercel.com/font), a new font family for Vercel.

## Learn More

To learn more about Next.js, take a look at the following resources:

- [Next.js Documentation](https://nextjs.org/docs) - learn about Next.js features and API.
- [Learn Next.js](https://nextjs.org/learn) - an interactive Next.js tutorial.

You can check out [the Next.js GitHub repository](https://github.com/vercel/next.js) - your feedback and contributions are welcome!

## Deploy on Vercel

The easiest way to deploy your Next.js app is to use the [Vercel Platform](https://vercel.com/new?utm_medium=default-template&filter=next.js&utm_source=create-next-app&utm_campaign=create-next-app-readme) from the creators of Next.js.

Check out our [Next.js deployment documentation](https://nextjs.org/docs/app/building-your-application/deploying) for more details.
