namespace TradeBot.App.Infrastructure.Settings;

public sealed record ApiSettings
{
    public bool Enabled { get; init; }
    public string ApiKey { get; init; } = string.Empty;
}