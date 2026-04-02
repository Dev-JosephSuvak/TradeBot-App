using Google.Cloud.Firestore;
using TradeBot.App.Application.Contracts.Persistence;
using TradeBot.App.Domain.Reporting;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Infrastructure.DataAccess.Persistence;

public sealed class FirestoreRunReportStore : IRunReportStore
{
    private readonly TradeBotSettings _settings;

    public FirestoreRunReportStore(TradeBotSettings settings)
    {
        _settings = settings;
    }

    public async Task<IReadOnlyList<RunReportStoreResult>> PersistAsync(RunReport report, CancellationToken cancellationToken)
    {
        if (!_settings.Firebase.Enabled)
        {
            return [new RunReportStoreResult("Firestore", true, "Skipped (disabled).")];
        }

        if (string.IsNullOrWhiteSpace(_settings.Firebase.ProjectId))
        {
            return [new RunReportStoreResult("Firestore", false, "Missing Firebase project ID.")];
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_settings.Firebase.CredentialsPath) && File.Exists(_settings.Firebase.CredentialsPath))
            {
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", _settings.Firebase.CredentialsPath);
            }

            if (!string.IsNullOrWhiteSpace(_settings.Firebase.EmulatorHost))
            {
                Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", _settings.Firebase.EmulatorHost);
            }

            var databaseId = string.IsNullOrWhiteSpace(_settings.Firebase.DatabaseId) ? "(default)" : _settings.Firebase.DatabaseId;
            var builder = new FirestoreDbBuilder
            {
                ProjectId = _settings.Firebase.ProjectId,
                DatabaseId = databaseId
            };
            var db = await builder.BuildAsync(cancellationToken);

            var runId = $"run-{report.GeneratedAtUtc:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
            var document = db.Collection(_settings.Firebase.CollectionName).Document(runId);

            var firestorePayload = new Dictionary<string, object>
            {
                ["generatedAtUtc"] = Timestamp.FromDateTime(report.GeneratedAtUtc),
                ["mode"] = report.Mode,
                ["source"] = "TradeBot.App",
                ["results"] = report.Results.Select(result => new Dictionary<string, object?>
                {
                    ["provider"] = result.Provider,
                    ["symbol"] = result.Symbol,
                    ["status"] = result.Status,
                    ["candles"] = result.Candles,
                    ["message"] = result.Message,
                    ["error"] = result.Error,
                    ["signal"] = result.Signal is null ? null : new Dictionary<string, object?>
                    {
                        ["provider"] = result.Signal.Provider,
                        ["symbol"] = result.Signal.Symbol,
                        ["action"] = result.Signal.Action.ToString(),
                        ["latestClose"] = (double)result.Signal.LatestClose,
                        ["shortAverage"] = (double)result.Signal.ShortAverage,
                        ["longAverage"] = (double)result.Signal.LongAverage,
                        ["reason"] = result.Signal.Reason,
                        ["generatedAtUtc"] = Timestamp.FromDateTime(result.Signal.GeneratedAtUtc)
                    },
                    ["paperTrade"] = result.PaperTrade is null ? null : new Dictionary<string, object?>
                    {
                        ["provider"] = result.PaperTrade.Provider,
                        ["symbol"] = result.PaperTrade.Symbol,
                        ["action"] = result.PaperTrade.Action.ToString(),
                        ["quantity"] = (double)result.PaperTrade.Quantity,
                        ["referencePrice"] = (double)result.PaperTrade.ReferencePrice,
                        ["status"] = result.PaperTrade.Status,
                        ["notes"] = result.PaperTrade.Notes,
                        ["recordedAtUtc"] = Timestamp.FromDateTime(result.PaperTrade.RecordedAtUtc)
                    }
                }).ToList()
            };

            await document.SetAsync(firestorePayload, cancellationToken: cancellationToken);
            return [new RunReportStoreResult("Firestore", true, $"Saved to {_settings.Firebase.CollectionName}/{runId}")];
        }
        catch (Exception ex)
        {
            return [new RunReportStoreResult("Firestore", false, ex.Message)];
        }
    }
}
