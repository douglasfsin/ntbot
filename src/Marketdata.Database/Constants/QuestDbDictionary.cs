namespace Marketdata.Database.Constants;

public static class QuestDbDictionary
{
    public static class Tables
    {
        public const string Trades = "trades";
        public const string Agents = "agents";
        public const string TradeTypes = "trade_types";
        public const string TradeSide = "trade_side";
        public const string PointsOfInterest = "points_of_interest";
    }

    public static class TradesColumns
    {
        public const string Ticker = "ticker";
        public const string Side = "side";
        public const string BuyAgent = "buy_agent";
        public const string SellAgent = "sell_agent";
        public const string Price = "price";
        public const string Quantity = "quantity";
        public const string TradeType = "trade_type";
        public const string TradeNumber = "trade_number";
        public const string IsAuction = "is_auction";
    }

    public static class AgentsColumns
    {
        public const string AgentId = "agent_id";
        public const string FullName = "full_name";
        public const string ShortName = "short_name";
    }

    public static class TradeTypesColumns
    {
        public const string Description = "description";
        public const string TypeId = "type_id";
        public const string Comment = "comment";
    }

    public static class TradeSideColumns
    {
        public const string SideId = "side_id";
        public const string Description = "description";
    }

    public static class PointsOfInterestColumns
    {
        public const string PoiId = "poi_id";
        public const string CreatedAt = "created_at";
        public const string Ticker = "ticker";
        public const string Type = "type";
        public const string Side = "side";
        public const string Description = "description";
        public const string PriceStart = "price_start";
        public const string PriceEnd = "price_end";
        public const string IsActive = "is_active";
    }
}
