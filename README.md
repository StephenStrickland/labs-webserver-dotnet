# labs-webserver-dotnet

A simple web server implementation in .NET using only the standard library. This project explores building HTTP servers from scratch without any external dependencies or web frameworks. It includes two different implementations:

1. **HttpListener-based Server** (`Labs.WebServer`)
   - Uses the built-in `HttpListener` class
   - High-level abstraction over HTTP protocol
   - Easy-to-use API for handling HTTP requests

2. **TCP-based Server** (`Labs.TCPWebServer`)
   - Built directly on top of TCP sockets
   - Manual HTTP protocol implementation
   - Lower-level control over networking details
   - Educational implementation showing HTTP internals

### Available Routes

Both servers implement the same routes:

1. **Root Route** (`GET /`)
   - Returns a simple welcome message
   - Example response: "Hello from .NET server!" (HttpListener) or "Hello from TCP .NET server!" (TCP)

2. **Static Files** (`GET /static/{filename}`)
   - Serves files from the static directory
   - Automatically detects content type based on file extension
   - Supports common file types (txt, html, css, js, json, jpg, png, gif, svg)
   - Prevents directory traversal attacks
   - Returns appropriate HTTP status codes (200, 404, 400)

## Goals
- Build lightweight web servers using different .NET networking approaches
- Handle basic HTTP requests and responses
- Understand the fundamentals of web server architecture
- Explore both high-level and low-level networking concepts in .NET
- Compare different implementation approaches

## Getting Started

### Prerequisites
- .NET 9.0 SDK or later
- Git

### Building Locally
1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/labs-webserver-dotnet.git
   cd labs-webserver-dotnet
   ```

2. Build the project:
   ```bash
   # On Windows
   ./build.cmd

   # On macOS/Linux
   ./build.sh
   ```

### Running Tests
Execute the test suite using:
```bash
# On Windows
./build.cmd test

# On macOS/Linux
./build.sh test
```

The test results will be displayed in the console, showing the status of each test case for both server implementations.

## Benchmarks

The project includes performance benchmarks using BenchmarkDotNet. The benchmarks test:

- Root endpoint performance
- Static file serving (small and large files)
- 404 response handling

To run the benchmarks locally:

```bash
# On Windows
./build.cmd benchmark --configuration Release

# On macOS/Linux
./build.sh benchmark --configuration Release
```

> **Note**: Always run benchmarks with `--configuration Release` for accurate performance measurements. Debug builds include additional overhead that can significantly impact benchmark results.

Benchmarks are also run automatically:
- On every push to main
- On pull requests
- Can be triggered manually from the Actions tab

Results are stored as artifacts in GitHub Actions for 90 days.
