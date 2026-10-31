using System.IO.Pipes;
using System.Text.Json;

namespace MonitorsFocus.Core;

public static class ControlProtocol
{
    public static string PipeName => $"MonitorsFocus.ControlCenter.{Environment.UserName}";
    public static JsonSerializerOptions Json { get; } = new() { WriteIndented = false };

    public static async Task<ControlSnapshot> SendAsync(ControlRequest request, CancellationToken cancellation = default)
    {
        using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(3000, cancellation);
        using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, leaveOpen: true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(request, Json));
        var response = await reader.ReadLineAsync(cancellation);
        return JsonSerializer.Deserialize<ControlSnapshot>(response ?? throw new IOException("控制核心已断开。"), Json)
            ?? throw new IOException("控制核心未返回状态。");
    }
}
