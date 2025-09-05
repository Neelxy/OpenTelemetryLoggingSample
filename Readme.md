# OpenTelemetry Logging Sample

A comprehensive .NET application demonstrating OpenTelemetry logging with structured logs, automatic log generation, and integration with observability platforms like Grafana and Loki.

## 🚀 Features

- **OpenTelemetry Integration**: Full OTLP (OpenTelemetry Protocol) support
- **Structured Logging**: Rich, searchable log data with semantic conventions
- **Continuous Log Generation**: Simulates real-world application scenarios
- **Multiple Log Levels**: Information, Warning, Error, and Debug logs
- **Business Scenarios**: Weather API calls, order processing, system metrics
- **Graceful Shutdown**: Proper handling of Ctrl+C interruption
- **Dashboard Ready**: Compatible with Grafana, Loki, and other observability tools

## 🛠️ Quick Start

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/Neelxy/My-Repository.git
   cd My-Repository
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the application**
   ```bash
   dotnet build
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

That's it! The application will start generating logs and sending them to `http://localhost:4318/v1/logs` (OpenTelemetry Collector endpoint).

### What happens when you run it

- 🚀 Starts the logging service
- 📊 Generates structured logs every 30 seconds
- 🌤️ Simulates weather API calls
- 🛒 Processes mock orders
- 💹 Reports system metrics
- ⚠️ Occasionally generates warnings and errors
- ⌨️ Press **Ctrl+C** to stop gracefully

## 🎯 Configuration

### Change the OTLP Endpoint

Edit the endpoint in `Program.cs` (line ~50):

```csharp
otlpOptions.Endpoint = new Uri("http://your-collector:4318/v1/logs");
```

### Adjust Log Frequency

Modify the delay in `LoggingBackgroundService.ExecuteAsync()` (line ~190):

```csharp
await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Change 30 to your preference
```

## 📦 Dependencies

The project uses these NuGet packages:
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `Microsoft.Extensions.Hosting`

## 🧪 Sample Log Output

The application generates various types of structured logs:

```
🌤️ Weather request initiated - City: Tokyo, RequestId: abc123, Environment: development
🛒 Order processing started - OrderId: 12345, Customer: John Doe, Amount: $99.99
💹 System metrics recorded - CPU: 45%, Memory: 1024MB, ActiveUsers: 250
⚠️ Performance warning - Component: DatabaseConnection, ResponseTime: 2500ms
✅ Logging cycle completed - Cycle: 5, Duration: 1250ms
```

## 🔗 Next Steps

To view these logs in a dashboard:
1. Set up an OpenTelemetry Collector at `localhost:4318`
2. Configure it to forward logs to Loki, Elasticsearch, or your preferred backend
3. Create dashboards in Grafana or your visualization tool

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.