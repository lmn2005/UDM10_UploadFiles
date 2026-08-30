# Dữ liệu bổ sung

Thư mục này dùng để lưu dữ liệu bổ sung cho quá trình phát triển và kiểm thử:

- Ảnh chụp màn hình.
- Log kiểm thử.
- Dữ liệu test.
- Kết quả stress test.
- Kết quả performance test.
- Sơ đồ kiến trúc.

**Lưu ý:** Không lưu password, secret hoặc dữ liệu cá nhân thật trong thư mục này.

Kết quả benchmark chính thức phải được tạo trên Windows bằng `Benchmark/Benchmark.csproj`. File có hậu tố `-non-windows` và các log benchmark FileStream cũ chỉ dùng để tham khảo, không phải bằng chứng nghiệm thu Client–Server TCP.
