using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HarvestCraft2.TestClient.Models;

namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// 가상 플레이어 관리 및 자동 거래 시뮬레이션을 담당하는 서비스 인터페이스
    /// </summary>
    public interface IPlayerService
    {
        // ============================================================================
        // 플레이어 관리
        // ============================================================================

        /// <summary>
        /// 등록된 가상 플레이어 목록
        /// </summary>
        IReadOnlyList<VirtualPlayer> Players { get; }

        /// <summary>
        /// 현재 선택된 플레이어
        /// </summary>
        VirtualPlayer? SelectedPlayer { get; set; }

        /// <summary>
        /// 가상 플레이어 생성
        /// </summary>
        Task<VirtualPlayer> CreatePlayerAsync(string playerName, decimal initialBalance = 1000m, CancellationToken cancellationToken = default);

        /// <summary>
        /// 기존 플레이어 불러오기 (API에서)
        /// </summary>
        Task<VirtualPlayer> LoadPlayerAsync(string playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 플레이어 정보 새로고침 (API에서 최신 정보 가져오기)
        /// </summary>
        Task RefreshPlayerAsync(VirtualPlayer player, CancellationToken cancellationToken = default);

        /// <summary>
        /// 플레이어 삭제
        /// </summary>
        Task<bool> RemovePlayerAsync(string playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 모든 플레이어 정보 새로고침
        /// </summary>
        Task RefreshAllPlayersAsync(CancellationToken cancellationToken = default);

        // ============================================================================
        // 잔액 관리
        // ============================================================================

        /// <summary>
        /// 플레이어 잔액 조회
        /// </summary>
        Task<decimal> GetPlayerBalanceAsync(string playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 플레이어 잔액 설정
        /// </summary>
        Task<bool> SetPlayerBalanceAsync(string playerId, decimal amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// 플레이어에게 돈 추가
        /// </summary>
        Task<bool> AddMoneyAsync(string playerId, decimal amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// 플레이어에게서 돈 차감
        /// </summary>
        Task<bool> DeductMoneyAsync(string playerId, decimal amount, CancellationToken cancellationToken = default);

        // ============================================================================
        // 거래 관리
        // ============================================================================

        /// <summary>
        /// 플레이어 거래 내역 조회
        /// </summary>
        Task<List<TransactionResponse>> GetPlayerTransactionsAsync(string playerId, int page = 1, int size = 50, CancellationToken cancellationToken = default);

        /// <summary>
        /// 수동 거래 실행 (아이템 구매)
        /// </summary>
        Task<PurchaseResponse> PurchaseItemAsync(string playerId, string itemId, int quantity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 수동 거래 실행 (아이템 판매)
        /// </summary>
        Task<SellResponse> SellItemAsync(string playerId, string itemId, int quantity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 배치 거래 실행
        /// </summary>
        Task<BatchTradeResponse> BatchTradeAsync(string playerId, List<TradeRequest> trades, CancellationToken cancellationToken = default);

        // ============================================================================
        // 자동 거래 시뮬레이션
        // ============================================================================

        /// <summary>
        /// 자동 거래 봇 활성화 여부
        /// </summary>
        bool IsAutoTradingEnabled { get; }

        /// <summary>
        /// 자동 거래 시작
        /// </summary>
        Task StartAutoTradingAsync(AutoTradingSettings settings, CancellationToken cancellationToken = default);

        /// <summary>
        /// 자동 거래 중지
        /// </summary>
        Task StopAutoTradingAsync();

        /// <summary>
        /// 개별 플레이어 자동 거래 설정
        /// </summary>
        Task SetPlayerAutoTradingAsync(string playerId, PlayerTradingBehavior behavior, CancellationToken cancellationToken = default);

        /// <summary>
        /// 랜덤 거래 실행 (시뮬레이션용)
        /// </summary>
        Task<TransactionResult> ExecuteRandomTradeAsync(string playerId, CancellationToken cancellationToken = default);

        // ============================================================================
        // 통계 및 분석
        // ============================================================================

        /// <summary>
        /// 플레이어 거래 통계 조회
        /// </summary>
        Task<PlayerStats> GetPlayerStatsAsync(string playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 전체 플레이어 통계 요약
        /// </summary>
        Task<OverallPlayerStats> GetOverallStatsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 플레이어 수익성 분석
        /// </summary>
        Task<ProfitabilityAnalysis> AnalyzeProfitabilityAsync(string playerId, CancellationToken cancellationToken = default);

        // ============================================================================
        // 가상 플레이어 프리셋
        // ============================================================================

        /// <summary>
        /// 기본 테스트 플레이어들 생성
        /// </summary>
        Task CreateDefaultPlayersAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 특정 거래 패턴을 가진 플레이어 생성
        /// </summary>
        Task<VirtualPlayer> CreateBehaviorPlayerAsync(string baseName, PlayerTradingBehavior behavior, CancellationToken cancellationToken = default);

        /// <summary>
        /// 대량 플레이어 생성 (부하 테스트용)
        /// </summary>
        Task<List<VirtualPlayer>> CreateBulkPlayersAsync(int count, string namePrefix = "TestPlayer", CancellationToken cancellationToken = default);

        // ============================================================================
        // 이벤트
        // ============================================================================

        /// <summary>
        /// 플레이어 추가됨
        /// </summary>
        event EventHandler<PlayerAddedEventArgs> PlayerAdded;

        /// <summary>
        /// 플레이어 제거됨
        /// </summary>
        event EventHandler<PlayerRemovedEventArgs> PlayerRemoved;

        /// <summary>
        /// 플레이어 정보 업데이트됨
        /// </summary>
        event EventHandler<PlayerUpdatedEventArgs> PlayerUpdated;

        /// <summary>
        /// 자동 거래 상태 변경됨
        /// </summary>
        event EventHandler<AutoTradingStatusChangedEventArgs> AutoTradingStatusChanged;

        /// <summary>
        /// 거래 실행됨
        /// </summary>
        event EventHandler<TradeExecutedEventArgs> TradeExecuted;
    }

    // ============================================================================
    // 가상 플레이어 모델
    // ============================================================================

    /// <summary>
    /// 가상 플레이어 정보
    /// </summary>
    public class VirtualPlayer
    {
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool IsOnline { get; set; }
        public bool IsAutoTradingEnabled { get; set; }
        public PlayerTradingBehavior TradingBehavior { get; set; } = new();
        public PlayerStats Stats { get; set; } = new();

        /// <summary>
        /// 플레이어가 API에서 생성된 실제 플레이어인지 여부
        /// </summary>
        public bool IsApiPlayer { get; set; }

        /// <summary>
        /// 마지막 거래 시간
        /// </summary>
        public DateTime? LastTradeTime { get; set; }

        /// <summary>
        /// 선호하는 아이템 카테고리
        /// </summary>
        public List<string> PreferredCategories { get; set; } = new();

        /// <summary>
        /// 현재 자동 거래 상태
        /// </summary>
        public AutoTradingStatus AutoTradingStatus { get; set; } = AutoTradingStatus.Stopped;
    }

    // ============================================================================
    // 거래 행동 모델
    // ============================================================================

    /// <summary>
    /// 플레이어 거래 행동 패턴
    /// </summary>
    public class PlayerTradingBehavior
    {
        /// <summary>
        /// 거래 빈도 (초 단위)
        /// </summary>
        public int TradingIntervalSeconds { get; set; } = 300; // 5분

        /// <summary>
        /// 구매 확률 (0.0 ~ 1.0)
        /// </summary>
        public double BuyProbability { get; set; } = 0.5;

        /// <summary>
        /// 최소 거래 수량
        /// </summary>
        public int MinQuantity { get; set; } = 1;

        /// <summary>
        /// 최대 거래 수량
        /// </summary>
        public int MaxQuantity { get; set; } = 10;

        /// <summary>
        /// 선호 아이템 목록
        /// </summary>
        public List<string> PreferredItems { get; set; } = new();

        /// <summary>
        /// 거래 예산 (잔액의 비율, 0.0 ~ 1.0)
        /// </summary>
        public double TradingBudgetRatio { get; set; } = 0.1; // 잔액의 10%

        /// <summary>
        /// 리스크 성향 (Conservative, Moderate, Aggressive)
        /// </summary>
        public RiskProfile RiskProfile { get; set; } = RiskProfile.Moderate;

        /// <summary>
        /// 가격 기반 결정 활성화 여부
        /// </summary>
        public bool UsePriceBasedDecision { get; set; } = true;
    }

    // ============================================================================
    // 열거형 정의
    // ============================================================================

    public enum AutoTradingStatus
    {
        Stopped,
        Running,
        Paused,
        Error
    }

    public enum RiskProfile
    {
        Conservative, // 안전한 거래 선호
        Moderate,     // 중간 수준 위험
        Aggressive    // 고위험 고수익 추구
    }

    // ============================================================================
    // 설정 및 통계 모델
    // ============================================================================

    /// <summary>
    /// 자동 거래 전역 설정
    /// </summary>
    public class AutoTradingSettings
    {
        public bool EnableGlobalAutoTrading { get; set; } = true;
        public int MaxConcurrentTrades { get; set; } = 10;
        public int GlobalTradingIntervalMs { get; set; } = 5000; // 5초
        public bool EnableMarketPressureSimulation { get; set; } = false;
        public List<string> AllowedItems { get; set; } = new();
        public List<string> RestrictedItems { get; set; } = new();
    }

    /// <summary>
    /// 플레이어 개별 통계
    /// </summary>
    public class PlayerStats
    {
        public long TotalTransactions { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalEarned { get; set; }
        public decimal NetProfit => TotalEarned - TotalSpent;
        public double ProfitMargin => TotalSpent > 0 ? (double)(NetProfit / TotalSpent) * 100 : 0;
        public int UniqueCategoriesTraded { get; set; }
        public int UniqueItemsTraded { get; set; }
        public DateTime? FirstTradeTime { get; set; }
        public DateTime? LastTradeTime { get; set; }
        public string MostTradedItem { get; set; } = string.Empty;
        public string MostProfitableItem { get; set; } = string.Empty;
    }

    /// <summary>
    /// 전체 플레이어 통계
    /// </summary>
    public class OverallPlayerStats
    {
        public int TotalPlayers { get; set; }
        public int ActivePlayers { get; set; }
        public int AutoTradingPlayers { get; set; }
        public decimal TotalBalance { get; set; }
        public decimal AverageBalance { get; set; }
        public long TotalTransactions { get; set; }
        public decimal TotalVolume { get; set; }
        public VirtualPlayer? MostActivePlayer { get; set; }
        public VirtualPlayer? MostProfitablePlayer { get; set; }
    }

    /// <summary>
    /// 수익성 분석 결과
    /// </summary>
    public class ProfitabilityAnalysis
    {
        public string PlayerId { get; set; } = string.Empty;
        public decimal TotalProfit { get; set; }
        public double ProfitMargin { get; set; }
        public List<ItemProfitability> ItemProfits { get; set; } = new();
        public List<CategoryProfitability> CategoryProfits { get; set; } = new();
        public TradingRecommendation Recommendation { get; set; } = new();
    }

    public class ItemProfitability
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Profit { get; set; }
        public int TradeCount { get; set; }
        public double AverageProfit { get; set; }
    }

    public class CategoryProfitability
    {
        public string Category { get; set; } = string.Empty;
        public decimal Profit { get; set; }
        public int TradeCount { get; set; }
    }

    public class TradingRecommendation
    {
        public List<string> RecommendedItems { get; set; } = new();
        public List<string> AvoidItems { get; set; } = new();
        public RiskProfile SuggestedRiskProfile { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// 거래 실행 결과
    /// </summary>
    public class TransactionResult
    {
        public bool Success { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Amount { get; set; }
        public bool IsPurchase { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime ExecutedAt { get; set; }
    }

    // ============================================================================
    // 이벤트 인수 클래스들
    // ============================================================================

    public class PlayerAddedEventArgs : EventArgs
    {
        public VirtualPlayer Player { get; set; } = new();
        public DateTime AddedAt { get; set; }
    }

    public class PlayerRemovedEventArgs : EventArgs
    {
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public DateTime RemovedAt { get; set; }
    }

    public class PlayerUpdatedEventArgs : EventArgs
    {
        public VirtualPlayer Player { get; set; } = new();
        public List<string> ChangedProperties { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }

    public class AutoTradingStatusChangedEventArgs : EventArgs
    {
        public string? PlayerId { get; set; } // null이면 전역 상태
        public AutoTradingStatus OldStatus { get; set; }
        public AutoTradingStatus NewStatus { get; set; }
        public string? Reason { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class TradeExecutedEventArgs : EventArgs
    {
        public TransactionResult Result { get; set; } = new();
        public bool IsAutoTrade { get; set; }
        public DateTime ExecutedAt { get; set; }
    }
}