# UDM_10 - Upload nhiều file

## Thông tin đề tài
- Môn học: Lập trình mạng
- Mã lớp: 304
- Nhóm: 11
- Mã đề tài: UDM_10
- Tên đề tài: Upload nhiều file
- Ngôn ngữ: C#

## Thành viên

| STT | MSSV | Họ và tên | Vai trò |
|---:|---|---|---|
| 1 | 095205005482| Lê Văn Nhựt | TODO |
| 2 | 075205019210 | Phạm Anh Tuấn | TODO |
| 3 | 087205010642 | Nguyễn Tấn Hiệp | TODO |
| 4 | 054206006612| Huỳnh Việt Tiến | TODO |
| 5 | 051206006174 | Huỳnh Anh Kiệt | TODO |
| 6 | 045205006605 | Võ Nhật Linh | TODO |

## Giới thiệu

Ứng dụng Client–Server bằng C# cho phép người dùng kéo thả và upload nhiều file từ Client lên Server qua mạng. Mỗi file có trạng thái, tiến trình và tốc độ riêng.

## Mục tiêu

- Kéo thả một hoặc nhiều file vào GUI.
- Upload nhiều file lên Server.
- Theo dõi trạng thái riêng từng file.
- Hiển thị progress và tốc độ riêng.
- Giới hạn tối đa 3 file upload đồng thời.
- File lỗi không làm dừng các file còn lại.
- Server xử lý file trùng tên.
- Tác vụ mạng không làm treo GUI.

## Kiến trúc hệ thống

- Mô hình: Client–Server
- Client: C# WPF
- Server: C# Console Application
- Shared: C# Class Library
- Protocol dự kiến: TCP
- Port mặc định dự kiến: 9000
- IP và port phải cấu hình được
- Mỗi file dự kiến sử dụng một kết nối TCP riêng
- Giới hạn upload đồng thời dự kiến: 3 file
- Chunk dự kiến: 64 KB

*Lưu ý: Protocol và cấu trúc message đang trong quá trình thiết kế, chưa được xem là hoàn thành.*

## Cấu trúc message dự kiến

[4 byte metadata length]
[JSON metadata]
[file binary data]
[server response]

## Cấu trúc repository

```text
304-Nhom11-UDM_10/
├── Code/
├── DOCX/
├── Extra/
├── PPTX/
├── README.md
└── .gitignore
```

## Yêu cầu môi trường

- Windows để chạy WPF.
- .NET SDK tương thích với project.
- Visual Studio hoặc IDE hỗ trợ C#.
- Git.

## Cài đặt

```bash
git clone <repository-url>
cd 304-Nhom11-UDM_10
dotnet restore Code/UDM10.UploadFiles.sln
dotnet build Code/UDM10.UploadFiles.sln
```

## Hướng dẫn chạy

### Server
```bash
dotnet run --project Code/UDM10.Server
```

### Client
```bash
dotnet run --project Code/UDM10.Client
```
*(Client WPF cần chạy trên Windows)*

## Cấu hình

IP, port, giới hạn upload đồng thời và thư mục lưu file sẽ được đưa vào file cấu hình, không hard-code cho một máy duy nhất.

## Chức năng

- [ ] Kéo thả nhiều file
- [ ] Hiển thị danh sách file
- [ ] Trạng thái riêng từng file
- [ ] Progress riêng từng file
- [ ] Tốc độ riêng từng file
- [ ] Hàng đợi upload
- [ ] Tối đa 3 file upload đồng thời
- [ ] File lỗi không dừng file khác
- [ ] Xử lý file trùng tên trên Server
- [ ] Timeout và xử lý mất kết nối
- [ ] Server log
- [ ] Functional test
- [ ] Stress test
- [ ] Performance test

## Quy tắc file trùng tên dự kiến

Server không ghi đè file cũ. Ví dụ:
- report.pdf
- report_1.pdf
- report_2.pdf

## Quy tắc file chưa hoàn tất

File đang nhận được lưu với đuôi .part. Chỉ khi nhận đủ dữ liệu mới chuyển thành file hoàn chỉnh. Nếu lỗi hoặc mất kết nối thì file .part phải được xử lý hoặc xóa.

## Kiểm thử

- Functional test
- Dữ liệu không hợp lệ
- Mất kết nối
- Stress test
- Performance test

*(Bằng chứng kiểm thử sẽ được lưu trong Extra/)*

## Git workflow

- Mỗi task dùng một feature branch.
- Dev đồng bộ main vào branch trước khi code và trước khi test.
- Tester kiểm tra feature branch.
- Chỉ merge về main khi test passed.
- Không commit trực tiếp lên main.

## Demo

- Video: TODO
- Slide: PPTX/
- Báo cáo: DOCX/
- Bằng chứng: Extra/

## Phạm vi không thực hiện

- Pause upload
- Resume upload
- Web App
- Đăng nhập tài khoản
- Cloud storage
- Upload thư mục
- Tiếp tục upload sau khi tắt ứng dụng

## Giới hạn hiện tại

Đây mới là bộ khung ban đầu, chưa có chức năng upload hoàn chỉnh.
