using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ShareX.Linux.Core.Helpers;

namespace ShareX.Linux.Ipc;

public static class IpcClient
{
    public static async Task<(bool Success, string Response)> SendCommandAsync(string command, int timeoutMs = 3000)
    {
        var socketPath = PathsHelper.IpcSocketPath;
        if (!File.Exists(socketPath))
        {
            return (false, "Socket file does not exist (daemon not running)");
        }

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var cts = new System.Threading.CancellationTokenSource(timeoutMs);

            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cts.Token).ConfigureAwait(false);

            using var stream = new NetworkStream(socket, ownsSocket: false);
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8);

            await writer.WriteLineAsync(command).ConfigureAwait(false);
            var response = await reader.ReadLineAsync(cts.Token).ConfigureAwait(false);

            return (true, response ?? "OK");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<(bool Success, string Response)> SendCommandWithRetryAsync(string command, int maxRetries = 10, int delayMs = 150)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            var res = await SendCommandAsync(command, timeoutMs: 1000).ConfigureAwait(false);
            if (res.Success) return res;

            if (i < maxRetries - 1)
            {
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
        }

        return await SendCommandAsync(command).ConfigureAwait(false);
    }
}
