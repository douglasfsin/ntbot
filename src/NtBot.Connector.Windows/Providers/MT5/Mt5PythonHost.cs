using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NtBot.Connector.Windows.Configuration;

namespace NtBot.Connector.Windows.Providers.MT5;

/// <summary>
/// Inicia o serviço Flask/Python em segundo plano (padrão DashboardLeilaoRTD).
/// </summary>
public sealed class Mt5PythonHost : IAsyncDisposable
{
    private readonly ILogger<Mt5PythonHost> _logger;
    private Process? _process;
    private readonly StringBuilder _stderr = new();
    private int _activePort;

    public Mt5PythonHost(ILogger<Mt5PythonHost> logger) => _logger = logger;

    public string BaseUrl { get; private set; } = string.Empty;

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(Mt5Config config, CancellationToken ct)
    {
        await StopAsync();
        TryFreePort(config.ApiPort);

        var pythonDir = Path.Combine(AppContext.BaseDirectory, "python");
        if (!Directory.Exists(pythonDir))
            throw new DirectoryNotFoundException($"Pasta Python MT5 não encontrada: {pythonDir}");

        var appPath = Path.Combine(pythonDir, "app.py");
        if (!File.Exists(appPath))
            throw new FileNotFoundException("app.py MT5 não encontrado", appPath);

        BaseUrl = $"http://127.0.0.1:{config.ApiPort}";
        _activePort = config.ApiPort;

        var (pythonExe, pythonPrefix) = ResolvePythonExecutable(config.PythonExecutable);
        var symbols = string.Join(",", config.Symbols);

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = string.IsNullOrEmpty(pythonPrefix) ? "app.py" : $"{pythonPrefix} app.py",
            WorkingDirectory = pythonDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        psi.Environment["FLASK_HOST"] = "127.0.0.1";
        psi.Environment["FLASK_PORT"] = config.ApiPort.ToString();
        psi.Environment["FLASK_DEBUG"] = "false";
        psi.Environment["MT5_SYMBOLS"] = symbols;
        psi.Environment["MT5_TICK_INTERVAL"] = config.TickIntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        psi.Environment["MT5_BOOK_DEPTH"] = config.BookDepth.ToString();

        if (config.SymbolAliases.Count > 0)
        {
            psi.Environment["MT5_SYMBOL_ALIASES"] = JsonSerializer.Serialize(
                config.SymbolAliases.ToDictionary(
                    kv => kv.Key.ToUpperInvariant(),
                    kv => kv.Value,
                    StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(config.Mt5Path))
            psi.Environment["MT5_PATH"] = config.Mt5Path!;
        if (config.Login > 0)
            psi.Environment["MT5_LOGIN"] = config.Login.ToString();
        if (!string.IsNullOrWhiteSpace(config.Password))
            psi.Environment["MT5_PASSWORD"] = config.Password!;
        if (!string.IsNullOrWhiteSpace(config.Server))
            psi.Environment["MT5_SERVER"] = config.Server!;

        _logger.LogInformation("Iniciando MT5 Python ({Exe}) em {Url} símbolos=[{Symbols}]", pythonExe, BaseUrl, symbols);

        lock (_stderr) { _stderr.Clear(); }

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Falha ao iniciar processo Python MT5");

        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _logger.LogDebug("[mt5-python] {Line}", e.Data);
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            lock (_stderr) { _stderr.AppendLine(e.Data); }
            _logger.LogWarning("[mt5-python] {Line}", e.Data);
        };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitForHealthAsync(config, ct);
    }

    public async Task StopAsync()
    {
        if (_process == null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao encerrar processo Python MT5");
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }

        if (_activePort > 0)
            TryFreePort(_activePort);
    }

    public string GetRecentStderr()
    {
        lock (_stderr)
            return _stderr.ToString();
    }

    private async Task WaitForHealthAsync(Mt5Config config, CancellationToken ct)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(120);
        var expectedSymbols = config.Symbols
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (_process?.HasExited == true)
            {
                throw new InvalidOperationException(
                    $"Processo Python MT5 encerrou (code={_process.ExitCode}). {GetRecentStderr()}");
            }

            try
            {
                var json = await client.GetStringAsync($"{BaseUrl}/api/status", ct);
                if (!json.Contains("\"connected\": true", StringComparison.OrdinalIgnoreCase)
                    && !json.Contains("\"connected\":true", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(1500, ct);
                    continue;
                }

                if (expectedSymbols.Count == 0)
                {
                    _logger.LogInformation("MT5 Python API pronta: {Url}", BaseUrl);
                    return;
                }

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("available_symbols", out var symbolsEl))
                {
                    await Task.Delay(1500, ct);
                    continue;
                }

                var available = symbolsEl.EnumerateArray()
                    .Select(el => el.GetString()?.Trim().ToUpperInvariant())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!expectedSymbols.IsSubsetOf(available))
                {
                    _logger.LogDebug(
                        "MT5 Python ainda não refletiu todos os símbolos (esperado={Expected}, api={Available})",
                        string.Join(",", expectedSymbols),
                        string.Join(",", available));
                    await Task.Delay(1500, ct);
                    continue;
                }

                if (doc.RootElement.TryGetProperty("resolved_symbols", out var resolvedEl)
                    && resolvedEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var symbol in expectedSymbols)
                    {
                        if (!resolvedEl.TryGetProperty(symbol, out var mt5El))
                            continue;

                        var mt5Name = mt5El.GetString();
                        if (string.IsNullOrWhiteSpace(mt5Name))
                            _logger.LogWarning("Símbolo {Symbol} configurado mas não encontrado no MT5", symbol);
                        else if (!symbol.Equals(mt5Name, StringComparison.OrdinalIgnoreCase))
                            _logger.LogInformation("Símbolo {Logical} → {Mt5} no MT5", symbol, mt5Name);
                    }
                }

                _logger.LogInformation("MT5 Python API pronta: {Url} ({Count} símbolos)", BaseUrl, expectedSymbols.Count);
                return;
            }
            catch
            {
                // API ainda subindo ou MT5 inicializando
            }

            await Task.Delay(1500, ct);
        }

        throw new TimeoutException(
            $"MT5 Python API não respondeu com todos os símbolos em {BaseUrl} dentro do timeout. {GetRecentStderr()}");
    }

    private void TryFreePort(int port)
    {
        foreach (var pid in FindListeningPids(port))
        {
            if (_process is { HasExited: false } && _process.Id == pid)
                continue;

            try
            {
                var proc = Process.GetProcessById(pid);
                _logger.LogWarning("Encerrando processo {Pid} ({Name}) que ocupa a porta {Port}", pid, proc.ProcessName, port);
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Falha ao liberar porta {Port} (PID {Pid})", port, pid);
            }
        }
    }

    private static IEnumerable<int> FindListeningPids(int port)
    {
        var needle = $":{port}";
        var psi = new ProcessStartInfo
        {
            FileName = "netstat",
            Arguments = "-ano -p tcp",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            yield break;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!line.Contains(needle, StringComparison.Ordinal))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 5 && int.TryParse(parts[^1], out var pid) && pid > 0)
                yield return pid;
        }
    }

    private static (string FileName, string PrefixArgs) ResolvePythonExecutable(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return (configured, string.Empty);

        foreach (var candidate in new[] { "python", "python3", "py" })
        {
            try
            {
                var args = candidate == "py" ? "-3 --version" : "--version";
                var psi = new ProcessStartInfo(candidate, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit(5000);
                    if (p.ExitCode == 0)
                        return candidate == "py" ? ("py", "-3") : (candidate, string.Empty);
                }
            }
            catch
            {
                // try next
            }
        }

        throw new FileNotFoundException(
            "Python não encontrado. Instale Python 3.10+ e MetaTrader5 (`pip install -r python/requirements.txt`).");
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
