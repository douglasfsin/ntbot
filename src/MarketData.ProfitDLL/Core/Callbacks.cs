using ProfitDLLClient.Core;
using ProfitDLLClient.Helpers;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ProfitDLLClient;

/// <summary>
/// Implementação de todos os callbacks da DLL Profit
/// </summary>
public static class Callbacks
{
    private static readonly CultureInfo Provider = CultureInfo.InvariantCulture;
    private const string DateFormat = "dd/MM/yyyy HH:mm:ss.fff";

    public static string AssetListFilter { get; set; } = "";

    #region State and Connection Callbacks

    public static void StateCallback(int nEstado, int nNivel)
    {
        string strEstado;
        string strNivel = nNivel switch
        {
            0 => "Profit",
            1 => "Login",
            2 => "Broker",
            3 => "Market",
            _ => "Level: " + nNivel
        };

        strEstado = nEstado switch
        {
            0 => "Desconectado",
            1 => "Conectando",
            2 => "Conectado",
            3 => "Aguardando Login",
            4 => "HCS Conectando",
            5 => "HCS Conectado",
            6 => "csConnectedWaiting",
            _ => "State: " + nEstado
        };

        ConsoleHelper.WriteSync($"{strNivel}: {strEstado}");

        if (nNivel == 3 && nEstado == 2)
        {
            DataStore.IsMarketConnected = true;
        }
    }

    #endregion

    #region Asset Callbacks

    public static void InvalidTickerCallback(TConnectorAssetIdentifier assetId)
    {
        if (string.IsNullOrWhiteSpace(AssetListFilter) && AssetListFilter == assetId.Ticker)
        {
            ConsoleHelper.WriteSync($"InvalidTickerCallback: {assetId.Ticker}");
        }
    }

    public static void ChangeCotationCallback(TAssetID assetId, [MarshalAs(UnmanagedType.LPWStr)] string date, uint tradeNumber, double sPrice)
    {
        ConsoleHelper.WriteSync($"ChangeCotationCallback: {assetId.Ticker} : {date} : {sPrice}");
    }

    public static void AssetListCallback(TAssetID assetId, [MarshalAs(UnmanagedType.LPWStr)] string strName)
    {
        if (string.IsNullOrWhiteSpace(AssetListFilter) || AssetListFilter == assetId.Ticker)
        {
            ConsoleHelper.WriteSync($"AssetListCallback: {assetId.Ticker} : {strName}");
        }
    }

    public static void AssetListInfoCallback(TAssetID assetId, [MarshalAs(UnmanagedType.LPWStr)] string strName,
        [MarshalAs(UnmanagedType.LPWStr)] string strDescription, int nMinOrderQtd, int nMaxOrderQtd, int nLote,
        int stSecurityType, int ssSecuritySubType, double sMinPriceInc, double sContractMultiplier,
        [MarshalAs(UnmanagedType.LPWStr)] string validityDate, [MarshalAs(UnmanagedType.LPWStr)] string strISIN)
    {
        if ((string.IsNullOrWhiteSpace(AssetListFilter) && !string.IsNullOrWhiteSpace(strISIN)) || AssetListFilter == assetId.Ticker)
        {
            ConsoleHelper.WriteSync($"AssetListInfoCallback: {assetId.Ticker} : {strName} - {strDescription} : ISIN: {strISIN}");
        }
    }

    public static void AssetListInfoCallbackV2(TAssetID assetId, [MarshalAs(UnmanagedType.LPWStr)] string strName,
        [MarshalAs(UnmanagedType.LPWStr)] string strDescription, int nMinOrderQtd, int nMaxOrderQtd, int nLote,
        int stSecurityType, int ssSecuritySubType, double sMinPriceInc, double sContractMultiplier,
        [MarshalAs(UnmanagedType.LPWStr)] string validityDate, [MarshalAs(UnmanagedType.LPWStr)] string strISIN,
        [MarshalAs(UnmanagedType.LPWStr)] string strSetor, [MarshalAs(UnmanagedType.LPWStr)] string strSubSetor,
        [MarshalAs(UnmanagedType.LPWStr)] string strSegmento)
    {
        if ((string.IsNullOrWhiteSpace(AssetListFilter) && !string.IsNullOrWhiteSpace(strISIN)) || AssetListFilter == assetId.Ticker)
        {
            ConsoleHelper.WriteSync($"AssetListInfoCallback: {assetId.Ticker} : {strName} - {strDescription} : ISIN: {strISIN} - Setor: {strSetor}");
        }
    }

    public static void ChangeStateTickerCallback(TAssetID assetId, [MarshalAs(UnmanagedType.LPWStr)] string strDate, int nState)
    {
        ConsoleHelper.WriteSync($"ChangeStateTickerCallback: ticker={assetId.Ticker} Date={strDate} nState={nState}");
    }

    #endregion

    #region Position and Account Callbacks

    public static void AssetPositionListCallback(TConnectorAccountIdentifier accountId, TConnectorAssetIdentifier assetId, int eventId)
    {
        ConsoleHelper.WriteSync($"AssetPositionListCallback: {accountId.AccountID} - {assetId.Ticker} - {eventId}");
    }

    public static void AccountCallback(int nCorretora, [MarshalAs(UnmanagedType.LPWStr)] string corretoraNomeCompleto,
        [MarshalAs(UnmanagedType.LPWStr)] string accountId, [MarshalAs(UnmanagedType.LPWStr)] string nomeTitular)
    {
        ConsoleHelper.WriteSync($"AccountCallback: {accountId} - {nomeTitular}");
    }

    public static void BrokerAccountListChangedCallback(int nCorretora, int nChanged)
    {
        int count = ProfitDLL.GetAccountCountByBroker(nCorretora);
        ConsoleHelper.WriteSync($"BrokerAccountListChangedCallback: Corretora: {nCorretora} - Contas: {count}");
    }

    public static void BrokerSubAccountListChangedCallback(TConnectorAccountIdentifier accountId)
    {
        int count = ProfitDLL.GetSubAccountCount(ref accountId);
        ConsoleHelper.WriteSync($"BrokerSubAccountListChangedCallback: {accountId} - {count}");
    }

    #endregion

    #region Order Callbacks

    public static void OrderCallbackWrapper(TConnectorOrderIdentifier orderId)
    {
        ConsoleHelper.WriteSync($"OrderCallbackWrapper: OrderID={orderId}");
    }

    public static void OrderCallback(TConnectorOrderIdentifier orderId, TConnectorAccountIdentifier accountId, TConnectorAssetIdentifier assetId)
    {
        var order = new TConnectorOrderOut
        {
            Version = 1,
            OrderID = orderId,
            AccountID = new TConnectorAccountIdentifierOut
            {
                Version = accountId.Version,
                BrokerID = accountId.BrokerID,
                AccountID = "",
                AccountIDLength = 100,
                SubAccountID = "",
                SubAccountIDLength = 100
            },
            AssetID = new TConnectorAssetIdentifierOut
            {
                Version = assetId.Version,
                Ticker = "",
                TickerLength = 50,
                Exchange = "",
                ExchangeLength = 50
            },
            TextMessage = "",
            TextMessageLength = 255
        };

        if (ProfitDLL.GetOrderDetails(ref order) != 0) return;

        ConsoleHelper.WriteSync($"OrderCallback: {order.AssetID.Ticker} | {order.TradedQuantity} | {order.OrderSide} | {order.Price} | {order.AccountID.AccountID} | {order.OrderID.ClOrderID} | {order.OrderStatus} | {order.TextMessage}");
    }

    public static void OrderHistoryCallback(TConnectorAccountIdentifier accountId)
    {
        try
        {
            // Note: Calling EnumerateAllOrders from within OrderHistoryCallback causes AccessViolationException
            // This appears to be a limitation of the DLL or a threading/timing issue
            // For now, just log that we received the callback
            ConsoleHelper.WriteSync($"OrderHistoryCallback: Account {accountId.BrokerID}:{accountId.AccountID}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteSync($"Error in OrderHistoryCallback: {ex.Message}");
        }
    }

    #endregion

    #region Book Callbacks

    public static void OfferBookCallbackV2(TAssetID assetId, int nAction, int nPosition, int side, int nQtd, int nAgent,
        long nOfferID, double sPrice, int bHasPrice, int bHasQtd, int bHasDate, int bHasOfferID, int bHasAgent,
        [MarshalAs(UnmanagedType.LPWStr)] string dateStr, IntPtr pArraySell, IntPtr pArrayBuy)
    {
        List<TConnectorOffer> lstBook = side == 0 ? DataStore.OfferBuy : DataStore.OfferSell;

        if (!DateTime.TryParseExact(dateStr, DateFormat, null, DateTimeStyles.None, out DateTime date))
        {
            date = DateTime.MinValue;
        }

        var offer = new TConnectorOffer(sPrice, nQtd, nAgent, nOfferID, date);

        switch (nAction)
        {
            case 0: // Insert
                if (nPosition >= 0 && nPosition < lstBook.Count)
                {
                    lstBook.Insert(lstBook.Count - nPosition, offer);
                }
                break;

            case 1: // Edit
                if (nPosition >= 0 && nPosition < lstBook.Count)
                {
                    TConnectorOffer currentOffer = lstBook[lstBook.Count - 1 - nPosition];
                    if (bHasQtd != 0) currentOffer.Qtd += offer.Qtd;
                    if (bHasPrice != 0) currentOffer.Price = offer.Price;
                    if (bHasOfferID != 0) currentOffer.OfferID = offer.OfferID;
                    if (bHasAgent != 0) currentOffer.Agent = offer.Agent;
                    if (bHasDate != 0) currentOffer.Date = offer.Date;
                    lstBook[lstBook.Count - 1 - nPosition] = currentOffer;
                }
                break;

            case 2: // Delete
                if (nPosition >= 0 && nPosition < lstBook.Count)
                    lstBook.RemoveAt(lstBook.Count - nPosition - 1);
                break;

            case 3: // DeleteFrom
                if (nPosition >= 0 && nPosition < lstBook.Count)
                    lstBook.RemoveRange(lstBook.Count - nPosition - 1, nPosition + 1);
                break;

            case 4: // FullBook
                if (pArraySell != IntPtr.Zero)
                {
                    MarshalHelper.MarshalOfferBuffer(pArraySell, DataStore.OfferSell);
                }
                if (pArrayBuy != IntPtr.Zero)
                {
                    MarshalHelper.MarshalOfferBuffer(pArrayBuy, DataStore.OfferBuy);
                }
                break;
        }
    }

    public static void NewTinyBookCallBack(TAssetID assetId, [MarshalAs(UnmanagedType.LPWStr)] string strDate,
        double dBuyPrice, int nBuyQtd, double dSellPrice, int nSellQtd)
    {
        ConsoleHelper.WriteSync($"NewTinyBookCallBack: {assetId.Ticker} | Buy: {dBuyPrice:N2} x {nBuyQtd} | Sell: {dSellPrice:N2} x {nSellQtd}");
    }

    #endregion

    #region Daily and History Callbacks

    public static void NewDailyCallback(TAssetID assetId, [MarshalAs(UnmanagedType.LPWStr)] string strDate,
        double dOpen, double dHigh, double dLow, double dClose, double dVol, double dAjuste, double dMaxLimit,
        double dMinLimit, double dVolBuyer, double dVolSeller, int nQtd, int nNegocios, int nContratosOpen,
        int nQtdBuyer, int nQtdSeller, [MarshalAs(UnmanagedType.LPWStr)] string strObs)
    {
        if (!string.IsNullOrWhiteSpace(AssetListFilter) && AssetListFilter != assetId.Ticker)
            return;

        ConsoleHelper.WriteSync($"NewDailyCallback: {assetId.Ticker} | Date: {strDate} | Close: {dClose:N2} | Vol: {dVol:N0}");
    }

    public static void TheoreticalPriceCallback(TAssetID assetId, double dValue)
    {
        ConsoleHelper.WriteSync($"TheoreticalPriceCallback: {assetId.Ticker} = {dValue:N2}");
    }

    public static void AdjustHistoryCallbackV2(TAssetID assetId, TConnectorAdjustType AdjustType,
        [MarshalAs(UnmanagedType.LPWStr)] string strDate, double dAdjust)
    {
        ConsoleHelper.WriteSync($"AdjustHistoryCallbackV2: {assetId.Ticker} | {AdjustType} | {strDate} | {dAdjust}");
    }

    #endregion

    #region Price Depth Callback

    public static void PriceDepthCallback(TConnectorAssetIdentifier assetId, byte side, int position, byte updateType)
    {
        const uint PG_IS_THEORIC = 1;

        switch ((TConnectorUpdateType)updateType)
        {
            case TConnectorUpdateType.Add:
                break;

            case TConnectorUpdateType.Edit:
            case TConnectorUpdateType.Insert:
                {
                    var priceGroup = new TConnectorPriceGroup { Version = 0 };
                    if (ProfitDLL.GetPriceGroup(assetId, side, position, ref priceGroup) == 0)
                    {
                        if ((priceGroup.PriceGroupFlags & PG_IS_THEORIC) == PG_IS_THEORIC)
                        {
                            if (ProfitDLL.GetTheoreticalValues(assetId, out var theoricPrice, out _) == 0)
                            {
                                priceGroup.Price = theoricPrice;
                            }
                        }

                        ConsoleHelper.WriteSync($"PriceDepthCallback: {assetId} | {(TConnectorBookSideType)side} : {(TConnectorUpdateType)updateType} | {priceGroup.Price:N} : {priceGroup.Count} : {priceGroup.Quantity}");
                    }
                    break;
                }

            case TConnectorUpdateType.Delete:
                ConsoleHelper.WriteSync($"PriceDepthCallback: {assetId} | {(TConnectorBookSideType)side} : Delete | Position={position}");
                break;

            case TConnectorUpdateType.FullBook:
                {
                    int countBuy = ProfitDLL.GetPriceDepthSideCount(assetId, (byte)TConnectorBookSideType.Buy);
                    if (countBuy >= 0)
                    {
                        ConsoleHelper.WriteSync($"PriceDepthCallback: {assetId} | Buy : FullBook | Count={countBuy}");
                    }

                    int countSell = ProfitDLL.GetPriceDepthSideCount(assetId, (byte)TConnectorBookSideType.Sell);
                    if (countSell >= 0)
                    {
                        ConsoleHelper.WriteSync($"PriceDepthCallback: {assetId} | Sell : FullBook | Count={countSell}");
                    }
                    break;
                }

            case TConnectorUpdateType.DeleteFrom:
                ConsoleHelper.WriteSync($"PriceDepthCallback: {assetId} | {(TConnectorBookSideType)side} : DeleteFrom | Position={position}");
                break;
        }
    }

    #endregion
}
