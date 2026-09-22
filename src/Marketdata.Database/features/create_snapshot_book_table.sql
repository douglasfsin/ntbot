-- Criação da Tabela para Snapshot Completo do Book (sem limites de níveis)
-- Utilizamos particionamento por DIA (DAY) para manter alta performance de consulta temporal no QuestDB

CREATE TABLE snapshot_book (
    ticker SYMBOL,
    side SYMBOL,             -- 'Buy' ou 'Sell'
    position INT,            -- Posição na fila (1, 2, 3...)
    price DOUBLE,            -- Preço do nível
    quantity LONG,           -- Quantidade total no nível
    agent_count INT,         -- Número de agentes neste nível
    agent_details STRING,    -- Detalhamento das ordens/agentes (JSON)
    timestamp TIMESTAMP
) timestamp(timestamp) 
PARTITION BY DAY WAL;

-- Criação de índices para acelerar filtragem por Ticker e Side
ALTER TABLE snapshot_book ALTER COLUMN ticker ADD INDEX;
ALTER TABLE snapshot_book ALTER COLUMN side ADD INDEX;
