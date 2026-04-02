using System.Text.Json;
using TradeBot.App.Application.Contracts.Persistence;
using TradeBot.App.Domain.Reporting;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Infrastructure.DataAccess.Persistence;

public sealed class FileRunReportStore : IRunReportStore
{
    private readonly string _baseDirectory;
    private readonly TradeBotSettings _settings;

    public FileRunReportStore(string baseDirectory, TradeBotSettings settings)
    {
        _baseDirectory = baseDirectory;
        _settings = settings;
    }

    public async Task<IReadOnlyList<RunReportStoreResult>> PersistAsync(RunReport report, CancellationToken cancellationToken)
    {
        var outputDirectory = Path.Combine(_baseDirectory, _settings.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, "latest-run.json");
        var payload = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, payload, cancellationToken);

        return [new RunReportStoreResult("File", true, $"Saved to {outputPath}")];
    }
}
