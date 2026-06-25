using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetryTracingSample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    var openTelemetrySection = context.Configuration.GetSection("OpenTelemetry");
                    var serviceName = openTelemetrySection["ServiceName"] ?? "OpenTelemetryTracingSample";
                    var serviceVersion = openTelemetrySection["ServiceVersion"] ?? "1.0.0";
                    var otlpEndpoint = openTelemetrySection.GetSection("Otlp")["Endpoint"] ?? "http://localhost:4318/v1/traces";

                    services.AddSingleton<WeatherService>();
                    services.AddSingleton<OrderService>();
                    services.AddHostedService<TracingBackgroundService>();

                    services.AddOpenTelemetry()
                        .ConfigureResource(resource => resource
                            .AddService(
                                serviceName: serviceName,
                                serviceVersion: serviceVersion,
                                serviceInstanceId: Environment.MachineName)
                            .AddAttributes(new Dictionary<string, object>
                            {
                                ["service.namespace"] = "demo",
                                ["deployment.environment"] = "development",
                                ["host.name"] = Environment.MachineName,
                                ["host.arch"] = Environment.Is64BitOperatingSystem ? "amd64" : "x86",
                                ["process.pid"] = Environment.ProcessId,
                                ["process.runtime.name"] = ".NET",
                                ["process.runtime.version"] = Environment.Version.ToString(),
                                ["application.name"] = serviceName,
                                ["team"] = "development",
                                ["region"] = "local"
                            }))
                        .WithTracing(tracing => tracing
                            .AddSource(
                                Telemetry.ActivitySourceName,
                                Telemetry.WeatherActivitySourceName,
                                Telemetry.OrderActivitySourceName)
                            .SetSampler(new AlwaysOnSampler())
                            .AddOtlpExporter(otlpOptions =>
                            {
                                otlpOptions.Endpoint = new Uri(otlpEndpoint);
                                otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                                otlpOptions.TimeoutMilliseconds = 10000;
                            }));
                })
                .Build();

            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
                Console.WriteLine("\n🛑 Shutdown requested. Stopping gracefully...");
            };

            try
            {
                Console.WriteLine("🚀 OpenTelemetry Tracing Service Started");
                Console.WriteLine("📡 Sending continuous traces to OpenTelemetry Collector");
                Console.WriteLine($"📅 Started at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine("⌨️  Press Ctrl+C to stop");
                Console.WriteLine();

                await host.RunAsync(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                Console.WriteLine("✅ Service stopped gracefully");
            }
        }
    }

    public static class Telemetry
    {
        public const string ActivitySourceName = "OpenTelemetryTracingSample.Tracing";
        public const string WeatherActivitySourceName = "OpenTelemetryTracingSample.Tracing.Weather";
        public const string OrderActivitySourceName = "OpenTelemetryTracingSample.Tracing.Order";
    }

    public class TracingBackgroundService : BackgroundService
    {
        private readonly ILogger<TracingBackgroundService> _logger;
        private readonly WeatherService _weatherService;
        private readonly OrderService _orderService;
        private readonly ActivitySource _activitySource = new(Telemetry.ActivitySourceName);

        public TracingBackgroundService(
            ILogger<TracingBackgroundService> logger,
            WeatherService weatherService,
            OrderService orderService)
        {
            _logger = logger;
            _weatherService = weatherService;
            _orderService = orderService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Continuous tracing service started - Environment: {Environment}, Host: {HostName}",
                "development", Environment.MachineName);

            var cities = new[] { "New York", "London", "Tokyo", "Sydney", "Paris", "Berlin", "Toronto", "Mumbai" };
            var customers = new[] { "John Doe", "Jane Smith", "Alice Johnson", "Bob Wilson", "Carol Brown", "David Lee" };
            var items = new[] { "Widget A", "Widget B", "Gadget X", "Tool Y", "Device Z", "Component K" };

            int cycleCount = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    cycleCount++;

                    using var cycleActivity = _activitySource.StartActivity("TraceCycle", ActivityKind.Internal);
                    cycleActivity?.SetTag("demo.cycle.number", cycleCount);
                    cycleActivity?.SetTag("deployment.environment", "development");
                    cycleActivity?.AddEvent(new ActivityEvent("cycle.started"));

                    var randomCity = cities[Random.Shared.Next(cities.Length)];
                    await _weatherService.GetWeatherAsync(randomCity);

                    var order = new Order
                    {
                        Id = Random.Shared.Next(10000, 99999),
                        CustomerName = customers[Random.Shared.Next(customers.Length)],
                        Amount = Random.Shared.Next(10, 500),
                        Items = new[] { items[Random.Shared.Next(items.Length)], items[Random.Shared.Next(items.Length)] }
                    };

                    await _orderService.ProcessOrderAsync(order);
                    await RecordSystemHealthAsync();
                    await RecordBusinessEventAsync();

                    cycleActivity?.SetStatus(ActivityStatusCode.Ok);
                    cycleActivity?.AddEvent(new ActivityEvent("cycle.completed"));

                    _logger.LogInformation("✅ Trace cycle completed - Cycle: {CycleNumber}", cycleCount);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    Activity.Current?.RecordException(ex);

                    _logger.LogError(ex, "❌ Error in trace cycle - Cycle: {CycleNumber}, Environment: {Environment}",
                        cycleCount, "development");

                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("🛑 Continuous tracing service stopped - StopTime: {StopTime}, TotalCycles: {TotalCycles}",
                DateTime.UtcNow, cycleCount);
        }

        private Task RecordSystemHealthAsync()
        {
            using var healthActivity = _activitySource.StartActivity("SystemHealthCheck", ActivityKind.Internal);

            var cpuUsage = Random.Shared.Next(10, 90);
            var memoryUsage = Random.Shared.Next(512, 2048);
            var activeUsers = Random.Shared.Next(50, 500);

            healthActivity?.SetTag("system.cpu.usage_percent", cpuUsage);
            healthActivity?.SetTag("system.memory.usage_mb", memoryUsage);
            healthActivity?.SetTag("app.active_users", activeUsers);
            healthActivity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation("💹 System health span created - CPU: {CpuUsage}%, Memory: {MemoryUsageMB}MB, ActiveUsers: {ActiveUsers}",
                cpuUsage, memoryUsage, activeUsers);

            return Task.CompletedTask;
        }

        private Task RecordBusinessEventAsync()
        {
            using var businessActivity = _activitySource.StartActivity("BusinessTransaction", ActivityKind.Internal);

            var saleValue = Random.Shared.Next(100, 1000);

            businessActivity?.SetTag("event.type", "SaleCompleted");
            businessActivity?.SetTag("transaction.amount", saleValue);
            businessActivity?.SetTag("transaction.currency", "USD");
            businessActivity?.SetTag("cloud.region", "us-east-1");
            businessActivity?.AddEvent(new ActivityEvent("business.sale.completed"));
            businessActivity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation("📈 Business transaction span created - Value: {Value}, Currency: {Currency}, Region: {Region}",
                saleValue, "USD", "us-east-1");

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _activitySource.Dispose();
            base.Dispose();
        }
    }

    public class WeatherService : IDisposable
    {
        private readonly ILogger<WeatherService> _logger;
        private readonly ActivitySource _activitySource = new(Telemetry.WeatherActivitySourceName);
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        public WeatherService(ILogger<WeatherService> logger)
        {
            _logger = logger;
        }

        public async Task<WeatherInfo> GetWeatherAsync(string city)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid();

            using var activity = _activitySource.StartActivity("WeatherRequest", ActivityKind.Client);
            activity?.SetTag("weather.city", city);
            activity?.SetTag("request.id", requestId.ToString());
            activity?.SetTag("server.address", "weather-api.local");
            activity?.AddEvent(new ActivityEvent("weather.request.started"));

            _logger.LogInformation("🌤️ Weather request started - City: {City}, RequestId: {RequestId}", city, requestId);

            try
            {
                await Task.Delay(Random.Shared.Next(100, 500));

                var weather = new WeatherInfo
                {
                    City = city,
                    Temperature = Random.Shared.Next(-20, 40),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)],
                    Timestamp = DateTime.UtcNow
                };

                stopwatch.Stop();

                activity?.SetTag("weather.temperature", weather.Temperature);
                activity?.SetTag("weather.condition", weather.Summary);
                activity?.SetTag("weather.duration_ms", stopwatch.ElapsedMilliseconds);
                activity?.SetStatus(ActivityStatusCode.Ok);
                activity?.AddEvent(new ActivityEvent("weather.response.received"));

                _logger.LogInformation(
                    "✅ Weather trace completed - City: {City}, Temperature: {Temperature}, Condition: {WeatherCondition}, Duration: {DurationMs}ms",
                    weather.City,
                    weather.Temperature,
                    weather.Summary,
                    stopwatch.ElapsedMilliseconds);

                return weather;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);

                _logger.LogError(ex,
                    "❌ Weather request failed - City: {City}, Duration: {DurationMs}ms, RequestId: {RequestId}",
                    city,
                    stopwatch.ElapsedMilliseconds,
                    requestId);

                throw;
            }
        }

        public void Dispose()
        {
            _activitySource.Dispose();
        }
    }

    public class OrderService : IDisposable
    {
        private readonly ILogger<OrderService> _logger;
        private readonly ActivitySource _activitySource = new(Telemetry.OrderActivitySourceName);

        public OrderService(ILogger<OrderService> logger)
        {
            _logger = logger;
        }

        public async Task ProcessOrderAsync(Order order)
        {
            using var scope = _logger.BeginScope("Order_{OrderId}", order.Id);
            using var activity = _activitySource.StartActivity("OrderProcessing", ActivityKind.Internal);

            activity?.SetTag("order.id", order.Id.ToString());
            activity?.SetTag("order.customer", order.CustomerName);
            activity?.SetTag("order.amount", order.Amount.ToString("F2"));
            activity?.SetTag("order.item_count", order.Items?.Length ?? 0);
            activity?.AddEvent(new ActivityEvent("order.started"));

            _logger.LogInformation(
                "🛒 Order processing started - OrderId: {OrderId}, Customer: {CustomerName}, ItemCount: {ItemCount}, Amount: {Amount}",
                order.Id,
                order.CustomerName,
                order.Items?.Length ?? 0,
                order.Amount);

            try
            {
                await ValidateOrderAsync(order);
                await ProcessPaymentAsync(order);
                await UpdateInventoryAsync(order);

                activity?.SetTag("order.status", "completed");
                activity?.SetStatus(ActivityStatusCode.Ok);
                activity?.AddEvent(new ActivityEvent("order.completed"));

                _logger.LogInformation("✅ Order trace completed - OrderId: {OrderId}, Customer: {CustomerName}, Amount: {Amount}",
                    order.Id, order.CustomerName, order.Amount);
            }
            catch (Exception ex)
            {
                activity?.SetTag("order.status", "failed");
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);

                _logger.LogError(ex, "❌ Order processing failed - OrderId: {OrderId}, Customer: {CustomerName}",
                    order.Id, order.CustomerName);

                throw;
            }
        }

        private async Task ValidateOrderAsync(Order order)
        {
            using var validationActivity = _activitySource.StartActivity("ValidateOrder", ActivityKind.Internal);
            validationActivity?.SetTag("order.id", order.Id.ToString());

            await Task.Delay(Random.Shared.Next(50, 150));

            if (order.Amount <= 0)
            {
                validationActivity?.SetStatus(ActivityStatusCode.Error, "Order amount must be positive");
                throw new InvalidOperationException("Order amount must be positive");
            }

            validationActivity?.SetStatus(ActivityStatusCode.Ok);
        }

        private async Task ProcessPaymentAsync(Order order)
        {
            using var paymentActivity = _activitySource.StartActivity("ProcessPayment", ActivityKind.Internal);
            paymentActivity?.SetTag("order.id", order.Id.ToString());
            paymentActivity?.SetTag("payment.amount", order.Amount.ToString("F2"));

            await Task.Delay(Random.Shared.Next(200, 800));

            if (Random.Shared.NextDouble() < 0.15)
            {
                var exception = new InvalidOperationException("Payment processing failed");
                paymentActivity?.SetTag("payment.status", "failed");
                paymentActivity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                paymentActivity?.RecordException(exception);
                throw exception;
            }

            paymentActivity?.SetTag("payment.status", "approved");
            paymentActivity?.SetStatus(ActivityStatusCode.Ok);
        }

        private async Task UpdateInventoryAsync(Order order)
        {
            using var inventoryActivity = _activitySource.StartActivity("UpdateInventory", ActivityKind.Internal);
            inventoryActivity?.SetTag("order.id", order.Id.ToString());
            inventoryActivity?.SetTag("inventory.item_count", order.Items?.Length ?? 0);

            if (order.Items != null)
            {
                foreach (var item in order.Items)
                {
                    inventoryActivity?.AddEvent(new ActivityEvent("inventory.item.updated",
                        tags: new ActivityTagsCollection { ["item.name"] = item }));
                    await Task.Delay(Random.Shared.Next(30, 100));
                }
            }

            inventoryActivity?.SetStatus(ActivityStatusCode.Ok);
        }

        public void Dispose()
        {
            _activitySource.Dispose();
        }
    }

    public class WeatherInfo
    {
        public string City { get; set; } = string.Empty;
        public int Temperature { get; set; }
        public string Summary { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class Order
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string[]? Items { get; set; }
    }
}
