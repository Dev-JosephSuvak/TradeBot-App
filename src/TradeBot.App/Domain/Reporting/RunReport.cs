namespace TradeBot.App.Domain.Reporting;

public sealed record RunReport(
    DateTime GeneratedAtUtc,
    string Mode,
    IReadOnlyList<RunResultItem> Results);
