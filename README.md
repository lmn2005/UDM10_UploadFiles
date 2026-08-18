# UDM_10 - Upload nhiều file

## Thông tin đề tài
- Tên và mã đề tài: UDM_10 - Upload nhiều file
- Kiến trúc hệ thống: Client-Server qua TCP socket
- Port mặc định: 9000
- Giao tiếp: TCP socket thuần, không dùng HTTP/web framework
- Ngôn ngữ/Công nghệ: C#, .NET 8.0, WPF, Console App

## Thành viên

| STT | MSSV | Họ và tên | Vai trò |
|---:|---|---|---|
| 1 | 095205005482| Lê Văn Nhựt | Leader |
| 2 | 075205019210 | Phạm Anh Tuấn | Member |
| 3 | 087205010642 | Nguyễn Tấn Hiệp | Member |
| 4 | 051206006174 | Huỳnh Anh Kiệt | Member |
| 5 | 045205006605 | Võ Nhật Linh | Member |

## Kiến trúc hệ thống

- Client: WPF Application, project `UDM10.Client`
- Server: Console Application, project `UDM10.Server`
- Shared: Class Library, project `UDM10.Shared`
- Cấu hình mạng được đọc từ `appsettings.json`, không hard-code IP/port trong code.

## Cấu trúc repository

```text
UDM10-DoAnMangMayTinh/
├── Code/
│   ├── UDM10.sln
│   ├── Client/
│   ├── Server/
│   └── Shared/
├── DOCX/
├── Extra/
├── PPTX/
├── README.md
└── .gitignore
```

## Yêu cầu môi trường

- Windows
- .NET 8.0 SDK
- Visual Studio 2022

## Hướng dẫn chạy

### Server
```bash
dotnet run --project Code/Server
```

### Client
```bash
dotnet run --project Code/Client
```
Hoặc mở `Code/UDM10.sln` bằng Visual Studio 2022 và set `UDM10.Client` làm Startup Project.

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

## Kiểm thử

- [ ] Functional test
- [ ] Dữ liệu không hợp lệ
- [ ] Mất kết nối
- [ ] Stress test
- [ ] Performance test

## Demo

- [ ] Video demo
- [ ] Slide trong `PPTX/`
- [ ] Báo cáo trong `DOCX/`
- [ ] Bằng chứng kiểm thử trong `Extra/`

## Giới hạn

- [ ] Pause upload
- [ ] Resume upload
- [ ] Đăng nhập tài khoản
- [ ] Cloud storage
- [ ] Upload thư mục
