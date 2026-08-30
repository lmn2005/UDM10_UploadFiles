# UDM10 — Upload nhiều file

UDM10 là ứng dụng Client–Server truyền nhiều file qua TCP. Client WPF cho phép chọn hoặc kéo thả file, hiển thị trạng thái, tiến độ và tốc độ riêng cho từng file. Server kiểm tra metadata, nhận đúng số byte đã công bố, xác minh SHA-256 và xử lý tên trùng mà không ghi đè file cũ.

Môi trường chạy và demo chính thức là Windows 10/11. Có thể phát triển trên macOS, nhưng Client WPF phải được build và chạy trong Windows VM.

## 1. Kiến trúc

- `Client`: ứng dụng WPF, lập hàng đợi và giới hạn tối đa 3 upload đồng thời. Lỗi hoặc thao tác Cancel của một file không làm dừng các file còn lại.
- `Server`: ứng dụng console lắng nghe TCP, xử lý nhiều kết nối bất đồng bộ, xác thực metadata và lưu file.
- `Shared`: thư viện dùng chung cho Client và Server, chứa model request/response, hằng số, validation và bộ đọc/ghi Protocol v3.

Luồng upload của một file:

1. Client tính SHA-256 và gửi `UploadRequest`.
2. Server kiểm tra framing, JSON và toàn bộ metadata.
3. Server trả `Ready` hoặc `Error`. Client chỉ gửi dữ liệu sau khi nhận `Ready` hợp lệ.
4. Client gửi đúng `fileSize` byte; Server đọc theo chunk và ghi vào file tạm `.part`.
5. Server so sánh SHA-256. Nếu đúng, file tạm được đổi thành file chính thức và Server trả `Completed`.
6. Khi lỗi, timeout, mất kết nối hoặc sai checksum, Server xóa file `.part` và trả `Error` nếu kết nối còn dùng được.

Nếu tên đã tồn tại, Server tạo tên mới theo dạng `ten_1.ext`, `ten_2.ext`, ... thay vì ghi đè.

## 2. Protocol v3

### 2.1. Message framing

Mỗi message metadata request/response có cấu trúc:

| Thành phần | Kích thước | Quy ước |
| --- | ---: | --- |
| Length prefix | 4 byte | Số nguyên có dấu, little-endian; giá trị từ 1 đến 4096 |
| JSON payload | `length` byte | UTF-8 hợp lệ, tên thuộc tính dạng camelCase |

Sau response `Ready`, Client gửi trực tiếp phần dữ liệu file, không đặt length prefix cho từng chunk. Server chỉ đọc đúng số byte trong `fileSize`; message response cuối được đọc sau phần dữ liệu đó.

JSON có field lạ, UTF-8 lỗi, length không hợp lệ, message bị cắt hoặc payload vượt 4096 byte đều bị từ chối mà không làm Server crash.

### 2.2. UploadRequest

```json
{
  "protocolVersion": "V3",
  "requestId": "0f3d1e5a680a40a0a386957e25f88b0b",
  "fileName": "bao-cao.pdf",
  "fileSize": 123456,
  "fileHash": "<64 ky tu hex SHA-256>",
  "status": 1
}
```

Quy tắc validation:

- `protocolVersion` bắt buộc và phải khớp chính xác `V3`.
- `requestId` dài từ 1 đến 128 ký tự; không có khoảng trắng đầu/cuối hoặc ký tự điều khiển.
- `status` nhận `Request` hoặc `Retry`. Giá trị enum không tồn tại và status không dành cho request bị từ chối.
- `fileName` dài từ 1 đến 255 ký tự, chỉ là tên file, không chứa đường dẫn, ký tự điều khiển, ký tự cấm hoặc tên thiết bị dành riêng của Windows như `CON`, `NUL`, `COM1`.
- `fileSize` có thể bằng 0 và không được âm hoặc vượt `Upload:MaxAllowedSizeInBytes`.
- `fileHash` là SHA-256 gồm đúng 64 ký tự hex, kể cả đối với file rỗng.

### 2.3. UploadResponse

```json
{
  "protocolVersion": "V3",
  "requestId": "0f3d1e5a680a40a0a386957e25f88b0b",
  "status": 2,
  "errorCode": 0,
  "errorMessage": "Server sẵn sàng nhận file."
}
```

Client chỉ chấp nhận response khi `protocolVersion` và `requestId` khớp request hiện tại. Response `Error` phải có mã lỗi khác `None` và nội dung lỗi; response thành công không được chứa mã lỗi.

### 2.4. UploadStatus

| Giá trị | Tên | Hướng sử dụng |
| ---: | --- | --- |
| 0 | `None` | Giá trị mặc định, không hợp lệ trên wire |
| 1 | `Request` | Client → Server, upload mới |
| 2 | `Ready` | Server → Client, metadata hợp lệ |
| 3 | `Completed` | Server → Client, đã nhận đủ byte và đúng SHA-256 |
| 4 | `Error` | Server → Client, yêu cầu hoặc upload thất bại |
| 5 | `Cancel` | Giá trị dự phòng; Client hiện hủy bằng cách đóng luồng upload đang chạy |
| 6 | `Retry` | Client → Server, thực hiện lại như một request mới |

Không có Pause/Resume. Cancel dừng đúng upload hiện tại bằng cancellation token và đóng kết nối; Server phát hiện luồng bị ngắt rồi dọn file `.part`.

### 2.5. ErrorCode

| Giá trị | Tên | Ý nghĩa |
| ---: | --- | --- |
| 0 | `None` | Không lỗi |
| 1 | `UnknownError` | Lỗi Server chưa phân loại |
| 2 | `InvalidMetadata` | JSON hoặc metadata không hợp lệ |
| 3 | `FileNameEmpty` | Thiếu tên file |
| 4 | `FileSizeInvalid` | Kích thước âm hoặc vượt giới hạn |
| 5 | `ProtocolVersionMismatch` | Sai hoặc thiếu phiên bản protocol |
| 6 | `MissingRequestId` | Thiếu request ID |
| 7 | `ServerBusy` | Dự phòng khi Server giới hạn tải |
| 8 | `ConnectionLost` | Mất kết nối, message/file bị cắt hoặc timeout |
| 9 | `StorageError` | Lỗi lưu trữ phía Server |
| 10 | `CancelledByUser` | Người dùng hủy upload |
| 11 | `ChecksumMismatch` | SHA-256 nhận được không khớp metadata |
| 12 | `UnsupportedStatus` | Status không hợp lệ cho request |

Client hiển thị lỗi Server theo dạng `ErrorCode: ErrorMessage`, không tự thay bằng một thông báo chung nếu response hợp lệ.

## 3. Cấu hình

### Server — `Server/appsettings.json`

| Key | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `Network:ServerIp` | `0.0.0.0` | Lắng nghe trên mọi card mạng; dùng để chạy LAN |
| `Network:Port` | `9000` | Cổng TCP |
| `Network:ReceiveTimeoutMs` | `30000` | Idle timeout cho metadata và từng lần chờ dữ liệu file |
| `Upload:SaveDirectory` | `Uploads` | Thư mục lưu file, tính từ thư mục chạy Server nếu dùng đường dẫn tương đối |
| `Upload:ChunkSizeBytes` | `65536` | Chunk ghi file; giá trị không dương tự về 8192 byte |
| `Upload:MaxAllowedSizeInBytes` | `10737418240` | Giới hạn 10 GiB cho một file |

### Client — `Client/appsettings.json`

| Key | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `Network:ServerIp` | `127.0.0.1` | IP Server; đổi thành IPv4 của máy Server khi chạy LAN |
| `Network:Port` | `9000` | Phải khớp cổng Server |
| `Network:ConnectTimeoutMs` | `5000` | Timeout kết nối TCP |
| `Network:ReceiveTimeoutMs` | `30000` | Timeout chờ response từ Server |
| `Upload:ChunkSizeBytes` | `65536` | Chunk gửi file, 64 KiB |
| `Upload:MaxConcurrentFiles` | `3` | Số upload đồng thời; code luôn chặn không vượt quá 3 |

`Client/appsettings.json` cung cấp IP/port ban đầu. Người dùng có thể sửa Server IP và port ngay trên giao diện trước khi chọn hoặc kéo thả file; thay đổi trên giao diện chỉ áp dụng cho phiên đang chạy.

## 4. Build và chạy trên Windows

Yêu cầu: Windows 10/11 hoặc Windows VM và .NET 10 SDK. Mở PowerShell tại thư mục `Code`.

```powershell
dotnet --info
dotnet restore .\UDM10.sln
dotnet build .\UDM10.sln -c Release --no-restore
```

Build nghiệm thu phải kết thúc với `0 Warning(s)` và `0 Error(s)`.

Chạy Server trước, sau đó chạy Client ở cửa sổ PowerShell khác:

```powershell
dotnet run --project .\Server\UDM10.Server.csproj -c Release
dotnet run --project .\Client\UDM10.Client.csproj -c Release
```

Publish Release Candidate trên Windows, vẫn từ thư mục `Code`:

```powershell
dotnet publish .\Server\UDM10.Server.csproj -c Release -r win-x64 --self-contained false -o ..\publish\Server
dotnet publish .\Client\UDM10.Client.csproj -c Release -r win-x64 --self-contained false -o ..\publish\Client
```

Hai thư mục publish dùng chung assembly Protocol v3. Thư mục `publish` được đặt ngoài `Code` để không đưa artefact build vào mã nguồn.

## 5. Chạy Client–Server trên hai máy hoặc hai Windows VM

1. Đặt `Network:ServerIp` của Server là `0.0.0.0` và chạy Server.
2. Trên máy Server, dùng `ipconfig` để lấy địa chỉ IPv4 của card mạng LAN/VM.
3. Mở inbound TCP port 9000 trong Windows Defender Firewall. Có thể chạy PowerShell bằng quyền Administrator:

   ```powershell
   New-NetFirewallRule -DisplayName "UDM10 TCP 9000" -Direction Inbound -Protocol TCP -LocalPort 9000 -Action Allow
   ```

4. Trên máy Client, nhập IPv4 của máy Server và port tương ứng trên giao diện. Có thể sửa `Client/appsettings.json` nếu muốn dùng các giá trị đó làm mặc định khi khởi động.
5. Kiểm tra hai máy cùng subnet và ping được nhau, rồi chạy Client.
6. Upload nhiều file và xác nhận từng file có trạng thái/progress riêng, tối đa 3 file đang upload, file khác vẫn tiếp tục khi một file lỗi hoặc bị Cancel.
7. Đối chiếu tên, kích thước và SHA-256 của file nguồn với file trong `Uploads`; xác nhận không còn `.part` sau lỗi hoặc ngắt kết nối.

`127.0.0.1` chỉ dùng khi Client và Server chạy trên cùng một máy/VM.

## 6. Benchmark TCP trên Windows

Benchmark chạy một tiến trình Client benchmark và một tiến trình `UDM10.Server` riêng, truyền dữ liệu qua TCP loopback. Hai mức tải mặc định là 32 MiB/chunk 64 KiB và 512 MiB/chunk 256 KiB. Công cụ đo throughput TCP, CPU và peak working set riêng của hai tiến trình, đối chiếu size/SHA-256, đồng thời kiểm tra upload thiếu byte bị từ chối và `.part` được dọn.

Mở PowerShell tại thư mục gốc repository trên Windows:

```powershell
dotnet build .\Code\UDM10.sln -c Release
dotnet run --project .\Benchmark\Benchmark.csproj -c Release
```

Kết quả chính thức được ghi vào:

- `Extra\Performance\upload-performance-summary.json`
- `Extra\Performance\upload-performance-summary.md`
- `Extra\Performance\upload-performance-server.log`

Benchmark từ chối chạy chính thức ngoài Windows. Tham số `--allow-non-windows` chỉ dành cho kiểm tra kỹ thuật và tạo file có hậu tố `-non-windows`; tuyệt đối không dùng các file này làm bằng chứng nghiệm thu.

Benchmark loopback xác nhận hiệu năng TCP của code trên một máy. Bài demo Client–Server trên hai máy/VM ở mục 5 vẫn phải thực hiện riêng để xác nhận firewall, IP LAN và hoạt động thực tế của giao diện WPF.

## 7. Phạm vi hoàn thành

Code đã có scheduler tối đa 3 upload, trạng thái và thống kê từng phiên, xử lý tên trùng, đọc đúng `fileSize`, kiểm tra SHA-256, timeout và cleanup `.part`. Protocol request/response sử dụng chung cách serialize/deserialize và validation ở `Shared`.

Việc demo hai máy, chụp bằng chứng, chạy lại benchmark TCP và xác nhận Release Candidate vẫn là bước nghiệm thu thủ công trên Windows; README không thay thế các bằng chứng đó.
