# Upload TCP performance summary

## Trạng thái

**PENDING_WINDOWS_TCP_RUN** — Chưa có kết quả benchmark TCP chính thức trên Windows.

Kết quả cũ được tạo trên macOS bằng cách copy `FileStream` trong cùng tiến trình, không đi qua Client–Server TCP nên không được dùng làm bằng chứng nghiệm thu.

Chạy lại trên Windows 10/11 hoặc Windows VM từ thư mục gốc repository:

```powershell
dotnet build .\Code\UDM10.sln -c Release
dotnet run --project .\Benchmark\Benchmark.csproj -c Release
```

Lệnh trên sẽ thay nội dung file này bằng hai mức tải TCP thật, gồm throughput, CPU/RAM riêng của tiến trình benchmark và Server, size/SHA-256 cùng kết quả cleanup upload thiếu byte.
