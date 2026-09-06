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

Ngày 06/09/2026, log benchmark cũ và bộ rà soát đã được chuyển ra nơi lưu trữ riêng ngoài dự án; các thư mục TestLogs, TestData và Review đã được bỏ. Kết quả nghiệm thu mới vẫn phải có bằng chứng theo yêu cầu môn học. Khi lưu log nghiệm thu mới, bổ sung quy tắc .gitignore phù hợp để log đó được đưa vào Git.
