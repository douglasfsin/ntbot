namespace NtBot.Connector.Windows.UI;

/// <summary>
/// Form invisível que ancora o message loop WinForms — necessário para cliques no NotifyIcon funcionarem.
/// Deve permanecer visível (fora da tela); se Hide(), forms filhos também ficam invisíveis.
/// </summary>
internal sealed class TrayHostForm : Form
{
    public TrayHostForm()
    {
        Text = "NTBot Connector";
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        Size = new Size(1, 1);
        Opacity = 0;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
    }
}
