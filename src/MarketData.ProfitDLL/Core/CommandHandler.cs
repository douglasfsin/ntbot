using ProfitDLLClient.Helpers;

namespace ProfitDLLClient.Core;

/// <summary>
/// Gerencia comandos e operações disponíveis
/// </summary>
public static class CommandHandler
{
    private const int NL_OK = 0;

    #region Subscribe/Unsubscribe

    public static void SubscribeTicker()
    {
        ConsoleHelper.WriteSync("Digite o ticker (ex: PETR4:BOVESPA):");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input)) return;

        var parts = input.Split(':');
        if (parts.Length != 2)
        {
            ConsoleHelper.WriteSync("Formato inválido. Use TICKER:EXCHANGE");
            return;
        }

        var result = ProfitDLL.SubscribeTicker(parts[0], parts[1]);
        ConsoleHelper.WriteResult(result);
    }

    public static void UnsubscribeTicker()
    {
        ConsoleHelper.WriteSync("Digite o ticker (ex: PETR4:BOVESPA):");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input)) return;

        var parts = input.Split(':');
        if (parts.Length != 2)
        {
            ConsoleHelper.WriteSync("Formato inválido. Use TICKER:EXCHANGE");
            return;
        }

        var result = ProfitDLL.UnsubscribeTicker(parts[0], parts[1]);
        ConsoleHelper.WriteResult(result);
    }

    public static void SubscribePriceBook()
    {
        var assetId = MarshalHelper.ReadAssetId();
        var result = ProfitDLL.SubscribePriceBook(assetId.Ticker, assetId.Exchange);
        ConsoleHelper.WriteResult(result);
    }

    public static void SubscribeOfferBook()
    {
        var assetId = MarshalHelper.ReadAssetId();
        var result = ProfitDLL.SubscribeOfferBook(assetId.Ticker, assetId.Exchange);
        ConsoleHelper.WriteResult(result);
    }

    public static void SubscribePriceDepth()
    {
        var assetId = MarshalHelper.ReadAssetId();
        var result = ProfitDLL.SubscribePriceDepth(assetId);
        ConsoleHelper.WriteResult(result);
    }

    public static void UnsubscribePriceDepth()
    {
        var assetId = MarshalHelper.ReadAssetId();
        var result = ProfitDLL.UnsubscribePriceDepth(assetId);
        ConsoleHelper.WriteResult(result);
    }

    #endregion

    #region History and Orders

    public static void RequestHistory()
    {
        var assetId = MarshalHelper.ReadAssetId();

        ConsoleHelper.WriteSync("Data início (dd/MM/yyyy): ");
        var startDate = Console.ReadLine();

        ConsoleHelper.WriteSync("Data fim (dd/MM/yyyy): ");
        var endDate = Console.ReadLine();

        var result = ProfitDLL.GetHistoryTrades(assetId.Ticker, assetId.Exchange, startDate, endDate);
        ConsoleHelper.WriteResult(result);
    }

    public static void RequestOrder()
    {
        var accountId = MarshalHelper.ReadAccountId();

        var order = new TConnectorOrderOut
        {
            Version = 1,
            AccountID = new TConnectorAccountIdentifierOut
            {
                Version = accountId.Version,
                BrokerID = accountId.BrokerID,
                AccountID = "",
                AccountIDLength = 100,
                SubAccountID = "",
                SubAccountIDLength = 100
            },
            TextMessage = "",
            TextMessageLength = 255
        };

        ConsoleHelper.WriteSync("ClOrderID: ");
        order.OrderID.ClOrderID = Console.ReadLine() ?? "";

        var result = ProfitDLL.GetOrderDetails(ref order);

        if (result == NL_OK)
        {
            ConsoleHelper.WriteSync($"Order: {order.AssetID.Ticker} | Qty: {order.Quantity} | Price: {order.Price} | Status: {order.OrderStatus}");
        }
        else
        {
            ConsoleHelper.WriteSync($"Erro ao obter ordem: {(NResult)result}");
        }
    }

    #endregion

    #region Position

    public static void GetPosition()
    {
        var assetId = MarshalHelper.ReadAssetId();
        var accountId = MarshalHelper.ReadAccountId();

        ConsoleHelper.WriteSync("Tipo da posição (1 - day trade, 2 - consolidado): ");
        var input = Console.ReadLine();

        if (!byte.TryParse(input, out byte posType) || (posType != 1 && posType != 2))
        {
            ConsoleHelper.WriteSync("Tipo de posição inválido");
            return;
        }

        var positionType = (TConnectorPositionType)posType;

        var position = new TConnectorTradingAccountPosition
        {
            Version = 1,
            AssetID = assetId,
            AccountID = accountId,
            PositionType = positionType
        };

        var result = ProfitDLL.GetPositionV2(ref position);

        if (result == NL_OK)
        {
            ConsoleHelper.WriteSync($"Lado: {position.OpenSide} | Preço Médio: {position.OpenAveragePrice:N2} | Quantidade: {position.OpenQuantity}");
            ConsoleHelper.WriteSync($"Compra Dia: {position.DailyBuyQuantity} @ {position.DailyAverageBuyPrice:N2}");
            ConsoleHelper.WriteSync($"Venda Dia: {position.DailySellQuantity} @ {position.DailyAverageSellPrice:N2}");
        }
        else
        {
            ConsoleHelper.WriteSync($"Erro ao obter posição: {(NResult)result}");
        }
    }

    public static void GetPositionAssets()
    {
        bool EnumAssets(in TConnectorAssetIdentifier asset, nint param)
        {
            ConsoleHelper.WriteSync($"Asset: {asset.Ticker}");
            ConsoleHelper.WriteSync($"Exchange: {asset.Exchange}");
            ConsoleHelper.WriteSync($"FeedType: {asset.FeedType}");
            return true;
        }

        var accountId = MarshalHelper.ReadAccountId();

        ConsoleHelper.WriteSync($"Broker: {accountId.BrokerID}");
        ConsoleHelper.WriteSync($"ID: {accountId.AccountID}");
        ConsoleHelper.WriteSync($"SubID: {accountId.SubAccountID}");

        var result = ProfitDLL.EnumerateAllPositionAssets(ref accountId, 0, IntPtr.Zero, EnumAssets);
        ConsoleHelper.WriteResult(result);
    }

    #endregion

    #region Accounts

    public static void GetAccountsByBroker()
    {
        ConsoleHelper.WriteSync("Código da corretora: ");
        var input = Console.ReadLine();

        if (!int.TryParse(input, out int brokerId))
        {
            ConsoleHelper.WriteSync("Código inválido");
            return;
        }

        var count = ProfitDLL.GetAccountCountByBroker(brokerId);
        ConsoleHelper.WriteSync($"Total de contas: {count}");

        if (count <= 0) return;

        var accounts = new TConnectorAccountIdentifierOut[count];
        var result = ProfitDLL.GetAccountsByBroker(brokerId, 0, 0, count, accounts);

        if (result == NL_OK)
        {
            for (int i = 0; i < count; i++)
            {
                ConsoleHelper.WriteSync($"[{i}] Broker: {accounts[i].BrokerID} | Account: {accounts[i].AccountID.Trim()}");
            }
        }
    }

    public static string GetAgentName()
    {
        ConsoleHelper.WriteSync("Código do agente: ");
        var input = Console.ReadLine();

        if (!int.TryParse(input, out int agentId))
        {
            return "Código inválido";
        }

        var nameLength = ProfitDLL.GetAgentNameLength(agentId, 0);
        if (nameLength <= 0)
        {
            return $"Erro ao obter comprimento do nome: {nameLength}";
        }

        var nameBuilder = new System.Text.StringBuilder(nameLength + 1);
        var result = ProfitDLL.GetAgentName(nameLength, agentId, nameBuilder, 0);

        return result == NL_OK ? nameBuilder.ToString() : $"Erro: {(NResult)result}";
    }

    #endregion

    #region Server Clock

    public static void PrintServerClock()
    {
        double serverClock = 0.0;
        int year = 0, month = 0, day = 0, hour = 0, min = 0, sec = 0, mili = 0;

        ProfitDLL.GetServerClock(ref serverClock, ref year, ref month, ref day, ref hour, ref min, ref sec, ref mili);

        ConsoleHelper.WriteSync($"Server Clock: {hour:D2}:{min:D2}:{sec:D2}.{mili:D3}");
        ConsoleHelper.WriteSync($"Date: {day:D2}/{month:D2}/{year}");
    }

    #endregion
}
