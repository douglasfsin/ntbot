//+------------------------------------------------------------------+
//|                                                     NTBot.mqh |
//|                        Copyright 2026, NTBot Team                  |
//|                          https://github.com/ntbot/ntbot            |
//+------------------------------------------------------------------+
#property copyright "Copyright 2026, NTBot Team"
#property link      "https://github.com/ntbot/ntbot"
#property version   "1.00"

//--- NTBot Communication Library
class CNTBotConnector
{
private:
    string m_server_url;
    string m_api_key;
    int m_timeout;

public:
    CNTBotConnector(string server_url, string api_key = "", int timeout = 5000)
    {
        m_server_url = server_url;
        m_api_key = api_key;
        m_timeout = timeout;
    }

    bool SendMessage(string endpoint, string json_data)
    {
        string url = m_server_url + endpoint;
        string headers = "Content-Type: application/json\r\n";
        if(m_api_key != "")
            headers += "Authorization: Bearer " + m_api_key + "\r\n";

        char post_data[];
        StringToCharArray(json_data, post_data);

        char result[];
        string result_headers;

        int res = WebRequest("POST", url, headers, m_timeout, post_data, result, result_headers);

        return (res != -1);
    }

    bool SendTickData(string symbol, double bid, double ask, int spread)
    {
        string json = StringFormat(
            "{\"source\":\"MT5\",\"symbol\":\"%s\",\"bid\":%.5f,\"ask\":%.5f,\"spread\":%d,\"timestamp\":\"%s\"}",
            symbol, bid, ask, spread, TimeToString(TimeCurrent())
        );

        return SendMessage("/api/marketdata/tick", json);
    }

    bool SendTradeUpdate(string symbol, string type, double volume, double price, double profit)
    {
        string json = StringFormat(
            "{\"symbol\":\"%s\",\"type\":\"%s\",\"volume\":%.2f,\"price\":%.5f,\"profit\":%.2f,\"timestamp\":\"%s\"}",
            symbol, type, volume, price, profit, TimeToString(TimeCurrent())
        );

        return SendMessage("/api/trading/update", json);
    }

    bool SendHeartbeat(string symbol)
    {
        string json = StringFormat(
            "{\"symbol\":\"%s\",\"status\":\"alive\",\"timestamp\":\"%s\"}",
            symbol, TimeToString(TimeCurrent())
        );

        return SendMessage("/api/mt5/heartbeat", json);
    }
};

//--- Trading Utilities
class CTradingUtils
{
public:
    static bool IsValidSymbol(string symbol)
    {
        return SymbolInfoDouble(symbol, SYMBOL_BID) > 0;
    }

    static double GetLotSize(double base_lot, int multiplier = 1)
    {
        double min_lot = SymbolInfoDouble(Symbol(), SYMBOL_VOLUME_MIN);
        double max_lot = SymbolInfoDouble(Symbol(), SYMBOL_VOLUME_MAX);
        double lot_step = SymbolInfoDouble(Symbol(), SYMBOL_VOLUME_STEP);

        double lot = base_lot * multiplier;
        lot = MathRound(lot / lot_step) * lot_step;

        return MathMax(min_lot, MathMin(max_lot, lot));
    }

    static bool CheckMargin(double lot_size)
    {
        double margin_required = 0;
        return OrderCalcMargin(ORDER_TYPE_BUY, Symbol(), lot_size, SymbolInfoDouble(Symbol(), SYMBOL_ASK), margin_required) &&
               AccountInfoDouble(ACCOUNT_MARGIN_FREE) >= margin_required;
    }

    static string GetPositionTypeString(ENUM_POSITION_TYPE type)
    {
        return type == POSITION_TYPE_BUY ? "BUY" : "SELL";
    }

    static string GetOrderTypeString(ENUM_ORDER_TYPE type)
    {
        switch(type)
        {
            case ORDER_TYPE_BUY: return "BUY";
            case ORDER_TYPE_SELL: return "SELL";
            case ORDER_TYPE_BUY_LIMIT: return "BUY_LIMIT";
            case ORDER_TYPE_SELL_LIMIT: return "SELL_LIMIT";
            case ORDER_TYPE_BUY_STOP: return "BUY_STOP";
            case ORDER_TYPE_SELL_STOP: return "SELL_STOP";
            default: return "UNKNOWN";
        }
    }
};

//--- Risk Management
class CRiskManager
{
private:
    double m_daily_loss_limit;
    double m_max_drawdown;
    double m_daily_pnl;
    datetime m_daily_reset;

public:
    CRiskManager(double daily_loss_limit = 100, double max_drawdown = 200)
    {
        m_daily_loss_limit = daily_loss_limit;
        m_max_drawdown = max_drawdown;
        m_daily_pnl = 0;
        m_daily_reset = TimeCurrent() - (TimeCurrent() % 86400) + 86400; // Next midnight
    }

    bool CanTrade()
    {
        // Reset daily stats at midnight
        if(TimeCurrent() >= m_daily_reset)
        {
            m_daily_pnl = 0;
            m_daily_reset += 86400;
        }

        // Check daily loss limit
        if(m_daily_pnl <= -m_daily_loss_limit)
            return false;

        // Check drawdown
        if(AccountInfoDouble(ACCOUNT_EQUITY) - AccountInfoDouble(ACCOUNT_BALANCE) <= -m_max_drawdown)
            return false;

        return true;
    }

    void UpdatePnL(double pnl_change)
    {
        m_daily_pnl += pnl_change;
    }

    double GetDailyPnL() { return m_daily_pnl; }
    double GetDrawdown() { return AccountInfoDouble(ACCOUNT_BALANCE) - AccountInfoDouble(ACCOUNT_EQUITY); }
};

//--- Grid Trading Engine
class CGridEngine
{
private:
    double m_step_points;
    int m_max_orders;
    double m_lot_multiplier;
    bool m_use_martingale;
    double m_last_grid_price;

public:
    CGridEngine(double step_points = 10, int max_orders = 10, double lot_multiplier = 1.5, bool martingale = false)
    {
        m_step_points = step_points;
        m_max_orders = max_orders;
        m_lot_multiplier = lot_multiplier;
        m_use_martingale = martingale;
        m_last_grid_price = 0;
    }

    bool ShouldPlaceGridOrder(double current_price, int current_order_count)
    {
        if(current_order_count >= m_max_orders)
            return false;

        if(m_last_grid_price == 0)
        {
            m_last_grid_price = current_price;
            return false;
        }

        return MathAbs(current_price - m_last_grid_price) >= m_step_points * Point();
    }

    double CalculateGridPrice(double current_price)
    {
        double direction = current_price < m_last_grid_price ? -1 : 1;
        return current_price + direction * m_step_points * Point();
    }

    ENUM_ORDER_TYPE GetGridOrderType(double current_price)
    {
        return current_price < m_last_grid_price ? ORDER_TYPE_BUY_LIMIT : ORDER_TYPE_SELL_LIMIT;
    }

    double CalculateLotSize(double base_lot, int order_count)
    {
        if(!m_use_martingale)
            return base_lot;

        return base_lot * MathPow(m_lot_multiplier, order_count);
    }

    void UpdateLastGridPrice(double price)
    {
        m_last_grid_price = price;
    }
};

//--- Statistics Tracker
class CStatisticsTracker
{
private:
    int m_total_trades;
    int m_winning_trades;
    double m_total_profit;
    double m_total_loss;
    datetime m_start_time;

public:
    CStatisticsTracker()
    {
        Reset();
    }

    void Reset()
    {
        m_total_trades = 0;
        m_winning_trades = 0;
        m_total_profit = 0;
        m_total_loss = 0;
        m_start_time = TimeCurrent();
    }

    void AddTrade(double profit)
    {
        m_total_trades++;
        if(profit > 0)
        {
            m_winning_trades++;
            m_total_profit += profit;
        }
        else
        {
            m_total_loss += MathAbs(profit);
        }
    }

    double GetWinRate()
    {
        return m_total_trades > 0 ? (double)m_winning_trades / m_total_trades * 100 : 0;
    }

    double GetProfitFactor()
    {
        return m_total_loss > 0 ? m_total_profit / m_total_loss : 0;
    }

    double GetAverageWin()
    {
        return m_winning_trades > 0 ? m_total_profit / m_winning_trades : 0;
    }

    double GetAverageLoss()
    {
        int losing_trades = m_total_trades - m_winning_trades;
        return losing_trades > 0 ? m_total_loss / losing_trades : 0;
    }

    int GetTotalTrades() { return m_total_trades; }
    double GetTotalProfit() { return m_total_profit; }
    double GetTotalLoss() { return m_total_loss; }
    double GetNetProfit() { return m_total_profit - m_total_loss; }
};

//+------------------------------------------------------------------+