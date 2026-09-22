# Enumera janelas/controles do MT5 e tenta IDs de comando proximos de 32851.
Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class EnumMt5 {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr hWnd, EnumProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    public const uint WM_COMMAND = 0x0111;
}
"@

$proc = Get-Process terminal64 | Select-Object -First 1
$hwnd = $proc.MainWindowHandle
Write-Output "Main HWND=$hwnd title=$($proc.MainWindowTitle)"

$script:children = New-Object System.Collections.Generic.List[string]
$cb = [EnumMt5+EnumProc]{
    param($h,$l)
    $name = New-Object System.Text.StringBuilder 256
    $cls = New-Object System.Text.StringBuilder 256
    [void][EnumMt5]::GetWindowTextW($h, $name, 256)
    [void][EnumMt5]::GetClassNameW($h, $cls, 256)
    $n = $name.ToString(); $c = $cls.ToString()
    if ($n -or $c -match "Toolbar|Button|Expert|Trade|msctls") {
        $script:children.Add("hwnd=$h class='$c' text='$n'")
    }
    return $true
}
[void][EnumMt5]::EnumChildWindows($hwnd, $cb, [IntPtr]::Zero)
Write-Output "Children matched: $($script:children.Count)"
$script:children | Select-Object -First 40 | ForEach-Object { Write-Output $_ }

# Tenta faixa de command IDs comuns para AutoTrading
[void][EnumMt5]::ShowWindow($hwnd, 3)
[void][EnumMt5]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 500

$ids = @(32851, 33020, 33134, 33000, 32900, 32850, 32852, 33048, 35400, 35401)
foreach ($id in $ids) {
    [void][EnumMt5]::PostMessageW($hwnd, [EnumMt5]::WM_COMMAND, [IntPtr]$id, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 400
    $state = python -c "import MetaTrader5 as m; m.initialize(); print(m.terminal_info().trade_allowed); m.shutdown()"
    Write-Output "ID $id -> trade_allowed=$state"
    if ($state -match "True") { break }
}
