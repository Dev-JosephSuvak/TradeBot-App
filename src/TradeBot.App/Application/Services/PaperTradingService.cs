using TradeBot.App.Domain;

namespace TradeBot.App.Application.Services;

public sealed class PaperTradingService
{
	public PaperTradeDecision CreateDecision(TradeSignal signal)
	{
		var quantity = signal.Action == TradeSignalAction.Hold ? 0m : 1m;
		var status = signal.Action == TradeSignalAction.Hold ? "NoOrder" : "Simulated";
		var notes = signal.Action == TradeSignalAction.Hold
			? "No paper trade created because the signal is HOLD."
			: "Paper trade only. No live brokerage or exchange execution is performed.";

		return new PaperTradeDecision(
			signal.Symbol,
			signal.Provider,
			signal.Action,
			quantity,
			signal.LatestClose,
			status,
			notes,
			DateTime.UtcNow);
	}
}
