using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HarvestCraft2.Economy.API.Models
{
    /// <summary>
    /// 상점 거래 내역 기록
    /// </summary>
    [Table("shop_transactions")]
    public class ShopTransaction
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [MaxLength(36)]
        [Column("player_id")]
        public string PlayerId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("player_name")]
        public string PlayerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("item_id")]
        public string ItemId { get; set; } = string.Empty;

        [Required]
        [Column("transaction_type")]
        public TransactionType TransactionType { get; set; }

        [Required]
        [Column("quantity")]
        [Range(1, 999)]
        public int Quantity { get; set; }

        [Required]
        [Column("unit_price")]
        [Precision(10, 2)]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column("total_amount")]
        [Precision(15, 2)]
        public decimal TotalAmount { get; set; }

        // 시장 상황 스냅샷
        [Column("demand_pressure")]
        [Precision(6, 3)]
        public decimal DemandPressure { get; set; } = 0;

        [Column("supply_pressure")]
        [Precision(6, 3)]
        public decimal SupplyPressure { get; set; } = 0;

        [Required]
        [Column("online_players")]
        public int OnlinePlayers { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey(nameof(ItemId))]
        public virtual ShopItem? ShopItem { get; set; }

        // 계산된 속성들
        [NotMapped]
        public decimal NetPressure => DemandPressure - SupplyPressure;

        [NotMapped]
        public bool IsBuyTransaction => TransactionType == TransactionType.BuyFromNpc;

        [NotMapped]
        public bool IsSellTransaction => TransactionType == TransactionType.SellToNpc;

        [NotMapped]
        public string TransactionDescription
        {
            get
            {
                var action = IsBuyTransaction ? "구매" : "판매";
                return $"{PlayerName}이(가) {ShopItem?.DisplayName ?? ItemId}을(를) {Quantity}개 {action} - {TotalAmount:C}";
            }
        }

        [NotMapped]
        public TimeSpan TimeSinceTransaction => DateTime.UtcNow - CreatedAt;

        [NotMapped]
        public bool IsRecentTransaction => TimeSinceTransaction.TotalMinutes <= 10;

        /// <summary>
        /// 거래 가중치 계산 (세션 시간 기반)
        /// </summary>
        /// <param name="sessionStartTime">플레이어 세션 시작 시간</param>
        /// <returns>거래 가중치 (0.3 ~ 1.0)</returns>
        public decimal CalculateTransactionWeight(DateTime sessionStartTime)
        {
            var sessionDuration = CreatedAt - sessionStartTime;

            return sessionDuration.TotalMinutes switch
            {
                >= 120 => 1.0m,    // 2시간 이상 (장기)
                >= 30 => 0.8m,     // 30분-2시간 (중기)
                >= 10 => 0.6m,     // 10-30분 (단기)
                _ => 0.3m           // 10분 미만 (즉석)
            };
        }

        /// <summary>
        /// 시간대별 가중치 계산
        /// </summary>
        /// <returns>시간대 가중치 (0.3 ~ 1.0)</returns>
        public decimal CalculateTimeWeight()
        {
            var hour = CreatedAt.Hour;
            var dayOfWeek = CreatedAt.DayOfWeek;
            var isWeekend = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;

            // 피크 시간대 (18:00-24:00, 주말 10:00-24:00)
            if (isWeekend && hour >= 10 && hour < 24) return 1.0m;
            if (!isWeekend && hour >= 18 && hour < 24) return 1.0m;

            // 비활성 시간대 (02:00-08:00, 평일 낮)
            if (hour >= 2 && hour < 8) return 0.3m;
            if (!isWeekend && hour >= 9 && hour < 17) return 0.3m;

            // 반활성 시간대 (나머지)
            return 0.7m;
        }

        /// <summary>
        /// 접속자 수 기반 보정 계수 계산
        /// </summary>
        /// <param name="baseOnlinePlayers">기준 접속자 수</param>
        /// <returns>접속자 보정 계수 (최대 2.0)</returns>
        public decimal CalculatePlayerCorrectionFactor(int baseOnlinePlayers)
        {
            if (OnlinePlayers == 0) return 2.0m;
            return Math.Min(2.0m, (decimal)baseOnlinePlayers / OnlinePlayers);
        }

        /// <summary>
        /// 최종 가중 거래량 계산
        /// </summary>
        /// <param name="sessionStartTime">세션 시작 시간</param>
        /// <param name="baseOnlinePlayers">기준 접속자 수</param>
        /// <returns>가중 거래량</returns>
        public decimal CalculateWeightedVolume(DateTime sessionStartTime, int baseOnlinePlayers)
        {
            var sessionWeight = CalculateTransactionWeight(sessionStartTime);
            var timeWeight = CalculateTimeWeight();
            var playerCorrection = CalculatePlayerCorrectionFactor(baseOnlinePlayers);

            return Quantity * sessionWeight * timeWeight * playerCorrection;
        }

        /// <summary>
        /// 거래 유효성 검증
        /// </summary>
        /// <returns>검증 결과</returns>
        public (bool IsValid, string ErrorMessage) ValidateTransaction()
        {
            if (string.IsNullOrWhiteSpace(PlayerId))
                return (false, "플레이어 ID가 필요합니다.");

            if (string.IsNullOrWhiteSpace(ItemId))
                return (false, "아이템 ID가 필요합니다.");

            if (Quantity <= 0)
                return (false, "수량은 1개 이상이어야 합니다.");

            if (UnitPrice <= 0)
                return (false, "단가는 0보다 커야 합니다.");

            if (Math.Abs(TotalAmount - (UnitPrice * Quantity)) > 0.01m)
                return (false, "총액이 단가 × 수량과 일치하지 않습니다.");

            if (OnlinePlayers < 0)
                return (false, "접속자 수는 0 이상이어야 합니다.");

            return (true, string.Empty);
        }

        /// <summary>
        /// 거래 정보를 문자열로 표현
        /// </summary>
        public override string ToString()
        {
            var typeText = TransactionType == TransactionType.BuyFromNpc ? "구매" : "판매";
            return $"[{CreatedAt:yyyy-MM-dd HH:mm}] {PlayerName}: {ItemId} {Quantity}개 {typeText} ({TotalAmount:C})";
        }
    }

    /// <summary>
    /// 거래 유형
    /// </summary>
    public enum TransactionType
    {
        [Display(Name = "NPC에게서 구매")]
        BuyFromNpc = 0,

        [Display(Name = "NPC에게 판매")]
        SellToNpc = 1
    }

    /// <summary>
    /// 거래 통계 집계 결과
    /// </summary>
    public class TransactionSummary
    {
        public string ItemId { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // 원시 거래량
        public int TotalBuyVolume { get; set; }
        public int TotalSellVolume { get; set; }
        public int TotalTransactions { get; set; }

        // 가중 거래량
        public decimal WeightedBuyVolume { get; set; }
        public decimal WeightedSellVolume { get; set; }

        // 거래 금액
        public decimal TotalBuyAmount { get; set; }
        public decimal TotalSellAmount { get; set; }
        public decimal AverageBuyPrice { get; set; }
        public decimal AverageSellPrice { get; set; }

        // 시장 상황
        public decimal AverageDemandPressure { get; set; }
        public decimal AverageSupplyPressure { get; set; }
        public int AverageOnlinePlayers { get; set; }

        // 계산된 속성
        [NotMapped]
        public int NetVolume => TotalBuyVolume - TotalSellVolume;

        [NotMapped]
        public decimal NetWeightedVolume => WeightedBuyVolume - WeightedSellVolume;

        [NotMapped]
        public decimal NetAmount => TotalBuyAmount - TotalSellAmount;

        [NotMapped]
        public bool HasHighActivity => TotalTransactions >= 10;

        [NotMapped]
        public string PeriodDescription => $"{PeriodStart:MM/dd HH:mm} ~ {PeriodEnd:MM/dd HH:mm}";
    }
}