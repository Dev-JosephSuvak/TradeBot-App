using System.Text.Json;

namespace TradeBot.App.Infrastructure.Settings;

public static class RuntimeGlobalsLoader
{
    public static bool ApplyFromFile(string globalsFilePath, out string message)
    {
        if (string.IsNullOrWhiteSpace(globalsFilePath))
        {
            message = "No globals file provided.";
            return false;
        }

        if (!File.Exists(globalsFilePath))
        {
            message = $"Globals file not found: {globalsFilePath}";
            return false;
        }

        var payload = File.ReadAllText(globalsFilePath);
        var document = JsonDocument.Parse(payload);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            message = $"Globals file must contain a single JSON object: {globalsFilePath}";
            return false;
        }

        var applied = 0;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var current = Environment.GetEnvironmentVariable(property.Name);
            if (!string.IsNullOrWhiteSpace(current))
            {
                continue;
            }

            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => property.Value.GetRawText()
            };

            if (value is null)
            {
                continue;
            }

            Environment.SetEnvironmentVariable(property.Name, value);
            applied++;
        }

        message = $"Applied {applied} runtime variables from {globalsFilePath}";
        return true;
    }
}