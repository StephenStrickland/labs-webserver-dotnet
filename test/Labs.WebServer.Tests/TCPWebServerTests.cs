using System.Net;
using System.Net.Sockets;
using System.Text;
using Labs.TCPWebServer;

namespace Labs.WebServer.Tests;

public class TCPWebServerTests : IDisposable
{
    private readonly Labs.TCPWebServer.TCPWebServer _server;
    private readonly string _tempPath;
    private readonly int _port;

    public TCPWebServerTests()
    {
        // Use a random port for testing to avoid conflicts
        _port = new Random().Next(50000, 60000);
        _tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempPath);
        _server = new Labs.TCPWebServer.TCPWebServer(_port, _tempPath);
        Task.Run(() => _server.StartAsync());
        Thread.Sleep(100); // Give the server a moment to start
    }

    public void Dispose()
    {
        _server.Dispose();
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }
    }

    private async Task<(string response, int statusCode)> SendHttpRequestAsync(string path, string method = "GET")
    {
        using var client = new TcpClient();
        await client.ConnectAsync("localhost", _port);
        
        using var stream = client.GetStream();
        var request = $"{method} {path} HTTP/1.1\r\nHost: localhost\r\n\r\n";
        var requestBytes = Encoding.ASCII.GetBytes(request);
        await stream.WriteAsync(requestBytes);

        using var reader = new StreamReader(stream, Encoding.ASCII);
        var statusLine = await reader.ReadLineAsync();
        var headers = new List<string>();
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            headers.Add(line);
        }

        var contentLengthHeader = headers.FirstOrDefault(h => h.StartsWith("Content-Length: ", StringComparison.OrdinalIgnoreCase));
        var contentLength = 0;
        if (contentLengthHeader != null)
        {
            contentLength = int.Parse(contentLengthHeader.Split(": ")[1]);
        }

        var content = "";
        if (contentLength > 0)
        {
            var buffer = new char[contentLength];
            await reader.ReadBlockAsync(buffer, 0, contentLength);
            content = new string(buffer);
        }

        var statusCode = int.Parse(statusLine?.Split(' ')[1] ?? "0");
        return (content, statusCode);
    }

    [Fact]
    public async Task RootPath_ReturnsHelloMessage()
    {
        var (response, statusCode) = await SendHttpRequestAsync("/");
        
        Assert.Equal(200, statusCode);
        Assert.Equal("Hello from TCP .NET server!", response);
    }

    [Fact]
    public async Task InvalidMethod_Returns405()
    {
        var (_, statusCode) = await SendHttpRequestAsync("/", "POST");
        
        Assert.Equal(405, statusCode);
    }

    [Fact]
    public async Task NonexistentPath_Returns404()
    {
        var (_, statusCode) = await SendHttpRequestAsync("/nonexistent");
        
        Assert.Equal(404, statusCode);
    }

    [Fact]
    public async Task StaticFile_WhenExists_ReturnsFileContent()
    {
        var testContent = "Test file content";
        var testFilePath = Path.Combine(_tempPath, "test.txt");
        await File.WriteAllTextAsync(testFilePath, testContent);

        var (response, statusCode) = await SendHttpRequestAsync("/static/test.txt");
        
        Assert.Equal(200, statusCode);
        Assert.Equal(testContent, response);
    }

    [Fact]
    public async Task StaticFile_WhenNotExists_Returns404()
    {
        var (_, statusCode) = await SendHttpRequestAsync("/static/nonexistent.txt");
        
        Assert.Equal(404, statusCode);
    }

    [Fact]
    public async Task StaticFile_PathTraversal_Returns404()
    {
        var (_, statusCode) = await SendHttpRequestAsync("/static/../sensitive.txt");
        
        Assert.Equal(404, statusCode);
    }

    [Fact]
    public async Task EmptyStaticPath_Returns400()
    {
        var (_, statusCode) = await SendHttpRequestAsync("/static/");
        
        Assert.Equal(400, statusCode);
    }
} 