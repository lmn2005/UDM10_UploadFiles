## 3. Giao thức truyền tải (Protocol v3)

Protocol v3 kế thừa cơ chế truyền file của Protocol v2 và bổ sung
cơ chế quản lý phiên upload bằng `RequestId`, xử lý `CANCEL`,
`RETRY`, chuẩn hóa `ErrorCode` và kiểm tra tính hợp lệ của request.

### 3.1. Cấu trúc Request

Client gửi `UploadRequest` dưới dạng JSON, gồm các thông tin:

- `ProtocolVersion`: Phiên bản giao thức, hiện tại là `V3`.
- `RequestId`: Mã định danh duy nhất cho mỗi phiên upload.
- `FileName`: Tên file cần upload.
- `FileSize`: Kích thước file.
- `FileHash`: Hash dùng để kiểm tra tính toàn vẹn.
- `Status`: Trạng thái của request (`Request`, `Retry` hoặc `Cancel`).

### 3.2. Validation

Server kiểm tra request trước khi bắt đầu upload:

1. Kiểm tra `ProtocolVersion` có đúng `V3`.
2. Kiểm tra `RequestId` không được rỗng.
3. Kiểm tra `Status` có được Protocol v3 hỗ trợ.
4. Kiểm tra `FileName` không rỗng.
5. Kiểm tra `FileSize` hợp lệ và không vượt quá giới hạn cấu hình.

Nếu request không hợp lệ, Server trả về:

- `Status = Error`
- `ErrorCode` tương ứng
- `ErrorMessage` mô tả nguyên nhân
- `RequestId` của request nếu đã xác định được.

### 3.3. Lifecycle

Luồng upload thông thường:


Client
  |
  | Request
  | ProtocolVersion = V3
  | RequestId = GUID
  v
Server
  |
  | Validation
  |
  +---- Invalid ----> Error + ErrorCode
  |
  +---- Valid ------> Ready
                         |
                         v
                    File Transfer
                         |
                         v
                      Completed