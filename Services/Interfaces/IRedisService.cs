using HarvestCraft2.Economy.API.Models;
using StackExchange.Redis;

namespace HarvestCraft2.Economy.API.Services.Interfaces
{
    /// <summary>
    /// Redis 캐시 서비스 인터페이스
    /// </summary>
    public interface IRedisService
    {
        #region 연결 및 상태 관리

        /// <summary>
        /// Redis 연결 상태 확인
        /// </summary>
        Task<bool> IsConnectedAsync();

        /// <summary>
        /// Redis 서버 정보 조회
        /// </summary>
        Task<string> GetServerInfoAsync();

        /// <summary>
        /// 특정 패턴의 키 개수 조회
        /// </summary>
        Task<long> GetKeyCountAsync(string pattern = "*");

        #endregion

        #region 기본 키-값 작업

        /// <summary>
        /// 문자열 값 저장
        /// </summary>
        Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null);

        /// <summary>
        /// 문자열 값 조회
        /// </summary>
        Task<string?> GetStringAsync(string key);

        /// <summary>
        /// 객체를 JSON으로 직렬화하여 저장
        /// </summary>
        Task<bool> SetObjectAsync<T>(string key, T obj, TimeSpan? expiry = null);

        /// <summary>
        /// JSON에서 객체로 역직렬화하여 조회
        /// </summary>
        Task<T?> GetObjectAsync<T>(string key) where T : class;

        /// <summary>
        /// 키 존재 여부 확인
        /// </summary>
        Task<bool> KeyExistsAsync(string key);

        /// <summary>
        /// 키 삭제
        /// </summary>
        Task<bool> DeleteKeyAsync(string key);

        /// <summary>
        /// 여러 키 일괄 삭제
        /// </summary>
        Task<long> DeleteKeysAsync(params string[] keys);

        /// <summary>
        /// 키의 만료 시간 설정
        /// </summary>
        Task<bool> SetExpiryAsync(string key, TimeSpan expiry);

        /// <summary>
        /// 키의 남은 만료 시간 조회
        /// </summary>
        Task<TimeSpan?> GetTimeToLiveAsync(string key);

        #endregion

        #region 가격 관리

        /// <summary>
        /// 현재 가격 정보 저장
        /// </summary>
        Task<bool> SetCurrentPriceAsync(string itemId, decimal price, decimal basePrice);

        /// <summary>
        /// 현재 가격 정보 조회
        /// </summary>
        Task<(decimal currentPrice, decimal basePrice, DateTime lastUpdated)?> GetCurrentPriceAsync(string itemId);

        /// <summary>
        /// 모든 아이템의 현재 가격 조회
        /// </summary>
        Task<Dictionary<string, decimal>> GetAllCurrentPricesAsync();

        /// <summary>
        /// 가격 변동률 계산 및 저장
        /// </summary>
        Task<bool> SetPriceChangeAsync(string itemId, decimal changePercent);

        /// <summary>
        /// 가격 변동률 조회
        /// </summary>
        Task<decimal?> GetPriceChangeAsync(string itemId);

        #endregion

        #region 거래량 추적

        /// <summary>
        /// 10분 구간별 거래량 증가
        /// </summary>
        Task<bool> IncrementTradeVolumeAsync(string itemId, TransactionType transactionType, int quantity, decimal weightedQuantity);

        /// <summary>
        /// 현재 10분 구간의 거래량 조회
        /// </summary>
        Task<(int buyVolume, int sellVolume, decimal weightedBuyVolume, decimal weightedSellVolume)> GetCurrentPeriodVolumeAsync(string itemId);

        /// <summary>
        /// 지난 1시간 총 거래량 조회
        /// </summary>
        Task<(int totalBuyVolume, int totalSellVolume, decimal totalWeightedBuyVolume, decimal totalWeightedSellVolume)> GetHourlyVolumeAsync(string itemId);

        /// <summary>
        /// 거래량 데이터 정리 (1시간 이상 된 데이터 삭제)
        /// </summary>
        Task<long> CleanupOldTradeDataAsync();

        #endregion

        #region 시장 압력 관리

        /// <summary>
        /// 시장 압력 정보 저장
        /// </summary>
        Task<bool> SetMarketPressureAsync(string itemId, decimal demandPressure, decimal supplyPressure);

        /// <summary>
        /// 시장 압력 정보 조회
        /// </summary>
        Task<(decimal demandPressure, decimal supplyPressure, DateTime lastUpdated)?> GetMarketPressureAsync(string itemId);

        /// <summary>
        /// 모든 아이템의 시장 압력 조회
        /// </summary>
        Task<Dictionary<string, (decimal demand, decimal supply)>> GetAllMarketPressuresAsync();

        #endregion

        #region 플레이어 세션 관리

        /// <summary>
        /// 플레이어 온라인 상태 설정
        /// </summary>
        Task<bool> SetPlayerOnlineAsync(string playerId, string playerName);

        /// <summary>
        /// 플레이어 오프라인 상태 설정
        /// </summary>
        Task<bool> SetPlayerOfflineAsync(string playerId);

        /// <summary>
        /// 현재 온라인 플레이어 수 조회
        /// </summary>
        Task<int> GetOnlinePlayerCountAsync();

        /// <summary>
        /// 온라인 플레이어 목록 조회
        /// </summary>
        Task<List<string>> GetOnlinePlayersAsync();

        /// <summary>
        /// 플레이어 세션 시작 시간 저장
        /// </summary>
        Task<bool> SetPlayerSessionStartAsync(string playerId, DateTime startTime);

        /// <summary>
        /// 플레이어 세션 시작 시간 조회
        /// </summary>
        Task<DateTime?> GetPlayerSessionStartAsync(string playerId);

        /// <summary>
        /// 플레이어 마지막 활동 시간 업데이트
        /// </summary>
        Task<bool> UpdatePlayerActivityAsync(string playerId);

        #endregion

        #region 시스템 설정 캐시

        /// <summary>
        /// 서버 설정 캐시 저장
        /// </summary>
        Task<bool> CacheServerConfigAsync(string configKey, string configValue);

        /// <summary>
        /// 서버 설정 캐시 조회
        /// </summary>
        Task<string?> GetCachedServerConfigAsync(string configKey);

        /// <summary>
        /// 모든 서버 설정 캐시 새로고침
        /// </summary>
        Task<bool> RefreshServerConfigCacheAsync(Dictionary<string, string> configs);

        #endregion

        #region 통계 및 모니터링

        /// <summary>
        /// 시스템 통계 정보 저장
        /// </summary>
        Task<bool> SetSystemStatsAsync(string statsKey, object statsData);

        /// <summary>
        /// 시스템 통계 정보 조회
        /// </summary>
        Task<T?> GetSystemStatsAsync<T>(string statsKey) where T : class;

        /// <summary>
        /// 최근 거래 활동 기록
        /// </summary>
        Task<bool> LogRecentActivityAsync(string itemId, string activityType, object activityData);

        /// <summary>
        /// 최근 활동 기록 조회 (최대 100개)
        /// </summary>
        Task<List<T>> GetRecentActivitiesAsync<T>(string itemId, int count = 10) where T : class;

        #endregion

        #region 배치 작업

        /// <summary>
        /// 여러 가격을 한 번에 업데이트
        /// </summary>
        Task<bool> BatchUpdatePricesAsync(Dictionary<string, (decimal currentPrice, decimal basePrice)> priceUpdates);

        /// <summary>
        /// 여러 거래량을 한 번에 업데이트
        /// </summary>
        Task<bool> BatchUpdateTradeVolumesAsync(Dictionary<string, (int buyVolume, int sellVolume, decimal weightedBuyVolume, decimal weightedSellVolume)> volumeUpdates);

        /// <summary>
        /// 모든 시장 데이터 스냅샷 생성
        /// </summary>
        Task<MarketSnapshot?> CreateMarketSnapshotAsync();

        #endregion

        // ============================================================================
        // 9. 기본 Redis 데이터 타입 메소드 (Hash, Set, String 확장)
        // ============================================================================

        /// <summary>
        /// Hash 필드 값을 조회합니다.
        /// </summary>
        /// <param name="key">Hash 키</param>
        /// <param name="field">필드명</param>
        /// <returns>필드 값 (없으면 null)</returns>
        Task<RedisValue?> GetHashFieldAsync(string key, string field);

        /// <summary>
        /// Hash 여러 필드를 한번에 조회합니다.
        /// </summary>
        /// <param name="key">Hash 키</param>
        /// <param name="fields">필드명 배열</param>
        /// <returns>필드별 값 딕셔너리</returns>
        Task<Dictionary<string, RedisValue>> GetHashFieldsAsync(string key, params string[] fields);

        /// <summary>
        /// Hash 필드를 설정합니다.
        /// </summary>
        /// <param name="key">Hash 키</param>
        /// <param name="field">필드명</param>
        /// <param name="value">값</param>
        /// <returns>성공 여부</returns>
        Task<bool> SetHashFieldAsync(string key, string field, RedisValue value);

        /// <summary>
        /// Set의 모든 멤버를 조회합니다.
        /// </summary>
        /// <param name="key">Set 키</param>
        /// <returns>Set 멤버 목록</returns>
        Task<RedisValue[]> GetSetMembersAsync(string key);

        /// <summary>
        /// Set에 멤버를 추가합니다.
        /// </summary>
        /// <param name="key">Set 키</param>
        /// <param name="member">추가할 멤버</param>
        /// <returns>성공 여부</returns>
        Task<bool> AddToSetAsync(string key, RedisValue member);

        /// <summary>
        /// String 값을 설정합니다. (TTL 포함)
        /// </summary>
        /// <param name="key">키</param>
        /// <param name="value">값</param>
        /// <param name="expiry">만료 시간 (선택적)</param>
        /// <returns>성공 여부</returns>
        Task<bool> SetStringAsync(string key, RedisValue value, TimeSpan? expiry = null);

        #region 키 패턴 상수

        /// <summary>
        /// Redis 키 패턴 상수들
        /// </summary>
        public static class KeyPatterns
        {
            public const string CurrentPrice = "price:{0}";                    // price:minecraft:wheat
            public const string PriceChange = "price_change:{0}";              // price_change:minecraft:wheat
            public const string MarketPressure = "pressure:{0}";               // pressure:minecraft:wheat
            public const string TradeVolume = "trades_10min:{0}:{1}";          // trades_10min:minecraft:wheat:202506091500
            public const string OnlinePlayers = "online_players";              // online_players
            public const string PlayerSession = "session:{0}";                 // session:uuid
            public const string PlayerActivity = "activity:{0}";               // activity:uuid
            public const string ServerConfig = "config:{0}";                   // config:base_online_players
            public const string SystemStats = "stats:{0}";                     // stats:daily_summary
            public const string RecentActivity = "recent:{0}";                 // recent:minecraft:wheat
            public const string MarketSnapshot = "snapshot:{0}";               // snapshot:20250609T1500
        }

        #endregion
    }

    /// <summary>
    /// 시장 전체 스냅샷 데이터
    /// </summary>
    public class MarketSnapshot
    {
        public DateTime Timestamp { get; set; }
        public int OnlinePlayerCount { get; set; }
        public Dictionary<string, decimal> CurrentPrices { get; set; } = new();
        public Dictionary<string, (decimal demand, decimal supply)> MarketPressures { get; set; } = new();
        public Dictionary<string, (int buyVolume, int sellVolume)> TradeVolumes { get; set; } = new();
        public int TotalActiveItems { get; set; }
        public decimal AverageMarketActivity { get; set; }
        public List<string> HighVolatilityItems { get; set; } = new();
        public List<string> HighActivityItems { get; set; } = new();
    }

    /// <summary>
    /// 최근 활동 기록
    /// </summary>
    public class ActivityRecord
    {
        public DateTime Timestamp { get; set; }
        public string ItemId { get; set; } = string.Empty;
        public string ActivityType { get; set; } = string.Empty;
        public object Data { get; set; } = new();
        public string PlayerId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}