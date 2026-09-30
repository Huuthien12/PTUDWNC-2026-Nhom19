# Member 3 — Recipe contract, buổi 3 đợt 1

> Bổ sung đợt 4: FR-RCP-004 đã triển khai theo
> [recipe_update_member3.md](recipe_update_member3.md). Update giữ nguyên slug khi
> title đổi; nutrition bỏ qua/null giữ nguyên, object thay thế bốn giá trị (`{}`
> xóa giá trị). RowVersion trong body được persistence kiểm tra, stale trả 422;
> response trả trực tiếp RecipeDto với version mới. Các đoạn “dự kiến/chưa triển
> khai” bên dưới mô tả thời điểm chốt contract đợt 1.

Phạm vi: request/response DTO và FluentValidation cho FR-RCP-003/004, chưa có
endpoint, command/handler, persistence hay frontend. Đây là contract mutation;
không thay thế detail DTO của Member 2. Validators được assembly scanning hiện
có phát hiện, nhưng chưa có request dispatch: handler/command sau này phải gọi
hoặc tái sử dụng validator tương ứng; pipeline không tự validate DTO lồng nhau.

## Quyết định SRS để nhóm review

Căn cứ [FR-RCP-003/004 và model 7.2, concurrency 6.4](SRS_Culinary_Blog_v1.0.2.md)
và mục 4 của [tài liệu giải quyết mâu thuẫn](phan_tich_va_giai_phap_mau_thuan_SRS.md).

- JSON dùng `prepTime/cookTime` (phút), theo model và FR Update. Đây là khác biệt
  có chủ đích với `prepTimeMinutes/cookTimeMinutes` trong body ví dụ FR Create;
  không cung cấp alias cho hai tên cũ.
- Create: prepTime, cookTime, servings > 0 theo FR-RCP-003, ưu tiên quy tắc
  nghiệp vụ cụ thể hơn model CookTime >= 0.
- Update: FR-RCP-004 không nêu range riêng. Dùng ràng buộc model 7.2:
  prepTime > 0, cookTime >= 0, servings > 0; không suy diễn > 0 từ Create.
- Title không được trắng, dài 5–200 ký tự .NET, kiểm tra nguyên giá trị gửi lên
  (validator không trim/mutate). Description không null theo model; cho phép
  rỗng vì SRS không đặt minimum. Instructions tùy chọn ở Create (handler sau
  này chuẩn hóa null thành chuỗi rỗng); Update không null, được phép rỗng.
- CategoryId là Guid khác Empty; GUID sai cú pháp phải bị JSON binding từ chối.
  Difficulty dùng enum số hiện tại: Easy=1, Medium=2, Hard=3.
- Nutrition tùy chọn; mỗi calories/protein/carbs/fat là decimal nullable.
  Null là chưa cung cấp, 0 là giá trị thật. Khi có giá trị phải >= 0. Quy tắc
  không âm là quyết định triển khai để review, không phải range được SRS ghi rõ.
  Không tự đặt max/precision vì model chỉ ghi numeric nullable.
- Update nutrition bị bỏ qua/null: đề xuất giữ nguyên theo câu “cập nhật nếu có”.
  Object được cung cấp thay thế bốn giá trị; `{}` hoặc cả bốn null xóa các giá trị.
  Đây là semantics dự kiến cho handler, chưa được thực thi trong đợt này.
- RowVersion chọn truyền trong body dạng Base64 chuẩn, không rỗng, không có
  khoảng trắng/dấu quote ETag; không cố định số byte vì SRS không quy định.
  Response cũng dùng Base64. Validator chỉ kiểm tra encoding; stale token phải
  được persistence kiểm tra sau. Chưa hỗ trợ If-Match ở đợt contract này.
- AuthorId lấy từ claims, Status Create luôn Draft, Slug sinh tại server.
  Requests không khai báo ba field này; handler không được bind trực tiếp entity.
  Việc từ chối JSON field lạ chưa được cấu hình; mặc định serializer có thể bỏ qua.
- Create chọn nhánh thêm ingredients/steps riêng sau, được FR-RCP-003 cho phép;
  contract này chưa nhận children. Update không nhận children hay lifecycle status.
- HTTP dự kiến: validation 400, duplicate slug 409, concurrency 422 theo quy chuẩn
  đã giải quyết; không dùng các mã mâu thuẫn trong đoạn FR cũ.
- RecipeDto dưới đây là payload mutation. Envelope `{data,...}` của chương 8
  cần thống nhất khi viết endpoint (API hiện tại trả DTO trực tiếp); đợt này chưa
  quyết định envelope, chính sách đổi slug khi sửa title, hay triển khai HTTP.

## JSON dự kiến

Create body:

```json
{
  "title": "Canh chua",
  "description": "Canh chua ca",
  "categoryId": "11111111-1111-1111-1111-111111111111",
  "prepTime": 10,
  "cookTime": 20,
  "servings": 2,
  "difficulty": 1,
  "instructions": "Nau chin ca",
  "nutrition": { "calories": 150, "protein": 12.5, "carbs": null, "fat": 0 }
}
```

Update body cùng các field trên, bắt buộc instructions và thêm
`"rowVersion": "AQIDBA=="`. Recipe ID lấy từ route, không có trong body.
Update cho phép cookTime=0; Create từ chối 0.

RecipeDto dự kiến cho 201 Create / 200 Update:

```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "title": "Canh chua",
  "slug": "canh-chua",
  "description": "Canh chua ca",
  "instructions": "Nau chin ca",
  "categoryId": "11111111-1111-1111-1111-111111111111",
  "authorId": "identity-user-id",
  "prepTime": 10,
  "cookTime": 20,
  "servings": 2,
  "difficulty": 1,
  "status": 1,
  "nutrition": { "calories": 150, "protein": 12.5, "carbs": null, "fat": 0 },
  "rowVersion": "AQIDBA==",
  "createdAt": "2026-09-27T00:00:00Z",
  "updatedAt": "2026-09-27T00:00:00Z",
  "publishedAt": null
}
```

Nutrition response luôn có object, các giá trị có thể null, khớp owned navigation
bắt buộc của entity hiện tại. RowVersion trong ví dụ chỉ minh họa encoding;
chưa có cơ chế sinh/đổi token. Enum status hiện tại: Draft=1, Published=2, Archived=3.

## Trách nhiệm đợt sau

Handler kiểm tra Category tồn tại, owner/Admin, slug unique, mapping và
transaction/concurrency; API chuyển validation thành 400 ProblemDetails và stale
version thành 422. Không có database/service dependency trong validator.

Kiểm chứng: `dotnet build backend/CulinaryBlog.slnx` và
`dotnet test backend/CulinaryBlog.slnx --no-build`.
RecipeContractTests chỉ kiểm tra contract/validation/JSON, không chứng minh API,
quyền truy cập hay PostgreSQL concurrency hoạt động.
