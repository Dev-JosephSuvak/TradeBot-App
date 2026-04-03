using System.Collections.ObjectModel;
using TradeBot.App.Application.Contracts.Persistence;
using TradeBot.App.Application.Contracts.Portfolio;
using TradeBot.App.Domain;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Application.Services;

public sealed class PaperPortfolioService
{
    private readonly IPortfolioStore _portfolioStore;
    private readonly TradeBotSettings _settings;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private PortfolioState? _state;

    public PaperPortfolioService(IPortfolioStore portfolioStore, TradeBotSettings settings)
    {
        _portfolioStore = portfolioStore;
        _settings = settings;
    }

    public async Task<PortfolioState> GetStateAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _state!;
    }

    public async Task<PortfolioExecutionResult> ExecuteAsync(PortfolioExecutionRequest request, CancellationToken cancellationToken)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);
            var current = _state!;

            var ticker = request.Ticker.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(ticker))
            {
                return new PortfolioExecutionResult(false, "Ticker is required.", null, current);
            }

            if (request.Quantity <= 0 || request.Price <= 0)
            {
                return new PortfolioExecutionResult(false, "Quantity and price must be greater than zero.", null, current);
            }

            var action = ParseAction(request.Action);
            if (action is null)
            {
                return new PortfolioExecutionResult(false, "Action must be buy or sell.", null, current);
            }

            var positions = current.Positions.ToDictionary(p => p.Ticker, p => p);
            var history = current.TradeHistory.ToList();
            var cash = current.CashBalanceUsd;
            var realized = current.RealizedPnl;
            var now = DateTime.UtcNow;
            var strategyName = string.IsNullOrWhiteSpace(request.Strategy)
                ? _settings.Portfolio.DefaultTradeStrategy
                : request.Strategy.Trim();

            positions.TryGetValue(ticker, out var existingPosition);

            if (action == PortfolioTradeAction.Buy)
            {
                var notional = request.Quantity * request.Price;
                if (cash < notional)
                {
                    return new PortfolioExecutionResult(false, "Insufficient cash balance.", null, current);
                }

                var positionMarketValue = existingPosition is null ? 0m : existingPosition.Quantity * request.Price;
                var maxPositionValue = Math.Max(0m, current.Equity * _settings.Portfolio.MaxPositionSizePercent);
                if (positionMarketValue + notional > maxPositionValue)
                {
                    return new PortfolioExecutionResult(false, $"Risk limit exceeded. Max position value: {maxPositionValue:F2}", null, current);
                }

                cash -= notional;

                if (existingPosition is null)
                {
                    positions[ticker] = new PortfolioPosition(ticker, request.Quantity, request.Price, request.Price, now, now);
                }
                else
                {
                    var newQuantity = existingPosition.Quantity + request.Quantity;
                    var newAverage = ((existingPosition.Quantity * existingPosition.AverageEntryPrice) + (request.Quantity * request.Price)) / newQuantity;
                    positions[ticker] = existingPosition with
                    {
                        Quantity = newQuantity,
                        AverageEntryPrice = newAverage,
                        LastPrice = request.Price,
                        UpdatedAtUtc = now
                    };
                }

                var trade = new PortfolioTrade(
                    TradeId: $"trade-{Guid.NewGuid():N}",
                    Ticker: ticker,
                    Action: PortfolioTradeAction.Buy,
                    Quantity: request.Quantity,
                    Price: request.Price,
                    Notional: notional,
                    RealizedPnl: 0m,
                    Reason: request.Reason ?? "Manual buy",
                    ExecutedAtUtc: now,
                    StrategyName: strategyName);

                history.Add(trade);
                var newState = RebuildState(current.StartingBalanceUsd, cash, realized, positions.Values, history, now);
                _state = newState;
                await _portfolioStore.SaveAsync(newState, cancellationToken);
                WriteTradeLog(trade);
                return new PortfolioExecutionResult(true, "Buy executed.", trade, newState);
            }

            if (existingPosition is null || existingPosition.Quantity < request.Quantity)
            {
                return new PortfolioExecutionResult(false, "Insufficient position quantity to sell.", null, current);
            }

            var sellNotional = request.Quantity * request.Price;
            var sellRealized = request.Quantity * (request.Price - existingPosition.AverageEntryPrice);
            cash += sellNotional;
            realized += sellRealized;

            var remaining = existingPosition.Quantity - request.Quantity;
            if (remaining == 0)
            {
                positions.Remove(ticker);
            }
            else
            {
                positions[ticker] = existingPosition with
                {
                    Quantity = remaining,
                    LastPrice = request.Price,
                    UpdatedAtUtc = now
                };
            }

            var sellTrade = new PortfolioTrade(
                TradeId: $"trade-{Guid.NewGuid():N}",
                Ticker: ticker,
                Action: PortfolioTradeAction.Sell,
                Quantity: request.Quantity,
                Price: request.Price,
                Notional: sellNotional,
                RealizedPnl: sellRealized,
                Reason: request.Reason ?? "Manual sell",
                ExecutedAtUtc: now,
                StrategyName: strategyName);

            history.Add(sellTrade);
            var updatedState = RebuildState(current.StartingBalanceUsd, cash, realized, positions.Values, history, now);
            _state = updatedState;
            await _portfolioStore.SaveAsync(updatedState, cancellationToken);
            WriteTradeLog(sellTrade);
            return new PortfolioExecutionResult(true, "Sell executed.", sellTrade, updatedState);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<PortfolioState> RefreshTickerAndApplyStopLossAsync(string ticker, decimal latestPrice, CancellationToken cancellationToken)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);
            var state = _state!;
            var normalized = ticker.ToUpperInvariant();
            var positions = state.Positions.ToDictionary(p => p.Ticker, p => p);
            var history = state.TradeHistory.ToList();
            var now = DateTime.UtcNow;
            var cash = state.CashBalanceUsd;
            var realized = state.RealizedPnl;

            if (!positions.TryGetValue(normalized, out var position))
            {
                return state;
            }

            position = position with { LastPrice = latestPrice, UpdatedAtUtc = now };
            positions[normalized] = position;

            var stopPrice = position.StopLossPrice(_settings.Portfolio.StopLossPercent);
            if (latestPrice <= stopPrice)
            {
                var qty = position.Quantity;
                var notional = qty * latestPrice;
                var realizedPnl = qty * (latestPrice - position.AverageEntryPrice);
                cash += notional;
                realized += realizedPnl;

                positions.Remove(normalized);
                history.Add(new PortfolioTrade(
                    TradeId: $"trade-{Guid.NewGuid():N}",
                    Ticker: normalized,
                    Action: PortfolioTradeAction.StopLossSell,
                    Quantity: qty,
                    Price: latestPrice,
                    Notional: notional,
                    RealizedPnl: realizedPnl,
                    Reason: $"Stop-loss triggered at {latestPrice:F4}",
                        ExecutedAtUtc: now,
                        StrategyName: "StopLoss"));
            }

            var newState = RebuildState(state.StartingBalanceUsd, cash, realized, positions.Values, history, now);
            _state = newState;
            await _portfolioStore.SaveAsync(newState, cancellationToken);
                    if (history.Count > state.TradeHistory.Count)
                    {
                    WriteTradeLog(history[^1]);
                    }
            return newState;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private static PortfolioTradeAction? ParseAction(string action) => action.Trim().ToLowerInvariant() switch
    {
        "buy" => PortfolioTradeAction.Buy,
        "sell" => PortfolioTradeAction.Sell,
        _ => null
    };

    private static void WriteTradeLog(PortfolioTrade trade)
    {
        Console.WriteLine(
            $"[Trade] {trade.ExecutedAtUtc:O} ticker={trade.Ticker} action={trade.Action} qty={trade.Quantity:F4} price={trade.Price:F4} notional={trade.Notional:F2} realizedPnl={trade.RealizedPnl:F2} strategy={trade.StrategyName} reason=\"{trade.Reason}\"");
    }

    private static PortfolioState RebuildState(
        decimal startingBalance,
        decimal cash,
        decimal realized,
        IEnumerable<PortfolioPosition> positions,
        IReadOnlyList<PortfolioTrade> trades,
        DateTime updatedAtUtc)
    {
        var positionList = positions.ToList();
        var unrealized = positionList.Sum(p => p.UnrealizedPnl);
        var equity = cash + positionList.Sum(p => p.MarketValue);

        return new PortfolioState(
            StartingBalanceUsd: startingBalance,
            CashBalanceUsd: cash,
            RealizedPnl: realized,
            UnrealizedPnl: unrealized,
            Equity: equity,
            Positions: new ReadOnlyCollection<PortfolioPosition>(positionList),
            TradeHistory: new ReadOnlyCollection<PortfolioTrade>(trades.ToList()),
            UpdatedAtUtc: updatedAtUtc);
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task EnsureLoadedLockedAsync(CancellationToken cancellationToken)
    {
        if (_state is not null)
        {
            return;
        }

        var loaded = await _portfolioStore.LoadAsync(cancellationToken);
        _state = loaded.Success ? loaded.State : PortfolioState.New(_settings.Portfolio.StartingBalanceUsd);

        if (!loaded.Success)
        {
            await _portfolioStore.SaveAsync(_state, cancellationToken);
        }
    }
}
