using System;
using System.Net;
using System.Text;

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

    static void Main()
    {
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
                Logger.Info($"Received request: {request.HttpMethod} {request.Url}");

                // Only handle GET for this simple server
                if (!request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    context.Response.Close();
                    Logger.Warn("Method not allowed.");
                    continue;
                }

                // Construct the response
                var response = context.Response;
                string responseString = "Hello from .NET server!";
                var buffer = Encoding.UTF8.GetBytes(responseString);

                response.StatusCode = 200;
                response.ContentLength64 = buffer.Length;
                using (var output = response.OutputStream)
                    output.Write(buffer, 0, buffer.Length);

                Logger.Info("Responded with 200 OK.");
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
}
