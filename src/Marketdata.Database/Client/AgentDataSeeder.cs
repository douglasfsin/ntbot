using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Marketdata.Database.Client;

public class AgentDataSeeder
{
    private readonly ILogger<AgentDataSeeder> _logger;

    public AgentDataSeeder(ILogger<AgentDataSeeder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private record AgentMetadata(int Id, string NomeCompleto, string NomeAbreviado);

    public async Task SeedAgentesAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        // Dicionário inicial (amostra para expandir futuramente)
        var agentes = new List<AgentMetadata>
        {
            new(85, "XP INVESTIMENTOS CCTVM S/A", "XP"),
            new(27, "TULLETT PREBON BRASIL CVC S/A", "TULLETT"),
            new(16, "J.P. MORGAN CCVM S.A.", "JP MORGAN")
        };

        _logger.LogInformation("Iniciando processo de Seed para agentes_metadata...");

        try
        {
            // O PGWire (Npgsql) suporta a conexão padrão PostgreSQL na porta do QuestDB (8812)
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Criação da tabela (caso ainda não exista) com as colunas corretas para QuestDB
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS agents (
                    agent_id INT, 
                    full_name SYMBOL, 
                    short_name SYMBOL
                );";

            await using (var createCmd = new NpgsqlCommand(createTableSql, connection))
            {
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var agente in agentes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 1. Delete (workaround para atualizar registro estático no QuestDB via PGWire)
                var deleteSql = "DELETE FROM agents WHERE agent_id = @id;";
                await using (var deleteCmd = new NpgsqlCommand(deleteSql, connection))
                {
                    deleteCmd.Parameters.AddWithValue("id", agente.Id);
                    await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // 2. Insert
                var insertSql = "INSERT INTO agents (agent_id, full_name, short_name) VALUES (@id, @full_name, @short_name);";
                await using (var insertCmd = new NpgsqlCommand(insertSql, connection))
                {
                    insertCmd.Parameters.AddWithValue("id", agente.Id);
                    insertCmd.Parameters.AddWithValue("full_name", agente.NomeCompleto);
                    insertCmd.Parameters.AddWithValue("short_name", agente.NomeAbreviado);
                    await insertCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                _logger.LogInformation("Agente {Id} ({NomeAbreviado}) inserido/atualizado com sucesso no QuestDB.", agente.Id, agente.NomeAbreviado);
            }

            _logger.LogInformation("Processo de Seed finalizado. Total de agentes processados: {Total}", agentes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante a execução do Seed de agentes_metadata.");
            throw;
        }
    }
}
