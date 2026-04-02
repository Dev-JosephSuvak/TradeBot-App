namespace TradeBot.App.Domain;

public sealed record MarketCandle(
    DateTime TimestampUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);
