using TradeBot.App.Application.Contracts.Persistence;
using TradeBot.App.Domain;

namespace TradeBot.App.Infrastructure.DataAccess.Persistence;

public sealed class FirebaseFirstPortfolioStore : IPortfolioStore
{
    private readonly IPortfolioStore _primary;
    private readonly IPortfolioStore _fallback;

    public FirebaseFirstPortfolioStore(IPortfolioStore primary, IPortfolioStore fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public async Task<PortfolioLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        var primaryResult = await _primary.LoadAsync(cancellationToken);
        if (primaryResult.Success)
        {
            return primaryResult with { Message = $"Primary load succeeded: {primaryResult.Message}" };
        }

        var fallbackResult = await _fallback.LoadAsync(cancellationToken);
        if (fallbackResult.Success)
        {
            return fallbackResult with { Message = $"Primary failed ({primaryResult.Message}); fallback succeeded: {fallbackResult.Message}" };
        }

        return new PortfolioLoadResult(
            false,
            fallbackResult.State,
            "Failover",
            $"Primary failed: {primaryResult.Message}. Fallback failed: {fallbackResult.Message}");
    }

    public async Task<PortfolioStoreResult> SaveAsync(PortfolioState state, CancellationToken cancellationToken)
    {
        var primaryResult = await _primary.SaveAsync(state, cancellationToken);
        if (primaryResult.Success)
        {
            var backupResult = await _fallback.SaveAsync(state, cancellationToken);
            return backupResult.Success
                ? primaryResult with { Message = $"{primaryResult.Message}; backup file saved." }
                : primaryResult with { Message = $"{primaryResult.Message}; backup file failed: {backupResult.Message}" };
        }

        var fallbackResult = await _fallback.SaveAsync(state, cancellationToken);
        return fallbackResult.Success
            ? fallbackResult with { Message = $"Primary failed ({primaryResult.Message}); fallback saved." }
            : new PortfolioStoreResult("Failover", false, $"Primary failed: {primaryResult.Message}. Fallback failed: {fallbackResult.Message}");
    }
}
