# UDM_10 — Upload nhiều file

Đồ án môn **Lập trình mạng**: ứng dụng desktop C# WPF cho phép chọn/kéo thả nhiều file và upload tới TCP Server. Client và Server chạy ở **hai tiến trình riêng**, giao tiếp qua mạng thật. Đây không phải Web App.

- Mã đề tài: **UDM_10**; tên solution/assembly trong code: `UDM10`.
- Mã lớp: **304**; nhóm: **11** (khôi phục từ README trong lịch sử Git; cần đối chiếu CourseCode/GroupCode chính thức trước khi nộp).
- Repository: [lmn2005/UDM10_UploadFiles](https://github.com/lmn2005/UDM10_UploadFiles).
- Video báo cáo/demo: **CHƯA CÓ LINK** — bổ sung link chia sẻ Google Drive/YouTube tại đây và trong báo cáo cuối kỳ.
- Trạng thái tích hợp 12/09/2026: build Release sạch, bộ test riêng đạt 11/11; **còn phải quay GUI/LAN và chạy nghiệm thu chính thức trên Windows**. Báo cáo, slide và bộ test được lưu ngoài repository source.

## 1. Thành viên và phân công

MSSV/họ tên dưới đây lấy từ README lịch sử Git. Vai trò là đầu mối phụ trách hiện tại, không thay thế chứng cứ đóng góp bằng commit, tài liệu và kết quả test.

| MSSV | Thành viên | Phần phụ trách |
| --- | --- | --- |
| 087205010642 | Nguyễn Tấn Hiệp | Client TCP, scheduler/queue, Cancel/Retry, tích hợp |
| 075205019210 | Phạm Anh Tuấn | Shared protocol, framing, validation, tài liệu kỹ thuật |
| 095205005482 | Lê Văn Nhựt | WPF, kéo thả/chọn file, trạng thái, các nút thao tác, demo GUI |
| 051206006174 | Huỳnh Anh Kiệt | TCP Server, session, timeout, shutdown, logging |
| 045205006605 | Võ Nhật Linh | Chunk transfer, storage, SHA-256, thống kê và performance |
| 054206006612 | Huỳnh Việt Tiến | Theo thông tin nhóm cung cấp: không thực hiện phần việc; bảng tuần 1–4 ghi trễ hạn, tuần 5 không giao việc. Chưa ghi nhận commit mang tên/tài khoản nhận diện được của thành viên này trong lịch sử Git local đã kiểm tra. |

Công việc tồn được chia lại cho năm thành viên: Anh Tuấn phụ trách protocol và validation; Nhật Linh phụ trách transfer và storage; Anh Kiệt phụ trách session Server và logging; Nhựt phụ trách GUI; Tấn Hiệp kiểm tra scheduler và tích hợp cuối. Danh sách thành viên chính thức vẫn cần đối chiếu hồ sơ môn học trước khi nộp.

## 2. Mục tiêu, phạm vi và chức năng

Mục tiêu là truyền nhiều file ổn định, giữ GUI phản hồi, theo dõi kết quả từng file và xử lý lỗi độc lập.

| Chức năng | Hiện trạng |
| --- | --- |
| Chọn hoặc kéo thả một/nhiều file | Có code WPF; chỉ nhận file, bỏ qua thư mục; tự xếp hàng khi thêm |
| Trạng thái từng file | Waiting, Uploading, Completed, Error, Cancelled |
| Tiến độ và tốc độ từng file | Có; tốc độ là trung bình từ khi bắt đầu gửi dữ liệu, tính theo 1024 byte |
| Hàng đợi và upload đồng thời | Tối đa **3 file/Client**; cấu hình 1–3; giá trị trên 3 bị chặn về 3 |
| Lỗi một file không dừng các file khác | Có cơ chế xử lý độc lập; cần test hồi quy qua WPF trên Windows |
| File trùng tên | Thêm `_1`, `_2`, … trước phần mở rộng; không ghi đè |
| Toàn vẹn file | Nhận đúng số byte đã khai báo, kiểm tra SHA-256, đổi `.part` thành file chính thức |
| Cancel/Retry từng file | Có; Retry truyền lại từ đầu bằng request/kết nối mới |
| Xóa các mục hoàn tất | Có nút GUI; xóa lịch sử Client, không xóa file trên Server |
| Hủy tất cả / Thử lại tất cả | Đã nối nút GUI và cập nhật trạng thái bật tắt theo danh sách |
| Tên file thực tế Server đã lưu | Hiển thị trong cột riêng khi Server đổi tên do trùng |

Không có Pause/Resume, upload thư mục, đăng nhập, TLS, cloud storage hoặc khôi phục queue sau khi đóng Client. SHA-256 dùng kiểm tra nội dung, không thay thế mã hóa/xác thực. Server chưa có giới hạn tổng số kết nối/toàn bộ Client; mức 3 chỉ áp dụng cho từng Client.

## 3. Kiến trúc và giao tiếp

```text
WPF GUI -> MainViewModel -> UploadManager / UploadQueueService
                              | tối đa 3 lượt upload/Client
                              v
                      UploadClientService
                              | TCP, 1 kết nối/file
                              v
TcpListener -> ClientConnectionHandler -> FileStorageService
                                          -> TemporaryFileManager
                                          -> DuplicateFileNameResolver
Client và Server cùng dùng Code/Shared cho protocol và validation.
```

Protocol **V3**, TCP port mặc định **9000**. Một phiên hiện gồm:

1. Client tính SHA-256 và chụp kích thước/thời điểm sửa file trước, sau đó mới mở kết nối TCP.
2. Gửi metadata: **4 byte length little-endian + JSON UTF-8**, payload từ 1 đến 4096 byte.
3. Server kiểm tra request, trả `Ready` hoặc `Error`.
4. Sau `Ready`, gửi raw binary; Server đọc đúng `fileSize` byte, ghi file tạm theo chunk và tính hash.
5. Hash đúng thì đổi thành file chính thức, trả `Completed` kèm `savedFileName`. Lỗi thì dọn file tạm nếu có thể và trả `Error` khi kết nối còn sử dụng được.
6. Đóng stream/socket sau từng file. Không có heartbeat hoặc kết nối dùng chung lâu dài. Cancel hủy token và đóng kết nối, **không gửi message Cancel**. Retry hiện gửi status `Request` với request ID mới.

Ví dụ request file rỗng:

```json
{"protocolVersion":"V3","requestId":"demo-001","fileName":"empty.txt","fileSize":0,"fileHash":"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855","status":1}
```

Response gồm `protocolVersion`, `requestId`, `status`, `errorCode`, `errorMessage`; `savedFileName` chỉ có khi Completed. Status: Request=1, Ready=2, Completed=3, Error=4, Cancel=5 (dự phòng), Retry=6 (mã dự phòng cũ, Server không chấp nhận trên wire). Mọi lượt thử lại đều tạo request ID mới và gửi status `Request`. Chi tiết message, enum và quy tắc validation ở [Code/README.md](Code/README.md).

## 4. Môi trường, cấu hình và hướng dẫn chạy

Môi trường demo: **Windows 10/11 hoặc Windows VM, .NET 10 SDK**. WPF không chạy trên macOS/Linux; cross-build không chứng minh GUI hoạt động. Git để lấy source; Visual Studio là tùy chọn nếu dùng CLI.

Chạy PowerShell tại thư mục gốc repository:

```powershell
git clone https://github.com/lmn2005/UDM10_UploadFiles.git
cd UDM10_UploadFiles
dotnet restore .\Code\UDM10.sln
dotnet build .\Code\UDM10.sln -c Release --no-restore
```

Chạy Server và Client trong hai cửa sổ PowerShell riêng, cùng tại thư mục gốc:

```powershell
# Cửa sổ 1
dotnet run --project .\Code\Server\UDM10.Server.csproj -c Release
```

```powershell
# Cửa sổ 2
dotnet run --project .\Code\Client\UDM10.Client.csproj -c Release
```

Cấu hình trong `Code/Server/appsettings.json` và `Code/Client/appsettings.json`; build/run copy cấu hình vào output. Với bản publish, sửa file JSON bên cạnh ứng dụng đã publish. Nên khởi động lại sau khi đổi cấu hình.

| Tham số | Server | Client |
| --- | --- | --- |
| `Network:ServerIp` | `0.0.0.0` | `127.0.0.1` |
| `Network:Port` | 9000 | 9000 |
| `Network:ConnectTimeoutMs` | Không dùng | 5000 |
| `Network:ReceiveTimeoutMs` | 30000 | 30000 |
| `Upload:ChunkSizeBytes` | 65536 | 65536 |
| `Upload:MaxConcurrentFiles` | Không dùng | 3 |
| `Upload:MaxAllowedSizeInBytes` | 10737418240 (10 GiB/file) | Không dùng |
| `Upload:SaveDirectory` | `Uploads` | Không dùng để lưu file Server |

Đường dẫn tương đối `Uploads` và `Logs/server_log.txt` tính từ **working directory của Server**. GUI áp dụng IP/port hiện tại khi chọn, kéo-thả, Retry hoặc Retry All. Client có timeout kết nối, chờ response và ghi dữ liệu.

Chạy hai máy: Server bind `0.0.0.0`, lấy IPv4 LAN bằng `ipconfig`, cho phép inbound TCP 9000 trên firewall và nhập IP đó ở Client. `127.0.0.1` chỉ dùng cho cùng máy. Chọn/kéo file sẽ tự bắt đầu upload. Kịch bản LAN và lệnh publish chi tiết ở [hướng dẫn kỹ thuật](Code/README.md#5-chạy-clientserver-trên-hai-máy-hoặc-hai-windows-vm).

## 5. Kiểm thử và bằng chứng

Rà soát tích hợp ngày 12/09/2026 trên macOS 15.7.3 và .NET SDK 10.0.400:

- `dotnet build Code/UDM10.sln -c Release`: **0 warning, 0 error**.
- Bộ kiểm thử riêng ngoài repository đạt **11/11** ca protocol, storage, send timeout và scheduler.
- Probe TCP trực tiếp xác nhận request status sai nhận `Error`, Server ghi đủ `Error` và `Disconnect`, Ctrl+C thoát sạch.
- Benchmark TCP loopback kỹ thuật đạt **868,24 MB/s ở 32 MiB** và **1495,09 MB/s ở 512 MiB**; size/SHA-256 đúng, upload thiếu byte được dọn và năm upload trùng tên song song đều thành công.

Các kết quả trên chưa thay thế nghiệm thu GUI WPF và LAN trên Windows. Mã kiểm thử, báo cáo QA và kết quả benchmark được lưu riêng ngoài repository.

Chạy benchmark chính thức trên Windows từ thư mục bộ test riêng đặt cạnh thư mục source:

```powershell
dotnet build ..\UDM10_UploadFiles\Code\UDM10.sln -c Release
dotnet run --project .\Benchmark\Benchmark.csproj -c Release
```

Kết quả mặc định ghi ngoài repository tại `Documents/UDM10_Test_Results/Performance`; có thể chọn thư mục nộp riêng bằng `--output <đường-dẫn>`. Công cụ benchmark truyền từng kịch bản tuần tự, chưa thay thế stress test nhiều Client.

Bộ nghiệm thu còn phải thực hiện và lưu kết quả thực tế:

| Nhóm test | Kịch bản bắt buộc |
| --- | --- |
| Functional GUI | Chọn/kéo 1, 3, 5, 20 file; progress/tốc độ từng file; tối đa 3; GUI phản hồi; lỗi một file không dừng file khác; trùng tên |
| Điều khiển bổ sung | Cancel/Retry từng file, hàng loạt sau khi nối GUI, xóa Completed và kiểm tra thống kê |
| Invalid data | Thiếu trường, size âm/vượt giới hạn, JSON/UTF-8 lỗi, length quá lớn, sai version, path traversal, checksum sai |
| Disconnect | Ngắt Client, dừng Server giữa upload, timeout; không công nhận file thiếu; cleanup và upload lại |
| Stress mức 1 | 10 file × 10 MiB, công bố số Client và concurrency thực tế |
| Stress mức 2 | 30 file × 20 MiB, tăng số Client (đề xuất 3 Client × tối đa 3 upload) |
| Performance | Tối thiểu hai mức tải, đo thời gian/throughput, CPU/RAM, tỷ lệ lỗi |

Mỗi lần test phải ghi commit, OS/CPU/RAM/.NET, mạng, dữ liệu đầu vào, số Client, số lượt đồng thời, cách chạy, kết quả mong đợi/thực tế và đường dẫn log/ảnh/video. Khi đổi cả file size lẫn chunk size như benchmark hiện tại, không kết luận riêng ảnh hưởng của chunk size.

## 6. Cấu trúc repository

```text
Code/       Client WPF, Server TCP, Shared, UDM10.sln, README kỹ thuật
DOCX/       Vị trí dành cho báo cáo Word cuối kỳ
Extra/      Vị trí dành cho ảnh và bằng chứng cần thiết
PPTX/       Vị trí dành cho slide thuyết trình
README.md
.gitignore
```

Repository giữ đúng bốn thư mục bắt buộc. Mã kiểm thử, benchmark, log, dữ liệu demo và các bằng chứng cũ được đóng gói riêng; chỉ đưa báo cáo, slide và bằng chứng đã chốt vào đúng thư mục khi chuẩn bị gói Course cuối cùng.

## 7. Giới hạn và việc chưa hoàn thành

Các phần còn chờ nghiệm thu là giao diện WPF trên Windows, demo LAN hoặc hai VM, và xử lý file `.part` còn sót sau khi tiến trình Server bị kill hoặc máy mất điện; code hiện chưa quét dọn file tạm cũ khi khởi động. Bộ kiểm thử scheduler và protocol được lưu riêng, không đưa vào source nộp đồ án.

Chưa có bằng chứng Windows WPF/LAN, stress chính thức và video hoàn chỉnh. Báo cáo 12 trang và slide 10 trang đã có trong đúng thư mục; không đánh dấu toàn dự án đã nghiệm thu chỉ dựa vào build hoặc kiểm tra trên macOS.

## 8. Hồ sơ nộp và quy tắc Git

- [ ] Kiểm tra hạn đóng và yêu cầu cụ thể trên hệ thống môn học.
- [ ] Source code đầy đủ và giữ đúng cấu trúc `Code`, `DOCX`, `Extra`, `PPTX`, `README.md`, `.gitignore`; không kèm output build, dữ liệu upload, log hoặc mã test.
- [x] Báo cáo **.docx 12 trang** đã đặt trong `DOCX`; cần điền link video sau khi quay.
- [x] Slide **.pptx 10 trang** đã đặt trong `PPTX`.
- [ ] Video có âm thanh hoặc chú thích; mỗi thành viên trình bày phần việc và hiển thị khuôn mặt; link chia sẻ hoạt động trong README và báo cáo cuối kỳ.
- [ ] GitHub có lịch sử tiến độ hàng tuần; mỗi người commit bằng tài khoản cá nhân, message mô tả thay đổi. Không có tiến độ 3 tuần liên tiếp vi phạm yêu cầu môn học. Không tạo commit giả/lùi ngày để bổ sung lịch sử.
- [ ] Đóng gói đúng **CourseCode-GroupCode-ProjectCode.7z**. Chỉ dùng `304-Nhom11-UDM_10.7z` nếu Course xác nhận đúng các mã này.
- [ ] Dọn bản sao dùng để đóng gói: `bin`, `obj`, `.vs`, dependency cache, output publish/build, `.DS_Store`, dữ liệu upload/demo sinh tự động và toàn bộ mã/kết quả test. Bộ test phải nộp bằng gói riêng; không đóng gói `.git`. `.gitignore` không tự loại các file này nếu nén trực tiếp thư mục làm việc.
- [ ] Không đưa password/secret/private key vào source; dùng dữ liệu giả lập khi demo.

Phân công chỉ được chuyển sang **hoàn thành** khi có commit/sản phẩm, test đạt và bằng chứng; người review kiểm tra chéo trước khi tích hợp.
