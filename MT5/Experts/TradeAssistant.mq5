//+------------------------------------------------------------------+
//|                                                    TradeAssistant.mq5 |
//|                        Copyright 2026, NTBot Team                  |
//|                          https://github.com/ntbot/ntbot            |
//+------------------------------------------------------------------+
#property copyright "Copyright 2026, NTBot Team"
#property link      "https://github.com/ntbot/ntbot"
#property version   "1.00"
#property strict
#property indicator_chart_window

//--- Input parameters
input string   NTBot_Server     = "http://localhost:5053";  // NTBot API Server
input int      NTBot_Port       = 5053;                     // NTBot API Port
input string   API_Key          = "";                       // API Key for authentication
input bool     Enable_Grid      = true;                     // Enable Grid Trading
input double   Lot_Size         = 0.01;                     // Base Lot Size
input double   Grid_Step        = 10;                       // Grid Step in Points
input int      Max_Orders       = 10;                       // Maximum Grid Orders
input double   Take_Profit      = 50;                       // Take Profit in Points
input double   Stop_Loss        = 100;                      // Stop Loss in Points
input bool     Use_Martingale   = false;                    // Use Martingale
input double   Martingale_Multi = 1.5;                      // Martingale Multiplier
input int      Magic_Number     = 123456;                   // Magic Number
input string   Symbol_Filter    = "";                       // Symbol Filter (empty for all)

//--- Global variables
int panel_handle = 0;
string panel_name = "NTBot_TradeAssistant_Panel";
bool grid_enabled = true;
double daily_pnl = 0.0;
double current_exposure = 0.0;
double max_drawdown = 0.0;
datetime last_update = 0;
int total_orders = 0;
int winning_trades = 0;

//--- WebSocket client (simulated via HTTP for MT5 compatibility)
string websocket_url = "";

//+------------------------------------------------------------------+
//| Expert initialization function                                   |
//+------------------------------------------------------------------+
int OnInit()
{
    // Initialize WebSocket URL
    websocket_url = NTBot_Server + "/api/mt5/connect";

    // Create graphical panel
    if(!CreatePanel())
    {
        Print("Error creating panel");
        return(INIT_FAILED);
    }

    // Initialize daily stats
    ResetDailyStats();

    // Send initialization message to NTBot
    SendToNTBot("INIT", "EA initialized on " + Symbol());

    Print("TradeAssistant initialized successfully");
    return(INIT_SUCCEEDED);
}

//+------------------------------------------------------------------+
//| Expert deinitialization function                                 |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
{
    // Send deinit message
    SendToNTBot("DEINIT", "EA deinitialized");

    // Destroy panel
    if(panel_handle != 0)
        ObjectDelete(0, panel_name);

    Print("TradeAssistant deinitialized");
}

//+------------------------------------------------------------------+
//| Expert tick function                                             |
//+------------------------------------------------------------------+
void OnTick()
{
    // Update statistics every second
    if(TimeCurrent() - last_update >= 1)
    {
        UpdateStatistics();
        UpdatePanel();
        last_update = TimeCurrent();
    }

    // Check for grid trading opportunities
    if(grid_enabled && Enable_Grid)
    {
        CheckGridConditions();
    }

    // Send market data to NTBot
    SendMarketData();
}

//+------------------------------------------------------------------+
//| Chart event handler                                              |
//+------------------------------------------------------------------+
void OnChartEvent(const int id, const long &lparam, const double &dparam, const string &sparam)
{
    if(id == CHARTEVENT_OBJECT_CLICK)
    {
        if(sparam == panel_name + "_BUY")
        {
            ExecuteBuy();
        }
        else if(sparam == panel_name + "_SELL")
        {
            ExecuteSell();
        }
        else if(sparam == panel_name + "_CLOSE_ALL")
        {
            CloseAllPositions();
        }
        else if(sparam == panel_name + "_REVERSE")
        {
            ReversePosition();
        }
        else if(sparam == panel_name + "_CANCEL_ORDERS")
        {
            CancelPendingOrders();
        }
        else if(sparam == panel_name + "_GRID_TOGGLE")
        {
            grid_enabled = !grid_enabled;
            UpdatePanel();
        }
    }
}

//+------------------------------------------------------------------+
//| Trade event handler                                              |
//+------------------------------------------------------------------+
void OnTrade()
{
    // Update statistics on trade events
    UpdateStatistics();
    UpdatePanel();

    // Send trade update to NTBot
    SendTradeUpdate();
}

//+------------------------------------------------------------------+
//| Timer event handler                                              |
//+------------------------------------------------------------------+
void OnTimer()
{
    // Heartbeat to NTBot every 30 seconds
    SendHeartbeat();
}

//+------------------------------------------------------------------+
//| Create graphical panel                                           |
//+------------------------------------------------------------------+
bool CreatePanel()
{
    int x = 10;
    int y = 30;
    int width = 300;
    int height = 400;

    // Main panel background
    if(!ObjectCreate(0, panel_name, OBJ_RECTANGLE_LABEL, 0, 0, 0))
        return false;

    ObjectSetInteger(0, panel_name, OBJPROP_XDISTANCE, x);
    ObjectSetInteger(0, panel_name, OBJPROP_YDISTANCE, y);
    ObjectSetInteger(0, panel_name, OBJPROP_XSIZE, width);
    ObjectSetInteger(0, panel_name, OBJPROP_YSIZE, height);
    ObjectSetInteger(0, panel_name, OBJPROP_BGCOLOR, clrBlack);
    ObjectSetInteger(0, panel_name, OBJPROP_BORDER_COLOR, clrGray);
    ObjectSetInteger(0, panel_name, OBJPROP_CORNER, CORNER_LEFT_UPPER);
    ObjectSetInteger(0, panel_name, OBJPROP_SELECTABLE, false);

    // Title
    CreateLabel(panel_name + "_TITLE", "NTBot Trade Assistant", x + 10, y + 10, clrWhite, 12);

    // Buttons
    CreateButton(panel_name + "_BUY", "BUY", x + 10, y + 40, 60, 30, clrGreen);
    CreateButton(panel_name + "_SELL", "SELL", x + 80, y + 40, 60, 30, clrRed);
    CreateButton(panel_name + "_CLOSE_ALL", "CLOSE ALL", x + 150, y + 40, 80, 30, clrOrange);
    CreateButton(panel_name + "_REVERSE", "REVERSE", x + 240, y + 40, 50, 30, clrYellow);
    CreateButton(panel_name + "_CANCEL_ORDERS", "CANCEL", x + 10, y + 80, 60, 30, clrGray);
    CreateButton(panel_name + "_GRID_TOGGLE", grid_enabled ? "GRID ON" : "GRID OFF", x + 80, y + 80, 70, 30, grid_enabled ? clrGreen : clrRed);

    // Statistics labels
    CreateLabel(panel_name + "_PNL_LABEL", "Daily PnL:", x + 10, y + 120, clrWhite, 10);
    CreateLabel(panel_name + "_PNL_VALUE", "0.00", x + 100, y + 120, daily_pnl >= 0 ? clrGreen : clrRed, 10);

    CreateLabel(panel_name + "_EXPOSURE_LABEL", "Exposure:", x + 10, y + 140, clrWhite, 10);
    CreateLabel(panel_name + "_EXPOSURE_VALUE", "0.00", x + 100, y + 140, clrBlue, 10);

    CreateLabel(panel_name + "_DRAWDOWN_LABEL", "Max DD:", x + 10, y + 160, clrWhite, 10);
    CreateLabel(panel_name + "_DRAWDOWN_VALUE", DoubleToString(max_drawdown, 2), x + 100, y + 160, clrRed, 10);

    CreateLabel(panel_name + "_ORDERS_LABEL", "Total Orders:", x + 10, y + 180, clrWhite, 10);
    CreateLabel(panel_name + "_ORDERS_VALUE", IntegerToString(total_orders), x + 100, y + 180, clrWhite, 10);

    CreateLabel(panel_name + "_WINRATE_LABEL", "Win Rate:", x + 10, y + 200, clrWhite, 10);
    CreateLabel(panel_name + "_WINRATE_VALUE", "0.00%", x + 100, y + 200, clrWhite, 10);

    // Settings
    CreateLabel(panel_name + "_LOT_LABEL", "Lot Size:", x + 10, y + 230, clrWhite, 10);
    CreateLabel(panel_name + "_LOT_VALUE", DoubleToString(Lot_Size, 2), x + 100, y + 230, clrWhite, 10);

    CreateLabel(panel_name + "_STEP_LABEL", "Grid Step:", x + 10, y + 250, clrWhite, 10);
    CreateLabel(panel_name + "_STEP_VALUE", DoubleToString(Grid_Step, 0), x + 100, y + 250, clrWhite, 10);

    CreateLabel(panel_name + "_MAX_LABEL", "Max Orders:", x + 10, y + 270, clrWhite, 10);
    CreateLabel(panel_name + "_MAX_VALUE", IntegerToString(Max_Orders), x + 100, y + 270, clrWhite, 10);

    return true;
}

//+------------------------------------------------------------------+
//| Create label helper                                              |
//+------------------------------------------------------------------+
void CreateLabel(string name, string text, int x, int y, color clr, int font_size)
{
    if(!ObjectCreate(0, name, OBJ_LABEL, 0, 0, 0))
        return;

    ObjectSetInteger(0, name, OBJPROP_XDISTANCE, x);
    ObjectSetInteger(0, name, OBJPROP_YDISTANCE, y);
    ObjectSetString(0, name, OBJPROP_TEXT, text);
    ObjectSetInteger(0, name, OBJPROP_COLOR, clr);
    ObjectSetInteger(0, name, OBJPROP_FONTSIZE, font_size);
    ObjectSetString(0, name, OBJPROP_FONT, "Arial");
    ObjectSetInteger(0, name, OBJPROP_CORNER, CORNER_LEFT_UPPER);
    ObjectSetInteger(0, name, OBJPROP_SELECTABLE, false);
}

//+------------------------------------------------------------------+
//| Create button helper                                             |
//+------------------------------------------------------------------+
void CreateButton(string name, string text, int x, int y, int width, int height, color clr)
{
    if(!ObjectCreate(0, name, OBJ_BUTTON, 0, 0, 0))
        return;

    ObjectSetInteger(0, name, OBJPROP_XDISTANCE, x);
    ObjectSetInteger(0, name, OBJPROP_YDISTANCE, y);
    ObjectSetInteger(0, name, OBJPROP_XSIZE, width);
    ObjectSetInteger(0, name, OBJPROP_YSIZE, height);
    ObjectSetString(0, name, OBJPROP_TEXT, text);
    ObjectSetInteger(0, name, OBJPROP_BGCOLOR, clr);
    ObjectSetInteger(0, name, OBJPROP_CORNER, CORNER_LEFT_UPPER);
    ObjectSetInteger(0, name, OBJPROP_SELECTABLE, true);
}

//+------------------------------------------------------------------+
//| Update panel display                                             |
//+------------------------------------------------------------------+
void UpdatePanel()
{
    // Update button states
    ObjectSetString(0, panel_name + "_GRID_TOGGLE", grid_enabled ? "GRID ON" : "GRID OFF");
    ObjectSetInteger(0, panel_name + "_GRID_TOGGLE", OBJPROP_BGCOLOR, grid_enabled ? clrGreen : clrRed);

    // Update statistics
    ObjectSetString(0, panel_name + "_PNL_VALUE", DoubleToString(daily_pnl, 2));
    ObjectSetInteger(0, panel_name + "_PNL_VALUE", OBJPROP_COLOR, daily_pnl >= 0 ? clrGreen : clrRed);

    ObjectSetString(0, panel_name + "_EXPOSURE_VALUE", DoubleToString(current_exposure, 2));

    ObjectSetString(0, panel_name + "_DRAWDOWN_VALUE", DoubleToString(max_drawdown, 2));

    ObjectSetString(0, panel_name + "_ORDERS_VALUE", IntegerToString(total_orders));

    double winrate = total_orders > 0 ? (double)winning_trades / total_orders * 100 : 0;
    ObjectSetString(0, panel_name + "_WINRATE_VALUE", DoubleToString(winrate, 2) + "%");
}

//+------------------------------------------------------------------+
//| Update statistics                                                |
//+------------------------------------------------------------------+
void UpdateStatistics()
{
    // Calculate daily PnL
    double pnl = 0.0;
    for(int i = 0; i < PositionsTotal(); i++)
    {
        if(PositionGetSymbol(i) == Symbol() && PositionGetInteger(POSITION_MAGIC) == Magic_Number)
        {
            pnl += PositionGetDouble(POSITION_PROFIT) + PositionGetDouble(POSITION_SWAP);
        }
    }

    // Add closed trades from today
    datetime today_start = TimeCurrent() - (TimeCurrent() % 86400);
    HistorySelect(today_start, TimeCurrent());
    for(int i = 0; i < HistoryDealsTotal(); i++)
    {
        ulong ticket = HistoryDealGetTicket(i);
        if(HistoryDealGetString(ticket, DEAL_SYMBOL) == Symbol() &&
           HistoryDealGetInteger(ticket, DEAL_MAGIC) == Magic_Number)
        {
            pnl += HistoryDealGetDouble(ticket, DEAL_PROFIT);
        }
    }

    daily_pnl = pnl;

    // Calculate exposure
    current_exposure = 0.0;
    for(int i = 0; i < PositionsTotal(); i++)
    {
        if(PositionGetSymbol(i) == Symbol() && PositionGetInteger(POSITION_MAGIC) == Magic_Number)
        {
            current_exposure += PositionGetDouble(POSITION_VOLUME);
        }
    }

    // Calculate max drawdown (simplified)
    if(daily_pnl < max_drawdown)
        max_drawdown = daily_pnl;

    // Count orders
    total_orders = 0;
    winning_trades = 0;
    for(int i = 0; i < OrdersTotal(); i++)
    {
        if(OrderGetString(ORDER_SYMBOL) == Symbol() && OrderGetInteger(ORDER_MAGIC) == Magic_Number)
        {
            total_orders++;
        }
    }

    // Count winning trades (simplified)
    HistorySelect(0, TimeCurrent());
    for(int i = 0; i < HistoryDealsTotal(); i++)
    {
        ulong ticket = HistoryDealGetTicket(i);
        if(HistoryDealGetString(ticket, DEAL_SYMBOL) == Symbol() &&
           HistoryDealGetInteger(ticket, DEAL_MAGIC) == Magic_Number &&
           HistoryDealGetDouble(ticket, DEAL_PROFIT) > 0)
        {
            winning_trades++;
        }
    }
}

//+------------------------------------------------------------------+
//| Reset daily statistics                                           |
//+------------------------------------------------------------------+
void ResetDailyStats()
{
    daily_pnl = 0.0;
    max_drawdown = 0.0;
    total_orders = 0;
    winning_trades = 0;
}

//+------------------------------------------------------------------+
//| Execute buy order                                                |
//+------------------------------------------------------------------+
void ExecuteBuy()
{
    double price = SymbolInfoDouble(Symbol(), SYMBOL_ASK);
    double sl = Stop_Loss > 0 ? price - Stop_Loss * Point() : 0;
    double tp = Take_Profit > 0 ? price + Take_Profit * Point() : 0;

    MqlTradeRequest request = {};
    MqlTradeResult result = {};

    request.action = TRADE_ACTION_DEAL;
    request.symbol = Symbol();
    request.volume = Lot_Size;
    request.type = ORDER_TYPE_BUY;
    request.price = price;
    request.sl = sl;
    request.tp = tp;
    request.magic = Magic_Number;
    request.comment = "NTBot Buy";

    if(OrderSend(request, result))
    {
        Print("Buy order executed: ", result.order);
        SendToNTBot("TRADE", "BUY executed at " + DoubleToString(price, 5));
    }
    else
    {
        Print("Buy order failed: ", result.retcode);
    }
}

//+------------------------------------------------------------------+
//| Execute sell order                                               |
//+------------------------------------------------------------------+
void ExecuteSell()
{
    double price = SymbolInfoDouble(Symbol(), SYMBOL_BID);
    double sl = Stop_Loss > 0 ? price + Stop_Loss * Point() : 0;
    double tp = Take_Profit > 0 ? price - Take_Profit * Point() : 0;

    MqlTradeRequest request = {};
    MqlTradeResult result = {};

    request.action = TRADE_ACTION_DEAL;
    request.symbol = Symbol();
    request.volume = Lot_Size;
    request.type = ORDER_TYPE_SELL;
    request.price = price;
    request.sl = sl;
    request.tp = tp;
    request.magic = Magic_Number;
    request.comment = "NTBot Sell";

    if(OrderSend(request, result))
    {
        Print("Sell order executed: ", result.order);
        SendToNTBot("TRADE", "SELL executed at " + DoubleToString(price, 5));
    }
    else
    {
        Print("Sell order failed: ", result.retcode);
    }
}

//+------------------------------------------------------------------+
//| Close all positions                                              |
//+------------------------------------------------------------------+
void CloseAllPositions()
{
    for(int i = PositionsTotal() - 1; i >= 0; i--)
    {
        if(PositionGetSymbol(i) == Symbol() && PositionGetInteger(POSITION_MAGIC) == Magic_Number)
        {
            ulong ticket = PositionGetInteger(POSITION_TICKET);
            MqlTradeRequest request = {};
            MqlTradeResult result = {};

            request.action = TRADE_ACTION_DEAL;
            request.position = ticket;
            request.symbol = Symbol();
            request.volume = PositionGetDouble(POSITION_VOLUME);
            request.type = PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_BUY ? ORDER_TYPE_SELL : ORDER_TYPE_BUY;
            request.price = PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_BUY ?
                           SymbolInfoDouble(Symbol(), SYMBOL_BID) : SymbolInfoDouble(Symbol(), SYMBOL_ASK);
            request.magic = Magic_Number;

            OrderSend(request, result);
        }
    }

    SendToNTBot("TRADE", "All positions closed");
}

//+------------------------------------------------------------------+
//| Reverse position                                                 |
//+------------------------------------------------------------------+
void ReversePosition()
{
    // Close existing positions
    CloseAllPositions();

    // Open opposite position
    bool has_buy = false;
    bool has_sell = false;

    for(int i = 0; i < PositionsTotal(); i++)
    {
        if(PositionGetSymbol(i) == Symbol() && PositionGetInteger(POSITION_MAGIC) == Magic_Number)
        {
            if(PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_BUY)
                has_buy = true;
            else if(PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_SELL)
                has_sell = true;
        }
    }

    if(has_buy)
        ExecuteSell();
    else if(has_sell)
        ExecuteBuy();
    else
        ExecuteBuy(); // Default to buy if no positions

    SendToNTBot("TRADE", "Position reversed");
}

//+------------------------------------------------------------------+
//| Cancel pending orders                                            |
//+------------------------------------------------------------------+
void CancelPendingOrders()
{
    for(int i = OrdersTotal() - 1; i >= 0; i--)
    {
        if(OrderGetString(ORDER_SYMBOL) == Symbol() && OrderGetInteger(ORDER_MAGIC) == Magic_Number)
        {
            ulong ticket = OrderGetInteger(ORDER_TICKET);
            MqlTradeRequest request = {};
            MqlTradeResult result = {};

            request.action = TRADE_ACTION_REMOVE;
            request.order = ticket;

            OrderSend(request, result);
        }
    }

    SendToNTBot("TRADE", "Pending orders cancelled");
}

//+------------------------------------------------------------------+
//| Check grid conditions                                            |
//+------------------------------------------------------------------+
void CheckGridConditions()
{
    // Simplified grid logic - add orders at intervals
    double current_price = SymbolInfoDouble(Symbol(), SYMBOL_BID);
    static double last_grid_price = 0;

    if(last_grid_price == 0)
        last_grid_price = current_price;

    if(MathAbs(current_price - last_grid_price) >= Grid_Step * Point())
    {
        // Count current orders
        int order_count = 0;
        for(int i = 0; i < OrdersTotal(); i++)
        {
            if(OrderGetString(ORDER_SYMBOL) == Symbol() && OrderGetInteger(ORDER_MAGIC) == Magic_Number)
                order_count++;
        }

        if(order_count < Max_Orders)
        {
            // Place grid order
            MqlTradeRequest request = {};
            MqlTradeResult result = {};

            request.action = TRADE_ACTION_PENDING;
            request.symbol = Symbol();
            request.volume = Lot_Size * (Use_Martingale ? MathPow(Martingale_Multi, order_count) : 1);
            request.type = current_price < last_grid_price ? ORDER_TYPE_BUY_LIMIT : ORDER_TYPE_SELL_LIMIT;
            request.price = current_price < last_grid_price ?
                           current_price - Grid_Step * Point() :
                           current_price + Grid_Step * Point();
            request.magic = Magic_Number;
            request.comment = "NTBot Grid";

            if(OrderSend(request, result))
            {
                last_grid_price = current_price;
                SendToNTBot("GRID", "Grid order placed at " + DoubleToString(request.price, 5));
            }
        }
    }
}

//+------------------------------------------------------------------+
//| Send data to NTBot via HTTP                                      |
//+------------------------------------------------------------------+
void SendToNTBot(string type, string message)
{
    string url = NTBot_Server + "/api/mt5/update";
    string data = StringFormat("{\"type\":\"%s\",\"symbol\":\"%s\",\"message\":\"%s\",\"timestamp\":\"%s\"}",
                              type, Symbol(), message, TimeToString(TimeCurrent()));

    string headers = "Content-Type: application/json\r\n";
    if(API_Key != "")
        headers += "Authorization: Bearer " + API_Key + "\r\n";

    char post_data[];
    StringToCharArray(data, post_data);

    int timeout = 5000;
    char result[];
    string result_headers;

    int res = WebRequest("POST", url, headers, timeout, post_data, result, result_headers);

    if(res == -1)
    {
        Print("WebRequest failed: ", GetLastError());
    }
    else
    {
        Print("Data sent to NTBot: ", type);
    }
}

//+------------------------------------------------------------------+
//| Send market data to NTBot                                        |
//+------------------------------------------------------------------+
void SendMarketData()
{
    double bid = SymbolInfoDouble(Symbol(), SYMBOL_BID);
    double ask = SymbolInfoDouble(Symbol(), SYMBOL_ASK);
    int spread = (int)SymbolInfoInteger(Symbol(), SYMBOL_SPREAD);

    string data = StringFormat("{\"source\":\"MT5\",\"symbol\":\"%s\",\"bid\":%.5f,\"ask\":%.5f,\"spread\":%d,\"timestamp\":\"%s\"}",
                              Symbol(), bid, ask, spread, TimeToString(TimeCurrent()));

    string url = NTBot_Server + "/api/marketdata/tick";
    string headers = "Content-Type: application/json\r\n";
    if(API_Key != "")
        headers += "Authorization: Bearer " + API_Key + "\r\n";

    char post_data[];
    StringToCharArray(data, post_data);

    char result[];
    string result_headers;
    WebRequest("POST", url, headers, 1000, post_data, result, result_headers);
}

//+------------------------------------------------------------------+
//| Send trade update to NTBot                                       |
//+------------------------------------------------------------------+
void SendTradeUpdate()
{
    // Send current positions
    for(int i = 0; i < PositionsTotal(); i++)
    {
        if(PositionGetSymbol(i) == Symbol() && PositionGetInteger(POSITION_MAGIC) == Magic_Number)
        {
            string data = StringFormat("{\"symbol\":\"%s\",\"type\":\"%s\",\"volume\":%.2f,\"price\":%.5f,\"profit\":%.2f}",
                                      PositionGetString(POSITION_SYMBOL),
                                      PositionGetInteger(POSITION_TYPE) == POSITION_TYPE_BUY ? "BUY" : "SELL",
                                      PositionGetDouble(POSITION_VOLUME),
                                      PositionGetDouble(POSITION_PRICE_OPEN),
                                      PositionGetDouble(POSITION_PROFIT));

            SendToNTBot("POSITION", data);
        }
    }
}

//+------------------------------------------------------------------+
//| Send heartbeat                                                   |
//+------------------------------------------------------------------+
void SendHeartbeat()
{
    SendToNTBot("HEARTBEAT", "EA alive");
}

//+------------------------------------------------------------------+