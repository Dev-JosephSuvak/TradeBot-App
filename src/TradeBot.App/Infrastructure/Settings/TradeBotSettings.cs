namespace TradeBot.App.Infrastructure.Settings;

public sealed record TradeBotSettings
{
    public string OutputDirectory { get; init; } = "output";
    public int LookbackDays { get; init; } = 60;
    public int ShortWindow { get; init; } = 5;
    public int LongWindow { get; init; } = 20;
    public string PythonExecutable { get; init; } = "python3";
    public string YahooBridgeScriptRelativePath { get; init; } = "Infrastructure/Core/yahoo_bridge.py";
    public string IndicatorsBridgeScriptRelativePath { get; init; } = "Infrastructure/Core/indicator_bridge.py";
    public List<string> Symbols { get; init; } = ["AAPL", "MSFT", "BTC-USD", "ETH-USD"];
    public ApiSettings TwelveData { get; init; } = new();
    public ApiSettings Polygon { get; init; } = new();
    public ProviderSettings Binance { get; init; } = new();
    public FirebaseSettings Firebase { get; init; } = new();
    public SimulationSettings Simulation { get; init; } = new();
    public PortfolioSettings Portfolio { get; init; } = new();
}