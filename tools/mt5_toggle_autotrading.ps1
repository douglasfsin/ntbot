# Liga o botao AlgoTrading do MT5 via WM_COMMAND 32851 (mesmo mecanismo usado por scripts MQL).
# Nao depende de foco/mouse — PostMessage direto na janela principal.

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Mt5Toggle {
    public const uint WM_COMMAND = 0x0111;
    public const int MT5_WMCMD_EXPERTS = 32851;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowW(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
}
"@

$proc = Get-Process terminal64 -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) {
    Write-Error "terminal64 nao esta em execucao."
    exit 1
}

$script:hwnd = [IntPtr]::Zero
$targetPid = [uint32]$proc.Id
$callback = [Mt5Toggle+EnumWindowsProc]{
    param($h, $l)
    $pidOut = [uint32]0
    [void][Mt5Toggle]::GetWindowThreadProcessId($h, [ref]$pidOut)
    if ($pidOut -eq $targetPid) {
        $sb = New-Object System.Text.StringBuilder 512
        [void][Mt5Toggle]::GetWindowTextW($h, $sb, $sb.Capacity)
        $title = $sb.ToString()
        if ($title -match "MetaQuotes|MetaTrader|Conta Demo|XAUUSD") {
            $script:hwnd = $h
            return $false
        }
    }
    return $true
}

[void][Mt5Toggle]::EnumWindows($callback, [IntPtr]::Zero)

if ($script:hwnd -eq [IntPtr]::Zero) {
    # fallback: MainWindowHandle do processo
    $script:hwnd = $proc.MainWindowHandle
}

if ($script:hwnd -eq [IntPtr]::Zero) {
    Write-Error "Janela do MT5 nao encontrada."
    exit 1
}

Write-Output "HWND=$($script:hwnd) PID=$($proc.Id)"
[void][Mt5Toggle]::PostMessageW($script:hwnd, [Mt5Toggle]::WM_COMMAND, [IntPtr][Mt5Toggle]::MT5_WMCMD_EXPERTS, [IntPtr]::Zero)
Start-Sleep -Milliseconds 800
Write-Output "WM_COMMAND 32851 enviado."
