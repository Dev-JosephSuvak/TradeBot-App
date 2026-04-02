namespace TradeBot.App.Infrastructure.Settings;

public sealed record ProviderSettings
{
    public bool Enabled { get; init; } = true;
}