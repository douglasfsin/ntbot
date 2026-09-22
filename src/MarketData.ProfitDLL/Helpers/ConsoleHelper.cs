using System.Runtime.CompilerServices;

namespace ProfitDLLClient.Helpers;

/// <summary>
/// Helper para operações de console e sincronização
/// </summary>
public static class ConsoleHelper
{
    private static readonly object _writeLock = new();

    /// <summary>
    /// Escreve o resultado de uma operação no console
    /// </summary>
    public static void WriteResult(long result, [CallerMemberName] string callerName = "")
    {
        lock (_writeLock)
        {
            if (result <= 0)
            {
                Console.WriteLine($"{callerName}: {(NResult)result}");
            }
            else
            {
                Console.WriteLine($"{callerName}: {result}");
            }
        }
    }

    /// <summary>
    /// Escreve texto no console de forma thread-safe
    /// </summary>
    public static void WriteSync(string text)
    {
        lock (_writeLock)
        {
            Console.WriteLine(text);
        }
    }

    /// <summary>
    /// Lê senha do console de forma mascarada
    /// </summary>
    public static string ReadPassword()
    {
        Console.Write("Senha: ");

        var password = "";
        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            var key = keyInfo.Key;

            if (key == ConsoleKey.Enter)
            {
                break;
            }

            if (key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password[..^1];

                var (left, top) = Console.GetCursorPosition();
                Console.SetCursorPosition(left - 1, top);

                Console.Write(" ");
                Console.SetCursorPosition(left - 1, top);
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                Console.Write("*");
                password += keyInfo.KeyChar;
            }
        }

        Console.WriteLine();
        return password;
    }

    /// <summary>
    /// Lê entrada do usuário com prompt
    /// </summary>
    public static string ReadInput(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? "";
    }

    /// <summary>
    /// Exibe cabeçalho da aplicação
    /// </summary>
    public static void ShowHeader()
    {
        Console.Clear();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              ProfitDLL Client - Trading System                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    /// <summary>
    /// Exibe menu de comandos
    /// </summary>
    public static void ShowMenu()
    {
        Console.WriteLine("\n╔══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                        COMANDOS DISPONÍVEIS                      ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  subscribe               → Assinar ativo                         ║");
        Console.WriteLine("║  unsubscribe             → Desinscrever ativo                    ║");
        Console.WriteLine("║  pricebook               → Assinar price book                    ║");
        Console.WriteLine("║  offerbook               → Assinar offer book                    ║");
        Console.WriteLine("║  request history         → Solicitar histórico                   ║");
        Console.WriteLine("║  request order           → Solicitar ordem                       ║");
        Console.WriteLine("║  get position            → Obter posição                         ║");
        Console.WriteLine("║  get orders              → Obter ordens                          ║");
        Console.WriteLine("║  pos / posicoes          → Exibir posições abertas              ║");
        Console.WriteLine("║  strategies              → Listar estratégias                    ║");
        Console.WriteLine("║  menu                    → Exibir este menu                      ║");
        Console.WriteLine("║  exit                    → Sair                                  ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝\n");
    }
}
