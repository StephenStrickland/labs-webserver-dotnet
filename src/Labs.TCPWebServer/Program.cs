using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Labs.TCPWebServer;

public class Program
{
    public static async Task Main()
    {
        using var server = new TCPWebServer();
        await server.StartAsync();
    }
}

public class TCPWebServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly string _staticFilesPath;
    private bool _isRunning;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly int _port;

    public TCPWebServer(int port = 8080, string? staticFilesPath = null)
    {
        _port = port;
        _staticFilesPath = staticFilesPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "static");
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task StartAsync()
    {
        // Ensure static directory exists
        if (!Directory.Exists(_staticFilesPath))
        {
            Directory.CreateDirectory(_staticFilesPath);
            Logger.Info($"Created static files directory at {_staticFilesPath}");
        }

        try
        {
            _listener.Start();
            _isRunning = true;
            Logger.Info($"Server started. Listening at http://localhost:{_port}/");

            // Gracefully stop on Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Prevent immediate termination
                Stop();
            };

            while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (SocketException) when (!_isRunning)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to accept client: {ex.Message}");
                }
            }
        }
        catch (SocketException ex)
        {
            Logger.Error($"Failed to start server: {ex.Message}");
            throw;
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                
                // Read the request line
                var requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(requestLine))
                {
                    return;
                }

                var parts = requestLine.Split(' ');
                if (parts.Length != 3)
                {
                    await SendResponseAsync(stream, 400, "Bad Request", "text/plain", "Invalid HTTP request");
                    return;
                }

                var method = parts[0];
                var path = parts[1];
                var version = parts[2];

                Logger.Info($"Received request: {method} {path}");

                // Skip headers (for now we don't need them)
                string line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync())) { }

                if (!method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    await SendResponseAsync(stream, 405, "Method Not Allowed", "text/plain", "Method not allowed");
                    Logger.Warn("Method not allowed.");
                    return;
                }

                switch (path)
                {
                    case "/":
                        await HandleRootRouteAsync(stream);
                        break;
                    case string s when s.StartsWith("/static/", StringComparison.OrdinalIgnoreCase):
                        await HandleStaticFileAsync(stream, path);
                        break;
                    default:
                        await SendResponseAsync(stream, 404, "Not Found", "text/plain", "Not Found");
                        Logger.Warn($"Route not found: {path}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to handle client: {ex.Message}");
            }
        }
    }

    private async Task HandleRootRouteAsync(NetworkStream stream)
    {
        string responseString = "Hello from TCP .NET server!";
        await SendResponseAsync(stream, 200, "OK", "text/plain", responseString);
    }

    private async Task HandleStaticFileAsync(NetworkStream stream, string path)
    {
        try
        {
            string relativePath = path.Substring("/static/".Length);
            if (string.IsNullOrEmpty(relativePath))
            {
                await SendResponseAsync(stream, 400, "Bad Request", "text/plain", "Invalid path");
                Logger.Warn("Invalid static file path: URL was null or empty");
                return;
            }

            string filePath = Path.Combine(_staticFilesPath, relativePath);
            string fullPath = Path.GetFullPath(filePath);
            string fullStaticPath = Path.GetFullPath(_staticFilesPath);

            // Verify path is within static directory
            if (!fullPath.StartsWith(fullStaticPath))
            {
                await SendResponseAsync(stream, 404, "Not Found", "text/plain", "Not Found");
                Logger.Warn($"Invalid file path: {relativePath}");
                return;
            }

            if (!File.Exists(filePath))
            {
                await SendResponseAsync(stream, 404, "Not Found", "text/plain", "File not found");
                Logger.Warn($"File not found: {relativePath}");
                return;
            }

            string extension = Path.GetExtension(filePath);
            string contentType = extension.ToLower() switch
            {
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream",
            };

            await using var fileStream = File.OpenRead(filePath);
            await SendResponseAsync(stream, 200, "OK", contentType, fileStream);
            Logger.Info($"Served static file: {relativePath}");
        }
        catch (Exception ex)
        {
            await SendResponseAsync(stream, 500, "Internal Server Error", "text/plain", "Internal Server Error");
            Logger.Error($"Error serving static file: {ex.Message}");
        }
    }

    private async Task SendResponseAsync(NetworkStream stream, int statusCode, string statusText, string contentType, string content)
    {
        var contentBytes = Encoding.UTF8.GetBytes(content);
        await SendResponseAsync(stream, statusCode, statusText, contentType, contentBytes);
    }

    private async Task SendResponseAsync(NetworkStream stream, int statusCode, string statusText, string contentType, FileStream fileStream)
    {
        var headerBuilder = new StringBuilder();
        headerBuilder.AppendLine($"HTTP/1.1 {statusCode} {statusText}");
        headerBuilder.AppendLine($"Content-Type: {contentType}");
        headerBuilder.AppendLine($"Content-Length: {fileStream.Length}");
        headerBuilder.AppendLine("Connection: close");
        headerBuilder.AppendLine();

        var headerBytes = Encoding.ASCII.GetBytes(headerBuilder.ToString());
        await stream.WriteAsync(headerBytes);
        await fileStream.CopyToAsync(stream);
    }

    private async Task SendResponseAsync(NetworkStream stream, int statusCode, string statusText, string contentType, byte[] content)
    {
        var headerBuilder = new StringBuilder();
        headerBuilder.AppendLine($"HTTP/1.1 {statusCode} {statusText}");
        headerBuilder.AppendLine($"Content-Type: {contentType}");
        headerBuilder.AppendLine($"Content-Length: {content.Length}");
        headerBuilder.AppendLine("Connection: close");
        headerBuilder.AppendLine();

        var headerBytes = Encoding.ASCII.GetBytes(headerBuilder.ToString());
        await stream.WriteAsync(headerBytes);
        await stream.WriteAsync(content);
    }

    public void Stop()
    {
        Logger.Info("Shutdown signal received.");
        _isRunning = false;
        _cancellationTokenSource.Cancel();
        _listener.Stop();
    }

    public void Dispose()
    {
        Stop();
        _cancellationTokenSource.Dispose();
    }
}

public static class Logger
{
    private enum LogLevel
    {
        INFO,
        WARN,
        ERROR
    }

    private static void Log(LogLevel level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Console.WriteLine($"[{timestamp}] [{level}] {message}");
    }

    public static void Info(string message) => Log(LogLevel.INFO, message);
    public static void Warn(string message) => Log(LogLevel.WARN, message);
    public static void Error(string message) => Log(LogLevel.ERROR, message);
} 