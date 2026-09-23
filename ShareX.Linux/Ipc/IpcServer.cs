using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShareX.Linux.Core.Helpers;

namespace ShareX.Linux.Ipc;

public class IpcServer : IDisposable
{
    private readonly Socket _listener;
    private readonly string _socketPath;
    private readonly CancellationTokenSource _cts = new();
    private readonly Func<string, Task<string>> _commandHandler;

    public IpcServer(Func<string, Task<string>> commandHandler)
    {
        _commandHandler = commandHandler;
        _socketPath = PathsHelper.IpcSocketPath;

        var dir = Path.GetDirectoryName(_socketPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (File.Exists(_socketPath))
        {
            try { File.Delete(_socketPath); } catch { }
        }

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listener.Listen(10);
    }

    public void Start()
    {
        Task.Run(AcceptConnectionsLoopAsync);
    }

    private async Task AcceptConnectionsLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await _listener.AcceptAsync(_cts.Token).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(clientSocket), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[IpcServer] Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(Socket socket)
    {
        using (socket)
        {
            try
            {
                using var stream = new NetworkStream(socket, ownsSocket: false);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                var command = await reader.ReadLineAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(command))
                {
                    var response = await _commandHandler(command.Trim()).ConfigureAwait(false);
                    await writer.WriteLineAsync(response).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[IpcServer] Client handler error: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        try { _listener.Close(); } catch { }
        try { _listener.Dispose(); } catch { }
        if (File.Exists(_socketPath))
        {
            try { File.Delete(_socketPath); } catch { }
        }
    }
}
