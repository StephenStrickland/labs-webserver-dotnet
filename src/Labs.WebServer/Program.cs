using System;
using System.Net;
using System.Text;
using System.IO;

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

class Program
{
    private const string ListenerUrl = "http://localhost:8080/";
    private static readonly string StaticFilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "static");

    static void Main()
    {
        // Ensure static directory exists
        if (!Directory.Exists(StaticFilesPath))
        {
            Directory.CreateDirectory(StaticFilesPath);
            Logger.Info($"Created static files directory at {StaticFilesPath}");
        }

        var listener = new HttpListener();
        var isRunning = true;

        try
        {
            // Add prefix
            listener.Prefixes.Add(ListenerUrl);
        }
        catch (HttpListenerException ex)
        {
            Logger.Error($"Failed to add prefix: {ex.Message}");
            return;
        }

        try
        {
            listener.Start();
            Logger.Info($"Server started. Listening at {ListenerUrl}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Listener failed to start: {ex.Message}");
            return;
        }

        // Gracefully stop on Ctrl+C
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true; // Prevent immediate termination
            Logger.Info("Shutdown signal received.");
            isRunning = false;
            listener.Stop();
        };

        while (isRunning)
        {
            HttpListenerContext context;
            try
            {
                context = listener.GetContext();
            }
            catch (Exception ex)
            {
                if (!isRunning)
                {
                    break;
                }
                Logger.Error($"Failed to get context: {ex.Message}");
                continue;
            }

            try
            {
                var request = context.Request;
                Logger.Info($"Received request: {request.HttpMethod} {request.Url?.AbsolutePath ?? "/"}");

                // Only handle GET for this simple server
                if (!request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    context.Response.Close();
                    Logger.Warn("Method not allowed.");
                    continue;
                }

                // Route handling
                var path = request.Url?.AbsolutePath ?? "/";
                
                switch (path)
                {
                    case "/":
                        HandleRootRoute(context);
                        break;
                    case string s when s.StartsWith("/static/", StringComparison.OrdinalIgnoreCase):
                        HandleStaticFile(context);
                        break;
                    default:
                        // Handle 404 for unknown routes
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        context.Response.Close();
                        Logger.Warn($"Route not found: {path}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to handle request: {ex.Message}");
                // Attempt to write a 500 error response if possible
                try
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.Close();
                }
                catch { /* ignore */ }
            }
        }

        listener.Close();
        Logger.Info("Server shutdown complete.");
    }

    private static void HandleRootRoute(HttpListenerContext context)
    {
        var response = context.Response;
        string responseString = "Hello from .NET server!";
        var buffer = Encoding.UTF8.GetBytes(responseString);

        response.StatusCode = 200;
        response.ContentLength64 = buffer.Length;
        using (var output = response.OutputStream)
            output.Write(buffer, 0, buffer.Length);

        Logger.Info("Responded with 200 OK for root route.");
    }

    private static void HandleStaticFile(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Remove "/static/" from the beginning and get the file path
            string relativePath = request.Url?.AbsolutePath?.Substring("/static/".Length) ?? string.Empty;
            if (string.IsNullOrEmpty(relativePath))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Close();
                Logger.Warn("Invalid static file path: URL was null or empty");
                return;
            }
            string filePath = Path.Combine(StaticFilesPath, relativePath);

            // Prevent directory traversal attacks
            if (!filePath.StartsWith(StaticFilesPath))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Close();
                Logger.Warn($"Attempted directory traversal attack: {relativePath}");
                return;
            }

            if (!File.Exists(filePath))
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Close();
                Logger.Warn($"File not found: {relativePath}");
                return;
            }

            // Get MIME type
            string mimeType = GetMimeType(Path.GetExtension(filePath));
            response.ContentType = mimeType;

            // Read and send the file
            using (var fileStream = File.OpenRead(filePath))
            {
                response.ContentLength64 = fileStream.Length;
                fileStream.CopyTo(response.OutputStream);
            }

            Logger.Info($"Served static file: {relativePath}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error serving static file: {ex.Message}");
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            response.Close();
        }
    }

    private static string GetMimeType(string extension)
    {
        return extension.ToLower() switch
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
    }
}
