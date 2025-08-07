using System.Diagnostics.Metrics;

namespace Vestas.Psp_poc.Presentation.Worker;

public class PspMetrics
{
    internal const string METER_NAME = "Psp.Metrics";
    private readonly Counter<int> _scriptCounter;
    private readonly Counter<int> _scriptSuccessCounter;
    private readonly Counter<int> _scriptErrorCounter;
    private readonly Counter<int> _scriptSkippedCounter;
    private readonly Gauge<int> _dbCurrentVersion;
    private readonly Gauge<int> _dbPreviousVersion;

    public PspMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(METER_NAME);

        _scriptCounter = meter.CreateCounter<int>("script_total", "Total number of scripts executed");
        _scriptSuccessCounter = meter.CreateCounter<int>("script_success", "Number of scripts sucessfully executed");
        _scriptErrorCounter = meter.CreateCounter<int>("script_error", "Number of scripts that failed");
        _scriptSkippedCounter = meter.CreateCounter<int>("script_skipped", "Number of scripts that were skipped");
        _dbCurrentVersion = meter.CreateGauge<int>("db_current_version", "Current version of the script commands");
        _dbPreviousVersion = meter.CreateGauge<int>("db_previous_version", "Previous version of the db");
    }

    public void IncrementScriptCounter()
    {
        _scriptCounter.Add(1);
    }

    public void IncrementScriptSuccessCounter()
    {
        _scriptSuccessCounter.Add(1);
    }

    public void IncrementScriptErrorCounter()
    {
        _scriptErrorCounter.Add(1);
    }

    public void IncreamentScriptSkippedCounter()
    {
        _scriptSkippedCounter.Add(1);
    }

    public void SetCurrentVersion(int version)
    {
        _dbCurrentVersion.Record(version);
    }

    public void SetPreviousVersion(int version)
    {
        _dbPreviousVersion.Record(version);
    }
}




