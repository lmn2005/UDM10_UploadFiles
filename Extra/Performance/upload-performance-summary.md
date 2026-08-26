# Upload performance summary

## Machine configuration
- OS: macOS 26.5.1
- Runtime: .NET 10.0.10
- Logical processors: 8
- Available memory: 8,00 GB

## Dataset
- 2 load levels: light-load (32 MB / 64 KB chunk) and heavy-load (512 MB / 256 KB chunk)
- Files are generated in chunked fashion and streamed to disk without loading the full payload into memory at once.
- Integrity is verified by SHA-256 after transfer and short files are explicitly rejected as incomplete.

## Results
| Scenario | Size | Chunk | Time (ms) | Throughput (MB/s) | CPU (%) | RAM (MB) | Integrity | Partial rejection |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | 
| light-load | 32 MB | 64 KB | 40,38 | 792,41 | 35,6 | 46,97 | True | True |
| heavy-load | 512 MB | 256 KB | 325,65 | 1572,24 | 53,5 | 52,45 | True | True |
