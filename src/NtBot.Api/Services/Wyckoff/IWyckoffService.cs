using NtBot.Domain.Entities;

namespace NtBot.Api.Services.Wyckoff
{
    /// <summary>
    /// Interface do motor de análise Wyckoff
    /// </summary>
    public interface IWyckoffService
    {
        /// <summary>
        /// Analisa uma série de candles e detecta fase/evento Wyckoff
        /// </summary>
        Task<WyckoffAnalysisResult> AnalyzeAsync(string symbol, string timeframe, List<Candle> candles);
        
        /// <summary>
        /// Detecta Spring (falso rompimento de suporte com rejeição)
        /// </summary>
        Task<(bool detected, decimal confidence)> DetectSpringAsync(List<Candle> candles);
        
        /// <summary>
        /// Detecta Upthrust (falso rompimento de resistência com rejeição)
        /// </summary>
        Task<(bool detected, decimal confidence)> DetectUpthrustAsync(List<Candle> candles);
        
        /// <summary>
        /// Identifica range de acumulação/distribuição
        /// </summary>
        Task<(bool isRange, decimal high, decimal low, int candles)> IdentifyRangeAsync(List<Candle> candles);
        
        /// <summary>
        /// Analisa divergência de volume
        /// </summary>
        Task<bool> IsVolumeDivergentAsync(List<Candle> candles);
        
        /// <summary>
        /// Identifica níveis estruturais (suporte/resistência)
        /// </summary>
        Task<List<StructureLevel>> IdentifyStructureLevelsAsync(List<Candle> candles);
    }
}
