using ProfitDLLClient.Core;
using ProfitDLLClient.Helpers;

namespace ProfitDLLClient;

/// <summary>
/// Programa principal - Refatorado e organizado
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        try
        {
            ConsoleHelper.ShowHeader();

            // Solicita credenciais
            Console.Write("Activation Key: ");
            var activationKey = Console.ReadLine() ?? "";

            Console.Write("Usuário: ");
            var user = Console.ReadLine() ?? "";

            var password = ConsoleHelper.ReadPassword();

            // Conecta à DLL
            if (!ConnectionManager.Initialize(activationKey, user, password))
            {
                Console.WriteLine("\nPressione qualquer tecla para sair...");
                Console.ReadKey();
                return;
            }

            // Aguarda conexão com o mercado
            ConnectionManager.WaitForMarketConnection();

            // Inicializa sistema de estratégias (opcional)
            try
            {
                StrategyIntegration.Initialize();
                StrategyIntegration.Start();
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteSync($"Aviso: Sistema de estratégias não iniciado: {ex.Message}");
            }

            // Exibe menu
            ConsoleHelper.ShowMenu();

            // Loop principal
            RunCommandLoop();

            // Desconecta
            ConnectionManager.Disconnect();
            StrategyIntegration.Stop();
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteSync($"Erro fatal: {ex.Message}");
            ConsoleHelper.WriteSync($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine("\nPressione qualquer tecla para sair...");
        Console.ReadKey();
    }

    /// <summary>
    /// Loop principal de comandos
    /// </summary>
    static void RunCommandLoop()
    {
        bool terminate = false;

        while (!terminate)
        {
            try
            {
                Console.Write("\nComando: ");
                var command = Console.ReadLine()?.Trim().ToLower() ?? "";

                if (string.IsNullOrEmpty(command))
                    continue;

                switch (command)
                {
                    // Assinaturas
                    case "subscribe":
                    case "assinar":
                        CommandHandler.SubscribeTicker();
                        break;

                    case "unsubscribe":
                    case "desinscrever":
                        CommandHandler.UnsubscribeTicker();
                        break;

                    case "pricebook":
                        CommandHandler.SubscribePriceBook();
                        break;

                    case "offerbook":
                        CommandHandler.SubscribeOfferBook();
                        break;

                    case "subscribe price depth":
                        CommandHandler.SubscribePriceDepth();
                        break;

                    case "unsubscribe price depth":
                        CommandHandler.UnsubscribePriceDepth();
                        break;

                    // Histórico e Ordens
                    case "request history":
                    case "historico":
                        CommandHandler.RequestHistory();
                        break;

                    case "request order":
                    case "ordem":
                        CommandHandler.RequestOrder();
                        break;

                    // Posições
                    case "get position":
                    case "posicao":
                        CommandHandler.GetPosition();
                        break;

                    case "get position asset":
                    case "ativos posicao":
                        CommandHandler.GetPositionAssets();
                        break;

                    // Contas
                    case "get accounts broker":
                    case "contas":
                        CommandHandler.GetAccountsByBroker();
                        break;

                    case "get agent name":
                    case "agente":
                        ConsoleHelper.WriteSync($"Nome do agente: {CommandHandler.GetAgentName()}");
                        break;

                    // Clock
                    case "clock":
                    case "hora":
                        CommandHandler.PrintServerClock();
                        break;

                    // Estratégias
                    case "pos":
                    case "posicoes":
                        StrategyIntegration.ShowPositions();
                        break;

                    case "strategies":
                    case "estrategias":
                        StrategyIntegration.ListStrategies();
                        break;

                    case "testpos":
                        TestPositions();
                        break;

                    // Utilidades
                    case "menu":
                    case "help":
                    case "ajuda":
                        ConsoleHelper.ShowMenu();
                        break;

                    case "clear":
                    case "cls":
                        Console.Clear();
                        ConsoleHelper.ShowHeader();
                        break;

                    case "exit":
                    case "sair":
                    case "quit":
                        terminate = true;
                        break;

                    default:
                        ConsoleHelper.WriteSync($"Comando '{command}' não reconhecido. Digite 'menu' para ver comandos disponíveis.");
                        break;
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteSync($"Erro ao executar comando: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Adiciona posições de teste para demonstração
    /// </summary>
    static void TestPositions()
    {
        ConsoleHelper.WriteSync("Adicionando posições de teste...");

        StrategyIntegration.AddTestPosition(
            "PETR4", "BOVESPA", "123456", 1,
            TConnectorOrderSide.Buy, 100, 38.50, 39.20
        );

        StrategyIntegration.AddTestPosition(
            "VALE3", "BOVESPA", "123456", 1,
            TConnectorOrderSide.Buy, 200, 68.20, 67.80
        );

        StrategyIntegration.AddTestPosition(
            "ITUB4", "BOVESPA", "123456", 1,
            TConnectorOrderSide.Sell, 150, 32.10, 31.85
        );

        StrategyIntegration.ShowPositions();
    }
}
