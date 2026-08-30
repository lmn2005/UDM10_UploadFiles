using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UDM10.Shared;

return await UploadTcpBenchmark.RunAsync(args);

internal sealed record BenchmarkOptions(
    string OutputDirectory,
    string ServerBuildDirectory,
    bool AllowNonWindows);

internal sealed record PayloadScenario(
    string Name,
    int ChunkSizeBytes,
    long FileSizeBytes);

internal sealed class TransferResult
{
    public string Name { get; init; } = string.Empty;
    public int ChunkSizeBytes { get; init; }
    public long FileSizeBytes { get; init; }
    public long BytesSent { get; init; }
    public double ElapsedMs { get; init; }
    public double ThroughputMBps { get; init; }
    public double ClientCpuPercent { get; init; }
    public double ServerCpuPercent { get; init; }
    public long ClientPeakWorkingSetBytes { get; init; }
    public long ServerPeakWorkingSetBytes { get; init; }
    public long SourcePhysicalSizeBytes { get; init; }
    public long ReceivedPhysicalSizeBytes { get; init; }
    public string SourceSha256 { get; init; } = string.Empty;
    public string ReceivedSha256 { get; init; } = string.Empty;
    public bool IntegrityOk { get; init; }
    public bool TransferSucceeded { get; init; }
}

internal sealed class MachineProfile
{
    public string OsDescription { get; init; } = string.Empty;
    public string Framework { get; init; } = string.Empty;
    public int ProcessorCount { get; init; }
    public long TotalAvailableMemoryBytes { get; init; }
    public string NetworkDescription { get; init; } = string.Empty;
    public bool OfficialWindowsRun { get; init; }
}

internal sealed class ResourceSampler
{
    private readonly Process _clientProcess;
    private readonly Process _serverProcess;

    public ResourceSampler(
        Process clientProcess,
        Process serverProcess)
    {
        _clientProcess = clientProcess;
        _serverProcess = serverProcess;
    }

    public long ClientPeakWorkingSetBytes { get; private set; }
    public long ServerPeakWorkingSetBytes { get; private set; }

    public async Task SampleAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            SampleOnce();

            try
            {
                await Task.Delay(10, cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        SampleOnce();
    }

    private void SampleOnce()
    {
        try
        {
            _clientProcess.Refresh();
            ClientPeakWorkingSetBytes = Math.Max(
                ClientPeakWorkingSetBytes,
                _clientProcess.WorkingSet64);
        }
        catch (InvalidOperationException)
        {
        }

        try
        {
            _serverProcess.Refresh();
            ServerPeakWorkingSetBytes = Math.Max(
                ServerPeakWorkingSetBytes,
                _serverProcess.WorkingSet64);
        }
        catch (InvalidOperationException)
        {
        }
    }
}

internal static class UploadTcpBenchmark
{
    private static readonly PayloadScenario[] Scenarios =
    [
        new("light-load", 64 * 1024, 32L * 1024 * 1024),
        new("heavy-load", 256 * 1024, 512L * 1024 * 1024)
    ];

    public static async Task<int> RunAsync(string[] args)
    {
        BenchmarkOptions options;

        try
        {
            options = ParseOptions(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return 2;
        }

        bool isWindows = OperatingSystem.IsWindows();
        if (!isWindows && !options.AllowNonWindows)
        {
            Console.Error.WriteLine(
                "Benchmark chính thức chỉ được chạy trên Windows. " +
                "Dùng --allow-non-windows chỉ để kiểm tra kỹ thuật, " +
                "không dùng kết quả đó làm bằng chứng nghiệm thu.");
            return 3;
        }

        if (!Directory.Exists(options.ServerBuildDirectory) ||
            !File.Exists(Path.Combine(
                options.ServerBuildDirectory,
                "UDM10.Server.dll")))
        {
            Console.Error.WriteLine(
                $"Không tìm thấy Release build của Server tại: " +
                $"{options.ServerBuildDirectory}");
            Console.Error.WriteLine(
                "Hãy chạy: dotnet build Code/UDM10.sln -c Release");
            return 4;
        }

        Directory.CreateDirectory(options.OutputDirectory);

        string runRoot = Path.Combine(
            Path.GetTempPath(),
            "udm10-tcp-benchmark",
            Guid.NewGuid().ToString("N"));
        string serverDirectory = Path.Combine(runRoot, "Server");
        string uploadsDirectory = Path.Combine(runRoot, "Uploads");
        string payloadDirectory = Path.Combine(runRoot, "Payloads");
        Directory.CreateDirectory(serverDirectory);
        Directory.CreateDirectory(uploadsDirectory);
        Directory.CreateDirectory(payloadDirectory);

        Process? serverProcess = null;
        StringBuilder serverOutput = new();
        int port = GetAvailableTcpPort();

        try
        {
            CopyDirectory(
                options.ServerBuildDirectory,
                serverDirectory);
            await WriteServerSettingsAsync(
                serverDirectory,
                uploadsDirectory,
                port);

            serverProcess = StartServer(
                serverDirectory,
                runRoot,
                serverOutput);
            await WaitForServerAsync(
                serverProcess,
                serverOutput,
                port,
                TimeSpan.FromSeconds(10));

            MachineProfile machine = new()
            {
                OsDescription = RuntimeInformation.OSDescription,
                Framework = RuntimeInformation.FrameworkDescription,
                ProcessorCount = Environment.ProcessorCount,
                TotalAvailableMemoryBytes =
                    GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                NetworkDescription =
                    "TCP loopback; Benchmark client process -> UDM10.Server process",
                OfficialWindowsRun = isWindows
            };

            List<TransferResult> results = [];
            foreach (PayloadScenario scenario in Scenarios)
            {
                results.Add(
                    await MeasureScenarioAsync(
                        scenario,
                        payloadDirectory,
                        uploadsDirectory,
                        serverProcess,
                        port));
            }

            bool partialFileRejected =
                await ValidatePartialUploadCleanupAsync(
                    uploadsDirectory,
                    port);

            await WriteReportsAsync(
                options.OutputDirectory,
                machine,
                results,
                partialFileRejected,
                serverOutput.ToString());

            PrintResults(
                machine,
                results,
                partialFileRejected,
                options.OutputDirectory);

            bool passed =
                results.All(result =>
                    result.TransferSucceeded &&
                    result.IntegrityOk) &&
                partialFileRejected;

            return passed ? 0 : 1;
        }
        finally
        {
            if (serverProcess is not null)
            {
                await StopServerAsync(serverProcess);
                serverProcess.Dispose();
            }

            TryDeleteDirectory(runRoot);
        }
    }

    private static async Task<TransferResult> MeasureScenarioAsync(
        PayloadScenario scenario,
        string payloadDirectory,
        string uploadsDirectory,
        Process serverProcess,
        int port)
    {
        string requestId = Guid.NewGuid().ToString("N");
        string fileName = $"{scenario.Name}-{requestId}.bin";
        string sourcePath = Path.Combine(payloadDirectory, fileName);
        string receivedPath = Path.Combine(uploadsDirectory, fileName);

        await WriteFileInChunksAsync(
            sourcePath,
            scenario.FileSizeBytes,
            scenario.ChunkSizeBytes);
        string sourceHash = await ComputeHashAsync(sourcePath);

        using Process clientProcess = Process.GetCurrentProcess();
        clientProcess.Refresh();
        serverProcess.Refresh();
        TimeSpan clientCpuBefore = clientProcess.TotalProcessorTime;
        TimeSpan serverCpuBefore = serverProcess.TotalProcessorTime;

        ResourceSampler sampler = new(
            clientProcess,
            serverProcess);
        using CancellationTokenSource samplerCts = new();
        Task samplerTask = sampler.SampleAsync(samplerCts.Token);
        Stopwatch stopwatch = Stopwatch.StartNew();

        long bytesSent;
        try
        {
            bytesSent = await UploadFileAsync(
                sourcePath,
                fileName,
                scenario.FileSizeBytes,
                sourceHash,
                scenario.ChunkSizeBytes,
                requestId,
                port);
        }
        finally
        {
            stopwatch.Stop();
            samplerCts.Cancel();
            await samplerTask;
        }

        clientProcess.Refresh();
        serverProcess.Refresh();
        TimeSpan clientCpu =
            clientProcess.TotalProcessorTime - clientCpuBefore;
        TimeSpan serverCpu =
            serverProcess.TotalProcessorTime - serverCpuBefore;
        double seconds = Math.Max(
            stopwatch.Elapsed.TotalSeconds,
            0.001);

        if (!File.Exists(receivedPath))
        {
            throw new InvalidDataException(
                $"Server báo Completed nhưng không có file: {receivedPath}");
        }

        string receivedHash = await ComputeHashAsync(receivedPath);
        long sourceLength = new FileInfo(sourcePath).Length;
        long receivedLength = new FileInfo(receivedPath).Length;
        bool integrityOk =
            sourceLength == receivedLength &&
            string.Equals(
                sourceHash,
                receivedHash,
                StringComparison.OrdinalIgnoreCase);

        return new TransferResult
        {
            Name = scenario.Name,
            ChunkSizeBytes = scenario.ChunkSizeBytes,
            FileSizeBytes = scenario.FileSizeBytes,
            BytesSent = bytesSent,
            ElapsedMs = stopwatch.Elapsed.TotalMilliseconds,
            ThroughputMBps =
                bytesSent / (1024d * 1024d) / seconds,
            ClientCpuPercent = ToCpuPercent(clientCpu, seconds),
            ServerCpuPercent = ToCpuPercent(serverCpu, seconds),
            ClientPeakWorkingSetBytes =
                sampler.ClientPeakWorkingSetBytes,
            ServerPeakWorkingSetBytes =
                sampler.ServerPeakWorkingSetBytes,
            SourcePhysicalSizeBytes = sourceLength,
            ReceivedPhysicalSizeBytes = receivedLength,
            SourceSha256 = sourceHash,
            ReceivedSha256 = receivedHash,
            IntegrityOk = integrityOk,
            TransferSucceeded =
                bytesSent == scenario.FileSizeBytes
        };
    }

    private static async Task<long> UploadFileAsync(
        string sourcePath,
        string fileName,
        long fileSize,
        string fileHash,
        int chunkSize,
        string requestId,
        int port)
    {
        using CancellationTokenSource timeoutCts =
            new(TimeSpan.FromMinutes(5));
        using TcpClient client = new();
        await client.ConnectAsync(
            IPAddress.Loopback,
            port,
            timeoutCts.Token);
        await using NetworkStream stream = client.GetStream();

        UploadRequest request = new()
        {
            ProtocolVersion = ProtocolConstants.CurrentVersion,
            RequestId = requestId,
            FileName = fileName,
            FileSize = fileSize,
            FileHash = fileHash,
            Status = UploadStatus.Request
        };

        await ProtocolWriter.WriteRequestAsync(
            stream,
            request,
            timeoutCts.Token);
        UploadResponse ready =
            await ReadRequiredResponseAsync(
                stream,
                requestId,
                timeoutCts.Token);

        if (ready.Status != UploadStatus.Ready)
        {
            throw new InvalidDataException(
                $"Server không Ready: {ready.ErrorCode} - " +
                $"{ready.ErrorMessage}");
        }

        long bytesSent = 0;
        byte[] buffer = new byte[chunkSize];
        await using (FileStream source = File.OpenRead(sourcePath))
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(
                       buffer.AsMemory(),
                       timeoutCts.Token)) > 0)
            {
                await stream.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    timeoutCts.Token);
                bytesSent += bytesRead;
            }
        }

        await stream.FlushAsync(timeoutCts.Token);
        UploadResponse completed =
            await ReadRequiredResponseAsync(
                stream,
                requestId,
                timeoutCts.Token);

        if (completed.Status != UploadStatus.Completed ||
            completed.ErrorCode != ErrorCode.None)
        {
            throw new InvalidDataException(
                $"Upload không Completed: {completed.ErrorCode} - " +
                $"{completed.ErrorMessage}");
        }

        return bytesSent;
    }

    private static async Task<UploadResponse> ReadRequiredResponseAsync(
        Stream stream,
        string expectedRequestId,
        CancellationToken cancellationToken)
    {
        UploadResponse? response =
            await ProtocolReader.ReadResponseAsync(
                stream,
                cancellationToken);

        if (response is null ||
            !string.Equals(
                response.ProtocolVersion,
                ProtocolConstants.CurrentVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                response.RequestId,
                expectedRequestId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Response ProtocolVersion/RequestId không hợp lệ.");
        }

        return response;
    }

    private static async Task<bool> ValidatePartialUploadCleanupAsync(
        string uploadsDirectory,
        int port)
    {
        const int expectedLength = 1024 * 1024;
        string requestId = Guid.NewGuid().ToString("N");
        string fileName = $"partial-{requestId}.bin";
        string finalPath = Path.Combine(uploadsDirectory, fileName);
        string partPath = finalPath + ".part";
        byte[] completePayload = new byte[expectedLength];
        RandomNumberGenerator.Fill(completePayload);

        using (TcpClient client = new())
        {
            await client.ConnectAsync(IPAddress.Loopback, port);
            await using NetworkStream stream = client.GetStream();

            UploadRequest request = new()
            {
                ProtocolVersion = ProtocolConstants.CurrentVersion,
                RequestId = requestId,
                FileName = fileName,
                FileSize = expectedLength,
                FileHash = Convert.ToHexString(
                    SHA256.HashData(completePayload)).ToLowerInvariant(),
                Status = UploadStatus.Request
            };

            await ProtocolWriter.WriteRequestAsync(stream, request);
            UploadResponse ready =
                await ReadRequiredResponseAsync(
                    stream,
                    requestId,
                    CancellationToken.None);

            if (ready.Status != UploadStatus.Ready)
            {
                return false;
            }

            await stream.WriteAsync(
                completePayload.AsMemory(0, expectedLength / 2));
            await stream.FlushAsync();
        }

        // Ready được gửi trước khi Server mở file .part; chờ tối thiểu một khoảng
        // ngắn để tránh kết luận cleanup thành công trước khi session xử lý EOF.
        await Task.Delay(250);

        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline &&
               (File.Exists(finalPath) || File.Exists(partPath)))
        {
            await Task.Delay(50);
        }

        return !File.Exists(finalPath) &&
            !File.Exists(partPath);
    }

    private static async Task WriteFileInChunksAsync(
        string path,
        long totalSizeBytes,
        int chunkSizeBytes)
    {
        await using FileStream stream = File.Create(path);
        byte[] buffer = new byte[chunkSizeBytes];
        long remaining = totalSizeBytes;

        while (remaining > 0)
        {
            int writeLength = (int)Math.Min(
                buffer.Length,
                remaining);
            RandomNumberGenerator.Fill(
                buffer.AsSpan(0, writeLength));
            await stream.WriteAsync(
                buffer.AsMemory(0, writeLength));
            remaining -= writeLength;
        }
    }

    private static async Task<string> ComputeHashAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static double ToCpuPercent(
        TimeSpan cpuTime,
        double elapsedSeconds)
    {
        return cpuTime.TotalSeconds /
            elapsedSeconds /
            Environment.ProcessorCount *
            100d;
    }

    private static Process StartServer(
        string serverDirectory,
        string workingDirectory,
        StringBuilder output)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(
            Path.Combine(
                serverDirectory,
                "UDM10.Server.dll"));

        Process process = new()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        process.OutputDataReceived += (_, eventArgs) =>
            AppendProcessOutput(output, eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) =>
            AppendProcessOutput(output, eventArgs.Data);

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Không thể khởi động UDM10.Server.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void AppendProcessOutput(
        StringBuilder output,
        string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (output)
        {
            output.AppendLine(line);
        }
    }

    private static async Task WaitForServerAsync(
        Process process,
        StringBuilder output,
        int port,
        TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Server thoát sớm với mã {process.ExitCode}.\n" +
                    output);
            }

            try
            {
                using TcpClient probe = new();
                await probe.ConnectAsync(
                    IPAddress.Loopback,
                    port);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(50);
            }
        }

        throw new TimeoutException(
            $"Server không mở port {port} trong {timeout.TotalSeconds:F0} giây.");
    }

    private static async Task StopServerAsync(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
    }

    private static int GetAvailableTcpPort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Server.ExclusiveAddressUse = true;
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void CopyDirectory(
        string sourceDirectory,
        string destinationDirectory)
    {
        foreach (string directory in Directory.EnumerateDirectories(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(
                Path.Combine(
                    destinationDirectory,
                    Path.GetRelativePath(sourceDirectory, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            string destination = Path.Combine(
                destinationDirectory,
                Path.GetRelativePath(sourceDirectory, file));
            Directory.CreateDirectory(
                Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static async Task WriteServerSettingsAsync(
        string serverDirectory,
        string uploadsDirectory,
        int port)
    {
        var settings = new
        {
            Network = new
            {
                ServerIp = "127.0.0.1",
                Port = port,
                ReceiveTimeoutMs = 30000
            },
            Upload = new
            {
                SaveDirectory = uploadsDirectory,
                ChunkSizeBytes = 65536,
                MaxAllowedSizeInBytes = 2L * 1024 * 1024 * 1024
            }
        };

        await File.WriteAllTextAsync(
            Path.Combine(serverDirectory, "appsettings.json"),
            JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }

    private static async Task WriteReportsAsync(
        string outputDirectory,
        MachineProfile machine,
        IReadOnlyCollection<TransferResult> results,
        bool partialFileRejected,
        string serverOutput)
    {
        string suffix = machine.OfficialWindowsRun
            ? string.Empty
            : "-non-windows";
        string jsonPath = Path.Combine(
            outputDirectory,
            $"upload-performance-summary{suffix}.json");
        string markdownPath = Path.Combine(
            outputDirectory,
            $"upload-performance-summary{suffix}.md");
        string logPath = Path.Combine(
            outputDirectory,
            $"upload-performance-server{suffix}.log");

        var report = new
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Machine = machine,
            PartialFileRejected = partialFileRejected,
            Results = results
        };

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        await File.WriteAllTextAsync(
            markdownPath,
            BuildMarkdown(
                machine,
                results,
                partialFileRejected));
        await File.WriteAllTextAsync(logPath, serverOutput);
    }

    private static string BuildMarkdown(
        MachineProfile machine,
        IEnumerable<TransferResult> results,
        bool partialFileRejected)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Upload TCP performance summary");
        builder.AppendLine();
        builder.AppendLine("## Machine configuration");
        builder.AppendLine($"- OS: {machine.OsDescription}");
        builder.AppendLine($"- Runtime: {machine.Framework}");
        builder.AppendLine($"- Logical processors: {machine.ProcessorCount}");
        builder.AppendLine(
            $"- Available memory: " +
            $"{machine.TotalAvailableMemoryBytes / (1024d * 1024d * 1024d):F2} GB");
        builder.AppendLine($"- Network: {machine.NetworkDescription}");
        builder.AppendLine(
            $"- Official Windows evidence: {machine.OfficialWindowsRun}");
        builder.AppendLine();
        builder.AppendLine("## Results");
        builder.AppendLine(
            "| Scenario | Size | Chunk | Time (ms) | TCP throughput (MB/s) | Client CPU (%) | Server CPU (%) | Client peak RAM (MB) | Server peak RAM (MB) | Integrity |");
        builder.AppendLine(
            "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |");

        foreach (TransferResult result in results)
        {
            builder.AppendLine(
                $"| {result.Name} | " +
                $"{result.FileSizeBytes / (1024d * 1024d):F0} MB | " +
                $"{result.ChunkSizeBytes / 1024d:F0} KB | " +
                $"{result.ElapsedMs:F2} | " +
                $"{result.ThroughputMBps:F2} | " +
                $"{result.ClientCpuPercent:F1} | " +
                $"{result.ServerCpuPercent:F1} | " +
                $"{result.ClientPeakWorkingSetBytes / (1024d * 1024d):F2} | " +
                $"{result.ServerPeakWorkingSetBytes / (1024d * 1024d):F2} | " +
                $"{result.IntegrityOk} |");
        }

        builder.AppendLine();
        builder.AppendLine(
            $"- Partial upload rejected and .part cleaned: " +
            $"{partialFileRejected}");
        return builder.ToString();
    }

    private static void PrintResults(
        MachineProfile machine,
        IEnumerable<TransferResult> results,
        bool partialFileRejected,
        string outputDirectory)
    {
        Console.WriteLine("=== UDM10 TCP benchmark ===");
        Console.WriteLine($"OS: {machine.OsDescription}");
        Console.WriteLine($"Network: {machine.NetworkDescription}");

        foreach (TransferResult result in results)
        {
            Console.WriteLine(
                $"{result.Name}: " +
                $"{result.ThroughputMBps:F2} MB/s, " +
                $"clientCPU={result.ClientCpuPercent:F1}%, " +
                $"serverCPU={result.ServerCpuPercent:F1}%, " +
                $"clientRAM={result.ClientPeakWorkingSetBytes / (1024d * 1024d):F2} MB, " +
                $"serverRAM={result.ServerPeakWorkingSetBytes / (1024d * 1024d):F2} MB, " +
                $"integrity={result.IntegrityOk}");
        }

        Console.WriteLine(
            $"Partial upload cleanup: {partialFileRejected}");
        Console.WriteLine($"Reports: {outputDirectory}");

        if (!machine.OfficialWindowsRun)
        {
            Console.WriteLine(
                "WARNING: Đây chỉ là kiểm tra kỹ thuật ngoài Windows, " +
                "không phải bằng chứng nghiệm thu.");
        }
    }

    private static BenchmarkOptions ParseOptions(string[] args)
    {
        string repositoryRoot = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                ".."));
        string outputDirectory = Path.Combine(
            repositoryRoot,
            "Extra",
            "Performance");
        string serverBuildDirectory = Path.Combine(
            repositoryRoot,
            "Code",
            "Server",
            "bin",
            "Release",
            "net10.0");
        bool allowNonWindows = false;

        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--allow-non-windows":
                    allowNonWindows = true;
                    break;
                case "--output":
                    outputDirectory = ReadOptionValue(
                        args,
                        ref index,
                        "--output");
                    break;
                case "--server-build":
                    serverBuildDirectory = ReadOptionValue(
                        args,
                        ref index,
                        "--server-build");
                    break;
                default:
                    throw new ArgumentException(
                        $"Tham số không hỗ trợ: {args[index]}");
            }
        }

        return new BenchmarkOptions(
            Path.GetFullPath(outputDirectory),
            Path.GetFullPath(serverBuildDirectory),
            allowNonWindows);
    }

    private static string ReadOptionValue(
        string[] args,
        ref int index,
        string option)
    {
        if (++index >= args.Length ||
            string.IsNullOrWhiteSpace(args[index]))
        {
            throw new ArgumentException(
                $"Thiếu giá trị cho {option}.");
        }

        return args[index];
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            "dotnet run --project Benchmark/Benchmark.csproj -c Release -- " +
            "[--output <directory>] [--server-build <directory>] " +
            "[--allow-non-windows]");
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine(
                $"Không thể dọn thư mục benchmark tạm '{path}': " +
                ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine(
                $"Không thể dọn thư mục benchmark tạm '{path}': " +
                ex.Message);
        }
    }
}
