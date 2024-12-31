using System;
using System.Net;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Labs.WebServer;

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

public class WebServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _listenerUrl;
    private readonly string _staticFilesPath;
    private bool _isRunning;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public WebServer(string url = "http://localhost:8080/", string? staticFilesPath = null)
    {
        _listenerUrl = url;
        _staticFilesPath = staticFilesPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "static");
        _listener = new HttpListener();
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
            if (_listener.IsListening)
            {
                _listener.Stop();
            }

            _listener.Prefixes.Clear();
            _listener.Prefixes.Add(_listenerUrl);
            _listener.Start();
            _isRunning = true;
            Logger.Info($"Server started. Listening at {_listenerUrl}");

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
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (HttpListenerException) when (!_isRunning)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to get context: {ex.Message}");
                }
            }
        }
        catch (HttpListenerException ex)
        {
            Logger.Error($"Failed to start server: {ex.Message}");
            throw;
        }
    }

    public void Stop()
    {
        Logger.Info("Shutdown signal received.");
        _isRunning = false;
        _cancellationTokenSource.Cancel();
        _listener.Stop();
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            Logger.Info($"Received request: {request.HttpMethod} {request.Url?.AbsolutePath ?? "/"}");

            if (!request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                context.Response.Close();
                Logger.Warn("Method not allowed.");
                return;
            }

            var path = request.Url?.AbsolutePath ?? "/";
            
            switch (path)
            {
                case "/":
                    HandleRootRoute(context);
                    break;
                case string s when s.StartsWith("/static/", StringComparison.OrdinalIgnoreCase):
                    await HandleStaticFileAsync(context);
                    break;
                default:
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    Logger.Warn($"Route not found: {path}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to handle request: {ex.Message}");
            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.Close();
            }
            catch { /* ignore */ }
        }
    }

    private void HandleRootRoute(HttpListenerContext context)
    {
        var response = context.Response;
        string responseString = "Hello from .NET server!";
        var buffer = Encoding.UTF8.GetBytes(responseString);

        response.StatusCode = 200;
        response.ContentLength64 = buffer.Length;
        using (var output = response.OutputStream)
            output.Write(buffer, 0, buffer.Length);
    }

    private async Task HandleStaticFileAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            string relativePath = request.Url?.AbsolutePath?.Substring("/static/".Length) ?? string.Empty;
            if (string.IsNullOrEmpty(relativePath))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                Logger.Warn("Invalid static file path: URL was null or empty");
                return;
            }

            string filePath = Path.Combine(_staticFilesPath, relativePath);
            string fullPath = Path.GetFullPath(filePath);
            string fullStaticPath = Path.GetFullPath(_staticFilesPath);

            // Verify path is within static directory
            if (!fullPath.StartsWith(fullStaticPath))
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                Logger.Warn($"Invalid file path: {relativePath}");
                return;
            }

            if (!File.Exists(filePath))
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                Logger.Warn($"File not found: {relativePath}");
                return;
            }

            string extension = Path.GetExtension(filePath);
            response.ContentType = extension.ToLower() switch
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

            using (var fileStream = File.OpenRead(filePath))
            {
                response.ContentLength64 = fileStream.Length;
                await fileStream.CopyToAsync(response.OutputStream);
            }

            Logger.Info($"Served static file: {relativePath}");
        }
        finally
        {
            response.Close();
        }
    }

    public void Dispose()
    {
        Stop();
        _cancellationTokenSource.Dispose();
        _listener.Close();
    }
}

public class Program
{
    public static async Task Main()
    {
        using var server = new WebServer();
        await server.StartAsync();
    }
}
