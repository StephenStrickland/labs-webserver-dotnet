using System;
using System.Net;
using System.Text;

class Program
{
    static void Main()
    {
        var listener = new HttpListener();

        try
        {
            // Add prefix
            listener.Prefixes.Add("http://localhost:8080/");
        }
        catch (HttpListenerException ex)
        {
            Console.WriteLine($"[ERROR] Failed to add prefix: {ex.Message}");
            return;
        }

        try
        {
            listener.Start();
            Console.WriteLine("[INFO] Server started. Listening at http://localhost:8080/");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Listener failed to start: {ex.Message}");
            return;
        }

        // Gracefully stop on Ctrl+C
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("[INFO] Shutdown signal received.");
            listener.Stop();
            listener.Close();
            Environment.Exit(0);
        };

        while (true)
        {
            HttpListenerContext context;
            try
            {
                context = listener.GetContext();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to get context: {ex.Message}");
                continue;
            }

            try
            {
                var request = context.Request;
                Console.WriteLine($"[INFO] Received request: {request.HttpMethod} {request.Url}");

                // Only handle GET for this simple server
                if (!request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    context.Response.Close();
                    Console.WriteLine("[WARN] Method not allowed.");
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

                Console.WriteLine("[INFO] Responded with 200 OK.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to handle request: {ex.Message}");
                // Attempt to write a 500 error response if possible
                try
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.Close();
                }
                catch { /* ignore */ }
            }
        }
    }
}
