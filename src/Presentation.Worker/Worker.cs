using System.Net.Http.Json;

using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Vestas.Psp_poc.Presentation.Worker;

public class Worker(
    IHttpClientFactory httpClientFactory,
    PspMetrics pspMetrics,
    ILogger<Worker> logger,
    IOptions<WorkerOptions> options) : BackgroundService
{
    private const string COMMAND_TYPE = "psp-scripts";
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly PspMetrics _pspMetrics = pspMetrics;
    private readonly ILogger<Worker> _logger = logger;
    private readonly WorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_options.GatewayAddress);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: [{time}]", DateTimeOffset.Now);
            }

            using var databaseConnection = new NpgsqlConnection(_options.DatabaseConnectionString);
            try
            {
                databaseConnection.Open();
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "Error connecting to database with connection string [{connectionString}]", _options.DatabaseConnectionString);
                }

                await Task.Delay(_options.WorkerInterval, stoppingToken);
                continue;
            }

            var currentVersion = await GetCurrentDbVersion(databaseConnection);
            _pspMetrics.SetCurrentVersion(currentVersion);

            IList<ScriptCommandModel> scripts;
            try
            {
                scripts = await client.GetFromJsonAsync<IList<ScriptCommandModel>>($"plants/{_options.PlantId}/commands/{COMMAND_TYPE}", stoppingToken) ?? [];
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, "Error fetching script commands for plant [{plantId}]", _options.PlantId);
                }

                await Task.Delay(_options.WorkerInterval, stoppingToken);
                continue;
            }

            if (scripts.Count == 0)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("No script commands found for plant [{plantId}]", _options.PlantId);
                }

                await Task.Delay(_options.WorkerInterval, stoppingToken);
                continue;
            }

            foreach (var script in scripts)
            {
                _pspMetrics.IncrementScriptCounter();
                var isSuccess = await ExecuteScript(client, script, databaseConnection, stoppingToken);

                var exitEarly = false;
                switch (isSuccess)
                {
                    case ScriptResult.Success:
                        _pspMetrics.IncrementScriptSuccessCounter();
                        break;
                    case ScriptResult.Failed:
                        _pspMetrics.IncrementScriptErrorCounter();
                        exitEarly = true;
                        break;
                    case ScriptResult.Skipped:
                        _pspMetrics.IncreamentScriptSkippedCounter();
                        break;
                }

                if (exitEarly)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.LogWarning("Exiting early due to script failure for plant [{plantId}]", _options.PlantId);
                    }
                    break;
                }
            }

            await Task.Delay(_options.WorkerInterval, stoppingToken);
        }
    }

    private async Task<int> GetCurrentDbVersion(NpgsqlConnection databaseConnection)
    {
        try
        {
            return await databaseConnection.ExecuteScalarAsync<int>("SELECT version FROM public.version ORDER BY version DESC LIMIT 1;");
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error fetching current database version");
            }
            return 0;
        }
    }

    private async Task<ScriptResult> ExecuteScript(
        HttpClient client,
        ScriptCommandModel script,
        NpgsqlConnection databaseConnection,
        CancellationToken stoppingToken)
    {
        try
        {
            var previousVersion = await GetCurrentDbVersion(databaseConnection);

            if (previousVersion >= script.Version)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Script command [{ScriptIdentifier}] already applied for plant [{plantId}]. Current version: {CurrentVersion}, Script version: {ScriptVersion}",
                        script.Identifier, _options.PlantId, previousVersion, script.Version);
                }
                return ScriptResult.Skipped;
            }

            using var resp = await client.GetAsync(
                $"packages/{COMMAND_TYPE}/{script.Identifier}",
                HttpCompletionOption.ResponseHeadersRead,
                stoppingToken);

            if (!resp.IsSuccessStatusCode)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError("Failed to download script {ScriptIdentifier} for plant [{plantId}]. Status code: {StatusCode}",
                        script.Identifier, _options.PlantId, resp.StatusCode);
                }

                return ScriptResult.Failed;
            }

            var scriptContent = await resp.Content.ReadAsStringAsync(stoppingToken);

            await databaseConnection.ExecuteAsync(scriptContent);

            var currentVersion = await GetCurrentDbVersion(databaseConnection);

            _pspMetrics.SetPreviousVersion(previousVersion);
            _pspMetrics.SetCurrentVersion(currentVersion);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error processing script {ScriptIdentifier} command for plant [{plantId}]", script.Identifier, _options.PlantId);
            }

            await Task.Delay(_options.WorkerInterval, stoppingToken);
            return ScriptResult.Failed;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Script command [{ScriptIdentifier}] processed successfully for plant [{plantId}]", script.Identifier, _options.PlantId);
        }

        return ScriptResult.Success;
    }
}
