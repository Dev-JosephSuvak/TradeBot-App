using System.Text.Json;
using TradeBot.App.Application.Contracts.Persistence;
using TradeBot.App.Domain;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Infrastructure.DataAccess.Persistence;

public sealed class FilePortfolioStore : IPortfolioStore
{
    private readonly string _baseDirectory;
    private readonly TradeBotSettings _settings;

    public FilePortfolioStore(string baseDirectory, TradeBotSettings settings)
    {
        _baseDirectory = baseDirectory;
        _settings = settings;
    }

    public async Task<PortfolioLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var outputDirectory = Path.Combine(_baseDirectory, _settings.OutputDirectory);
            Directory.CreateDirectory(outputDirectory);

            var outputPath = Path.Combine(outputDirectory, "latest-portfolio.json");
            if (!File.Exists(outputPath))
            {
                return new PortfolioLoadResult(
                    true,
                    PortfolioState.New(_settings.Portfolio.StartingBalanceUsd),
                    "File",
                    "No local portfolio found; initialized new state.");
            }

            var payload = await File.ReadAllTextAsync(outputPath, cancellationToken);
            var state = JsonSerializer.Deserialize<PortfolioState>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (state is null)
            {
                return new PortfolioLoadResult(
                    false,
                    PortfolioState.New(_settings.Portfolio.StartingBalanceUsd),
                    "File",
                    "Local portfolio JSON is malformed.");
            }

            return new PortfolioLoadResult(true, state, "File", $"Loaded {outputPath}");
        }
        catch (Exception ex)
        {
            return new PortfolioLoadResult(
                false,
                PortfolioState.New(_settings.Portfolio.StartingBalanceUsd),
                "File",
                ex.Message);
        }
    }

    public async Task<PortfolioStoreResult> SaveAsync(PortfolioState state, CancellationToken cancellationToken)
    {
        try
        {
            var outputDirectory = Path.Combine(_baseDirectory, _settings.OutputDirectory);
            Directory.CreateDirectory(outputDirectory);

            var outputPath = Path.Combine(outputDirectory, "latest-portfolio.json");
            var payload = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, payload, cancellationToken);

            return new PortfolioStoreResult("File", true, $"Saved to {outputPath}");
        }
        catch (Exception ex)
        {
            return new PortfolioStoreResult("File", false, ex.Message);
        }
    }
}
