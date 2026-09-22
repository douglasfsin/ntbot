# Clica o botao "Algotrading" via UI Automation (nao depende de coordenadas).
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$proc = Get-Process terminal64 -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc -or $proc.MainWindowHandle -eq [IntPtr]::Zero) {
    Write-Error "MT5 nao encontrado."
    exit 1
}

$root = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
$condName = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, "Algotrading")
$btn = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condName)

if (-not $btn) {
    # fallback em ingles
    $condName = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "Algo Trading")
    $btn = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condName)
}

if (-not $btn) {
    Write-Output "Botao Algotrading nao encontrado via UIA. Listando toolbar..."
    $condToolbar = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ToolBar)
    $toolbars = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condToolbar)
    foreach ($tb in $toolbars) {
        Write-Output ("TOOLBAR: " + $tb.Current.Name)
        $children = $tb.FindAll([System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($c in $children) {
            Write-Output ("  - name='$($c.Current.Name)' type=$($c.Current.ControlType.ProgrammaticName) help='$($c.Current.HelpText)'")
        }
    }
    exit 2
}

Write-Output "Encontrado: name='$($btn.Current.Name)' type=$($btn.Current.ControlType.ProgrammaticName)"
$invoke = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
if ($invoke) {
    $invoke.Invoke()
    Write-Output "Invoke() OK"
} else {
    $tp = $btn.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if ($tp) {
        if ($tp.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) {
            $tp.Toggle()
            Write-Output "Toggle() -> On"
        } else {
            Write-Output "Ja estava On"
        }
    } else {
        Write-Error "Sem Invoke/Toggle pattern."
        exit 3
    }
}
