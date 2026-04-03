using Google.Cloud.Firestore;
using TradeBot.App.Application.Contracts.Persistence;
using TradeBot.App.Domain;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Infrastructure.DataAccess.Persistence;

public sealed class FirestorePortfolioStore : IPortfolioStore
{
    private readonly TradeBotSettings _settings;

    public FirestorePortfolioStore(TradeBotSettings settings)
    {
        _settings = settings;
    }

    public async Task<PortfolioLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        if (!_settings.Firebase.Enabled)
        {
            return new PortfolioLoadResult(false, PortfolioState.New(_settings.Portfolio.StartingBalanceUsd), "Firestore", "Skipped (disabled).");
        }

        if (string.IsNullOrWhiteSpace(_settings.Firebase.ProjectId))
        {
            return new PortfolioLoadResult(false, PortfolioState.New(_settings.Portfolio.StartingBalanceUsd), "Firestore", "Missing Firebase project ID.");
        }

        try
        {
            var db = await BuildDbAsync(cancellationToken);
            var doc = db.Collection(_settings.Firebase.PortfolioCollectionName).Document("current");
            var snapshot = await doc.GetSnapshotAsync(cancellationToken);

            if (!snapshot.Exists)
            {
                return new PortfolioLoadResult(true, PortfolioState.New(_settings.Portfolio.StartingBalanceUsd), "Firestore", "No Firestore portfolio found; initialized new state.");
            }

            var payload = snapshot.GetValue<string>("payloadJson");
            var state = System.Text.Json.JsonSerializer.Deserialize<PortfolioState>(payload, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (state is null)
            {
                return new PortfolioLoadResult(false, PortfolioState.New(_settings.Portfolio.StartingBalanceUsd), "Firestore", "Malformed Firestore payloadJson.");
            }

            return new PortfolioLoadResult(true, state, "Firestore", "Loaded from Firestore.");
        }
        catch (Exception ex)
        {
            return new PortfolioLoadResult(false, PortfolioState.New(_settings.Portfolio.StartingBalanceUsd), "Firestore", ex.Message);
        }
    }

    public async Task<PortfolioStoreResult> SaveAsync(PortfolioState state, CancellationToken cancellationToken)
    {
        if (!_settings.Firebase.Enabled)
        {
            return new PortfolioStoreResult("Firestore", false, "Skipped (disabled).");
        }

        if (string.IsNullOrWhiteSpace(_settings.Firebase.ProjectId))
        {
            return new PortfolioStoreResult("Firestore", false, "Missing Firebase project ID.");
        }

        try
        {
            var db = await BuildDbAsync(cancellationToken);
            var doc = db.Collection(_settings.Firebase.PortfolioCollectionName).Document("current");
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(state);

            await doc.SetAsync(new Dictionary<string, object>
            {
                ["payloadJson"] = payloadJson,
                ["updatedAtUtc"] = Timestamp.FromDateTime(DateTime.SpecifyKind(state.UpdatedAtUtc, DateTimeKind.Utc))
            }, cancellationToken: cancellationToken);

            return new PortfolioStoreResult("Firestore", true, $"Saved to {_settings.Firebase.PortfolioCollectionName}/current");
        }
        catch (Exception ex)
        {
            return new PortfolioStoreResult("Firestore", false, ex.Message);
        }
    }

    private async Task<FirestoreDb> BuildDbAsync(CancellationToken cancellationToken)
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

        return await builder.BuildAsync(cancellationToken);
    }
}
