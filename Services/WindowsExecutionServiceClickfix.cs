using DetonatorAgent.Services;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AutoIt;

namespace DetonatorAgent.Services;

[SupportedOSPlatform("windows")]
public class WindowsExecutionServiceClickfix : IExecutionService {
    private readonly ILogger<IExecutionService> _logger;
    public string ExecutionTypeName => "clickfix";

    private string cmd = "";

    public WindowsExecutionServiceClickfix(ILogger<IExecutionService> logger) {
        _logger = logger;
    }

    public async Task<bool> WriteMalwareAsync(string filePath, byte[] content, byte? xorKey = null) {
        cmd = System.Text.Encoding.UTF8.GetString(content);

        // xor decode
        if (xorKey.HasValue) {
            byte[] decodedBytes = new byte[content.Length];
            for (int i = 0; i < content.Length; i++) {
                decodedBytes[i] = (byte)(content[i] ^ xorKey.Value);
            }
            cmd = System.Text.Encoding.UTF8.GetString(decodedBytes);
        }

        return await Task.FromResult(true);
    }

    public async Task<(bool Success, int Pid, string? ErrorMessage)> StartProcessAsync(string? arguments = null) {
        try {
            _logger.LogInformation("Starting process using clickfix method");

            // Press Windows + R to open Run dialog
            AutoItX.Send("#r");
            await Task.Delay(500); // Wait for Run dialog to appear

            // Type cmd in the Run dialog
            AutoItX.Send(cmd);
            await Task.Delay(200);

            // Press Enter to execute
            AutoItX.Send("{ENTER}");
            await Task.Delay(500);

            _logger.LogInformation("Successfully executed clickfix sequence");
            return (true, 0, null);
        } catch (Exception ex) {
            _logger.LogError(ex, "Error during clickfix execution");
            return (false, 0, ex.Message);
        }
    }

    public Task<(bool Success, string? ErrorMessage)> KillLastExecutionAsync() {
        return Task.FromResult((false, null as string));
    }

    public Task<(int Pid, string Stdout, string Stderr)> GetExecutionLogsAsync() {
        return Task.FromResult((0, string.Empty, string.Empty));
    }
}