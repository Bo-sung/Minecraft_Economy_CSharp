using StackExchange.Redis;
using System.Text.Json;
using HarvestCraft2.Economy.API.Services.Interfaces;
using HarvestCraft2.Economy.API.Models;

namespace HarvestCraft2.Economy.API.Services
{
    /// <summary>
    /// Redis 캐시 서비스 구현체
    /// </summary>
    public class RedisService : IRedisService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly ILogger<RedisService> _logger;
        private readonly string _keyPrefix;

        public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger, IConfiguration configuration)
        {
            _redis = redis;
            _database = redis.GetDatabase();
            _logger = logger;
            _keyPrefix = configuration["RedisConfig:KeyPrefix"] ?? "hc2_economy:";
        }

        #region 연결 및 상태 관리

        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                await _database.PingAsync();
                return _redis.IsConnected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis connection check failed");
                return false;
            }
        }

        public async Task<string> GetServerInfoAsync()
        {
            try
            {
                var pingTime = await _database.PingAsync();
                var isConnected = _redis.IsConnected;
                var endpoint = _redis.GetEndPoints().FirstOrDefault()?.ToString() ?? "Unknown";

                return $"Redis Status: {(isConnected ? "Connected" : "Disconnected")}, " +
                       $"Ping: {pingTime.TotalMilliseconds:F2}ms, " +
                       $"Endpoint: {endpoint}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Redis server info");
                return $"Redis error: {ex.Message}";
            }
        }

        public async Task<long> GetKeyCountAsync(string pattern = "*")
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var keys = server.KeysAsync(pattern: _keyPrefix + pattern);

                long count = 0;
                await foreach (var key in keys)
                {
                    count++;
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to count keys with pattern {Pattern}", pattern);
                return 0;
            }
        }

        #endregion

        #region 기본 키-값 작업

        public async Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null)
        {
            try
            {
                var fullKey = _keyPrefix + key;
                return await _database.StringSetAsync(fullKey, value, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set string value for key {Key}", key);
                return false;
            }
        }

        public async Task<bool> SetObjectAsync<T>(string key, T obj, TimeSpan? expiry = null)
        {
            try
            {
                var fullKey = _keyPrefix + key;
                var json = JsonSerializer.Serialize(obj);
                return await _database.StringSetAsync(fullKey, json, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set object for key {Key}", key);
                return false;
            }
        }

        public async Task<T?> GetObjectAsync<T>(string key) where T : class
        {
            try
            {
                var fullKey = _keyPrefix + key;
                var value = await _database.StringGetAsync(fullKey);

                if (!value.HasValue)
                    return null;

                return JsonSerializer.Deserialize<T>(value.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get object for key {Key}", key);
                return null;
            }
        }

        public async Task<bool> KeyExistsAsync(string key)
        {
            try
            {
                var fullKey = _keyPrefix + key;
                return await _database.KeyExistsAsync(fullKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check key existence for {Key}", key);
                return false;
            }
        }

        public async Task<bool> DeleteKeyAsync(string key)
        {
            try
            {
                var fullKey = _keyPrefix + key;
                return await _database.KeyDeleteAsync(fullKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete key {Key}", key);
                return false;
            }
        }

        public async Task<long> DeleteKeysAsync(params string[] keys)
        {
            try
            {
                var fullKeys = keys.Select(k => (RedisKey)(_keyPrefix + k)).ToArray();
                return await _database.KeyDeleteAsync(fullKeys);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete multiple keys");
                return 0;
            }
        }

        public async Task<bool> SetExpiryAsync(string key, TimeSpan expiry)
        {
            try
            {
                var fullKey = _keyPrefix + key;
                return await _database.KeyExpireAsync(fullKey, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set expiry for key {Key}", key);
                return false;
            }
        }

        public async Task<TimeSpan?> GetTimeToLiveAsync(string key)
        {
            try
            {
                var fullKey = _keyPrefix + key;
                return await _database.KeyTimeToLiveAsync(fullKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get TTL for key {Key}", key);
                return null;
            }
        }

        #endregion

        #region 가격 관리

        public async Task<bool> SetCurrentPriceAsync(string itemId, decimal price, decimal basePrice)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.CurrentPrice, itemId);
                var priceData = new
                {
                    current = price,
                    @base = basePrice,
                    updated = DateTime.UtcNow.ToString("O")
                };

                return await SetObjectAsync(key, priceData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set current price for item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<(decimal currentPrice, decimal basePrice, DateTime lastUpdated)?> GetCurrentPriceAsync(string itemId)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.CurrentPrice, itemId);
                var priceJson = await GetStringAsync(key);

                if (string.IsNullOrEmpty(priceJson))
                    return null;

                using var doc = JsonDocument.Parse(priceJson);
                var root = doc.RootElement;

                var currentPrice = root.GetProperty("current").GetDecimal();
                var basePrice = root.GetProperty("base").GetDecimal();
                var lastUpdated = DateTime.Parse(root.GetProperty("updated").GetString()!);

                return (currentPrice, basePrice, lastUpdated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get current price for item {ItemId}", itemId);
                return null;
            }
        }

        public async Task<Dictionary<string, decimal>> GetAllCurrentPricesAsync()
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var pattern = _keyPrefix + string.Format(IRedisService.KeyPatterns.CurrentPrice, "*");
                var keys = server.KeysAsync(pattern: pattern);

                var prices = new Dictionary<string, decimal>();

                await foreach (var key in keys)
                {
                    try
                    {
                        var keyStr = key.ToString();
                        var itemId = ExtractItemIdFromPriceKey(keyStr);
                        var priceData = await GetCurrentPriceAsync(itemId);

                        if (priceData.HasValue)
                        {
                            prices[itemId] = priceData.Value.currentPrice;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get price for key {Key}", key);
                    }
                }

                return prices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all current prices");
                return new Dictionary<string, decimal>();
            }
        }

        public async Task<bool> SetPriceChangeAsync(string itemId, decimal changePercent)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.PriceChange, itemId);
                return await SetStringAsync(key, changePercent.ToString(), TimeSpan.FromMinutes(15));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set price change for item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<decimal?> GetPriceChangeAsync(string itemId)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.PriceChange, itemId);
                var value = await GetStringAsync(key);

                return decimal.TryParse(value, out var change) ? change : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get price change for item {ItemId}", itemId);
                return null;
            }
        }

        #endregion

        #region 거래량 추적

        public async Task<bool> IncrementTradeVolumeAsync(string itemId, TransactionType transactionType, int quantity, decimal weightedQuantity)
        {
            try
            {
                var timestamp = GetCurrentPeriodTimestamp();
                var key = string.Format(IRedisService.KeyPatterns.TradeVolume, itemId, timestamp);

                var fieldName = transactionType == TransactionType.BuyFromNpc ? "buy" : "sell";
                var weightedFieldName = transactionType == TransactionType.BuyFromNpc ? "weighted_buy" : "weighted_sell";

                var batch = _database.CreateBatch();
                var tasks = new List<Task>
                {
                    batch.HashIncrementAsync(_keyPrefix + key, fieldName, quantity),
                    batch.HashIncrementAsync(_keyPrefix + key, weightedFieldName, (double)weightedQuantity),
                    batch.KeyExpireAsync(_keyPrefix + key, TimeSpan.FromHours(1))
                };

                batch.Execute();
                await Task.WhenAll(tasks);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to increment trade volume for item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<(int buyVolume, int sellVolume, decimal weightedBuyVolume, decimal weightedSellVolume)> GetCurrentPeriodVolumeAsync(string itemId)
        {
            try
            {
                var timestamp = GetCurrentPeriodTimestamp();
                var key = string.Format(IRedisService.KeyPatterns.TradeVolume, itemId, timestamp);
                var fullKey = _keyPrefix + key;

                var hash = await _database.HashGetAllAsync(fullKey);
                var hashDict = hash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                var buyVolume = hashDict.TryGetValue("buy", out var buy) ? int.Parse(buy) : 0;
                var sellVolume = hashDict.TryGetValue("sell", out var sell) ? int.Parse(sell) : 0;
                var weightedBuyVolume = hashDict.TryGetValue("weighted_buy", out var wBuy) ? decimal.Parse(wBuy) : 0;
                var weightedSellVolume = hashDict.TryGetValue("weighted_sell", out var wSell) ? decimal.Parse(wSell) : 0;

                return (buyVolume, sellVolume, weightedBuyVolume, weightedSellVolume);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get current period volume for item {ItemId}", itemId);
                return (0, 0, 0, 0);
            }
        }

        public async Task<(int totalBuyVolume, int totalSellVolume, decimal totalWeightedBuyVolume, decimal totalWeightedSellVolume)> GetHourlyVolumeAsync(string itemId)
        {
            try
            {
                var now = DateTime.UtcNow;
                var periods = new List<string>();

                // 지난 1시간의 모든 10분 구간 생성
                for (int i = 0; i < 6; i++)
                {
                    var periodTime = now.AddMinutes(-i * 10);
                    periods.Add(GetPeriodTimestamp(periodTime));
                }

                int totalBuy = 0, totalSell = 0;
                decimal totalWeightedBuy = 0, totalWeightedSell = 0;

                foreach (var period in periods)
                {
                    var key = string.Format(IRedisService.KeyPatterns.TradeVolume, itemId, period);
                    var fullKey = _keyPrefix + key;

                    var hash = await _database.HashGetAllAsync(fullKey);
                    var hashDict = hash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

                    totalBuy += hashDict.TryGetValue("buy", out var buy) ? int.Parse(buy) : 0;
                    totalSell += hashDict.TryGetValue("sell", out var sell) ? int.Parse(sell) : 0;
                    totalWeightedBuy += hashDict.TryGetValue("weighted_buy", out var wBuy) ? decimal.Parse(wBuy) : 0;
                    totalWeightedSell += hashDict.TryGetValue("weighted_sell", out var wSell) ? decimal.Parse(wSell) : 0;
                }

                return (totalBuy, totalSell, totalWeightedBuy, totalWeightedSell);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get hourly volume for item {ItemId}", itemId);
                return (0, 0, 0, 0);
            }
        }

        public async Task<long> CleanupOldTradeDataAsync()
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var cutoffTime = DateTime.UtcNow.AddHours(-1);
                var cutoffTimestamp = GetPeriodTimestamp(cutoffTime);

                var pattern = _keyPrefix + "trades_10min:*";
                var keys = server.KeysAsync(pattern: pattern);

                long deletedCount = 0;

                await foreach (var key in keys)
                {
                    try
                    {
                        var keyStr = key.ToString();
                        var timestamp = ExtractTimestampFromTradeKey(keyStr);

                        if (string.Compare(timestamp, cutoffTimestamp, StringComparison.Ordinal) < 0)
                        {
                            if (await _database.KeyDeleteAsync(key))
                            {
                                deletedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process key {Key} during cleanup", key);
                    }
                }

                _logger.LogInformation("Cleaned up {Count} old trade data keys", deletedCount);
                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old trade data");
                return 0;
            }
        }

        #endregion

        #region 시장 압력 관리

        public async Task<bool> SetMarketPressureAsync(string itemId, decimal demandPressure, decimal supplyPressure)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.MarketPressure, itemId);
                var pressureData = new
                {
                    demand = demandPressure,
                    supply = supplyPressure,
                    updated = DateTime.UtcNow.ToString("O")
                };

                return await SetObjectAsync(key, pressureData, TimeSpan.FromMinutes(15));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set market pressure for item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<(decimal demandPressure, decimal supplyPressure, DateTime lastUpdated)?> GetMarketPressureAsync(string itemId)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.MarketPressure, itemId);
                var pressureJson = await GetStringAsync(key);

                if (string.IsNullOrEmpty(pressureJson))
                    return null;

                using var doc = JsonDocument.Parse(pressureJson);
                var root = doc.RootElement;

                var demandPressure = root.GetProperty("demand").GetDecimal();
                var supplyPressure = root.GetProperty("supply").GetDecimal();
                var lastUpdated = DateTime.Parse(root.GetProperty("updated").GetString()!);

                return (demandPressure, supplyPressure, lastUpdated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get market pressure for item {ItemId}", itemId);
                return null;
            }
        }

        public async Task<Dictionary<string, (decimal demand, decimal supply)>> GetAllMarketPressuresAsync()
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var pattern = _keyPrefix + string.Format(IRedisService.KeyPatterns.MarketPressure, "*");
                var keys = server.KeysAsync(pattern: pattern);

                var pressures = new Dictionary<string, (decimal demand, decimal supply)>();

                await foreach (var key in keys)
                {
                    try
                    {
                        var keyStr = key.ToString();
                        var itemId = ExtractItemIdFromPressureKey(keyStr);
                        var pressureData = await GetMarketPressureAsync(itemId);

                        if (pressureData.HasValue)
                        {
                            pressures[itemId] = (pressureData.Value.demandPressure, pressureData.Value.supplyPressure);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get pressure for key {Key}", key);
                    }
                }

                return pressures;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all market pressures");
                return new Dictionary<string, (decimal demand, decimal supply)>();
            }
        }

        #endregion

        #region 플레이어 세션 관리

        public async Task<bool> SetPlayerOnlineAsync(string playerId, string playerName)
        {
            try
            {
                var batch = _database.CreateBatch();
                var tasks = new List<Task>
                {
                    batch.SetAddAsync(_keyPrefix + IRedisService.KeyPatterns.OnlinePlayers, playerId),
                    batch.StringSetAsync(_keyPrefix + string.Format(IRedisService.KeyPatterns.PlayerSession, playerId),
                        JsonSerializer.Serialize(new { name = playerName, loginTime = DateTime.UtcNow }),
                        TimeSpan.FromHours(24))
                };

                batch.Execute();
                await Task.WhenAll(tasks);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set player online {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<bool> SetPlayerOfflineAsync(string playerId)
        {
            try
            {
                var batch = _database.CreateBatch();
                var tasks = new List<Task>
                {
                    batch.SetRemoveAsync(_keyPrefix + IRedisService.KeyPatterns.OnlinePlayers, playerId),
                    batch.KeyDeleteAsync(_keyPrefix + string.Format(IRedisService.KeyPatterns.PlayerSession, playerId))
                };

                batch.Execute();
                await Task.WhenAll(tasks);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set player offline {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<int> GetOnlinePlayerCountAsync()
        {
            try
            {
                var count = await _database.SetLengthAsync(_keyPrefix + IRedisService.KeyPatterns.OnlinePlayers);
                return (int)count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get online player count");
                return 0;
            }
        }

        public async Task<List<string>> GetOnlinePlayersAsync()
        {
            try
            {
                var players = await _database.SetMembersAsync(_keyPrefix + IRedisService.KeyPatterns.OnlinePlayers);
                return players.Select(p => p.ToString()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get online players");
                return new List<string>();
            }
        }

        public async Task<bool> SetPlayerSessionStartAsync(string playerId, DateTime startTime)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.PlayerSession, playerId);
                var sessionData = new { startTime = startTime.ToString("O") };
                return await SetObjectAsync(key, sessionData, TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set player session start {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<DateTime?> GetPlayerSessionStartAsync(string playerId)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.PlayerSession, playerId);
                var sessionJson = await GetStringAsync(key);

                if (string.IsNullOrEmpty(sessionJson))
                    return null;

                using var doc = JsonDocument.Parse(sessionJson);
                var startTimeStr = doc.RootElement.GetProperty("startTime").GetString();

                return DateTime.TryParse(startTimeStr, out var startTime) ? startTime : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get player session start {PlayerId}", playerId);
                return null;
            }
        }

        public async Task<bool> UpdatePlayerActivityAsync(string playerId)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.PlayerActivity, playerId);
                return await SetStringAsync(key, DateTime.UtcNow.ToString("O"), TimeSpan.FromMinutes(30));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update player activity {PlayerId}", playerId);
                return false;
            }
        }

        #endregion

        #region 시스템 설정 캐시

        public async Task<bool> CacheServerConfigAsync(string configKey, string configValue)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.ServerConfig, configKey);
                return await SetStringAsync(key, configValue, TimeSpan.FromHours(1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cache server config {ConfigKey}", configKey);
                return false;
            }
        }

        public async Task<string?> GetCachedServerConfigAsync(string configKey)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.ServerConfig, configKey);
                return await GetStringAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cached server config {ConfigKey}", configKey);
                return null;
            }
        }

        public async Task<bool> RefreshServerConfigCacheAsync(Dictionary<string, string> configs)
        {
            try
            {
                var batch = _database.CreateBatch();
                var tasks = configs.Select(kvp =>
                {
                    var key = string.Format(IRedisService.KeyPatterns.ServerConfig, kvp.Key);
                    return batch.StringSetAsync(_keyPrefix + key, kvp.Value, TimeSpan.FromHours(1));
                }).ToList();

                batch.Execute();
                await Task.WhenAll(tasks);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh server config cache");
                return false;
            }
        }

        #endregion

        #region 통계 및 모니터링

        public async Task<bool> SetSystemStatsAsync(string statsKey, object statsData)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.SystemStats, statsKey);
                return await SetObjectAsync(key, statsData, TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set system stats {StatsKey}", statsKey);
                return false;
            }
        }

        public async Task<T?> GetSystemStatsAsync<T>(string statsKey) where T : class
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.SystemStats, statsKey);
                return await GetObjectAsync<T>(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get system stats {StatsKey}", statsKey);
                return null;
            }
        }

        public async Task<bool> LogRecentActivityAsync(string itemId, string activityType, object activityData)
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.RecentActivity, itemId);
                var activity = new ActivityRecord
                {
                    Timestamp = DateTime.UtcNow,
                    ItemId = itemId,
                    ActivityType = activityType,
                    Data = activityData,
                    Description = $"{activityType} activity for {itemId}"
                };

                var fullKey = _keyPrefix + key;

                // 리스트의 앞쪽에 추가하고 100개로 제한
                var batch = _database.CreateBatch();
                var tasks = new List<Task>
                {
                    batch.ListLeftPushAsync(fullKey, JsonSerializer.Serialize(activity)),
                    batch.ListTrimAsync(fullKey, 0, 99), // 최대 100개 유지
                    batch.KeyExpireAsync(fullKey, TimeSpan.FromHours(24))
                };

                batch.Execute();
                await Task.WhenAll(tasks);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log recent activity for item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<List<T>> GetRecentActivitiesAsync<T>(string itemId, int count = 10) where T : class
        {
            try
            {
                var key = string.Format(IRedisService.KeyPatterns.RecentActivity, itemId);
                var fullKey = _keyPrefix + key;

                var activities = await _database.ListRangeAsync(fullKey, 0, count - 1);
                var result = new List<T>();

                foreach (var activity in activities)
                {
                    try
                    {
                        var obj = JsonSerializer.Deserialize<T>(activity.ToString());
                        if (obj != null)
                        {
                            result.Add(obj);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize activity record");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent activities for item {ItemId}", itemId);
                return new List<T>();
            }
        }

        #endregion

        #region 배치 작업

        public async Task<bool> BatchUpdatePricesAsync(Dictionary<string, (decimal currentPrice, decimal basePrice)> priceUpdates)
        {
            try
            {
                var batch = _database.CreateBatch();
                var tasks = priceUpdates.Select(kvp =>
                {
                    var key = string.Format(IRedisService.KeyPatterns.CurrentPrice, kvp.Key);
                    var priceData = new
                    {
                        current = kvp.Value.currentPrice,
                        @base = kvp.Value.basePrice,
                        updated = DateTime.UtcNow.ToString("O")
                    };
                    return batch.StringSetAsync(_keyPrefix + key, JsonSerializer.Serialize(priceData));
                }).ToList();

                batch.Execute();
                await Task.WhenAll(tasks);

                _logger.LogInformation("Batch updated {Count} prices", priceUpdates.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to batch update prices");
                return false;
            }
        }

        public async Task<bool> BatchUpdateTradeVolumesAsync(Dictionary<string, (int buyVolume, int sellVolume, decimal weightedBuyVolume, decimal weightedSellVolume)> volumeUpdates)
        {
            try
            {
                var timestamp = GetCurrentPeriodTimestamp();
                var batch = _database.CreateBatch();
                var tasks = new List<Task>();

                foreach (var kvp in volumeUpdates)
                {
                    var key = string.Format(IRedisService.KeyPatterns.TradeVolume, kvp.Key, timestamp);
                    var fullKey = _keyPrefix + key;

                    tasks.AddRange(new[]
                    {
                       batch.HashSetAsync(fullKey, "buy", kvp.Value.buyVolume),
                       batch.HashSetAsync(fullKey, "sell", kvp.Value.sellVolume),
                       batch.HashSetAsync(fullKey, "weighted_buy", (double)kvp.Value.weightedBuyVolume),
                       batch.HashSetAsync(fullKey, "weighted_sell", (double)kvp.Value.weightedSellVolume),
                       batch.KeyExpireAsync(fullKey, TimeSpan.FromHours(1))
                   });
                }

                batch.Execute();
                await Task.WhenAll(tasks);

                _logger.LogInformation("Batch updated {Count} trade volumes", volumeUpdates.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to batch update trade volumes");
                return false;
            }
        }

        public async Task<MarketSnapshot?> CreateMarketSnapshotAsync()
        {
            try
            {
                var timestamp = DateTime.UtcNow;
                var onlinePlayerCount = await GetOnlinePlayerCountAsync();
                var currentPrices = await GetAllCurrentPricesAsync();
                var marketPressures = await GetAllMarketPressuresAsync();

                // 거래량 데이터 수집
                var tradeVolumes = new Dictionary<string, (int buyVolume, int sellVolume)>();
                var highVolatilityItems = new List<string>();
                var highActivityItems = new List<string>();

                foreach (var itemId in currentPrices.Keys)
                {
                    var volume = await GetCurrentPeriodVolumeAsync(itemId);
                    tradeVolumes[itemId] = (volume.buyVolume, volume.sellVolume);

                    // 고변동성 아이템 식별 (압력이 높은 경우)
                    if (marketPressures.TryGetValue(itemId, out var pressure))
                    {
                        if (Math.Abs(pressure.demand - pressure.supply) > 0.3m)
                        {
                            highVolatilityItems.Add(itemId);
                        }
                    }

                    // 고활성 아이템 식별 (거래량이 많은 경우)
                    if (volume.buyVolume + volume.sellVolume > 50)
                    {
                        highActivityItems.Add(itemId);
                    }
                }

                // double에서 decimal로 명시적 변환
                var totalActivity = tradeVolumes.Values.Select(v => v.buyVolume + v.sellVolume).DefaultIfEmpty(0).Average();

                var snapshot = new MarketSnapshot
                {
                    Timestamp = timestamp,
                    OnlinePlayerCount = onlinePlayerCount,
                    CurrentPrices = currentPrices,
                    MarketPressures = marketPressures,
                    TradeVolumes = tradeVolumes,
                    TotalActiveItems = currentPrices.Count,
                    AverageMarketActivity = (decimal)totalActivity, // double을 decimal로 명시적 변환
                    HighVolatilityItems = highVolatilityItems,
                    HighActivityItems = highActivityItems
                };

                // 스냅샷 저장
                var snapshotKey = string.Format(IRedisService.KeyPatterns.MarketSnapshot, timestamp.ToString("yyyyMMddTHHmm"));
                await SetObjectAsync(snapshotKey, snapshot, TimeSpan.FromHours(24));

                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create market snapshot");
                return null;
            }
        }

        #endregion

        // ============================================================================
        // 9. 기본 Redis 데이터 타입 메소드 구현
        // ============================================================================

        public async Task<RedisValue?> GetHashFieldAsync(string key, string field)
        {
            try
            {
                var value = await _database.HashGetAsync(key, field);

                if (value.HasValue)
                {
                    _logger.LogDebug("Hash 필드 조회 성공: {Key}:{Field} = {Value}", key, field, value);
                    return value;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hash 필드 조회 실패: {Key}:{Field}", key, field);
                return null;
            }
        }

        public async Task<Dictionary<string, RedisValue>> GetHashFieldsAsync(string key, params string[] fields)
        {
            var result = new Dictionary<string, RedisValue>();

            try
            {
                var values = await _database.HashGetAsync(key, fields.Select(f => (RedisValue)f).ToArray());

                for (int i = 0; i < fields.Length; i++)
                {
                    if (values[i].HasValue)
                    {
                        result[fields[i]] = values[i];
                    }
                }

                _logger.LogDebug("Hash 다중 필드 조회: {Key} - {Count}개 필드", key, result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hash 다중 필드 조회 실패: {Key}", key);
                return result;
            }
        }

        public async Task<bool> SetHashFieldAsync(string key, string field, RedisValue value)
        {
            try
            {
                var success = await _database.HashSetAsync(key, field, value);

                _logger.LogDebug("Hash 필드 설정: {Key}:{Field} = {Value}", key, field, value);
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hash 필드 설정 실패: {Key}:{Field}", key, field);
                return false;
            }
        }

        public async Task<RedisValue[]> GetSetMembersAsync(string key)
        {
            try
            {
                var members = await _database.SetMembersAsync(key);

                _logger.LogDebug("Set 멤버 조회: {Key} - {Count}개", key, members.Length);
                return members;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Set 멤버 조회 실패: {Key}", key);
                return Array.Empty<RedisValue>();
            }
        }

        public async Task<bool> AddToSetAsync(string key, RedisValue member)
        {
            try
            {
                var added = await _database.SetAddAsync(key, member);

                _logger.LogDebug("Set 멤버 추가: {Key} + {Member}", key, member);
                return added;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Set 멤버 추가 실패: {Key}", key);
                return false;
            }
        }

        public async Task<bool> SetStringAsync(string key, RedisValue value, TimeSpan? expiry = null)
        {
            try
            {
                var success = await _database.StringSetAsync(key, value, expiry);

                _logger.LogDebug("String 설정: {Key} = {Value} (TTL: {Expiry})", key, value, expiry?.ToString() ?? "무제한");
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "String 설정 실패: {Key}", key);
                return false;
            }
        }

        public async Task<string?> GetStringAsync(string key)
        {
            try
            {
                var value = await _database.StringGetAsync(key);

                if (value.HasValue)
                {
                    _logger.LogDebug("String 조회 성공: {Key} = {Value}", key, value);
                    return value;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "String 조회 실패: {Key}", key);
                return null;
            }
        }

        #region 헬퍼 메소드

        /// <summary>
        /// 현재 10분 구간의 타임스탬프 생성 (예: 202506091530)
        /// </summary>
        private string GetCurrentPeriodTimestamp()
        {
            return GetPeriodTimestamp(DateTime.UtcNow);
        }

        /// <summary>
        /// 지정된 시간의 10분 구간 타임스탬프 생성
        /// </summary>
        private string GetPeriodTimestamp(DateTime dateTime)
        {
            var roundedMinutes = (dateTime.Minute / 10) * 10;
            var roundedTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, roundedMinutes, 0);
            return roundedTime.ToString("yyyyMMddHHmm");
        }

        /// <summary>
        /// 가격 키에서 아이템 ID 추출
        /// </summary>
        private string ExtractItemIdFromPriceKey(string key)
        {
            // key format: hc2_economy:price:minecraft:wheat
            var prefix = _keyPrefix + "price:";
            return key.StartsWith(prefix) ? key.Substring(prefix.Length) : string.Empty;
        }

        /// <summary>
        /// 압력 키에서 아이템 ID 추출
        /// </summary>
        private string ExtractItemIdFromPressureKey(string key)
        {
            // key format: hc2_economy:pressure:minecraft:wheat
            var prefix = _keyPrefix + "pressure:";
            return key.StartsWith(prefix) ? key.Substring(prefix.Length) : string.Empty;
        }

        /// <summary>
        /// 거래량 키에서 타임스탬프 추출
        /// </summary>
        private string ExtractTimestampFromTradeKey(string key)
        {
            // key format: hc2_economy:trades_10min:minecraft:wheat:202506091530
            var parts = key.Split(':');
            return parts.Length >= 5 ? parts[^1] : string.Empty;
        }

        #endregion
    }
}