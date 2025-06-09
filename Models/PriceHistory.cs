using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HarvestCraft2.Economy.API.Models
{
    /// <summary>
    /// 10분 주기 가격 변동 히스토리
    /// </summary>
    [Table("price_history")]
    public class PriceHistory
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("item_id")]
        public string ItemId { get; set; } = string.Empty;

        [Required]
        [Column("price_timestamp")]
        public DateTime PriceTimestamp { get; set; }

        // 가격 정보
        [Required]
        [Column("current_price")]
        [Precision(10, 2)]
        public decimal CurrentPrice { get; set; }

        [Required]
        [Column("base_price")]
        [Precision(10, 2)]
        public decimal BasePrice { get; set; }

        [Required]
        [Column("price_change_percent")]
        [Precision(5, 2)]
        public decimal PriceChangePercent { get; set; }

        // 시장 압력 정보
        [Required]
        [Column("demand_pressure")]
        [Precision(6, 3)]
        public decimal DemandPressure { get; set; }

        [Required]
        [Column("supply_pressure")]
        [Precision(6, 3)]
        public decimal SupplyPressure { get; set; }

        [Required]
        [Column("net_pressure")]
        [Precision(6, 3)]
        public decimal NetPressure { get; set; }

        // 거래량 정보
        [Column("period_buy_volume")]
        public int PeriodBuyVolume { get; set; } = 0;

        [Column("period_sell_volume")]
        public int PeriodSellVolume { get; set; } = 0;

        [Column("weighted_buy_volume")]
        [Precision(8, 1)]
        public decimal WeightedBuyVolume { get; set; } = 0;

        [Column("weighted_sell_volume")]
        [Precision(8, 1)]
        public decimal WeightedSellVolume { get; set; } = 0;

        // 시스템 정보
        [Required]
        [Column("online_players")]
        public int OnlinePlayers { get; set; }

        [Required]
        [Column("player_correction_factor")]
        [Precision(4, 2)]
        public decimal PlayerCorrectionFactor { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(ItemId))]
        public virtual ShopItem? ShopItem { get; set; }

        // 계산된 속성들
        [NotMapped]
        public int NetVolume => PeriodBuyVolume - PeriodSellVolume;

        [NotMapped]
        public decimal NetWeightedVolume => WeightedBuyVolume - WeightedSellVolume;

        [NotMapped]
        public decimal PriceVolatility => Math.Abs(PriceChangePercent);

        [NotMapped]
        public bool IsHighVolatility => PriceVolatility >= 0.05m; // 5% 이상 변동

        [NotMapped]
        public bool IsUpwardTrend => PriceChangePercent > 0;

        [NotMapped]
        public bool IsDownwardTrend => PriceChangePercent < 0;

        [NotMapped]
        public PriceDirection PriceDirection
        {
            get
            {
                if (PriceChangePercent > 0.01m) return PriceDirection.Rising;
                if (PriceChangePercent < -0.01m) return PriceDirection.Falling;
                return PriceDirection.Stable;
            }
        }

        [NotMapped]
        public MarketCondition MarketCondition
        {
            get
            {
                if (DemandPressure > 0.2m && SupplyPressure < 0.1m) return MarketCondition.HighDemand;
                if (SupplyPressure > 0.2m && DemandPressure < 0.1m) return MarketCondition.HighSupply;
                if (Math.Abs(NetPressure) < 0.05m) return MarketCondition.Balanced;
                return MarketCondition.Volatile;
            }
        }

        [NotMapped]
        public TimeSpan TimeSinceUpdate => DateTime.UtcNow - PriceTimestamp;

        [NotMapped]
        public bool IsCurrentPeriod => TimeSinceUpdate.TotalMinutes <= 10;

        /// <summary>
        /// 다음 가격 예측 (단순 트렌드 기반)
        /// </summary>
        /// <param name="maxChangePercent">최대 변동률 제한</param>
        /// <returns>예측 가격</returns>
        public decimal PredictNextPrice(decimal maxChangePercent = 0.10m)
        {
            var pressureImpact = NetPressure * PlayerCorrectionFactor;
            var predictedChange = Math.Min(Math.Abs(pressureImpact), maxChangePercent) * Math.Sign(pressureImpact);
            var predictedPrice = CurrentPrice * (1 + predictedChange);

            return ShopItem?.ClampPrice(predictedPrice) ?? predictedPrice;
        }

        /// <summary>
        /// 시장 활성도 점수 계산 (0-100)
        /// </summary>
        /// <returns>활성도 점수</returns>
        public int CalculateMarketActivityScore()
        {
            var volumeScore = Math.Min(50, (PeriodBuyVolume + PeriodSellVolume) * 2);
            var pressureScore = Math.Min(30, Math.Abs(NetPressure) * 100);
            var playerScore = Math.Min(20, OnlinePlayers);

            return (int)(volumeScore + pressureScore + playerScore);
        }

        /// <summary>
        /// 가격 안정성 지수 계산 (0-100, 높을수록 안정)
        /// </summary>
        /// <param name="recentHistories">최근 히스토리 목록</param>
        /// <returns>안정성 지수</returns>
        public int CalculatePriceStabilityIndex(IEnumerable<PriceHistory> recentHistories)
        {
            if (!recentHistories.Any()) return 50;

            var volatilities = recentHistories.Select(h => h.PriceVolatility).ToList();
            var averageVolatility = volatilities.Average();
            var maxVolatility = volatilities.Max();

            // 변동성이 낮을수록 안정성 높음
            var stabilityScore = Math.Max(0, 100 - (averageVolatility * 1000) - (maxVolatility * 500));
            return (int)Math.Min(100, stabilityScore);
        }

        /// <summary>
        /// 압력 균형 상태 확인
        /// </summary>
        /// <returns>균형 상태 설명</returns>
        public string GetPressureBalanceDescription()
        {
            var absDemand = Math.Abs(DemandPressure);
            var absSupply = Math.Abs(SupplyPressure);

            if (absDemand > 0.3m && absSupply < 0.1m)
                return "강한 수요 압력";

            if (absSupply > 0.3m && absDemand < 0.1m)
                return "강한 공급 압력";

            if (absDemand > 0.1m && absSupply > 0.1m)
                return "수요/공급 경쟁";

            if (Math.Abs(NetPressure) < 0.05m)
                return "균형 상태";

            return "불안정";
        }

        /// <summary>
        /// 가격 변동 요약 정보 생성
        /// </summary>
        /// <returns>변동 요약</returns>
        public PriceChangeSummary CreateChangeSummary()
        {
            return new PriceChangeSummary
            {
                ItemId = ItemId,
                Timestamp = PriceTimestamp,
                PreviousPrice = BasePrice,
                CurrentPrice = CurrentPrice,
                ChangeAmount = CurrentPrice - BasePrice,
                ChangePercent = PriceChangePercent,
                Direction = PriceDirection,
                MarketCondition = MarketCondition,
                ActivityScore = CalculateMarketActivityScore(),
                PressureDescription = GetPressureBalanceDescription()
            };
        }

        /// <summary>
        /// 가격 히스토리를 문자열로 표현
        /// </summary>
        public override string ToString()
        {
            var direction = PriceChangePercent > 0 ? "↗" : PriceChangePercent < 0 ? "↘" : "→";
            return $"[{PriceTimestamp:MM/dd HH:mm}] {ItemId}: {CurrentPrice:C} {direction} ({PriceChangePercent:P2})";
        }
    }

    /// <summary>
    /// 가격 변동 방향
    /// </summary>
    public enum PriceDirection
    {
        [Display(Name = "하락")]
        Falling = -1,

        [Display(Name = "안정")]
        Stable = 0,

        [Display(Name = "상승")]
        Rising = 1
    }

    /// <summary>
    /// 시장 상황
    /// </summary>
    public enum MarketCondition
    {
        [Display(Name = "균형")]
        Balanced,

        [Display(Name = "높은 수요")]
        HighDemand,

        [Display(Name = "높은 공급")]
        HighSupply,

        [Display(Name = "변동성")]
        Volatile
    }

    /// <summary>
    /// 가격 변동 요약 정보
    /// </summary>
    public class PriceChangeSummary
    {
        public string ItemId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public decimal PreviousPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal ChangeAmount { get; set; }
        public decimal ChangePercent { get; set; }
        public PriceDirection Direction { get; set; }
        public MarketCondition MarketCondition { get; set; }
        public int ActivityScore { get; set; }
        public string PressureDescription { get; set; } = string.Empty;

        [NotMapped]
        public bool IsSignificantChange => Math.Abs(ChangePercent) >= 0.05m;

        [NotMapped]
        public string DirectionSymbol => Direction switch
        {
            PriceDirection.Rising => "📈",
            PriceDirection.Falling => "📉",
            _ => "➡️"
        };

        [NotMapped]
        public string FormattedChange => $"{(ChangeAmount >= 0 ? "+" : "")}{ChangeAmount:C} ({ChangePercent:P2})";
    }

    /// <summary>
    /// 시장 트렌드 분석 결과
    /// </summary>
    public class MarketTrendAnalysis
    {
        public string ItemId { get; set; } = string.Empty;
        public DateTime AnalysisDate { get; set; }
        public TimeSpan AnalysisPeriod { get; set; }

        // 가격 트렌드
        public decimal StartPrice { get; set; }
        public decimal EndPrice { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal AveragePrice { get; set; }

        // 변동성 분석
        public decimal AverageVolatility { get; set; }
        public decimal MaxVolatility { get; set; }
        public int VolatilePeriods { get; set; }

        // 거래량 분석
        public int TotalVolume { get; set; }
        public decimal AverageVolume { get; set; }
        public int ActivePeriods { get; set; }

        // 압력 분석
        public decimal AverageDemandPressure { get; set; }
        public decimal AverageSupplyPressure { get; set; }
        public decimal DominantPressure { get; set; }

        [NotMapped]
        public decimal TotalPriceChange => EndPrice - StartPrice;

        [NotMapped]
        public decimal TotalPriceChangePercent => StartPrice != 0 ? (EndPrice - StartPrice) / StartPrice : 0;

        [NotMapped]
        public decimal PriceRange => MaxPrice - MinPrice;

        [NotMapped]
        public PriceDirection OverallTrend
        {
            get
            {
                if (TotalPriceChangePercent > 0.02m) return PriceDirection.Rising;
                if (TotalPriceChangePercent < -0.02m) return PriceDirection.Falling;
                return PriceDirection.Stable;
            }
        }

        [NotMapped]
        public bool IsHighVolatilityItem => AverageVolatility > 0.05m;

        [NotMapped]
        public bool IsActivelyTraded => ActivePeriods > (AnalysisPeriod.TotalMinutes / 10) * 0.3;
    }
}