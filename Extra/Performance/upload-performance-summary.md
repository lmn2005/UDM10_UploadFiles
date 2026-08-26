# Upload performance summary

## Machine configuration
- OS: macOS 26.5.1
- Runtime: .NET 10.0.10
- Logical processors: 8
- Available memory: 8,00 GB
- Network: In-process FileStream to FileStream; no external TCP network involved

## Dataset
- 2 load levels: light-load (32 MB / 64 KB chunk) and heavy-load (512 MB / 256 KB chunk)
- Files are generated in chunked fashion and streamed to disk without loading the full payload into memory at once.
- Integrity is verified by SHA-256 after transfer and short files are explicitly rejected as incomplete.

## Results
| Scenario | Size | Chunk | Time (ms) | Throughput (MB/s) | CPU (%) | Allocated (MB) | RAM (MB) | Success | Integrity | Partial rejection |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- |
| light-load | 32 MB | 64 KB | 32,79 | 975,86 | 38,7 | 0,19 | 46,98 | True | True | True |
| heavy-load | 512 MB | 256 KB | 387,11 | 1322,61 | 42,8 | 0,38 | 52,06 | True | True | True |
