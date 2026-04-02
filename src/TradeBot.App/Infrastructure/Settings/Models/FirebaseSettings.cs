namespace TradeBot.App.Infrastructure.Settings;

public sealed record FirebaseSettings
{
    public bool Enabled { get; init; }
    public string ProjectId { get; init; } = string.Empty;
    public string DatabaseId { get; init; } = "(default)";
    public string CollectionName { get; init; } = "tradebot_runs";
    public string CredentialsPath { get; init; } = string.Empty;
    public string EmulatorHost { get; init; } = string.Empty;
}