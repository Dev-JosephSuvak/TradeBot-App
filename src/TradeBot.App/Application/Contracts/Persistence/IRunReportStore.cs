using TradeBot.App.Domain.Reporting;

namespace TradeBot.App.Application.Contracts.Persistence;

public interface IRunReportStore
{
    Task<IReadOnlyList<RunReportStoreResult>> PersistAsync(RunReport report, CancellationToken cancellationToken);
}
