# UDM10 Release Candidate 0.4.5-rc.1

## Phạm vi

- Client upload tối đa 3 file đồng thời, queue FIFO và chống upload trùng.
- Cancel/Retry độc lập; Completed/Error/Cancelled không làm dừng queue.
- Scheduler theo dõi task đang chạy, cleanup khi đóng Client và không rò rỉ slot.
- Thống kê tổng file, Completed, Error, Cancelled, tổng byte, thời gian và tốc độ trung bình.
- Server TCP lắng nghe trên mọi network interface; Client cho phép nhập IP/port Server.

## Build và kiểm tra

```bash
dotnet test Code/ClientCoordinator.Tests/UDM10.ClientCoordinator.Tests.csproj -c Release
dotnet build Code/UDM10.sln -c Release
```

## Chạy trên hai máy Windows

1. Hai máy phải cài .NET 10 Desktop Runtime hoặc dùng bản publish self-contained.
2. Trên máy Server, mở TCP port `9000` trong Windows Firewall và chạy `UDM10.Server.exe`.
3. Dùng `ipconfig` để lấy IPv4 của máy Server, ví dụ `192.168.1.10`.
4. Bảo đảm hai máy cùng mạng và Client ping được IP Server.
5. Trên máy Client, chạy `UDM10.Client.exe`, nhập IPv4 Server và port `9000`, sau đó chọn file.
6. File nhận được nằm trong thư mục `Uploads` cạnh thư mục làm việc của Server; log nằm trong `Logs/server_log.txt`.

## Kiểm tra bằng tiến trình Console

```bash
dotnet run --project Code/TestClientConsole -- <duong-dan-file> <server-ip> 9000
```

## Checklist trước demo

- Build solution không có lỗi/cảnh báo.
- Chạy toàn bộ coordinator tests.
- Upload đồng thời nhiều file; xác nhận không vượt quá 3 connection.
- Cancel file đang chạy và Retry lại từ đầu.
- Đóng Client trong lúc upload; xác nhận Server cleanup file tạm.
- Kiểm tra thống kê trên GUI khớp trạng thái danh sách file.
- Kiểm tra thực tế trên hai máy Windows cùng mạng.

## Kết quả xác minh RC1 ngày 18/08/2026

- `dotnet test` Release: 11/11 test đạt.
- Hai test race/stress trọng yếu chạy lặp 20 vòng liên tiếp đều đạt.
- `dotnet build` Release: 0 lỗi, 0 cảnh báo.
- Đã chạy Server và hai tiến trình TestClient đồng thời qua địa chỉ LAN; cả hai file hoàn tất
  và SHA-256 phía gửi/nhận khớp nhau.
- Kiểm tra trên hai máy vật lý Windows vẫn là bước thủ công bắt buộc trước buổi demo.

## Trạng thái tích hợp ngày 18/08/2026

Nhánh này đã tích hợp `origin/main` đến PR #21 và phần Client scheduler/metrics. Các nhánh
`feature/upload-protocol-v3`, `feature/server-upload-session-v3`,
`feature/chunk-transfer-integrity` và `feature/storage-recovery-integration-v3`
chưa có commit mới để tích hợp tại thời điểm tạo RC1. Cần merge các nhánh đó và chạy lại checklist
trước khi gắn nhãn RC cuối cùng cho toàn nhóm.
