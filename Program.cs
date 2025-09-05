using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using System.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace OpenTelemetryLoggingSample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddSingleton<WeatherService>();
                    services.AddSingleton<OrderService>();
                    services.AddHostedService<LoggingBackgroundService>();
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddOpenTelemetry(options =>
                    {
                        options.SetResourceBuilder(ResourceBuilder.CreateDefault()
                            .AddService(
                                serviceName: "OpenTelemetryLoggingSample",
                                serviceVersion: "1.0.0",
                                serviceInstanceId: Environment.MachineName)
                            .AddAttributes(new Dictionary<string, object>
                            {
                                // Standard OpenTelemetry semantic conventions
                                ["service.name"] = "OpenTelemetryLoggingSample",
                                ["service.version"] = "1.0.0",
                                ["service.instance.id"] = Environment.MachineName,
                                ["service.namespace"] = "demo",
                                
                                // Deployment attributes
                                ["deployment.environment"] = "development",
                                ["deployment.environment.name"] = "development",
                                
                                // Host attributes
                                ["host.name"] = Environment.MachineName,
                                ["host.type"] = "vm",
                                ["host.arch"] = Environment.Is64BitOperatingSystem ? "amd64" : "x86",
                                
                                // Process attributes
                                ["process.pid"] = Environment.ProcessId,
                                ["process.executable.name"] = "OpenTelemetryLoggingSample",
                                ["process.runtime.name"] = ".NET",
                                ["process.runtime.version"] = Environment.Version.ToString(),
                                
                                // Operating system attributes
                                ["os.type"] = Environment.OSVersion.Platform.ToString().ToLower(),
                                ["os.description"] = Environment.OSVersion.ToString(),
                                
                                // Application attributes
                                ["application.name"] = "OpenTelemetryLoggingSample",
                                ["team"] = "development",
                                ["region"] = "local"
                            }));
                        options.AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri("http://localhost:4318/v1/logs");
                            otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                            otlpOptions.TimeoutMilliseconds = 10000;
                        });
                        options.IncludeFormattedMessage = true;
                        options.IncludeScopes = true;
                    });
                })
                .Build();

            // Handle Ctrl+C gracefully
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
                Console.WriteLine("\n🛑 Shutdown requested. Stopping gracefully...");
            };

            try
            {
                Console.WriteLine("🚀 OpenTelemetry Logging Service Started");
                Console.WriteLine("📡 Sending continuous logs to OpenTelemetry Collector → Loki");
                Console.WriteLine($"📅 Started at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine("⌨️  Press Ctrl+C to stop");
                Console.WriteLine();

                // Start the host and wait for cancellation
                await host.RunAsync(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            finally
            {
                Console.WriteLine("✅ Service stopped gracefully");
            }
        }
    }

    public class LoggingBackgroundService : BackgroundService
    {
        private readonly ILogger<LoggingBackgroundService> _logger;
        private readonly WeatherService _weatherService;
        private readonly OrderService _orderService;
        private readonly ActivitySource _activitySource;

        public LoggingBackgroundService(
            ILogger<LoggingBackgroundService> logger,
            WeatherService weatherService,
            OrderService orderService)
        {
            _logger = logger;
            _weatherService = weatherService;
            _orderService = orderService;
            _activitySource = new ActivitySource("LoggingBackgroundService");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Continuous logging service started - Environment: {Environment}, Host: {HostName}", 
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
                    using var activity = _activitySource.StartActivity($"LoggingCycle_{cycleCount}");
                    
                    _logger.LogInformation("📊 Logging cycle started - Cycle: {CycleNumber}, Timestamp: {Timestamp}", 
                        cycleCount, DateTime.UtcNow);

                    // Generate weather logs with structured data
                    var randomCity = cities[Random.Shared.Next(cities.Length)];
                    await _weatherService.GetWeatherAsync(randomCity);

                    // Generate order processing logs with business context
                    using (_logger.BeginScope("OrderProcessing_{CycleNumber}_{Timestamp}", cycleCount, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")))
                    {
                        var order = new Order
                        {
                            Id = Random.Shared.Next(10000, 99999),
                            CustomerName = customers[Random.Shared.Next(customers.Length)],
                            Amount = Random.Shared.Next(10, 500),
                            Items = new[] { items[Random.Shared.Next(items.Length)], items[Random.Shared.Next(items.Length)] }
                        };

                        await _orderService.ProcessOrderAsync(order);
                    }

                    // Generate system metrics with standard names
                    _logger.LogInformation("💹 System metrics recorded - CPU: {CpuUsage}%, Memory: {MemoryUsageMB}MB, ActiveUsers: {ActiveUsers}, Environment: {Environment}, Host: {HostName}",
                        Random.Shared.Next(10, 90),
                        Random.Shared.Next(512, 2048),
                        Random.Shared.Next(50, 500),
                        "development",
                        Environment.MachineName);

                    // Application health metrics
                    _logger.LogInformation("🏥 Health check - Status: {HealthStatus}, ResponseTime: {ResponseTimeMs}ms, RequestCount: {RequestCount}",
                        Random.Shared.NextDouble() > 0.1 ? "healthy" : "degraded",
                        Random.Shared.Next(50, 500),
                        Random.Shared.Next(100, 1000));

                    // Occasionally generate warnings and errors with context
                    if (Random.Shared.NextDouble() < 0.2) // 20% chance
                    {
                        _logger.LogWarning("⚠️ Performance warning - Component: {Component}, Metric: {Metric}, Value: {Value}, Threshold: {Threshold}, Environment: {Environment}",
                            "DatabaseConnection", "ResponseTime", Random.Shared.Next(1000, 5000), 1000, "development");
                    }

                    if (Random.Shared.NextDouble() < 0.1) // 10% chance
                    {
                        _logger.LogError("❌ Application error - ErrorType: {ErrorType}, Component: {Component}, Duration: {Duration}ms, Environment: {Environment}",
                            "TimeoutException", "ExternalAPI", Random.Shared.Next(5000, 30000), "development");
                    }

                    // Business events
                    _logger.LogInformation("📈 Business event - EventType: {EventType}, Value: {Value}, Currency: {Currency}, Region: {Region}",
                        "SaleCompleted", Random.Shared.Next(100, 1000), "USD", "US-EAST");

                    _logger.LogInformation("✅ Logging cycle completed - Cycle: {CycleNumber}, Duration: {Duration}ms", 
                        cycleCount, Random.Shared.Next(1000, 3000));

                    // Wait before next cycle (30 seconds)
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in logging cycle - Cycle: {CycleNumber}, Environment: {Environment}", 
                        cycleCount, "development");
                    
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("🛑 Continuous logging service stopped - StopTime: {StopTime}, TotalCycles: {TotalCycles}", 
                DateTime.UtcNow, cycleCount);
        }

        public override void Dispose()
        {
            _activitySource?.Dispose();
            base.Dispose();
        }
    }

    public class WeatherService
    {
        private readonly ILogger<WeatherService> _logger;
        private readonly ActivitySource _activitySource;
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        public WeatherService(ILogger<WeatherService> logger)
        {
            _logger = logger;
            _activitySource = new ActivitySource("WeatherService");
        }

        public async Task<WeatherInfo> GetWeatherAsync(string city)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid();
            
            using var activity = _activitySource.StartActivity("WeatherRequest");
            activity?.SetTag("weather.city", city);
            activity?.SetTag("request.id", requestId.ToString());
            
            _logger.LogInformation("🌤️ Weather request initiated - City: {City}, RequestId: {RequestId}, Environment: {Environment}", 
                city, requestId, "development");

            try
            {
                // Simulate API call delay
                await Task.Delay(Random.Shared.Next(100, 500));

                var weather = new WeatherInfo
                {
                    City = city,
                    Temperature = Random.Shared.Next(-20, 40),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)],
                    Timestamp = DateTime.UtcNow
                };
                stopwatch.Stop();
                _logger.LogInformation(
                    "✅ Weather data retrieved - City: {City}, Temperature: {Temperature}, Condition: {WeatherCondition}, Duration: {DurationMs}ms, RequestId: {RequestId}",
                    weather.City,
                    weather.Temperature,
                    weather.Summary,
                    stopwatch.ElapsedMilliseconds,
                    requestId);
                activity?.SetTag("weather.temperature", weather.Temperature);
                activity?.SetTag("weather.condition", weather.Summary);
                return weather;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, 
                    "❌ Weather request failed - City: {City}, Duration: {DurationMs}ms, RequestId: {RequestId}, Environment: {Environment}", 
                    city, 
                    stopwatch.ElapsedMilliseconds,
                    requestId,
                    "development");
                throw;
            }
        }
    }

    public class OrderService
    {
        private readonly ILogger<OrderService> _logger;
        private readonly ActivitySource _activitySource;

        public OrderService(ILogger<OrderService> logger)
        {
            _logger = logger;
            _activitySource = new ActivitySource("OrderService");
        }

        public async Task ProcessOrderAsync(Order order)
        {
            using var scope = _logger.BeginScope("Order_{OrderId}", order.Id);
            using var activity = _activitySource.StartActivity("OrderProcessing");
            
            activity?.SetTag("order.id", order.Id.ToString());
            activity?.SetTag("order.customer", order.CustomerName);
            activity?.SetTag("order.amount", order.Amount.ToString("F2"));

            _logger.LogInformation(
                "🛒 Order processing started - OrderId: {OrderId}, Customer: {CustomerName}, ItemCount: {ItemCount}, Amount: {Amount}, Environment: {Environment}",
                order.Id,
                order.CustomerName,
                order.Items?.Length ?? 0,
                order.Amount,
                "development");

            try
            {
                await ValidateOrderAsync(order);
                await ProcessPaymentAsync(order);
                await UpdateInventoryAsync(order);

                _logger.LogInformation("✅ Order completed successfully - OrderId: {OrderId}, Customer: {CustomerName}, Amount: {Amount}", 
                    order.Id, order.CustomerName, order.Amount);
                
                activity?.SetTag("order.status", "completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Order processing failed - OrderId: {OrderId}, Customer: {CustomerName}, Environment: {Environment}", 
                    order.Id, order.CustomerName, "development");
                
                activity?.SetTag("order.status", "failed");
                throw;
            }
        }

        private async Task ValidateOrderAsync(Order order)
        {
            _logger.LogDebug("🔍 Order validation started - OrderId: {OrderId}", order.Id);
            await Task.Delay(Random.Shared.Next(50, 150));
            
            if (order.Amount <= 0)
            {
                _logger.LogWarning("⚠️ Order validation failed - OrderId: {OrderId}, Reason: {Reason}, Amount: {Amount}", 
                    order.Id, "InvalidAmount", order.Amount);
                throw new InvalidOperationException("Order amount must be positive");
            }

            _logger.LogDebug("✅ Order validation passed - OrderId: {OrderId}", order.Id);
        }

        private async Task ProcessPaymentAsync(Order order)
        {
            _logger.LogInformation("💳 Payment processing started - OrderId: {OrderId}, Amount: {Amount}", 
                order.Id, order.Amount);
            
            await Task.Delay(Random.Shared.Next(200, 800));
            
            if (Random.Shared.NextDouble() < 0.15) // 15% failure rate
            {
                _logger.LogWarning("⚠️ Payment failed - OrderId: {OrderId}, Reason: {Reason}, Amount: {Amount}, Environment: {Environment}", 
                    order.Id, "InsufficientFunds", order.Amount, "development");
                throw new InvalidOperationException("Payment processing failed");
            }

            _logger.LogInformation("✅ Payment completed - OrderId: {OrderId}, Amount: {Amount}", 
                order.Id, order.Amount);
        }

        private async Task UpdateInventoryAsync(Order order)
        {
            _logger.LogInformation("📦 Inventory update started - OrderId: {OrderId}", order.Id);
            
            if (order.Items != null)
            {
                foreach (var item in order.Items)
                {
                    _logger.LogDebug("📝 Inventory item updated - Item: {Item}, OrderId: {OrderId}", 
                        item, order.Id);
                    await Task.Delay(Random.Shared.Next(30, 100));
                }
            }

            _logger.LogInformation("✅ Inventory update completed - OrderId: {OrderId}, ItemCount: {ItemCount}", 
                order.Id, order.Items?.Length ?? 0);
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