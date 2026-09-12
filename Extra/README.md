# Dữ liệu bổ sung

Thư mục này chỉ dùng cho tài nguyên bổ sung thuộc bộ source đồ án:

- Ảnh chụp màn hình.
- Sơ đồ kiến trúc.

**Lưu ý:** Không lưu password, secret hoặc dữ liệu cá nhân thật trong thư mục này.

Kết quả benchmark chính thức phải được tạo trên Windows bằng `Benchmark/Benchmark.csproj` trong bộ test riêng và lưu ngoài repository source bằng tham số `--output`. Nếu không truyền tham số, công cụ lưu vào `Documents/UDM10_Test_Results/Performance`. File có hậu tố `-non-windows` chỉ dùng để tham khảo, không phải bằng chứng nghiệm thu Client–Server TCP.

Log, dữ liệu test, báo cáo QA và kết quả benchmark được lưu trong bộ kiểm thử riêng, không commit vào source nộp đồ án.
