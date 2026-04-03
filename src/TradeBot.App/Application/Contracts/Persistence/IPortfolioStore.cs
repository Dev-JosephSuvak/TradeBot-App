using TradeBot.App.Domain;

namespace TradeBot.App.Application.Contracts.Persistence;

public interface IPortfolioStore
{
    Task<PortfolioLoadResult> LoadAsync(CancellationToken cancellationToken);
    Task<PortfolioStoreResult> SaveAsync(PortfolioState state, CancellationToken cancellationToken);
}
