using System.Text.Json;

namespace TradeBot.App.Infrastructure.Settings;

public static class TradeBotSettingsLoader
{
    public static TradeBotSettings Load(string baseDirectory)
    {
        var configPath = Path.Combine(baseDirectory, "appsettings.json");
        TradeBotSettings settings;

        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            settings = JsonSerializer.Deserialize<TradeBotSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new TradeBotSettings();
        }
        else
        {
            settings = new TradeBotSettings();
        }

        return settings with
        {
            PythonExecutable = GetOverride("TRADEBOT_PYTHON", settings.PythonExecutable),
            OutputDirectory = GetOverride("TRADEBOT_OUTPUT_DIR", settings.OutputDirectory),
            TwelveData = settings.TwelveData with
            {
                ApiKey = GetOverride("TWELVE_DATA_API_KEY", settings.TwelveData.ApiKey),
                Enabled = GetBoolOverride("TWELVE_DATA_ENABLED", settings.TwelveData.Enabled)
            },
            Polygon = settings.Polygon with
            {
                ApiKey = GetOverride("POLYGON_API_KEY", settings.Polygon.ApiKey),
                Enabled = GetBoolOverride("POLYGON_ENABLED", settings.Polygon.Enabled)
            },
            Binance = settings.Binance with
            {
                Enabled = GetBoolOverride("BINANCE_ENABLED", settings.Binance.Enabled)
            },
            Firebase = settings.Firebase with
            {
                Enabled = GetBoolOverride("FIREBASE_ENABLED", settings.Firebase.Enabled),
                ProjectId = GetOverride("FIREBASE_PROJECT_ID", settings.Firebase.ProjectId),
                DatabaseId = GetOverride("FIREBASE_DATABASE_ID", settings.Firebase.DatabaseId),
                CollectionName = GetOverride("FIREBASE_COLLECTION_NAME", settings.Firebase.CollectionName),
                CredentialsPath = GetOverride("GOOGLE_APPLICATION_CREDENTIALS", settings.Firebase.CredentialsPath),
                EmulatorHost = GetOverride("FIRESTORE_EMULATOR_HOST", settings.Firebase.EmulatorHost)
            }
        };
    }

    private static string GetOverride(string variableName, string currentValue)
    {
        var overrideValue = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(overrideValue) ? currentValue : overrideValue;
    }

    private static bool GetBoolOverride(string variableName, bool currentValue)
    {
        var overrideValue = Environment.GetEnvironmentVariable(variableName);
        return bool.TryParse(overrideValue, out var parsed) ? parsed : currentValue;
    }
}