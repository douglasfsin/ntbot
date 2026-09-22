using MarketData.API.Data.Model;
using StackExchange.Redis;
using System.Text.Json;

namespace MarketData.API.Data
{
    public class RedisMarketCache : IMarketCache
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisMarketCache(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task SetAsync(string ticker, PriceTick tick)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync($"ticker:{ticker}", JsonSerializer.Serialize(tick));
        }

        public async Task<PriceTick?> GetAsync(string ticker)
        {
            var db = _redis.GetDatabase();
            var data = await db.StringGetAsync($"ticker:{ticker}");
            return data.HasValue
                ? JsonSerializer.Deserialize<PriceTick>(data!)
                : null;
        }
    }
}
