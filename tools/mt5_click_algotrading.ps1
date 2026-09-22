# Liga/desliga o botao "Algotrading" da toolbar do MT5.
# A API Python nao expoe esse toggle: terminal_info().trade_allowed so muda pela UI.
# Maximiza a janela primeiro para que o offset da toolbar seja estavel.
param(
    [int]$OffsetX = 384,
    [int]$OffsetY = 67
)

$proc = Get-Process terminal64 -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc -or $proc.MainWindowHandle -eq [IntPtr]::Zero) {
    Write-Error "Janela do MT5 nao encontrada."
    exit 1
}

Add-Type @"
using System;
using System.Runtime.InteropServices;
public struct RECT2 { public int Left, Top, Right, Bottom; }
public static class Click {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT2 r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint cButtons, UIntPtr dwExtraInfo);
}
"@

$hwnd = $proc.MainWindowHandle
[Click]::ShowWindow($hwnd, 3) | Out-Null   # SW_MAXIMIZE
Start-Sleep -Milliseconds 500
[Click]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 700

$r = New-Object RECT2
[Click]::GetWindowRect($hwnd, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$h = $r.Bottom - $r.Top
if ($w -lt 800) {
    Write-Error "Janela do MT5 pequena demais ($w x $h) para localizar a toolbar."
    exit 2
}

$x = $r.Left + $OffsetX
$y = $r.Top + $OffsetY

[Click]::SetCursorPos($x, $y) | Out-Null
Start-Sleep -Milliseconds 300

$MOUSEEVENTF_LEFTDOWN = 0x0002
$MOUSEEVENTF_LEFTUP = 0x0004
[Click]::mouse_event($MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 90
[Click]::mouse_event($MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1500

Write-Output "Clique em ($x, $y). Janela ($($r.Left), $($r.Top)) tamanho $w x $h."
