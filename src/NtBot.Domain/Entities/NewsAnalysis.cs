namespace NtBot.Domain.Entities
{
    /// <summary>
    /// Análise de notícia com sentimento e impacto
    /// </summary>
    public class NewsAnalysis
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        
        // Análise de sentimento
        public decimal SentimentScore { get; set; } // -1 (bearish) to +1 (bullish)
        public SentimentType Sentiment { get; set; }
        
        // Impacto estimado
        public decimal ImpactScore { get; set; } // 0 to 100
        
        // Símbolos relacionados
        public string RelatedSymbols { get; set; } = "[]"; // JSON array: ["MNQ","NQ","ES"]
        
        // Conteúdo
        public string? Summary { get; set; }
        public string? Content { get; set; }
        
        // Entidades extraídas (NER)
        public string? Entities { get; set; } // JSON: {"companies": [...], "people": [...]}
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
    
    public enum SentimentType
    {
        VERY_BEARISH,
        BEARISH,
        NEUTRAL,
        BULLISH,
        VERY_BULLISH
    }
}
