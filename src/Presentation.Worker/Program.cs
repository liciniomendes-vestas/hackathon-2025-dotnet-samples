using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using Vestas.Psp_poc.Presentation.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(nameof(WorkerOptions)));
builder.Services.AddLogging(l => l.AddJsonConsole());
builder.Services.AddHttpClient();

// Metrics
var otelAddress = builder.Configuration.GetValue<string>("WorkerOptions:OtelAddress")
    ?? throw new InvalidOperationException("OtelAddress configuration is required.");

builder.Services.AddSingleton<PspMetrics>();
var meterProvider = Sdk.CreateMeterProviderBuilder()
    // Other setup code, like setting a resource goes here too
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otelAddress);
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
    })
    .AddMeter(PspMetrics.METER_NAME)
    .AddMeter("System.Runtime")
    .AddMeter("System.Net.Http")
    .Build();

var host = builder.Build();
host.Run();
