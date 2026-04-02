using TradeBot.App.Application.Contracts.Persistence;
using TradeBot.App.Domain.Reporting;

namespace TradeBot.App.Infrastructure.DataAccess.Persistence;

public sealed class CompositeRunReportStore : IRunReportStore
{
    private readonly IReadOnlyList<IRunReportStore> _stores;

    public CompositeRunReportStore(params IRunReportStore[] stores)
    {
        _stores = stores;
    }

    public async Task<IReadOnlyList<RunReportStoreResult>> PersistAsync(RunReport report, CancellationToken cancellationToken)
    {
        var results = new List<RunReportStoreResult>();

        foreach (var store in _stores)
        {
            var storeResults = await store.PersistAsync(report, cancellationToken);
            results.AddRange(storeResults);
        }

        return results;
    }
}
