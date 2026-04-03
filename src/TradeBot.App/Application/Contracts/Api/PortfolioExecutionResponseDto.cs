using System.Text.Json.Serialization;

namespace TradeBot.App.Application.Contracts.Api;

public sealed record PortfolioExecutionResponseDto(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("trade")] TradeDto? Trade,
    [property: JsonPropertyName("final_portfolio")] PortfolioStateDto FinalPortfolio,
    [property: JsonPropertyName("realized_pnl")] double RealizedPnl,
    [property: JsonPropertyName("unrealized_pnl")] double UnrealizedPnl);