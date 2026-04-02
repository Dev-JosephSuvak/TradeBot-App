using System.Text.Json.Serialization;

namespace TradeBot.App.Application.Contracts.Api;

public sealed record SimulationResultDto(
    [property: JsonPropertyName("trades")] IReadOnlyList<TradeDto> Trades,
    [property: JsonPropertyName("final_portfolio")] PortfolioStateDto FinalPortfolio,
    [property: JsonPropertyName("realized_pnl")] double RealizedPnl,
    [property: JsonPropertyName("unrealized_pnl")] double UnrealizedPnl);
