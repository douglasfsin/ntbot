using System.Runtime.InteropServices;
using RTDTrading;

namespace NtBot.Connector.Windows.Providers.Profit;

/// <summary>
/// Callback COM exigido pelo servidor RTD do ProfitChart (equivalente ao cliente Excel).
/// </summary>
internal sealed class ProfitRtdUpdateSink : IRTDUpdateEvent
{
    private readonly Action _onUpdate;

    public ProfitRtdUpdateSink(Action onUpdate) => _onUpdate = onUpdate;

    public int HeartbeatInterval { get; set; } = -1;

    public void UpdateNotify() => _onUpdate();

    public void Disconnect()
    {
        // Profit não exige ação aqui
    }
}
