using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Labs.WebServer;
using System.Net.Http;
using System.Text;
using Xunit;

namespace Labs.WebServer.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<WebServerBenchmarks>();
    }
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class WebServerBenchmarks
{
    private WebServer _server;
    private HttpClient _client;
    private const string BaseUrl = "http://localhost:8080/";
    private readonly byte[] _largePayload;

    public WebServerBenchmarks()
    {
        // Create a 1MB payload for testing
        _largePayload = Encoding.UTF8.GetBytes(new string('x', 1024 * 1024));
    }

    [GlobalSetup]
    public async Task Setup()
    {
        _server = new WebServer(BaseUrl);
        _ = _server.StartAsync(); // Start server in background
        await Task.Delay(100); // Give server time to start
        
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            AutomaticDecompression = System.Net.DecompressionMethods.None
        };
        _client = new HttpClient(handler)
        {
            DefaultRequestVersion = new Version(1, 1),
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Create test files
        var staticPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "static");
        Directory.CreateDirectory(staticPath);
        await File.WriteAllBytesAsync(Path.Combine(staticPath, "large.txt"), _largePayload);
        await File.WriteAllTextAsync(Path.Combine(staticPath, "small.txt"), "Hello World!");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Benchmark(Description = "GET /")]
    public async Task BenchmarkRootEndpoint()
    {
        var response = await _client.GetAsync(BaseUrl);
        response.EnsureSuccessStatusCode();
    }

    [Benchmark(Description = "GET /static/small.txt")]
    public async Task BenchmarkSmallStaticFile()
    {
        var response = await _client.GetAsync(BaseUrl + "static/small.txt");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark(Description = "GET /static/large.txt")]
    public async Task BenchmarkLargeStaticFile()
    {
        var response = await _client.GetAsync(BaseUrl + "static/large.txt");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark(Description = "GET /nonexistent")]
    public async Task Benchmark404Request()
    {
        var response = await _client.GetAsync(BaseUrl + "nonexistent");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
