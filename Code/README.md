# UDM10 - Hệ Thống Client/Server Upload File

Hệ thống cho phép nhiều Client kết nối và truyền tải file đến Server thông qua giao thức TCP. Được thiết kế với khả năng điều phối bất đồng bộ, hỗ trợ truyền file dung lượng lớn và phục hồi khi có sự cố mạng.

## 1. Kiến trúc Hệ Thống
Hệ thống sử dụng mô hình **Client - Server** kết nối qua TCP/IP.
*   **UDM10.Client:** Ứng dụng WPF cho phép chọn file, theo dõi tiến độ (Progress, Speed) và điều khiển luồng (Cancel/Retry). Hỗ trợ tối đa 3 tiến trình upload đồng thời.
*   **UDM10.Server:** Ứng dụng Console chịu trách nhiệm lắng nghe kết nối, xác thực metadata, lưu trữ file theo dạng chunk (mảnh) và quản lý tài nguyên.
*   **UDM10.Shared:** Class Library chứa giao thức giao tiếp chung (Protocol v3), hằng số và cấu trúc dữ liệu.

## 2. Cấu hình (appsettings.json)
Cả Client và Server đều sử dụng cấu hình mềm, không hard-code. Trước khi chạy, vui lòng kiểm tra file `appsettings.json` ở từng project.

**Cấu hình Server (`Server/appsettings.json`):**
*   `Network:ServerIp`: `0.0.0.0` để nhận kết nối từ máy khác trong LAN.
*   `Network:Port`: Mặc định là `9000`.
*   `Network:ReceiveTimeoutMs`: Idle timeout cho từng lần đọc dữ liệu.
*   `Upload:ChunkSizeBytes`: Mặc định `65536` (64 KB).
*   `Upload:SaveDirectory`: Thư mục lưu file mặc định là `Uploads`.
*   `Upload:MaxAllowedSizeInBytes`: Kích thước file tối đa Server chấp nhận.

**Cấu hình Client (`Client/appsettings.json`):**
*   `Network:ServerIp`: IP của Server (`127.0.0.1` nếu chạy cùng máy).
*   `Network:Port`: Phải khớp `Network:Port` của Server.
*   `Network:ConnectTimeoutMs` và `Network:ReceiveTimeoutMs`: timeout kết nối/phản hồi.
*   `Upload:ChunkSizeBytes`: Kích thước chunk Client gửi.
*   `Upload:MaxConcurrentFiles`: Số file gửi đồng thời, luôn được giới hạn tối đa là 3.

## 3. Giao thức truyền tải (Protocol v3)
Giao thức định nghĩa cấu trúc Request/Response bằng chuỗi JSON, sử dụng kỹ thuật **Message Framing** (4 byte đầu tiên chứa độ dài bản tin) để tránh tình trạng dính/cắt gói tin TCP.

**Luồng hoạt động (Lifecycle):**
1.  **Request:** Client gửi `UploadRequest` chứa FileName, FileSize, SHA-256 FileHash, RequestId và ProtocolVersion.
2.  **Validate:** Server sử dụng `MetadataValidator` kiểm tra tính hợp lệ.
3.  **Ready/Error:** Server phản hồi `UploadResponse`. Nếu hợp lệ (Trạng thái `Ready`), Client bắt đầu gửi byte file.
4.  **Chunk Transfer:** Dữ liệu file được chia thành các chunk để truyền; Server chỉ đọc đúng `FileSize` byte.
5.  **Integrity:** Server tính lại SHA-256 và so sánh với `FileHash` trước khi công nhận file.
6.  **Completed:** Server đổi `.part` thành file chính thức. File trùng tên được thêm `_1`, `_2` mà không ghi đè file đã hoàn thành.

**Mã lỗi (ErrorCode):**
*   `0`: None (Không có lỗi)
*   `2`: InvalidMetadata (Metadata sai cấu trúc)
*   `4`: FileSizeInvalid (Kích thước file không hợp lệ)
*   `5`: ProtocolVersionMismatch (Không khớp phiên bản giao thức)
*   `10`: CancelledByUser (Người dùng chủ động hủy)
*   `11`: ChecksumMismatch (SHA-256 không khớp)

## 4. Build và chạy Release Candidate

Môi trường thống nhất của đồ án: Windows 10/11 hoặc Windows VM, cài .NET 10 SDK. Client sử dụng WPF nên không chạy trực tiếp trên macOS/Linux; nếu máy phát triển là macOS thì build và demo giao diện trong Windows VM.

```bash
dotnet restore UDM10.sln
dotnet build UDM10.sln -c Release --no-restore
dotnet run --project Server/UDM10.Server.csproj -c Release
dotnet run --project Client/UDM10.Client.csproj -c Release
```

Build đạt yêu cầu khi kết quả là `0 Warning(s), 0 Error(s)`.

Để publish Client và Server trên máy Windows/Windows VM:

```bash
dotnet publish Server/UDM10.Server.csproj -c Release -o publish/Server
dotnet publish Client/UDM10.Client.csproj -c Release -o publish/Client
```

Không có source hoặc protocol riêng cho từng hệ điều hành. Client và Server trong hai thư mục publish trên luôn được tạo từ cùng solution và cùng Protocol v3.

## 5. Chạy trên hai máy trong LAN

1. Chuẩn bị hai máy Windows hoặc hai Windows VM có thể nhìn thấy nhau trong cùng mạng ảo/LAN.
2. Trên máy Server, đặt `Network:ServerIp` là `0.0.0.0`, mở TCP port `9000` trên Windows Firewall và chạy Server.
3. Dùng `ipconfig` để lấy IPv4 của máy/VM Server.
4. Trên máy/VM Client, nhập IPv4 đó và port `9000` trên giao diện.
5. Upload ít nhất 10 file; xác nhận không quá 3 file ở trạng thái `Uploading` cùng lúc.
6. Đối chiếu tên, kích thước và SHA-256 của file nguồn với file trong thư mục `Uploads` của Server.
7. Thử Cancel, Retry, tắt Server giữa chừng rồi khởi động lại; các file khác phải tiếp tục hoạt động và Server không được còn file `.part` của phiên lỗi.

Nếu hai máy không kết nối được, kiểm tra cùng subnet, firewall, port và bảo đảm Client không dùng `127.0.0.1`.

## 6. Trạng thái RC 0.4.5-rc.1

- Scheduler giới hạn tối đa 3 upload, cleanup slot khi Completed/Error/Cancelled và không enqueue trùng cùng đường dẫn.
- Khi đóng Client, các item còn chờ được chuyển sang Cancelled; dispatcher chờ các upload đang chạy cleanup xong.
- Thống kê phiên gồm tổng file, Completed, Error, Cancelled, tổng byte, byte đã truyền, thời gian và tốc độ trung bình.
- Protocol request/response dùng chung JSON Web options; Server đọc đúng FileSize, kiểm tra SHA-256 và cleanup `.part` khi lỗi/ngắt kết nối.
- Việc chạy hai máy Windows hoặc hai Windows VM vẫn phải được người kiểm thử thực hiện theo mục 5 và lưu screenshot/log làm bằng chứng nghiệm thu.
