using System.Net;
using System.Text;
using System.Net.Http;

namespace Labs.WebServer.Tests;

public class WebServerFixture : IDisposable
{
    private readonly Labs.WebServer.WebServer _server;
    private readonly Task _serverTask;

    public WebServerFixture()
    {
        _server = new Labs.WebServer.WebServer();
        _serverTask = _server.StartAsync();

        // Give the server a moment to start
        Thread.Sleep(1000);
    }

    public void Dispose()
    {
        _server.Dispose();
    }
}

public class WebServerTests : IClassFixture<WebServerFixture>
{
    private readonly HttpClient _client;
    private readonly WebServerFixture _fixture;

    public WebServerTests(WebServerFixture fixture)
    {
        _fixture = fixture;
        _client = new HttpClient();
    }

    [Fact]
    public async Task RootEndpoint_ReturnsHelloMessage()
    {
        // Act
        var response = await _client.GetAsync("http://localhost:8080/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello from .NET server!", content);
    }

    [Fact]
    public async Task StaticFile_WhenExists_ReturnsFileContent()
    {
        // Arrange
        var testContent = "Test content";
        var testFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "static", "test.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(testFilePath)!);
        await File.WriteAllTextAsync(testFilePath, testContent);

        try
        {
            // Act
            var response = await _client.GetAsync("http://localhost:8080/static/test.txt");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(testContent, content);
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            // Cleanup
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }

    [Fact]
    public async Task StaticFile_WhenNotExists_Returns404()
    {
        // Act
        var response = await _client.GetAsync("http://localhost:8080/static/nonexistent.txt");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InvalidMethod_ReturnsMethodNotAllowed()
    {
        // Act
        var response = await _client.PostAsync("http://localhost:8080/", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [InlineData(".txt", "text/plain")]
    [InlineData(".html", "text/html")]
    [InlineData(".css", "text/css")]
    [InlineData(".js", "application/javascript")]
    [InlineData(".json", "application/json")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    public async Task StaticFile_ReturnsCorrectMimeType(string extension, string expectedMimeType)
    {
        // Arrange
        var testContent = "Test content";
        var fileName = $"test{extension}";
        var testFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "static", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(testFilePath)!);
        await File.WriteAllTextAsync(testFilePath, testContent);

        try
        {
            // Act
            var response = await _client.GetAsync($"http://localhost:8080/static/{fileName}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expectedMimeType, response.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            // Cleanup
            if (File.Exists(testFilePath))
            {
                File.Delete(testFilePath);
            }
        }
    }
}
