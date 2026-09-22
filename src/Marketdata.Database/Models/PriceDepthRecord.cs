using System;

namespace Marketdata.Database.Models;

public record PriceDepthRecord(
    string Ticker,
    string Side,
    int Position,
    double Price,
    long Quantity,
    int AgentCount,
    string AgentDetails,
    DateTime Timestamp
);
