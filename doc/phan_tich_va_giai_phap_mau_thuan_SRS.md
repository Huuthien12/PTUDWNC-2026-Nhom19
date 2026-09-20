# BÁO CÁO PHÂN TÍCH VÀ PHƯƠNG ÁN XỬ LÝ 6 MÂU THUẪN TRONG SRS (CULINARY BLOG v1.0.0)

Tài liệu này tổng hợp phân tích chi tiết 6 điểm mâu thuẫn kỹ thuật trong tài liệu SRS, đánh giá ưu/nhược điểm của từng phương án xử lý và đưa ra quyết định chốt nhằm đảm bảo tính đồng bộ tuyệt đối giữa Functional Requirements (FR), Non-Functional Requirements (NFR), Data Model và API Convention.

---

## 1. Cơ chế Xóa Công thức Nấu ăn (Soft Delete ↔ Hard Delete)

### Mô tả mâu thuẫn
* **FR-RCP-007:** Mô tả thao tác xóa công thức là xóa vật lý (Hard Delete) khỏi cơ sở dữ liệu.
* **NFR-REL-003 & Data Model:** Yêu cầu áp dụng cơ chế Soft Delete (đánh dấu `IsDeleted = true`) để khôi phục khi cần và đảm bảo toàn vẹn dữ liệu liên quan (lượt thích, bình luận, lịch sử).

### Phân tích các phương án

| Phương án | Ưu điểm | Nhược điểm |
| :--- | :--- | :--- |
| **Phương án A: Soft Delete (`IsDeleted = true`)** *(Đã chọn)* | • Bảo toàn dữ liệu và toàn vẹn tham chiếu (Foreign Key).<br>• Dễ dàng khôi phục dữ liệu khi tác giả/Admin yêu cầu.<br>• Giữ nguyên lịch sử báo cáo và thống kê. | • Dung lượng database tăng theo thời gian (cần chính sách dọn dẹp riêng sau này). |
| **Phương án B: Hard Delete (`DELETE FROM`)** | • Giải phóng dung lượng lưu trữ ngay lập tức.<br>• Logic truy vấn đơn giản, không cần filter `IsDeleted = false`. | • Làm đứt gãy liên kết dữ liệu với bình luận, đánh giá, bookmark.<br>• Không thể khôi phục nếu xóa nhầm.<br>• Vi phạm tiêu chuẩn NFR-REL-003. |

**Quyết định:** Chọn **Phương án A (Soft Delete)**. Sửa FR-RCP-007 đồng bộ với NFR và Data Model.

---

## 2. Cơ chế Lưu Cực bộ & Bộ nhớ Đệm (Redis ↔ IMemoryCache)

### Mô tả mâu thuẫn
* **FR-CAT-001:** Yêu cầu dùng `IMemoryCache` với TTL 60 phút.
* **NFR-SCALE-001 & NFR-PERF-003:** Yêu cầu dùng `Redis Distributed Cache` cho toàn bộ hệ thống với TTL Danh mục là 30 phút.

### Phân tích các phương án

| Phương án | Ưu điểm | Nhược điểm |
| :--- | :--- | :--- |
| **Phương án A: Redis Distributed Cache (TTL 30m)** *(Đã chọn)* | • Thích hợp cho kiến trúc mở rộng đa node (Horizontal Scaling).<br>• Dữ liệu cache nhất quán trên tất cả instance của API.<br>• Không làm phình bộ nhớ RAM của App Server. | • Cần quản lý thêm hạ tầng Redis.<br>• Latency tăng nhẹ (vài ms) so với RAM local. |
| **Phương án B: In-Memory Cache (TTL 60m)** | • Tốc độ cực nhanh (đọc trực tiếp trên RAM ứng dụng).<br>• Đơn giản, không tốn chi phí hạ tầng. | • Bất đồng bộ dữ liệu giữa các instance khi Scale Out.<br>• Mất toàn bộ cache khi ứng dụng Restart. |

**Quyết định:** Chọn **Phương án A (Redis Cache, TTL 30m)** để đáp ứng khả năng mở rộng hệ thống.

---

## 3. Quy chuẩn Tham số Sắp xếp (sortBy + sortOrder ↔ sort)

### Mô tả mâu thuẫn
* **FR-RCP-001:** Sử dụng chuỗi gộp kiểu `sort=-createdAt`.
* **API Convention (Appendix):** Quy định tách biệt thành 2 tham số `sortBy` và `sortOrder`.

### Phân tích các phương án

| Phương án | Ưu điểm | Nhược điểm |
| :--- | :--- | :--- |
| **Phương án A: `sortBy` + `sortOrder`** *(Đã chọn)* | • Rõ ràng, tách biệt giữa trường cần sắp xếp và hướng sắp xếp.<br>• Tự động Model Binding dễ dàng trong ASP.NET Core.<br>• Đúng chuẩn API Convention của dự án. | • URL dài hơn một chút (`?sortBy=createdAt&sortOrder=desc`). |
| **Phương án B: Chuỗi đơn `sort` (e.g. `sort=-title`)** | • URL ngắn gọn. | • Cần viết code parse custom chuỗi (tách dấu `-` hoặc prefix).<br>• Không thống nhất với các endpoint khác trong Appendix. |

**Quyết định:** Chọn **Phương án A (`sortBy` + `sortOrder`)** theo chuẩn API Convention.

---

## 4. Thống nhất Mã Phản hồi Lỗi HTTP (HTTP Status Codes)

### Mô tả mâu thuẫn
* **Trong các FR:** Sử dụng chồng chéo giữa `400 Bad Request`, `409 Conflict`, và `422 Unprocessable Entity` cho các lỗi kiểm tra dữ liệu và xung đột trạng thái.
* **Appendix A:** Đã có bảng quy định chuẩn mã lỗi cho toàn hệ thống.

### Phân tích các phương án

| Phương án | Ưu điểm | Nhược điểm |
| :--- | :--- | :--- |
| **Phương án A: Thống nhất theo Appendix A** *(Đã chọn)*<br>• 400: Validation / Business rules<br>• 409: Duplicate / Unique Conflict<br>• 422: Optimistic Concurrency (`RowVersion`) | • Mã lỗi nhất quán, giúp Frontend dễ viết hàm xử lý ngoại lệ chung.<br>• Đạt chuẩn thiết kế RESTful API và RFC 7807.<br>• Phân định rõ lỗi dữ liệu nhập (400) và lỗi xung đột phiên bản (422). | • Tốn công rà soát và chỉnh sửa lại danh sách mã lỗi trong từng FR. |
| **Phương án B: Giữ nguyên mã lỗi riêng lẻ từng FR** | • Không cần sửa đổi lại tài liệu SRS. | • Khó khăn cho Frontend integration do mỗi API trả một kiểu mã lỗi khác nhau khi cùng vi phạm rule. |

**Quyết định:** Chọn **Phương án A (Chuẩn hóa toàn bộ theo Appendix A)**.

---

## 5. Ràng buộc Điều kiện Xuất bản Công thức (Publish Recipe Validation)

### Mô tả mâu thuẫn
* **FR-RCP-005:** Chỉ kiểm tra danh sách bước thực hiện `Steps.Count > 0`.
* **Application Error Code:** `RECIPE_PUBLISH_INCOMPLETE` quy định phải có cả `Ingredients.Count >= 1` và `Steps.Count >= 1`.

### Phân tích các phương án

| Phương án | Ưu điểm | Nhược điểm |
| :--- | :--- | :--- |
| **Phương án A: Yêu cầu cả Ingredient ≥ 1 AND Step ≥ 1** *(Đã chọn)* | • Đảm bảo chất lượng nội dung công thức khi công khai.<br>• Phù hợp với thực tế ứng dụng nấu ăn.<br>• Khớp với Error Code `RECIPE_PUBLISH_INCOMPLETE`. | • Yêu cầu người dùng nhập đầy đủ dữ liệu trước khi bấm xuất bản. |
| **Phương án B: Chỉ yêu cầu Step ≥ 1** | • Quy trình đơn giản hơn cho người tạo bài viết. | • Tạo ra các công thức "rác" không có định lượng nguyên liệu.<br>• Sai lệch với bảng Error Code đã ban hành. |

**Quyết định:** Chọn **Phương án A (Yêu cầu cả Ingredient và Step)**.

---

## 6. Độ dài và Định dạng Refresh Token (128-bit ↔ 512-bit)

### Mô tả mâu thuẫn
* **FR-AUTH-002:** Ghi tạo Refresh Token ngẫu nhiên 512-bit.
* **NFR-SEC-002 & Data Model:** Yêu cầu dùng 128-bit cryptographically secure random bytes, băm SHA-256 trước khi lưu Database.

### Phân tích các phương án

| Phương án | Ưu điểm | Nhược điểm |
| :--- | :--- | :--- |
| **Phương án A: 128-bit Random Bytes (SHA-256 Hash)** *(Đã chọn)* | • Đạt chuẩn an toàn OWASP cho Token Entropy.<br>• Kích thước chuỗi vừa phải, tối ưu băng thông truyền tải.<br>• Đồng bộ hoàn toàn với NFR-SEC-002 và Data Model. | • Không có. |
| **Phương án B: 512-bit Random Bytes** | • Độ ngẫu nhiên cực cao. | • Kích thước quá lớn không cần thiết.<br>• Dư thừa về mặt kỹ thuật khi đã áp dụng băm SHA-256. |

**Quyết định:** Chọn **Phương án A (128-bit Random Bytes)**.

---

## BẢNG TỔNG HỢP QUYẾT ĐỊNH CUỐI CÙNG

| STT | Hạng mục mâu thuẫn | Quy chuẩn được duyệt áp dụng | File tài liệu cần cập nhật |
| :---: | :--- | :--- | :--- |
| 1 | **Xóa Công thức** | **Soft Delete** (`IsDeleted = true`) | `FR-RCP-007` |
| 2 | **Caching** | **Redis Cache** (Category TTL = 30 phút) | `FR-CAT-001` |
| 3 | **Sorting Parameter** | **`sortBy`** và **`sortOrder`** | `FR-RCP-001` |
| 4 | **HTTP Status Code** | **Chuẩn Appendix A** (400 / 409 / 422) | Tất cả các `FR` |
| 5 | **Publish Validation** | **`Ingredients >= 1` AND `Steps >= 1`** | `FR-RCP-005` |
| 6 | **Refresh Token** | **128-bit** + SHA-256 Hash | `FR-AUTH-001`, `FR-AUTH-002` |