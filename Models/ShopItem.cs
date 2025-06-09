using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HarvestCraft2.Economy.API.Models
{
    /// <summary>
    /// 상점에서 거래 가능한 아이템 정보
    /// </summary>
    [Table("shop_items")]
    public class ShopItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("item_id")]
        public string ItemId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Column("category")]
        public ItemCategory Category { get; set; }

        // 게임 정보
        [Column("hunger_restore")]
        public int HungerRestore { get; set; } = 0;

        [Column("saturation_restore")]
        [Precision(4, 1)]
        public decimal SaturationRestore { get; set; } = 0;

        [Column("complexity_level")]
        public ComplexityLevel ComplexityLevel { get; set; } = ComplexityLevel.Low;

        // 가격 정보
        [Required]
        [Column("base_sell_price")]
        [Precision(10, 2)]
        public decimal BaseSellPrice { get; set; }

        [Column("base_buy_price")]
        [Precision(10, 2)]
        public decimal? BaseBuyPrice { get; set; }

        [Required]
        [Column("min_price")]
        [Precision(10, 2)]
        public decimal MinPrice { get; set; }

        [Required]
        [Column("max_price")]
        [Precision(10, 2)]
        public decimal MaxPrice { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual ICollection<ShopTransaction> Transactions { get; set; } = new List<ShopTransaction>();
        public virtual ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();

        // 계산된 속성들
        [NotMapped]
        public decimal CurrentPrice { get; set; }

        [NotMapped]
        public decimal PriceChangePercent { get; set; }

        [NotMapped]
        public decimal DemandPressure { get; set; }

        [NotMapped]
        public decimal SupplyPressure { get; set; }

        [NotMapped]
        public bool IsAvailableForSale => IsActive && BaseBuyPrice.HasValue;

        [NotMapped]
        public bool IsAvailableForPurchase => IsActive;

        /// <summary>
        /// 복잡도 레벨에 따른 기본 가격 보너스 계산
        /// </summary>
        [NotMapped]
        public decimal ComplexityBonus
        {
            get
            {
                return ComplexityLevel switch
                {
                    ComplexityLevel.Low => 5m,      // 도구 1개 이하
                    ComplexityLevel.Medium => 15m,  // 도구 2-3개
                    ComplexityLevel.High => 30m,    // 도구 4-5개, 중간재료
                    ComplexityLevel.Extreme => 50m, // 복합 제작, 희귀재료
                    _ => 0m
                };
            }
        }

        /// <summary>
        /// 기본 가격 계산 공식 적용
        /// 기본가격 = (허기회복량 × 3) + (포만도 × 1.5) + 복잡도보너스
        /// </summary>
        [NotMapped]
        public decimal CalculatedBasePrice
        {
            get
            {
                return (HungerRestore * 3m) + (SaturationRestore * 1.5m) + ComplexityBonus;
            }
        }

        /// <summary>
        /// 현재 가격이 최소/최대 범위 내에 있는지 확인
        /// </summary>
        /// <param name="price">확인할 가격</param>
        /// <returns>유효한 가격 범위 여부</returns>
        public bool IsValidPriceRange(decimal price)
        {
            return price >= MinPrice && price <= MaxPrice;
        }

        /// <summary>
        /// 가격을 최소/최대 범위로 제한
        /// </summary>
        /// <param name="price">제한할 가격</param>
        /// <returns>범위 내로 제한된 가격</returns>
        public decimal ClampPrice(decimal price)
        {
            if (price < MinPrice) return MinPrice;
            if (price > MaxPrice) return MaxPrice;
            return price;
        }

        /// <summary>
        /// 기본 가격 대비 현재 가격의 변동률 계산
        /// </summary>
        /// <param name="currentPrice">현재 가격</param>
        /// <returns>변동률 (0.1 = +10%, -0.1 = -10%)</returns>
        public decimal CalculatePriceChangePercent(decimal currentPrice)
        {
            if (BaseSellPrice == 0) return 0;
            return (currentPrice - BaseSellPrice) / BaseSellPrice;
        }

        /// <summary>
        /// 아이템 정보를 문자열로 표현
        /// </summary>
        public override string ToString()
        {
            return $"{DisplayName} ({ItemId}) - {Category} [{ComplexityLevel}] - {BaseSellPrice:C}";
        }
    }

    /// <summary>
    /// 아이템 카테고리
    /// </summary>
    public enum ItemCategory
    {
        [Display(Name = "바닐라")]
        Vanilla = 0,

        [Display(Name = "HarvestCraft 2 Food Core")]
        FoodCore = 1,

        [Display(Name = "HarvestCraft 2 Crops")]
        Crops = 2,

        [Display(Name = "HarvestCraft 2 Food Extended")]
        FoodExtended = 3,

        [Display(Name = "도구")]
        Tools = 4
    }

    /// <summary>
    /// 제작 복잡도 레벨
    /// </summary>
    public enum ComplexityLevel
    {
        [Display(Name = "낮음", Description = "도구 1개 이하, 재료 3개 이하")]
        Low = 0,

        [Display(Name = "보통", Description = "도구 2-3개, 재료 4-6개")]
        Medium = 1,

        [Display(Name = "높음", Description = "도구 4-5개, 재료 7-10개, 중간재료 사용")]
        High = 2,

        [Display(Name = "최고", Description = "복합 제작 과정, 희귀 재료 사용")]
        Extreme = 3
    }
}