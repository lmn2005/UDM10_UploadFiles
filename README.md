# UDM10 — Upload nhiều file

Ứng dụng Client–Server truyền nhiều file qua TCP. Client WPF hỗ trợ chọn hoặc kéo thả file, hàng đợi tối đa 3 upload đồng thời, tiến độ/tốc độ riêng, Cancel và Retry. Server nhận đúng số byte, kiểm tra SHA-256, dọn file `.part` khi lỗi và đổi tên file trùng mà không ghi đè.

## Môi trường chính thức

- Build, chạy, demo và lấy bằng chứng nghiệm thu trên Windows 10/11 hoặc Windows VM.
- Client dùng WPF nên không chạy trên macOS/Linux.
- Kết quả build hoặc benchmark ngoài Windows chỉ dùng để kiểm tra kỹ thuật, không phải bằng chứng nghiệm thu.

## Cấu trúc

- `Code/Client`: ứng dụng WPF.
- `Code/Server`: TCP Server.
- `Code/Shared`: Protocol v3 dùng chung.
- `Benchmark`: công cụ benchmark TCP với tiến trình Server riêng.
- `Extra`: screenshot, log và kết quả hiệu năng.

Tài liệu đầy đủ về kiến trúc, Protocol v3, cấu hình, build, publish, chạy hai máy và benchmark nằm tại [Code/README.md](Code/README.md).

## Lưu ý Protocol v3

- Request upload mới dùng status `Request`; Retry tạo một request mới.
- Cancel trên Client hủy cancellation token và đóng kết nối upload hiện tại. Server phát hiện luồng bị ngắt và dọn `.part`.
- Không có Pause/Resume.
