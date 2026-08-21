# UDM10 - Hệ Thống Client/Server Upload File

Hệ thống cho phép nhiều Client kết nối và truyền tải file đến Server thông qua giao thức TCP. Được thiết kế với khả năng điều phối bất đồng bộ, hỗ trợ truyền file dung lượng lớn và phục hồi khi có sự cố mạng.

## 1. Kiến trúc Hệ Thống
Hệ thống sử dụng mô hình **Client - Server** kết nối qua TCP/IP.
*   **UDM10.Client:** Ứng dụng WPF cho phép chọn file, theo dõi tiến độ (Progress, Speed) và điều khiển luồng (Cancel/Retry). Hỗ trợ tối đa 3 tiến trình upload đồng thời.
*   **UDM10.Server:** Ứng dụng Console chịu trách nhiệm lắng nghe kết nối, xác thực metadata, lưu trữ file theo dạng chunk (mảnh) và quản lý tài nguyên.
*   **UDM10.Shared:** Class Library chứa giao thức giao tiếp chung (Protocol v3), hằng số và cấu trúc dữ liệu.

## 2. Cấu hình (appsettings.json)
Cả Client và Server đều sử dụng cấu hình mềm, không hard-code. Trước khi chạy, vui lòng kiểm tra file `appsettings.json` ở từng project.

**Cấu hình Server:**
*   `Port`: Mặc định là `9000`.
*   `ChunkSize`: Mặc định `65536` (64 KB).
*   `ConcurrentLimit`: Giới hạn Client đồng thời.
*   `UploadDirectory`: Thư mục lưu file mặc định là `Uploads`.

**Cấu hình Client:**
*   `ServerIp`: IP của Server (mặc định `127.0.0.1` nếu chạy local).
*   `ServerPort`: Phải khớp với port của Server (`9000`).

## 3. Giao thức truyền tải (Protocol v3)
Giao thức định nghĩa cấu trúc Request/Response bằng chuỗi JSON, sử dụng kỹ thuật **Message Framing** (4 byte đầu tiên chứa độ dài bản tin) để tránh tình trạng dính/cắt gói tin TCP.

**Luồng hoạt động (Lifecycle):**
1.  **Request:** Client gửi `UploadRequest` chứa Metadata (FileName, FileSize, RequestId, ProtocolVersion).
2.  **Validate:** Server sử dụng `MetadataValidator` kiểm tra tính hợp lệ.
3.  **Ready/Error:** Server phản hồi `UploadResponse`. Nếu hợp lệ (Trạng thái `Ready`), Client bắt đầu gửi byte file.
4.  **Chunk Transfer:** Dữ liệu file được băm thành các chunk (64KB) để truyền đi.
5.  **Completed:** Server xác nhận đủ byte và đổi tên file tạm `.part` thành file chính thức. Xử lý tự động thêm `_1`, `_2` nếu trùng tên.

**Mã lỗi (ErrorCode):**
*   `0`: None (Không có lỗi)
*   `2`: InvalidMetadata (Metadata sai cấu trúc)
*   `4`: FileSizeInvalid (Kích thước file không hợp lệ)
*   `5`: ProtocolVersionMismatch (Không khớp phiên bản giao thức)
*   `10`: CancelledByUser (Người dùng chủ động hủy)