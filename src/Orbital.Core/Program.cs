using System;
using System.Threading;
using System.Threading.Tasks;
using Marketdata.Database.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orbital.Core.Services;
using Orbital.Core.Models;

namespace Orbital.Core;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=====================================================");
        Console.WriteLine(" Orbital.Core - Batch Updater & Monitor de Tape Speed");
        Console.WriteLine("=====================================================\n");

        var connectionString = Environment.GetEnvironmentVariable("QUESTDB_CONNECTION") 
            ?? "Host=localhost;Port=8812;Database=qdb;Username=admin;Password=quest;Server Compatibility Mode=NoTypeLoading;";

        Console.WriteLine($"Conectando ao QuestDB em: {connectionString}\n");

        // 1. Executa o Seeder de Estudos SMC primeiro
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information);
        });

        var smcLogger = loggerFactory.CreateLogger<SMCDataSeeder>();
        var seeder = new SMCDataSeeder(smcLogger);

        try
        {
            Console.WriteLine("Iniciando injeção de pontos de estudo (SMC)...");
            await seeder.SeedPointsOfInterestAsync(connectionString);
            Console.WriteLine("[SUCESSO] Estudos de SMC semeados com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO] Falha ao semear estudos: {ex.Message}");
        }

        Console.WriteLine("\n-----------------------------------------------------");
        Console.WriteLine("Iniciando Modo de Demonstração Live de Tape Speed...");
        Console.WriteLine("-----------------------------------------------------\n");

        // 2. Cria o Container IoC (DI) para testar os novos Serviços de TapeSpeed
        var services = new ServiceCollection();
        
        // Loggers
        services.AddLogging(builder => 
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning); // Silencia os logs internos do DbClient para não poluir
        });

        // Registra Orbital.Core
        services.AddOrbitalCore(connectionString);

        var serviceProvider = services.BuildServiceProvider();

        // 3. Obtém o Engine
        var tapeSpeedEngine = serviceProvider.GetRequiredService<TapeSpeedEngine>();

        // 4. Assina o evento para printar a análise do motor no Console
        tapeSpeedEngine.OnAnalysisUpdated += (analysis) =>
        {
            Console.Clear();
            Console.WriteLine("=====================================================");
            Console.WriteLine("           MONITOR EM TEMPO REAL: TAPE SPEED         ");
            Console.WriteLine("=====================================================");
            Console.WriteLine($"Ativo: {analysis.Ticker} | Hora: {analysis.CalculatedAt:HH:mm:ss.fff}");
            Console.WriteLine("-----------------------------------------------------");

            // Pergunta 1: Apetite do Mercado
            string appetiteSymbol = analysis.CurrentAppetite == Appetite.Buy ? "🟢 COMPRA" : 
                                   analysis.CurrentAppetite == Appetite.Sell ? "🔴 VENDA" : "⚪ NEUTRO";
            Console.WriteLine($"1. Apetite do Mercado (Urgência):   {appetiteSymbol}");

            // Pergunta 2: Estado de Agitação
            string stateSymbol = analysis.State == MarketState.Agitated ? "🔥 AGITADO (Forte Fluxo)" : "❄️ CALMO";
            Console.WriteLine($"2. Estado do Mercado:                {stateSymbol}");

            // Pergunta 3: Médias
            Console.WriteLine($"3. Frequência de Negócios:           {analysis.AvgTradesPerSecond60s:N2} trades/seg");
            Console.WriteLine($"   Média de Contratos (Baseline 60s): {analysis.AvgContractsPerTrade60s:N1} contratos/trade");
            Console.WriteLine($"   Média de Contratos (Imediata 5s):   {analysis.AvgContractsPerTrade5s:N1} contratos/trade");

            // Pergunta 4: Comparativo
            string levelSymbol = analysis.IsAboveAverage ? "▲ ACIMA DA MÉDIA (Institucionais Ativos)" : "▼ ABAIXO DA MÉDIA (Varejo/Robôs)";
            Console.WriteLine($"4. Volume por Negócio Atual:         {levelSymbol}");
            Console.WriteLine("=====================================================");
            Console.WriteLine("Aguardando próxima leitura (a cada 1 segundo)...");
            Console.WriteLine("Pressione Ctrl+C para encerrar.");
        };

        // 5. Inicia o BackgroundService de Tape Speed
        var cts = new CancellationTokenSource();
        var engineTask = tapeSpeedEngine.StartAsync(cts.Token);

        // Deixa rodando em demonstração por 15 segundos ou até cancelar com Ctrl+C
        try
        {
            await Task.Delay(15000, cts.Token);
        }
        catch (TaskCanceledException) { }

        // Finaliza graciosamente
        cts.Cancel();
        await engineTask;

        Console.WriteLine("\n[FINALIZADO] Monitoramento de demonstração encerrado.");
    }
}
