Na arquitetura da ProfitDLL, as informações de um negócio executado chegam através do callback de Trades (geralmente associado à assinatura do SetTradeCallback ou SetNewTradeCallback).

Quando um trade ocorre na B3, a DLL disponibiliza uma estrutura de dados robusta via ponteiro (como a TConnectorTrade ou estruturas equivalentes mapeadas em C#).

Os dados fundamentais disponibilizados pela DLL a cada trade executado são:

1. Dados de Identificação e Tempo
Ativo (pszTicker / szAsset): O código do ativo em que o negócio ocorreu (ex: WDOFUT, WING26).

Data (szDate / tDate): A data da execução do negócio.

Horário/Timestamp (szTime / tTime ou nTimestamp): O horário exato da execução com precisão de milissegundos. No ecossistema Orbital, esse dado é crítico para ordenar os eventos e calcular as velocidades de agressão de 1 segundo de forma fidedigna antes do envio ao marketData.database.

2. Dados de Negócio (Preço e Volume)
Preço (dPrice): O preço exato em que o lote foi fechado (ex: 5052.50 para o dólar).

Quantidade (nQtd): O número de contratos ou ações negociados naquele trade específico.

Volume Financeiro (dVol): O valor financeiro totalizado daquela transação (Preço × Quantidade × Multiplicador do Contrato).

3. Dados de Fluxo e Dinâmica de Mercado
Tipo de Trade (nTradeType / nAgressionType): É o indicador que mapeia a natureza da operação (conforme vimos na tabela anterior: Compra Agressão, Venda Agressão, Direto/Cross, Leilão, RLP, etc.).

Identificador do Trade (nTradeId / nSequence): O número sequencial único gerado pela bolsa para aquele negócio. Essencial para o marketData.database evitar a duplicação de registros ao salvar o stream.

4. Dados de Agentes (Disponibilidade Variável)
Código da Corretora Compradora (nBidAgent / nBuyerAgent): O número de identificação da corretora que intermediou a ponta de compra (ex: 85 para XP, 27 para Tullett).

Código da Corretora Vendedora (nAskAgent / nSellerAgent): O número de identificação da corretora que intermediou a ponta de venda.


Como esses dados se encaixam no Orbital.Core
Para o seu planejamento, o projeto Orbital.Core receberá esses dados brutos da DLL estruturados em uma struct de Interop (C++) para C# semelhante a esta:

Exemplo de dados: 
C#
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct TConnectorTrade
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szAsset;
    
    public double dPrice;
    public double dVol;
    public int nQtd;
    public int nTradeType; // Mapeia as agressões e leilões
    
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szTime;
    
    public long nTradeId;      // Identificador sequencial da B3
    public int nBuyerAgent;    // Player Comprador
    public int nSellerAgent;   // Player Vendedor
}