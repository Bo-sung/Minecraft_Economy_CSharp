using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HarvestCraft2.TestClient.Models;

namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// HarvestCraft 2 Economy API와의 통신을 담당하는 서비스 인터페이스
    /// </summary>
    public interface IApiService
    {
        // ============================================================================
        // 연결 관리
        // ============================================================================

        /// <summary>
        /// API 서버 연결 상태
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// API 기본 URL
        /// </summary>
        string BaseUrl { get; set; }

        /// <summary>
        /// API 키
        /// </summary>
        string ApiKey { get; set; }

        /// <summary>
        /// API 서버 연결 테스트
        /// </summary>
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// API 서버 상태 정보 조회
        /// </summary>
        Task<ApiStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default);

        // ============================================================================
        // 상점 관련 API
        // ============================================================================

        /// <summary>
        /// 아이템 구매
        /// </summary>
        Task<PurchaseResponse> PurchaseItemAsync(string playerId, string itemId, int quantity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 아이템 판매
        /// </summary>
        Task<SellResponse> SellItemAsync(string playerId, string itemId, int quantity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 배치 거래 (여러 아이템 동시 거래)
        /// </summary>
        Task<BatchTradeResponse> BatchTradeAsync(string playerId, List<TradeRequest> trades, CancellationToken cancellationToken = default);

        /// <summary>
        /// 플레이어 거래 내역 조회
        /// </summary>
        Task<List<TransactionResponse>> GetPlayerTransactionsAsync(string playerId, int page = 1, int size = 50, CancellationToken cancellationToken = default);

        // ============================================================================
        // 가격 관련 API
        // ============================================================================

        /// <summary>
        /// 현재 아이템 가격 조회
        /// </summary>
        Task<PriceResponse> GetItemPriceAsync(string itemId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 여러 아이템 가격 일괄 조회
        /// </summary>
        Task<List<PriceResponse>> GetItemPricesAsync(List<string> itemIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// 아이템 가격 히스토리 조회
        /// </summary>
        Task<List<PriceHistoryResponse>> GetPriceHistoryAsync(string itemId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 가격 예측 데이터 조회
        /// </summary>
        Task<PricePredictionResponse> GetPricePredictionAsync(string itemId, int days = 7, CancellationToken cancellationToken = default);

        // ============================================================================
        // 시장 분석 API
        // ============================================================================

        /// <summary>
        /// 시장 대시보드 데이터 조회
        /// </summary>
        Task<MarketDashboardResponse> GetMarketDashboardAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 인기 아이템 순위 조회
        /// </summary>
        Task<List<PopularItemResponse>> GetPopularItemsAsync(int limit = 10, CancellationToken cancellationToken = default);

        /// <summary>
        /// 가격 변동성이 큰 아이템 조회
        /// </summary>
        Task<List<VolatileItemResponse>> GetVolatileItemsAsync(int limit = 10, CancellationToken cancellationToken = default);

        /// <summary>
        /// 카테고리별 시장 통계
        /// </summary>
        Task<List<CategoryStatsResponse>> GetCategoryStatsAsync(CancellationToken cancellationToken = default);

        // ============================================================================
        // 플레이어 관리 API
        // ============================================================================

        /// <summary>
        /// 플레이어 정보 조회
        /// </summary>
        Task<PlayerResponse> GetPlayerAsync(string playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 플레이어 생성 (테스트용)
        /// </summary>
        Task<PlayerResponse> CreatePlayerAsync(string playerName, decimal initialBalance = 1000m, CancellationToken cancellationToken = default);

        /// <summary>
        /// 플레이어 잔액 조회
        /// </summary>
        Task<BalanceResponse> GetPlayerBalanceAsync(string playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 플레이어 잔액 설정 (테스트용)
        /// </summary>
        Task<BalanceResponse> SetPlayerBalanceAsync(string playerId, decimal amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// 온라인 플레이어 목록 조회
        /// </summary>
        Task<List<PlayerResponse>> GetOnlinePlayersAsync(CancellationToken cancellationToken = default);

        // ============================================================================
        // 관리자 API
        // ============================================================================

        /// <summary>
        /// 시스템 메트릭 조회
        /// </summary>
        Task<SystemMetricsResponse> GetSystemMetricsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 아이템 목록 조회
        /// </summary>
        Task<List<ItemResponse>> GetItemsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 아이템 정보 업데이트
        /// </summary>
        Task<ItemResponse> UpdateItemAsync(string itemId, UpdateItemRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 가격 수동 조정
        /// </summary>
        Task<PriceResponse> AdjustPriceAsync(string itemId, decimal newPrice, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// 거래 데이터 정리
        /// </summary>
        Task<CleanupResponse> CleanupDataAsync(DateTime beforeDate, CancellationToken cancellationToken = default);

        // ============================================================================
        // 이벤트 및 알림
        // ============================================================================

        /// <summary>
        /// 가격 변동 알림 이벤트
        /// </summary>
        event EventHandler<PriceChangedEventArgs> PriceChanged;

        /// <summary>
        /// 연결 상태 변경 이벤트
        /// </summary>
        event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;

        /// <summary>
        /// API 오류 발생 이벤트
        /// </summary>
        event EventHandler<ApiErrorEventArgs> ApiError;
    }

    // ============================================================================
    // 이벤트 인수 클래스들
    // ============================================================================

    public class PriceChangedEventArgs : EventArgs
    {
        public string ItemId { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class ApiErrorEventArgs : EventArgs
    {
        public string Method { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    // ============================================================================
    // 요청/응답 모델 클래스들 (API 모델과 매핑)
    // ============================================================================

    public class TradeRequest
    {
        public string ItemId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public bool IsPurchase { get; set; } // true: 구매, false: 판매
    }

    public class UpdateItemRequest
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
    }
}

// ============================================================================
// API 응답 모델들 (별도 파일로 분리 예정)
// ============================================================================
namespace HarvestCraft2.TestClient.Models
{
    public class ApiStatusResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime ServerTime { get; set; }
        public bool DatabaseConnected { get; set; }
        public bool RedisConnected { get; set; }
        public int ActiveConnections { get; set; }
    }

    public class PurchaseResponse
    {
        public bool Success { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
        public decimal NewBalance { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class SellResponse
    {
        public bool Success { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public decimal TotalEarned { get; set; }
        public decimal NewBalance { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BatchTradeResponse
    {
        public bool Success { get; set; }
        public List<string> TransactionIds { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public decimal NewBalance { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class TransactionResponse
    {
        public string TransactionId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsPurchase { get; set; }
        public DateTime TransactionTime { get; set; }
    }

    public class PriceResponse
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal PreviousPrice { get; set; }
        public decimal ChangePercent { get; set; }
        public DateTime LastUpdated { get; set; }
        public int TotalVolume24h { get; set; }
    }

    public class PriceHistoryResponse
    {
        public DateTime Date { get; set; }
        public decimal Price { get; set; }
        public int Volume { get; set; }
        public decimal DemandPressure { get; set; }
        public decimal SupplyPressure { get; set; }
    }

    public class PricePredictionResponse
    {
        public string ItemId { get; set; } = string.Empty;
        public List<PredictedPrice> Predictions { get; set; } = new();
        public double Confidence { get; set; }
        public string Model { get; set; } = string.Empty;
    }

    public class PredictedPrice
    {
        public DateTime Date { get; set; }
        public decimal Price { get; set; }
        public decimal LowerBound { get; set; }
        public decimal UpperBound { get; set; }
    }

    public class MarketDashboardResponse
    {
        public int TotalOnlinePlayers { get; set; }
        public long TotalTransactions24h { get; set; }
        public decimal TotalVolume24h { get; set; }
        public int ActiveItems { get; set; }
        public decimal AveragePrice { get; set; }
        public List<TopItemData> TopItems { get; set; } = new();
    }

    public class TopItemData
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int Volume { get; set; }
        public decimal Price { get; set; }
    }

    public class PopularItemResponse
    {
        public int Rank { get; set; }
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int Volume24h { get; set; }
        public decimal Price { get; set; }
        public decimal ChangePercent { get; set; }
    }

    public class VolatileItemResponse
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Volatility { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal HighPrice24h { get; set; }
        public decimal LowPrice24h { get; set; }
    }

    public class CategoryStatsResponse
    {
        public string Category { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public long TotalVolume { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class PlayerResponse
    {
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline { get; set; }
        public long TotalTransactions { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalEarned { get; set; }
    }

    public class BalanceResponse
    {
        public string PlayerId { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class SystemMetricsResponse
    {
        public long TotalTransactions { get; set; }
        public decimal TotalVolume { get; set; }
        public int ActivePlayers { get; set; }
        public int TotalItems { get; set; }
        public DateTime SystemStartTime { get; set; }
        public string Version { get; set; } = string.Empty;
        public PerformanceMetrics Performance { get; set; } = new();
    }

    public class PerformanceMetrics
    {
        public double CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public double AverageResponseTime { get; set; }
        public int RequestsPerSecond { get; set; }
    }

    public class ItemResponse
    {
        public string ItemId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public bool IsEnabled { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class CleanupResponse
    {
        public bool Success { get; set; }
        public int DeletedTransactions { get; set; }
        public int DeletedPriceHistory { get; set; }
        public string? ErrorMessage { get; set; }
    }
}