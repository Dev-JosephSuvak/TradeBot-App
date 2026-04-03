using System.Diagnostics;
using System.Text.Json;
using TradeBot.App.Application.Contracts.Api;

namespace TradeBot.App.Infrastructure.Core;

public sealed class PythonIndicatorsBridgeRunner
{
    public async Task<IndicatorResponseDto> FetchIndicatorsAsync(
        string pythonExecutable,
        string scriptPath,
        string ticker,
        int period,
        string periodType,
        int shortWindow,
        int longWindow,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--ticker");
        startInfo.ArgumentList.Add(ticker);
        startInfo.ArgumentList.Add("--period");
        startInfo.ArgumentList.Add(period.ToString());
        startInfo.ArgumentList.Add("--period-type");
        startInfo.ArgumentList.Add(periodType);
        startInfo.ArgumentList.Add("--short-window");
        startInfo.ArgumentList.Add(shortWindow.ToString());
        startInfo.ArgumentList.Add("--long-window");
        startInfo.ArgumentList.Add(longWindow.ToString());

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Python indicators bridge failed: {stderr}".Trim());
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            throw new InvalidOperationException("Python indicators bridge returned empty output.");
        }

        var response = JsonSerializer.Deserialize<IndicatorResponseDto>(
            stdout,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (response is null)
        {
            throw new InvalidOperationException("Python indicators bridge returned malformed JSON.");
        }

        return response;
    }
}
