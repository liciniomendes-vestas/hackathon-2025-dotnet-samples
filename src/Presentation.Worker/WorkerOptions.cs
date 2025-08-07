namespace Vestas.Psp_poc.Presentation.Worker;

public record WorkerOptions
{
    public string DatabaseConnectionString { get; init; } = string.Empty;

    public string GatewayAddress { get; init; } = string.Empty;

    public TimeSpan WorkerInterval { get; init; } = TimeSpan.FromMinutes(5);

    public string PlantId { get; init; } = string.Empty;

    public string OtelAddress { get; init; } = string.Empty;
}



