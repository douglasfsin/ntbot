namespace ProfitDLLClient.Core;

/// <summary>
/// Gerenciador de callbacks da DLL Profit
/// </summary>
public static class CallbackManager
{
    // Delegates mantidos em memória para evitar GC
    public static TAssetListCallback AssetListCallback = new(Callbacks.AssetListCallback);
    public static TAssetListInfoCallback AssetListInfoCallback = new(Callbacks.AssetListInfoCallback);
    public static TAssetListInfoCallbackV2 AssetListInfoCallbackV2 = new(Callbacks.AssetListInfoCallbackV2);
    public static TStateCallback StateCallback = new(Callbacks.StateCallback);
    // Note: These callbacks have mismatched signatures in the DLL - not registering them for now
    // public static TNewDailyCallback NewDailyCallback = new(Callbacks.NewDailyCallback);
    // public static TNewTinyBookCallBack NewTinyBookCallBack = new(Callbacks.NewTinyBookCallBack);
    public static TAccountCallback AccountCallback = new(Callbacks.AccountCallback);
    public static TConnectorBrokerAccountListCallback BrokerAccountListCallback = new(Callbacks.BrokerAccountListChangedCallback);
    public static TConnectorBrokerSubAccountListCallback BrokerSubAccountListCallback = new(Callbacks.BrokerSubAccountListChangedCallback);
    public static TChangeStateTickerCallback ChangeStateTickerCallback = new(Callbacks.ChangeStateTickerCallback);
    // Note: TheoreticalPriceCallback has mismatched signature
    // public static TTheoreticalPriceCallback TheoreticalPriceCallback = new(Callbacks.TheoreticalPriceCallback);
    public static TConnectorAssetPositionListCallback AssetPositionListCallback = new(Callbacks.AssetPositionListCallback);
    // Note: AdjustHistoryCallbackV2 has mismatched signature
    // public static TAdjustHistoryCallbackV2 AdjustHistoryCallbackV2 = new(Callbacks.AdjustHistoryCallbackV2);
    // Note: OrderCallback has mismatched signature (expects 3 parameters, callback has 3)
    // Actually this should work, let me check the implementation again
    public static TConnectorOrderCallback OrderCallback = new(Callbacks.OrderCallbackWrapper);
    public static TConnectorAccountCallback OrderHistoryCallback = new(Callbacks.OrderHistoryCallback);
    public static TConnectorPriceDepthCallback PriceDepthCallback = new(Callbacks.PriceDepthCallback);

    /// <summary>
    /// Registra todos os callbacks na DLL
    /// </summary>
    public static void RegisterCallbacks()
    {
        ProfitDLL.SetPriceDepthCallback(PriceDepthCallback);
        ProfitDLL.SetBrokerAccountListChangedCallback(BrokerAccountListCallback);
        ProfitDLL.SetBrokerSubAccountListChangedCallback(BrokerSubAccountListCallback);
        ProfitDLL.SetOrderCallback(OrderCallback);
        ProfitDLL.SetOrderHistoryCallback(OrderHistoryCallback);
        // ProfitDLL.SetOfferBookCallbackV2(OfferBookCallbackV2); // Disabled due to signature mismatch
        ProfitDLL.SetAssetListInfoCallbackV2(AssetListInfoCallbackV2);
        // ProfitDLL.SetAdjustHistoryCallbackV2(AdjustHistoryCallbackV2); // Disabled due to signature mismatch
        ProfitDLL.SetAssetPositionListCallback(AssetPositionListCallback);
    }
}
