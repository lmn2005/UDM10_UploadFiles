using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

if (Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "--benchmark", StringComparison.OrdinalIgnoreCase)))
{
    BenchmarkRunner.Run<UploadChunkBenchmarks>();
    return;
}

await UploadPerformanceHarness.RunAsync();

public sealed class PayloadScenario
{
    public string Name { get; init; } = string.Empty;
    public int ChunkSizeBytes { get; init; }
    public long FileSizeBytes { get; init; }
}

public sealed class TransferResult
{
    public string Name { get; set; } = string.Empty;
    public int ChunkSizeBytes { get; set; }
    public long FileSizeBytes { get; set; }
    public long BytesRead { get; set; }
    public long BytesWritten { get; set; }
    public double ElapsedMs { get; set; }
    public double ThroughputMBps { get; set; }
    public double CpuPercent { get; set; }
    public long PeakWorkingSetBytes { get; set; }
    public long AllocatedBytes { get; set; }
    public long SourcePhysicalSizeBytes { get; set; }
    public long ReceivedPhysicalSizeBytes { get; set; }
    public string SourceSha256 { get; set; } = string.Empty;
    public string ReceivedSha256 { get; set; } = string.Empty;
    public bool ReceivedFileCanBeOpened { get; set; }
    public bool IntegrityOk { get; set; }
    public bool PartialFileRejected { get; set; }
    public bool UsesStreamingChunks { get; set; }
    public bool TransferSucceeded { get; set; }
}

public sealed class MachineProfile
{
    public string OsDescription { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public long TotalAvailableMemoryBytes { get; set; }
    public string NetworkDescription { get; set; } = string.Empty;
}

public static class UploadPerformanceHarness
{
    public static async Task RunAsync()
    {
        var scenarios = new[]
        {
            new PayloadScenario { Name = "light-load", ChunkSizeBytes = 64 * 1024, FileSizeBytes = 32L * 1024 * 1024 },
            new PayloadScenario { Name = "heavy-load", ChunkSizeBytes = 256 * 1024, FileSizeBytes = 512L * 1024 * 1024 }
        };

        var machine = new MachineProfile
        {
            OsDescription = RuntimeInformation.OSDescription,
            Framework = RuntimeInformation.FrameworkDescription,
            ProcessorCount = Environment.ProcessorCount,
            TotalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            NetworkDescription = "In-process FileStream to FileStream; no external TCP network involved"
        };

        var results = new List<TransferResult>();
        foreach (var scenario in scenarios)
        {
            var result = await MeasureScenarioAsync(scenario);
            results.Add(result);
        }

        var reportPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Extra",
            "Performance",
            "upload-performance-summary.json");

        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);

        var report = new
        {
            generatedAtUtc = DateTime.UtcNow,
            machine,
            scenarios,
            results
        };

        await File.WriteAllTextAsync(
            fullReportPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        var markdown = BuildMarkdown(machine, results);
        var markdownPath = Path.Combine(Path.GetDirectoryName(fullReportPath)!, "upload-performance-summary.md");
        await File.WriteAllTextAsync(markdownPath, markdown);
        var logDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullReportPath)!, "..", "TestLogs"));
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, $"upload-performance-{DateTime.UtcNow:yyyyMMddTHHmmssZ}.log");
        await File.WriteAllLinesAsync(logPath, results.Select(result =>
            $"{DateTime.UtcNow:O} scenario={result.Name} bytes={result.FileSizeBytes} " +
            $"elapsedMs={result.ElapsedMs:F2} throughputMBps={result.ThroughputMBps:F2} " +
            $"cpuPercent={result.CpuPercent:F2} allocatedBytes={result.AllocatedBytes} " +
            $"success={result.TransferSucceeded} integrity={result.IntegrityOk} " +
            $"partialRejected={result.PartialFileRejected}"));

        Console.WriteLine("=== Upload protocol validation ===");
        foreach (var result in results)
        {
            Console.WriteLine(
                $"{result.Name}: size={result.FileSizeBytes:N0}B chunk={result.ChunkSizeBytes:N0}B " +
                $"elapsed={result.ElapsedMs:F2}ms throughput={result.ThroughputMBps:F2} MB/s " +
                $"cpu={result.CpuPercent:F1}% RAM={result.PeakWorkingSetBytes / (1024d * 1024d):F2}MB " +
                $"integrity={result.IntegrityOk} partialReject={result.PartialFileRejected}");
        }

        Console.WriteLine($"Saved summary JSON: {fullReportPath}");
        Console.WriteLine($"Saved summary MD: {markdownPath}");
        Console.WriteLine($"Saved test log: {logPath}");
    }

    private static async Task<TransferResult> MeasureScenarioAsync(PayloadScenario scenario)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "udm10-upload-benchmark", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var sourcePath = Path.Combine(tempRoot, "source.bin");
        var finalPath = Path.Combine(tempRoot, "final.bin");
        var partialPath = Path.Combine(tempRoot, "partial.bin");

        try
        {
            await WriteFileInChunksAsync(sourcePath, scenario.FileSizeBytes, scenario.ChunkSizeBytes);

            var process = Process.GetCurrentProcess();
            var beforeCpu = process.TotalProcessorTime;
            var beforeRam = process.WorkingSet64;
            var beforeAllocated = GC.GetTotalAllocatedBytes(true);
            var stopwatch = Stopwatch.StartNew();

            long totalBytes = 0;
            using (var source = File.OpenRead(sourcePath))
            using (var target = File.Create(finalPath))
            {
                var buffer = new byte[scenario.ChunkSizeBytes];
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytes += bytesRead;
                }
            }

            stopwatch.Stop();
            var hash = await ComputeHashAsync(sourcePath);
            var finalHash = await ComputeHashAsync(finalPath);
            var memoryAfter = process.WorkingSet64;
            var allocatedBytes = Math.Max(0, GC.GetTotalAllocatedBytes(true) - beforeAllocated);
            var cpuTime = process.TotalProcessorTime - beforeCpu;
            var elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001d);
            var cpuPercent = (cpuTime.TotalSeconds / elapsedSeconds / Environment.ProcessorCount) * 100d;
            var peakWorkingSet = Math.Max(beforeRam, memoryAfter);

            var shortFileRejected = await ValidateShortFileRejectedAsync(sourcePath, scenario.ChunkSizeBytes);
            var receivedCanBeOpened = await CanOpenFileAsync(finalPath);

            var result = new TransferResult
            {
                Name = scenario.Name,
                ChunkSizeBytes = scenario.ChunkSizeBytes,
                FileSizeBytes = scenario.FileSizeBytes,
                BytesRead = totalBytes,
                BytesWritten = totalBytes,
                ElapsedMs = stopwatch.Elapsed.TotalMilliseconds,
                ThroughputMBps = (totalBytes / (1024d * 1024d)) / elapsedSeconds,
                CpuPercent = cpuPercent,
                PeakWorkingSetBytes = peakWorkingSet,
                AllocatedBytes = allocatedBytes,
                SourcePhysicalSizeBytes = new FileInfo(sourcePath).Length,
                ReceivedPhysicalSizeBytes = new FileInfo(finalPath).Length,
                SourceSha256 = hash,
                ReceivedSha256 = finalHash,
                ReceivedFileCanBeOpened = receivedCanBeOpened,
                IntegrityOk = string.Equals(hash, finalHash, StringComparison.OrdinalIgnoreCase) &&
                    new FileInfo(sourcePath).Length == new FileInfo(finalPath).Length &&
                    receivedCanBeOpened,
                PartialFileRejected = shortFileRejected,
                UsesStreamingChunks = scenario.ChunkSizeBytes > 0 && scenario.ChunkSizeBytes < scenario.FileSizeBytes,
                TransferSucceeded = totalBytes == scenario.FileSizeBytes
            };

            return result;
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static async Task WriteFileInChunksAsync(string path, long totalSizeBytes, int chunkSizeBytes)
    {
        await using var stream = File.Create(path);
        var buffer = new byte[chunkSizeBytes];
        long remaining = totalSizeBytes;

        while (remaining > 0)
        {
            var writeLen = (int)Math.Min(buffer.Length, remaining);
            for (var i = 0; i < writeLen; i++)
            {
                buffer[i] = (byte)((i + remaining) % 251);
            }

            await stream.WriteAsync(buffer.AsMemory(0, writeLen));
            remaining -= writeLen;
        }
    }

    private static async Task<string> ComputeHashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
        {
            hasher.AppendData(buffer, 0, bytesRead);
        }

        return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
    }

    private static async Task<bool> ValidateShortFileRejectedAsync(string sourcePath, int chunkSize)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "udm10-short-file-check", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var shortSource = Path.Combine(tempDir, "source.bin");
        var shortTarget = Path.Combine(tempDir, "target.part");

        try
        {
            var fileInfo = new FileInfo(sourcePath);
            var truncatedLength = Math.Max(1, fileInfo.Length - 1);
            File.Copy(sourcePath, shortSource, true);

            await using (var stream = File.Open(shortSource, FileMode.Open, FileAccess.ReadWrite))
            {
                stream.SetLength(truncatedLength);
            }

            var rejected = false;
            try
            {
                await CopyExactlyAsync(shortSource, shortTarget, fileInfo.Length, chunkSize);
            }
            catch (EndOfStreamException)
            {
                rejected = true;
            }

            return rejected && !File.Exists(shortTarget);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static async Task CopyExactlyAsync(string sourcePath, string targetPath, long expectedLength, int chunkSize)
    {
        try
        {
            var buffer = new byte[Math.Max(1, chunkSize)];
            var totalRead = 0L;
            await using var source = File.OpenRead(sourcePath);
            await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, true);
            while (totalRead < expectedLength)
            {
                var bytesRead = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, expectedLength - totalRead)));
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException($"Received {totalRead}/{expectedLength} bytes.");
                }

                await target.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;
            }
            await target.FlushAsync();
        }
        catch
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            throw;
        }
    }

    private static async Task<bool> CanOpenFileAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, true);
        return stream.Length >= 0;
    }

    private static string BuildMarkdown(MachineProfile machine, IEnumerable<TransferResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Upload performance summary");
        sb.AppendLine();
        sb.AppendLine("## Machine configuration");
        sb.AppendLine($"- OS: {machine.OsDescription}");
        sb.AppendLine($"- Runtime: {machine.Framework}");
        sb.AppendLine($"- Logical processors: {machine.ProcessorCount}");
        sb.AppendLine($"- Available memory: {machine.TotalAvailableMemoryBytes / (1024d * 1024d * 1024d):F2} GB");
        sb.AppendLine($"- Network: {machine.NetworkDescription}");
        sb.AppendLine();
        sb.AppendLine("## Dataset");
        sb.AppendLine("- 2 load levels: light-load (32 MB / 64 KB chunk) and heavy-load (512 MB / 256 KB chunk)");
        sb.AppendLine("- Files are generated in chunked fashion and streamed to disk without loading the full payload into memory at once.");
        sb.AppendLine("- Integrity is verified by SHA-256 after transfer and short files are explicitly rejected as incomplete.");
        sb.AppendLine();
        sb.AppendLine("## Results");
        sb.AppendLine("| Scenario | Size | Chunk | Time (ms) | Throughput (MB/s) | CPU (%) | Allocated (MB) | RAM (MB) | Success | Integrity | Partial rejection |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- |");

        foreach (var result in results)
        {
            sb.AppendLine(
                $"| {result.Name} | {result.FileSizeBytes / (1024d * 1024d):F0} MB | {result.ChunkSizeBytes / 1024d:F0} KB | {result.ElapsedMs:F2} | {result.ThroughputMBps:F2} | {result.CpuPercent:F1} | {result.AllocatedBytes / (1024d * 1024d):F2} | {result.PeakWorkingSetBytes / (1024d * 1024d):F2} | {result.TransferSucceeded} | {result.IntegrityOk} | {result.PartialFileRejected} |");
        }

        return sb.ToString();
    }
}

[MemoryDiagnoser]
public class UploadChunkBenchmarks
{
    [Params(64 * 1024, 256 * 1024)]
    public int ChunkSizeBytes { get; set; } = 64 * 1024;

    [Params(32L * 1024 * 1024, 512L * 1024 * 1024)]
    public long FileSizeBytes { get; set; } = 32L * 1024 * 1024;

    [Benchmark]
    public long StreamWriteInChunks()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "udm10-benchmark", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sourcePath = Path.Combine(tempDir, "source.bin");
        var targetPath = Path.Combine(tempDir, "target.bin");

        try
        {
            var pattern = new byte[ChunkSizeBytes];
            for (var i = 0; i < pattern.Length; i++)
            {
                pattern[i] = (byte)((i + FileSizeBytes) % 251);
            }

            using (var writer = File.Create(sourcePath))
            {
                long remaining = FileSizeBytes;
                while (remaining > 0)
                {
                    var writeLength = (int)Math.Min(pattern.Length, remaining);
                    writer.Write(pattern, 0, writeLength);
                    remaining -= writeLength;
                }
            }

            long totalBytes = 0;
            using (var source = File.OpenRead(sourcePath))
            using (var target = File.Create(targetPath))
            {
                var buffer = new byte[ChunkSizeBytes];
                int bytesRead;
                while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    target.Write(buffer, 0, bytesRead);
                    totalBytes += bytesRead;
                }
            }

            return totalBytes;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Benchmark]
    public string ComputeSha256Streaming()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "udm10-benchmark-sha", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var path = Path.Combine(tempDir, "source.bin");

        try
        {
            using (var stream = File.Create(path))
            {
                var buffer = new byte[ChunkSizeBytes];
                long remaining = FileSizeBytes;
                while (remaining > 0)
                {
                    var writeLength = (int)Math.Min(buffer.Length, remaining);
                    for (var i = 0; i < writeLength; i++)
                    {
                        buffer[i] = (byte)((i + remaining) % 251);
                    }

                    stream.Write(buffer, 0, writeLength);
                    remaining -= writeLength;
                }
            }

            using var file = File.OpenRead(path);
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var bytes = new byte[ChunkSizeBytes];
            int bytesRead;
            while ((bytesRead = file.Read(bytes, 0, bytes.Length)) > 0)
            {
                hasher.AppendData(bytes, 0, bytesRead);
            }

            return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
