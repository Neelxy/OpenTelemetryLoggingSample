# OpenTelemetry Tracing Sample

A .NET sample that continuously generates distributed traces with OpenTelemetry and exports them to an OTLP endpoint.

## 🚀 Features

- **OpenTelemetry Tracing**: OTLP trace export for local collectors and dashboards
- **Nested Spans**: End-to-end trace cycles with child spans for weather, ordering, health, and business events
- **Useful Attributes and Events**: Span tags, status codes, and activity events for richer analysis
- **Realistic Demo Flow**: Simulated external calls, payment handling, and inventory updates
- **Graceful Shutdown**: Proper handling of Ctrl+C interruption

## 🛠️ Quick Start

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Setup

1. **Restore dependencies**
   ```bash
   dotnet restore
   ```

2. **Build the application**
   ```bash
   dotnet build
   ```

3. **Run the application**
   ```bash
   dotnet run
   ```

The application will begin sending traces to `http://localhost:4318/v1/traces`.

### What happens when you run it

- 🚀 Starts a continuous trace producer
- 🔄 Creates a root span for each simulated processing cycle
- 🌤️ Generates a child span for a weather request
- 🛒 Generates nested order-processing spans
- 💹 Emits spans for system health and business activity
- ⌨️ Press **Ctrl+C** to stop gracefully

## 🎯 Configuration

### Change the OTLP Endpoint

Update `appsettings.json`:

```json
"OpenTelemetry": {
  "Otlp": {
    "Endpoint": "http://your-collector:4318/v1/traces"
  }
}
```

### Adjust Trace Frequency

Modify the delay in the `TracingBackgroundService.ExecuteAsync()` method in `Program.cs`.

## 📦 Dependencies

- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `Microsoft.Extensions.Hosting`

## 🧪 Sample Trace Shape

Each cycle produces a trace similar to:

- `TraceCycle`
  - `WeatherRequest`
  - `OrderProcessing`
    - `ValidateOrder`
    - `ProcessPayment`
    - `UpdateInventory`
  - `SystemHealthCheck`
  - `BusinessTransaction`

## 🔗 Next Steps

To view these traces in a dashboard:
1. Run an OpenTelemetry Collector at `localhost:4318`
2. Configure it to forward traces to Tempo, Jaeger, Zipkin, or your preferred backend
3. Visualize the traces in Grafana or another tracing UI
