namespace Vestas.Psp_poc.Presentation.Worker;

public record ScriptCommandModel
{
    public int Version { get; init; }

    public string Identifier { get; init; } = string.Empty;
}




