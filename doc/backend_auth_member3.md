# Backend buổi 2 — Member 3 (2312764)

Phạm vi: FR-AUTH-004, FR-AUTH-005 và token rotation; đối chiếu SRS v1.0.2
với các sai khác/diễn giải được ghi rõ bên dưới, chưa tuân thủ đầy đủ mọi mục SRS.
Login của Member 2 được mở rộng tối thiểu để phát hành refresh token; giữ nguyên
xác thực email/password, JWT signing, claims và cấu hình JWT hiện tại.

## API

### Login

`POST /api/v1/auth/login`

```json
{ "email": "author@example.com", "password": "<password>" }
```

HTTP 200 dùng chung `AuthResponseDto` với Refresh; ví dụ minh họa giá trị:

```json
{
  "accessToken": "<jwt>",
  "tokenType": "Bearer",
  "refreshToken": "<opaque-token>",
  "expiresAt": "2026-09-27T10:15:00Z",
  "user": {
    "id": "<identity-user-id>",
    "fullName": "Test Author",
    "email": "author@example.com",
    "userName": "test.author",
    "avatarUrl": null,
    "roles": ["Author"]
  }
}
```

`accessToken`, `refreshToken`, `tokenType` là chuỗi. `expiresAt` là thời điểm
hết hạn **access token**, DateTime UTC được serialize thành chuỗi ISO 8601 có
hậu tố `Z`, khớp chính xác claim `exp` của JWT (độ chính xác giây); không phải
hạn refresh token. SRS nêu tên field nhưng không ấn định format JSON thời gian;
ISO 8601 UTC là cách serialize của implementation.

`user.id`, `fullName`, `email`, `userName` là chuỗi lấy từ ApplicationUser đã
xác thực; id giữ kiểu string của Identity. `avatarUrl` là chuỗi hoặc null;
`roles` là mảng chuỗi lấy từ UserManager, có thể rỗng, không tự gán role.
`tokenType` giữ từ contract cũ để tương thích; SRS không yêu cầu field này.

### Refresh

`POST /api/v1/auth/refresh`, không cần Authorization header.

```json
{ "refreshToken": "<current-refresh-token>" }
```

HTTP 200 có cùng năm field và cấu trúc `user` như Login. Client phải thay thế cả hai token.
Profile/roles phản ánh dữ liệu Identity được đọc trong request refresh hiện tại,
không sao chép từ JWT cũ. JWT service trả lại thời điểm exp của chính token mới;
không có luồng phát JWT thứ hai hoặc truy vấn user bổ sung để dựng response.
Access token dùng thời hạn cấu hình JWT hiện tại (mặc định 15 phút);
refresh token mới có hạn 7 ngày kể từ lần cấp mới.

Lỗi dùng Problem Details, mã trong `type` theo convention code hiện tại:

| Trường hợp | HTTP | type |
|---|---|---|
| Thiếu/rỗng refreshToken | 400 | VALIDATION_ERROR |
| Token không tồn tại/đã soft delete hoặc hash trùng nhiều bản ghi | 401 | AUTH_TOKEN_INVALID |
| Hết hạn, bao gồm đúng thời điểm ExpiresAt | 401 | AUTH_REFRESH_TOKEN_EXPIRED |
| Đã revoke/đã thay thế/thua tranh chấp cập nhật | 401 | AUTH_REFRESH_TOKEN_REVOKED |
| User không còn tồn tại, inactive hoặc lockout | 401 | AUTH_ACCOUNT_DISABLED |

Nếu user bị xóa và FK cascade xóa token, kết quả là AUTH_TOKEN_INVALID.
Malformed JSON được Minimal API từ chối bằng HTTP 400.

### Logout

`POST /api/v1/auth/logout`

```http
Authorization: Bearer <access-token>
Content-Type: application/json
```

```json
{ "refreshToken": "<current-refresh-token>" }
```

- Access token hợp lệ: revoke token thuộc user; HTTP 204 không body.
  Token không tồn tại, đã revoke hoặc thuộc user khác cũng trả 204, không sửa token của user khác.
  Thiếu/rỗng refreshToken trả 400 VALIDATION_ERROR.
- Theo FR-AUTH-005 A2, access token hết hạn vẫn logout được nếu chữ ký,
  issuer, audience và not-before hợp lệ, refresh token còn hiệu lực, thuộc cùng
  user và user còn active/không lockout. Trường hợp này chỉ bỏ qua hạn exp;
  dùng lại token đã revoke hoặc thiếu/sai refresh token trả 401 AUTH_TOKEN_INVALID.
- Thiếu access token, token giả mạo hoặc sai issuer/audience trả 401.
- Access JWT vẫn có hiệu lực đến khi hết hạn. Frontend phải xóa trạng thái đăng nhập.
  Logout thu hồi token được gửi, không thu hồi mọi phiên hoặc mọi token kế nhiệm.
  Sau rotation phải gửi refresh token mới nhất.

## Rotation và persistence

- RefreshTokenGenerator tạo 16 byte (128 bit) CSPRNG, mã hóa Base64 cho client.
  Các bản ghi mới chỉ lưu SHA-256 hex chữ HOA trong `Token` và `ReplacedByToken`.
  Điều này không chứng minh dữ liệu cũ trong database đã là hash.
- Application dùng MediatR commands; Infrastructure cung cấp store, Identity,
  generator và JwtTokenService hiện có. Không có luồng phát JWT thứ hai.
- Rotation cập nhật có điều kiện `!IsRevoked`, chưa thay thế, chưa hết hạn
  tại mốc `now` của request,
  chưa soft delete; ghi IsRevoked/RevokedAt/ReplacedByToken và insert replacement
  trong một transaction. Hai request cùng token chỉ một request thành công.
  Insert lỗi sẽ rollback cả việc revoke token cũ.
- Giữ bản ghi cũ để phát hiện reuse. Reuse trả 401 và log WARNING chỉ chứa ID
  bản ghi, không log raw token/hash. Không bật paranoid mode thu hồi mọi phiên.
- Logout dùng conditional UPDATE; nếu logout thắng trước rotation thì rotation
  không thể tiêu thụ token đã bị revoke.

## Migration và kiểm thử

Migration `AddRefreshTokenRevokedAt` chỉ thêm cột nullable `RevokedAt` vào
RefreshTokens. Snapshot được cập nhật; không đổi hoặc xóa dữ liệu cũ.
`AppDbContextDesignFactory` dùng cấu hình offline để EF tooling không chạy
API startup migration/seed. Các lệnh EF truy cập database cần chỉ định kết nối
đích riêng; không dùng connection mặc định của factory.

Đã đối chiếu lịch sử và schema, áp thành công migration AddRefreshTokenRevokedAt trên CulinaryBlogDb_Thuan_Main trong kiểm thử tích hợp local buổi 2. Database cũ CulinaryBlogDb không bị thay đổi.
Trước khi áp hoặc chạy API local, phải xác nhận đúng database và đọc
`__EFMigrationsHistory`, đối chiếu migration trong repo. API hiện tự migrate/seed
khi startup ngoài môi trường `Testing`.

```powershell
dotnet restore backend/CulinaryBlog.slnx
dotnet build backend/CulinaryBlog.slnx --no-restore
dotnet test backend/CulinaryBlog.slnx --no-build --no-restore
dotnet ef migrations has-pending-model-changes --project backend/CulinaryBlog.Infrastructure --startup-project backend/CulinaryBlog.API --context AppDbContext
```

Tests chạy HTTP thật trong TestServer với JWT middleware, Identity và production
handlers/store. SQLite file tạm chỉ map auth entities; không kết nối PostgreSQL,
Redis, MinIO hay OCOP. Test relational kiểm tra CAS/concurrency và rollback.
Kết quả kiểm thử tự động trong repo: **31 backend test và 22 frontend test pass**; backend và frontend build đạt. Frontend tests dùng Node test runner với fetch/localStorage/Web Locks giả lập, không phải E2E trình duyệt.

### Kiểm thử tích hợp local đã thực hiện

Các kết quả sau dùng API/PostgreSQL thật trên CulinaryBlogDb_Thuan_Main và Edge, tách biệt với bộ test tự động trong repo:

- Login **200** → Refresh **200** → dùng lại token cũ **401** → Logout **204** → refresh sau logout **401**.
- Hai request refresh đồng thời cùng token cho **một 200 và một 401**.
- CORS cho phép http://localhost:3000, không cấp quyền cho origin ngoài danh sách.
- Trên Edge: guest chuyển về Login; user không có role Admin bị chặn; hai tab đồng bộ refresh/logout; refresh thất bại xóa phiên.

Chưa thử luồng Admin thành công vì database chưa có role Admin; chưa có E2E tự động trong repo; chưa kiểm thử PostgreSQL lock timeout/rollback thực tế.

## Tích hợp frontend và phần còn thiếu

Frontend đã lưu phiên, thay cả cặp token sau rotation, điều phối refresh trong một tab và giữa các tab, tự refresh/retry một lần, xử lý lỗi refresh, logout bằng cặp mới nhất, xóa trạng thái client và bảo vệ route cơ bản. Chi tiết tại [frontend/README.md](../frontend/README.md).

Phần còn thiếu ngoài phạm vi:

- Tích hợp tương tự vào Register/Google OAuth khi các luồng đó cấp token.
- Lockout/validation/error contract riêng của Login hiện tại vẫn thuộc Member 2;
  thay đổi này không triển khai lại FR-AUTH-002.

## Review contract so với SRS

| Mục SRS | Code hiện tại / phần còn thiếu |
|---|---|
| FR-AUTH-001 định nghĩa AuthResponseDto; FR-AUTH-002/004 và 8.2 tham chiếu DTO này | Login/Refresh đã dùng chung DTO với đủ `accessToken`, `refreshToken`, `expiresAt`, `user: { id, fullName, email, userName, avatarUrl, roles }`. Giữ thêm `tokenType` từ Login cũ; field bổ sung này không được SRS yêu cầu. |
| FR-AUTH-002 | Code Login cũ chưa kiểm tra lockout, tăng/reset AccessFailedCount, trả 423, hoặc validate email bằng pipeline; mã `INVALID_CREDENTIALS`/`USER_INACTIVE` khác Phụ lục B. Không sửa lại Login trong review này. |
| NFR-SEC-002 và FR-AUTH-004 | Refresh 7 ngày, 128-bit CSPRNG, hash và rotation đã có. SRS yêu cầu access 15 phút; code JWT cho phép cấu hình khác 15, vì vậy chỉ khớp thời hạn SRS khi cấu hình đúng. |
| FR-AUTH-004 A4 và Phụ lục B | Nội bộ SRS chưa đồng nhất: A4 yêu cầu 401 khi user bị khóa/xóa; B gán AUTH_ACCOUNT_DISABLED cho 403. Hiện giữ 401 theo A4 và dùng mã AUTH_ACCOUNT_DISABLED; không coi cặp mã/status này là được SRS quy định thống nhất. |
| 7.8 RefreshTokens | SRS yêu cầu `TokenHash varchar(64) UNIQUE` và `CreatedByIp nullable`. Schema hiện vẫn `Token text`, không unique, thiếu CreatedByIp; migration mới chỉ thêm RevokedAt. |
| 8.9 Problem Details | Có type/title/status; endpoints mới chưa bổ sung đồng bộ `instance`, `traceId`, `errors` như ví dụ SRS. |

Các lựa chọn triển khai không được SRS quy định cụ thể:

- Base64 cho token thô, hex chữ HOA cho hash và lưu hash trong ReplacedByToken;
  SRS chỉ yêu cầu entropy, SHA-256 trước DB và liên kết replacement.
- SRS không có mã riêng cho refresh token không tồn tại; dùng AUTH_TOKEN_INVALID
  là quy ước triển khai (Phụ lục B mô tả mã này cho access token).
- HTTP 400 cho field thiếu/rỗng dựa trên Phụ lục A; FR-AUTH-004/005 không liệt kê
  đầy đủ validation này. Ưu tiên báo revoked trước expired và từ chối hash trùng
  cũng là quyết định triển khai.
- FR-AUTH-005 A2 cho phép logout bằng refresh token hợp lệ khi access hết hạn,
  nhưng không đặc tả thuật toán. Yêu cầu vẫn gửi JWT hết hạn đã xác minh chữ ký,
  cùng chủ sở hữu và user active/không lockout là cách triển khai đã chọn.
- HTTP 204 khi token thuộc user khác/đã revoke mở rộng tính idempotent và che
  trạng thái; SRS chỉ nêu rõ trường hợp không tìm thấy, và yêu cầu kiểm tra chủ sở hữu.
- Revoke mọi phiên khi reuse là **tùy chọn** của FR-AUTH-004 A3, không bắt buộc.
  Thu hồi cả chuỗi token kế nhiệm khi logout không được SRS yêu cầu.

## Review dữ liệu cũ và schema

Migration `AddIdentityAndRefreshTokens` tạo `Token text NOT NULL`,
`ReplacedByToken text NULL`; không giới hạn độ dài hay format và không có
unique/index trên Token (chỉ PK Id và index UserId). Theo schema này, dữ liệu
cũ có thể là plaintext, hash nhiều định dạng, chuỗi rỗng, giá trị trùng hoặc
chuỗi dài tùy ý. Code Login ở HEAD trước thay đổi không ghi refresh token,
nhưng không thể suy ra database rỗng hoặc chưa có dữ liệu do seed/script khác.

Migration thêm RevokedAt không hash, rename, backfill hoặc revoke dữ liệu cũ.
Lookup mới luôn so `SHA256(raw)` dạng hex HOA với Token:

- Plaintext cũ thông thường không khớp: refresh trả 401 và phải đăng nhập lại.
  Logout dùng access hợp lệ có thể trả 204 do không tìm thấy hash, nhưng không
  đánh dấu bản ghi plaintext cũ là revoked. Bản ghi đó không dùng được với lookup mới.
- Hash cũ đúng thuật toán/encoding/casing vẫn dùng được nếu còn hiệu lực;
  hex thường hoặc Base64 digest không tương thích với phép so sánh hiện tại.
- Hash trùng bị từ chối 401 thay vì SingleOrDefault gây 500; không chọn tùy ý
  một bản ghi. Đây không thay thế được unique constraint trong database.
- Không thêm fallback plaintext hoặc tự hash mọi dòng: có thể double-hash dữ
  liệu đã băm và không thể xác định định dạng đáng tin chỉ từ độ dài chuỗi.

`text` chứa được digest 64 ký tự nhưng chưa bảo đảm SRS 7.8 và lookup có thể
quét bảng. Đích phù hợp là TokenHash varchar(64) + unique index sau khi có kế
hoạch xử lý dữ liệu legacy. Thu hẹp cột có thể lỗi hoặc cắt dữ liệu tùy SQL;
thêm unique có thể thất bại vì dữ liệu trùng. Không tạo migration ép chuyển đổi,
không sửa migration lịch sử, không tự xóa/revoke dữ liệu để làm index chạy được.
Đã đối chiếu lịch sử và schema thực tế của CulinaryBlogDb_Thuan_Main với migration repo trước khi áp AddRefreshTokenRevokedAt. RefreshTokens ban đầu có 0 dòng, không có token legacy, giá trị trùng hoặc sai định dạng hash cần xử lý trên database này. Kết quả này không thay thế việc kiểm tra database khác.

## Review concurrency trên PostgreSQL/Npgsql

Kiểm thử tích hợp local trên PostgreSQL thật đã xác nhận hai request refresh đồng thời cùng token cho một 200 và một 401. Phân tích dưới đây giải thích cơ chế; các nhánh lỗi database chưa được kiểm thử đầy đủ trên PostgreSQL.
[Npgsql BeginTransactionAsync](https://www.npgsql.org/doc/api/Npgsql.NpgsqlConnection.html)
mặc định ReadCommitted. Theo
[PostgreSQL 16, Read Committed](https://www.postgresql.org/docs/16/transaction-iso.html#XACT-READ-COMMITTED),
UPDATE tranh cùng hàng chờ transaction trước rồi kiểm tra lại WHERE trên hàng
đã commit. Với một bản ghi token:

1. A cập nhật theo PK và điều kiện còn active; B chờ khóa hàng.
2. A insert replacement và commit; B kiểm tra lại thấy IsRevoked=true, cập nhật
   0 hàng, không insert replacement và trả 401. Hai request không cùng thành công.
3. Nếu A lỗi trước commit, transaction rollback cả revoke và insert; B có thể
   thành công. Token cũ còn active sau rollback là kết quả đúng của tính nguyên tử.

[EF ExecuteUpdate](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete)
không tự kiểm tra concurrency token; code kiểm tra số hàng cập nhật. FindAsync
dùng AsNoTracking, SaveChanges chỉ insert replacement nên không ghi đè lại trạng
thái token cũ từ entity đã load. Race thông thường không sinh DbUpdateConcurrencyException.

Không có unique index trên Token nên race này không thể gây lỗi unique Token
trong schema repo. Vẫn có thể gặp lỗi PK/FK, timeout, deadlock, mất kết nối,
hoặc unique violation nếu schema thực tế có thêm constraint. Code chưa chuyển
những lỗi database này thành lỗi nghiệp vụ; có thể trả 500. Không bắt mọi lỗi rồi
gán thành reuse: rollback/commit không rõ kết quả khi mất kết nối phải được xử lý
riêng. Nếu thêm unique index sau này, cần test collision/retry có giới hạn.

Giới hạn: SQLite tests chứng minh nhánh xử lý/rollback trên SQLite; kiểm thử local PostgreSQL xác nhận rotation và hai request đồng thời nêu trên. Chưa có bộ test PostgreSQL tự động trong repo; chưa kiểm thử PostgreSQL lock timeout/rollback thực tế hoặc lỗi uniqueness. Không suy rộng kết quả SQLite thành bằng chứng xử lý mọi lỗi SQLSTATE của Npgsql.
Không đổi isolation sang RepeatableRead/Serializable mà không thiết kế retry.
Kiểm tra user active và thời hạn dùng dữ liệu/mốc thời gian đọc trước UPDATE,
không khóa user để loại trừ việc user bị khóa đồng thời.

Logout thắng trước rotation chặn rotation; nếu rotation commit trước, logout
token cũ trả 204 nhưng không revoke replacement. Không còn dùng được token cũ,
nhưng phiên dùng token kế nhiệm vẫn sống. Đây là giới hạn revoke-token-được-gửi,
không phải thu hồi toàn phiên; SRS không định nghĩa thứ tự race này.
