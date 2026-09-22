-- Criação da Tabela Otimizada para Price Depth (Book de Ofertas)
-- Utilizamos particionamento por DIA (DAY) para manter alta performance de consulta temporal no QuestDB

CREATE TABLE price_depth (
    ticker SYMBOL,
    side SYMBOL,             -- 'Buy' ou 'Sell'
    position INT,            -- Posição na fila (1, 2, 3...)
    price DOUBLE,            -- Preço do nível
    quantity LONG,           -- Quantidade total no nível
    agent_count INT,         -- Número de agentes neste nível
    agent_details STRING,    -- Pode armazenar JSON com o detalhamento das ordens/agentes
    timestamp TIMESTAMP
) timestamp(timestamp) 
PARTITION BY DAY WAL;

-- Criação de índices para acelerar filtragem por Ticker e Side
ALTER TABLE price_depth ALTER COLUMN ticker ADD INDEX;
ALTER TABLE price_depth ALTER COLUMN side ADD INDEX;
