# Liga o AutoTrading (Algo Trading) do MetaTrader 5.
# A API Python do MT5 não expõe esse toggle: terminal_info().trade_allowed só muda pela UI.
# Estratégia: focar a janela do terminal e enviar Ctrl+E via keybd_event (mais confiável que SendKeys).

$proc = Get-Process terminal64 -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) {
    Write-Error "MetaTrader 5 (terminal64) não está em execução."
    exit 1
}

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
"@

$hwnd = $proc.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) {
    Write-Error "Janela principal do MT5 não encontrada (terminal minimizado na bandeja?)."
    exit 1
}

# Anexa a fila de input da thread do MT5 para que SetForegroundWindow seja aceito.
$targetThread = [Win]::GetWindowThreadProcessId($hwnd, [IntPtr]::Zero)
$currentThread = [Win]::GetCurrentThreadId()
[Win]::AttachThreadInput($currentThread, $targetThread, $true) | Out-Null

[Win]::ShowWindow($hwnd, 9) | Out-Null   # SW_RESTORE
[Win]::BringWindowToTop($hwnd) | Out-Null
[Win]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 900

$focused = [Win]::GetForegroundWindow()
if ($focused -ne $hwnd) {
    [Win]::AttachThreadInput($currentThread, $targetThread, $false) | Out-Null
    Write-Error "Não foi possível trazer o MT5 para o primeiro plano. Ative o botão 'Algo Trading' manualmente."
    exit 2
}

$VK_CONTROL = 0x11
$VK_E = 0x45
$KEYEVENTF_KEYUP = 0x0002

[Win]::keybd_event($VK_CONTROL, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Win]::keybd_event($VK_E, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Win]::keybd_event($VK_E, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Win]::keybd_event($VK_CONTROL, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)

Start-Sleep -Milliseconds 1500
[Win]::AttachThreadInput($currentThread, $targetThread, $false) | Out-Null

Write-Output "Ctrl+E enviado ao MT5 (PID $($proc.Id))."
