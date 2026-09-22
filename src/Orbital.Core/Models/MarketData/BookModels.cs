using System;
using System.Collections.Generic;

namespace Orbital.Core.Models.MarketData
{
    // Snapshot evento para envio e cálculos do Orbital.Core
    public record BookSnapshotEvent(
        string Ticker,
        long TimestampMicroseconds,
        List<BookLevel> Bids,
        List<BookLevel> Asks,
        bool IsFullBook = false
    );

    // Representa um nível consolidado de preço
    public record BookLevel(
        int Side,         // 0 = Compra, 1 = Venda
        int Position,     // Posição no book (1, 2, 3...)
        double Price,
        long Quantity,
        uint Count,       // Qtde de agentes
        List<AgentOrder> Orders // Detalhamento de agentes
    );

    // Representa uma ordem individual no nível
    public record AgentOrder(
        int AgentId,
        long Quantity,
        long OrderId
    );
}
