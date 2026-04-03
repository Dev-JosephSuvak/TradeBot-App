using TradeBot.App.Application.Contracts.Api;
using TradeBot.App.Infrastructure.Core;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Application.Services;

public sealed class MarketIndicatorsService
{
    private readonly TradeBotSettings _settings;
    private readonly PythonIndicatorsBridgeRunner _runner;
    private readonly string _projectRoot;

    public MarketIndicatorsService(
        TradeBotSettings settings,
        PythonIndicatorsBridgeRunner runner,
        string projectRoot)
    {
        _settings = settings;
        _runner = runner;
        _projectRoot = projectRoot;
    }

    public Task<IndicatorResponseDto> GetIndicatorsAsync(
        string ticker,
        int period,
        string periodType,
        int? shortWindow,
        int? longWindow,
        CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(_projectRoot, _settings.IndicatorsBridgeScriptRelativePath);

        return _runner.FetchIndicatorsAsync(
            _settings.PythonExecutable,
            scriptPath,
            ticker,
            period,
            periodType,
            shortWindow ?? _settings.ShortWindow,
            longWindow ?? _settings.LongWindow,
            cancellationToken);
    }
}
