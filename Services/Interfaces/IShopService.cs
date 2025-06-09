using HarvestCraft2.Economy.API.Models;

namespace HarvestCraft2.Economy.API.Services.Interfaces
{
    /// <summary>
    /// 상점 거래 처리 시스템의 핵심 인터페이스
    /// 플레이어와 NPC 간의 모든 거래를 관리하고 기록합니다.
    /// </summary>
    public interface IShopService
    {
        // ============================================================================
        // 1. 핵심 거래 처리 메소드
        // ============================================================================

        /// <summary>
        /// 플레이어가 NPC에게 아이템을 판매합니다.
        /// 실시간 가격 계산, 플레이어 잔액 업데이트, 거래량 반영을 처리합니다.
        /// </summary>
        /// <param name="playerId">플레이어 UUID</param>
        /// <param name="itemId">아이템 ID (예: "minecraft:wheat")</param>
        /// <param name="quantity">판매 수량</param>
        /// <returns>거래 결과 정보</returns>
        Task<TransactionResult> SellToNpcAsync(string playerId, string itemId, int quantity);

        /// <summary>
        /// 플레이어가 NPC에게서 아이템을 구매합니다.
        /// 실시간 가격 계산, 플레이어 잔액 차감, 거래량 반영을 처리합니다.
        /// </summary>
        /// <param name="playerId">플레이어 UUID</param>
        /// <param name="itemId">아이템 ID</param>
        /// <param name="quantity">구매 수량</param>
        /// <returns>거래 결과 정보</returns>
        Task<TransactionResult> BuyFromNpcAsync(string playerId, string itemId, int quantity);

        /// <summary>
        /// 여러 아이템을 한번에 거래합니다. (배치 처리)
        /// 성능 최적화를 위해 Redis Pipeline을 활용합니다.
        /// </summary>
        /// <param name="playerId">플레이어 UUID</param>
        /// <param name="transactions">거래 요청 목록</param>
        /// <returns>각 거래의 결과 목록</returns>
        Task<List<TransactionResult>> ProcessBatchTransactionsAsync(string playerId, List<TransactionRequest> transactions);

        // ============================================================================
        // 2. 플레이어 잔액 관리 메소드
        // ============================================================================

        /// <summary>
        /// 플레이어의 현재 잔액을 조회합니다.
        /// Redis에서 실시간 조회하며, 없으면 기본값을 반환합니다.
        /// </summary>
        /// <param name="playerId">플레이어 UUID</param>
        /// <returns>현재 잔액 (Gold)</returns>
        Task<decimal> GetPlayerBalanceAsync(string playerId);

        /// <summary>
        /// 플레이어 잔액을 업데이트합니다.
        /// 거래 성공 시 자동으로 호출되며, 음수 잔액을 방지합니다.
        /// </summary>
        /// <param name="playerId">플레이어 UUID</param>
        /// <param name="amount">변경할 금액 (양수: 증가, 음수: 감소)</param>
        /// <returns>업데이트 후 잔액</returns>
        Task<decimal> UpdatePlayerBalanceAsync(string playerId, decimal amount);

        /// <summary>
        /// 플레이어 잔액을 설정합니다. (관리자용)
        /// 초기 잔액 설정이나 관리자 조정 시 사용됩니다.
        /// </summary>
        /// <param name="playerId">플레이어 UUID</param>
        /// <param name="newBalance">새로운 잔액</param>
        /// <returns>설정 성공 여부</returns>
        Task<bool> SetPlayerBalanceAsync(string playerId, decimal newBalance);

        // ============================================================================
        // 3. 아이템 재고 관리 메소드
        // ============================================================================

        /// <summary>
        /// 특정 아이템의 NPC 재고를 조회합니다.
        /// 무한 재고 아이템은 -1을 반환합니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>현재 재고 수량 (-1: 무한재고)</returns>
        Task<int> GetItemStockAsync(string itemId);

        /// <summary>
        /// 아이템 재고를 업데이트합니다.
        /// 거래 시 자동으로 호출되며, 음수 재고를 방지합니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <param name="quantityChange">변경할 수량 (양수: 증가, 음수: 감소)</param>
        /// <returns>업데이트 후 재고</returns>
        Task<int> UpdateItemStockAsync(string itemId, int quantityChange);

        /// <summary>
        /// 여러 아이템의 재고를 배치로 조회합니다.
        /// </summary>
        /// <param name="itemIds">아이템 ID 목록</param>
        /// <returns>아이템별 재고 딕셔너리</returns>
        Task<Dictionary<string, int>> GetMultipleItemStocksAsync(IEnumerable<string> itemIds);

        // ============================================================================
        // 4. 거래 검증 메소드
        // ============================================================================

        /// <summary>
        /// 판매 거래가 가능한지 검증합니다.
        /// 아이템 존재, 수량 유효성, 플레이어 상태 등을 확인합니다.
        /// </summary>
        /// <param name="playerId">플레이어 UUID</param>
        /// <param name="itemId">아이템 ID</param>
        /// <param name="quantity">판매 수량</param>
        /// <returns>검증 결과</returns>
        Task<ValidationResult> ValidateSellTransactionAsync(string playerId, string itemId, int quantity);

        /// <summary>
        /// 구매 거래가 가능한지 검증합니다.
        /// 아이템 존재, 수량 유효성, 플레이어 잔액, NPC 재고 등을 확인합니다.
        /// </summary>
        /// <param name="playerId">플레이어 UUID</param>
        /// <param name="itemId">아이템 ID</param>
        /// <param name="quantity">구매 수량</param>
        /// <returns>검증 결과</returns>
        Task<ValidationResult> ValidateBuyTransactionAsync(string playerId, string itemId, int quantity);

        /// <summary>
        /// 배치 거래 요청을 검증합니다.
        /// 각 개별 거래의 유효성과 전체 거래의 일관성을 확인합니다.
        /// </summary>
        /// <param name="playerId">플레이어 UUID</param>
        /// <param name="transactions">거래 요청 목록</param>
        /// <returns>전체 검증 결과</returns>
        Task<BatchValidationResult> ValidateBatchTransactionsAsync(string playerId, List<TransactionRequest> transactions);

        // ============================================================================
        // 5. 거래 기록 및 히스토리 메소드
        // ============================================================================

        /// <summary>
        /// 플레이어의 거래 히스토리를 조회합니다.
        /// 페이징과 필터링을 지원합니다.
        /// </summary>
        /// <param name="playerId">플레이어 UUID</param>
        /// <param name="pageNumber">페이지 번호 (1부터 시작)</param>
        /// <param name="pageSize">페이지 크기</param>
        /// <param name="transactionType">거래 타입 필터 (선택적)</param>
        /// <returns>거래 히스토리 정보</returns>
        Task<TransactionHistory> GetPlayerTransactionHistoryAsync(
            string playerId,
            int pageNumber = 1,
            int pageSize = 20,
            TransactionType? transactionType = null);

        /// <summary>
        /// 특정 아이템의 거래 통계를 조회합니다.
        /// 24시간, 7일, 30일 단위의 통계를 제공합니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>아이템 거래 통계</returns>
        Task<ItemTransactionStats> GetItemTransactionStatsAsync(string itemId);

        /// <summary>
        /// 거래를 데이터베이스에 기록합니다.
        /// 백그라운드에서 비동기적으로 처리됩니다.
        /// </summary>
        /// <param name="transaction">거래 정보</param>
        /// <returns>기록 성공 여부</returns>
        Task<bool> RecordTransactionAsync(ShopTransaction transaction);

        // ============================================================================
        // 6. 실시간 거래량 반영 메소드
        // ============================================================================

        /// <summary>
        /// 거래 발생 시 Redis에 실시간 거래량을 반영합니다.
        /// 가중치를 적용하여 시장 압력 계산에 사용됩니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <param name="transactionType">거래 타입</param>
        /// <param name="quantity">거래 수량</param>
        /// <param name="playerId">플레이어 ID (가중치 계산용)</param>
        /// <returns>반영 성공 여부</returns>
        Task<bool> UpdateRealTimeVolumeAsync(string itemId, TransactionType transactionType, int quantity, string playerId);

        /// <summary>
        /// 10분 구간별 거래량을 집계합니다.
        /// 가격 계산 시스템에서 사용하는 핵심 데이터입니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <param name="timestamp">10분 구간 타임스탬프</param>
        /// <returns>집계 성공 여부</returns>
        Task<bool> AggregateVolumeDataAsync(string itemId, string timestamp);

        // ============================================================================
        // 7. 상점 정보 및 설정 메소드
        // ============================================================================

        /// <summary>
        /// 현재 상점에서 판매 중인 모든 아이템 목록을 조회합니다.
        /// 실시간 가격과 재고 정보를 포함합니다.
        /// </summary>
        /// <param name="category">카테고리 필터 (선택적)</param>
        /// <returns>상점 아이템 목록</returns>
        Task<List<ShopItemInfo>> GetAvailableItemsAsync(string? category = null);

        /// <summary>
        /// 특정 아이템의 상세 정보를 조회합니다.
        /// 현재 가격, 재고, 최근 거래 동향 등을 포함합니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>아이템 상세 정보</returns>
        Task<ShopItemDetail> GetItemDetailAsync(string itemId);

        /// <summary>
        /// 아이템의 가격 변동 차트 데이터를 조회합니다.
        /// 지정된 기간 동안의 가격 변화를 반환합니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <param name="period">조회 기간 (HOUR, DAY, WEEK)</param>
        /// <returns>가격 차트 데이터</returns>
        Task<List<PriceChartData>> GetPriceChartDataAsync(string itemId, TimePeriod period);

        // ============================================================================
        // 8. 관리자 기능 메소드
        // ============================================================================

        /// <summary>
        /// 새로운 아이템을 상점에 추가합니다. (관리자용)
        /// </summary>
        /// <param name="shopItem">추가할 아이템 정보</param>
        /// <returns>추가 성공 여부</returns>
        Task<bool> AddItemToShopAsync(ShopItem shopItem);

        /// <summary>
        /// 아이템의 기본 가격을 업데이트합니다. (관리자용)
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <param name="newBasePrice">새로운 기본 가격</param>
        /// <returns>업데이트 성공 여부</returns>
        Task<bool> UpdateItemBasePriceAsync(string itemId, decimal newBasePrice);

        /// <summary>
        /// 아이템을 상점에서 비활성화/활성화합니다. (관리자용)
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <param name="isActive">활성화 상태</param>
        /// <returns>변경 성공 여부</returns>
        Task<bool> SetItemActiveStatusAsync(string itemId, bool isActive);

        // ============================================================================
        // 9. 대시보드 및 분석 메소드
        // ============================================================================

        /// <summary>
        /// 상점 전체의 실시간 대시보드 데이터를 조회합니다.
        /// 총 거래량, 활성 플레이어, 인기 아이템 등을 포함합니다.
        /// </summary>
        /// <returns>대시보드 데이터</returns>
        Task<ShopDashboardData> GetDashboardDataAsync();

        /// <summary>
        /// 경제 시스템의 건강도를 분석합니다.
        /// 가격 안정성, 거래 활성도, 시장 집중도 등을 평가합니다.
        /// </summary>
        /// <returns>경제 건강도 분석 결과</returns>
        Task<EconomyHealthReport> GetEconomyHealthReportAsync();
    }

    // ============================================================================
    // 지원 클래스 및 열거형
    // ============================================================================

    /// <summary>
    /// 거래 결과를 나타내는 클래스
    /// </summary>
    public class TransactionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PlayerBalanceAfter { get; set; }
        public int ItemStockAfter { get; set; }
        public DateTime TransactionTime { get; set; }
        public string TransactionId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 거래 요청을 나타내는 클래스
    /// </summary>
    public class TransactionRequest
    {
        public string ItemId { get; set; } = string.Empty;
        public TransactionType TransactionType { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>
    /// 검증 결과를 나타내는 클래스
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// 배치 검증 결과를 나타내는 클래스
    /// </summary>
    public class BatchValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationResult> IndividualResults { get; set; } = new();
        public decimal TotalCost { get; set; }
        public decimal PlayerBalance { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// 거래 히스토리를 나타내는 클래스
    /// </summary>
    public class TransactionHistory
    {
        public List<ShopTransaction> Transactions { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public decimal TotalVolume { get; set; }
        public decimal AverageTransactionSize { get; set; }
    }

    /// <summary>
    /// 아이템 거래 통계를 나타내는 클래스
    /// </summary>
    public class ItemTransactionStats
    {
        public string ItemId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        // 24시간 통계
        public int Transactions24h { get; set; }
        public decimal Volume24h { get; set; }
        public decimal AvgPrice24h { get; set; }

        // 7일 통계
        public int Transactions7d { get; set; }
        public decimal Volume7d { get; set; }
        public decimal AvgPrice7d { get; set; }

        // 30일 통계
        public int Transactions30d { get; set; }
        public decimal Volume30d { get; set; }
        public decimal AvgPrice30d { get; set; }

        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// 상점 아이템 정보를 나타내는 클래스
    /// </summary>
    public class ShopItemInfo
    {
        public string ItemId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal CurrentBuyPrice { get; set; }
        public decimal CurrentSellPrice { get; set; }
        public int Stock { get; set; }
        public bool IsInfiniteStock { get; set; }
        public double PriceChangePercent24h { get; set; }
        public int TransactionVolume24h { get; set; }
    }

    /// <summary>
    /// 아이템 상세 정보를 나타내는 클래스
    /// </summary>
    public class ShopItemDetail : ShopItemInfo
    {
        public decimal BasePrice { get; set; }
        public int HungerRestore { get; set; }
        public decimal SaturationRestore { get; set; }
        public string ComplexityLevel { get; set; } = string.Empty;
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public double CurrentDemandPressure { get; set; }
        public double CurrentSupplyPressure { get; set; }
        public List<PriceChartData> RecentPriceHistory { get; set; } = new();
    }

    /// <summary>
    /// 가격 차트 데이터를 나타내는 클래스
    /// </summary>
    public class PriceChartData
    {
        public DateTime Timestamp { get; set; }
        public decimal Price { get; set; }
        public int Volume { get; set; }
        public double MarketPressure { get; set; }
    }

    /// <summary>
    /// 대시보드 데이터를 나타내는 클래스
    /// </summary>
    public class ShopDashboardData
    {
        public int TotalTransactions24h { get; set; }
        public decimal TotalVolume24h { get; set; }
        public int ActivePlayers24h { get; set; }
        public int ActiveItems { get; set; }
        public decimal AverageTransactionSize { get; set; }
        public List<ShopItemInfo> TopTradedItems { get; set; } = new();
        public List<ShopItemInfo> MostVolatileItems { get; set; } = new();
        public double OverallMarketStability { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// 경제 건강도 보고서를 나타내는 클래스
    /// </summary>
    public class EconomyHealthReport
    {
        public double OverallHealthScore { get; set; }      // 0.0 ~ 1.0
        public double PriceStabilityScore { get; set; }    // 가격 안정성
        public double TradingActivityScore { get; set; }   // 거래 활성도
        public double MarketDiversityScore { get; set; }   // 시장 다양성
        public double PlayerParticipationScore { get; set; } // 플레이어 참여도

        public List<string> HealthIssues { get; set; } = new(); // 발견된 문제점들
        public List<string> Recommendations { get; set; } = new(); // 개선 권장사항

        public DateTime GeneratedAt { get; set; }
    }

    /// <summary>
    /// 시간 범위 열거형
    /// </summary>
    public enum TimePeriod
    {
        HOUR,
        DAY,
        WEEK,
        MONTH
    }
}