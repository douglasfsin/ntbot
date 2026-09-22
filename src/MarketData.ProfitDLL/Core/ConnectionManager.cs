using ProfitDLLClient.Core;
using ProfitDLLClient.Helpers;

namespace ProfitDLLClient;

/// <summary>
/// Gerencia a conexão com a DLL Profit
/// </summary>
public static class ConnectionManager
{
    private const int NL_OK = 0;

    /// <summary>
    /// Inicializa e conecta à DLL
    /// </summary>
    public static bool Initialize(string activationKey, string user, string password)
    {
        ConsoleHelper.WriteSync("Iniciando conexão...");

        int result = ProfitDLL.DLLInitializeLogin(
            activationKey,
            user,
            password,
            CallbackManager.StateCallback,
            null,  // historyCallBack
            null,  // orderChangeCallBack
            CallbackManager.AccountCallback,
            null,  // newTradeCallback
            null,  // NewDailyCallback - disabled due to signature mismatch
            null,  // priceBookCallback
            null,  // OfferBookCallbackV2 - disabled due to signature mismatch
            null,  // newHistoryCallback
            null,  // progressCallBack
            null   // NewTinyBookCallBack - disabled due to signature mismatch
        );

        if (result != NL_OK)
        {
            ConsoleHelper.WriteSync($"Erro na inicialização: {(NResult)result}");
            return false;
        }

        // Registra callbacks adicionais
        CallbackManager.RegisterCallbacks();

        ConsoleHelper.WriteSync("Conexão estabelecida com sucesso!");
        return true;
    }

    /// <summary>
    /// Aguarda até que o mercado esteja conectado
    /// </summary>
    public static void WaitForMarketConnection(int timeoutSeconds = 30)
    {
        ConsoleHelper.WriteSync("Aguardando conexão com o mercado...");

        var startTime = DateTime.Now;
        while (!DataStore.IsMarketConnected)
        {
            System.Threading.Thread.Sleep(1000);

            if ((DateTime.Now - startTime).TotalSeconds > timeoutSeconds)
            {
                ConsoleHelper.WriteSync("Timeout aguardando conexão com o mercado");
                break;
            }
        }

        if (DataStore.IsMarketConnected)
        {
            ConsoleHelper.WriteSync("Conectado ao mercado!");
        }
    }

    /// <summary>
    /// Desconecta da DLL
    /// </summary>
    public static void Disconnect()
    {
        ConsoleHelper.WriteSync("Desconectando...");
        DataStore.Clear();
    }
}
