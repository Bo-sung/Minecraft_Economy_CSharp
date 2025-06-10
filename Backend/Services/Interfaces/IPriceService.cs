using HarvestCraft2.Economy.API.Models;

namespace HarvestCraft2.Economy.API.Services.Interfaces
{
    /// <summary>
    /// 동적 가격 계산 시스템의 핵심 인터페이스
    /// 10분 주기 가격 업데이트와 실시간 시장 압력 계산을 담당
    /// </summary>
    public interface IPriceService
    {
        // ============================================================================
        // 1. 핵심 가격 계산 메소드
        // ============================================================================

        /// <summary>
        /// 특정 아이템의 현재 판매/구매 가격을 계산합니다.
        /// 실시간 시장 압력을 반영하여 동적 가격을 산출합니다.
        /// </summary>
        /// <param name="itemId">아이템 ID (예: "minecraft:wheat")</param>
        /// <param name="transactionType">거래 타입 (BUY_FROM_NPC/SELL_TO_NPC)</param>
        /// <returns>계산된 가격 (소수점 2자리)</returns>
        Task<decimal> CalculateCurrentPriceAsync(string itemId, TransactionType transactionType);

        /// <summary>
        /// 여러 아이템의 현재 가격을 배치로 계산합니다.
        /// Redis Pipeline을 사용하여 성능을 최적화합니다.
        /// </summary>
        /// <param name="itemIds">아이템 ID 목록</param>
        /// <param name="transactionType">거래 타입</param>
        /// <returns>아이템별 가격 딕셔너리</returns>
        Task<Dictionary<string, decimal>> CalculateBatchPricesAsync(
            IEnumerable<string> itemIds,
            TransactionType transactionType);

        // ============================================================================
        // 2. 시장 압력 계산 메소드
        // ============================================================================

        /// <summary>
        /// 특정 아이템의 수요 압력을 계산합니다.
        /// 10분간 가중 구매량을 기반으로 수요 압력을 산출합니다.
        /// 공식: (10분간 가중 구매량 × 6) / (1시간 기준 구매량) - 1
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>수요 압력 (-1.0 ~ 2.0 범위)</returns>
        Task<double> CalculateDemandPressureAsync(string itemId);

        /// <summary>
        /// 특정 아이템의 공급 압력을 계산합니다.
        /// 10분간 가중 판매량을 기반으로 공급 압력을 산출합니다.
        /// 공식: (10분간 가중 판매량 × 6) / (1시간 기준 판매량) - 1
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>공급 압력 (-1.0 ~ 2.0 범위)</returns>
        Task<double> CalculateSupplyPressureAsync(string itemId);

        /// <summary>
        /// 특정 아이템의 전체 시장 압력을 계산합니다.
        /// 수요 압력과 공급 압력을 종합하여 최종 시장 압력을 산출합니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>시장 압력 정보 (수요, 공급, 순 압력)</returns>
        Task<MarketPressureInfo> CalculateMarketPressureAsync(string itemId);

        // ============================================================================
        // 3. 접속자 기반 보정 계수 계산
        // ============================================================================

        /// <summary>
        /// 현재 접속자 수를 기반으로 거래량 보정 계수를 계산합니다.
        /// 접속자가 적을 때 거래량을 상향 보정하여 시장 안정성을 확보합니다.
        /// 공식: min(2.0, 기준_접속자 / 현재_접속자)
        /// </summary>
        /// <returns>접속자 보정 계수 (1.0 ~ 2.0)</returns>
        Task<double> CalculatePlayerCorrectionFactorAsync();

        /// <summary>
        /// 현재 시간대를 기반으로 시간대 가중치를 계산합니다.
        /// 활성 시간대(18:00-24:00, 주말)에는 1.0, 비활성 시간대에는 0.3~0.8 적용
        /// </summary>
        /// <returns>시간대 가중치 (0.3 ~ 1.0)</returns>
        double CalculateTimeWeightFactor();

        /// <summary>
        /// 플레이어의 세션 길이를 기반으로 세션 가중치를 계산합니다.
        /// 장기 접속자(2시간+): 1.0, 즉석 접속자(10분 미만): 0.3
        /// </summary>
        /// <param name="playerId">플레이어 UUID</param>
        /// <returns>세션 가중치 (0.3 ~ 1.0)</returns>
        Task<double> CalculateSessionWeightFactorAsync(string playerId);

        // ============================================================================
        // 4. 10분 주기 가격 업데이트 메소드
        // ============================================================================

        /// <summary>
        /// 모든 활성 아이템의 가격을 10분 주기로 업데이트합니다.
        /// 시장 압력을 반영하여 새로운 가격을 계산하고 Redis에 저장합니다.
        /// </summary>
        /// <returns>업데이트된 아이템 수</returns>
        Task<int> UpdateAllPricesAsync();

        /// <summary>
        /// 특정 아이템의 가격을 즉시 업데이트합니다.
        /// 대량 거래 발생 시 긴급 가격 조정용으로 사용됩니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>업데이트 성공 여부</returns>
        Task<bool> UpdateItemPriceAsync(string itemId);

        // ============================================================================
        // 5. 가격 제한 및 검증 메소드
        // ============================================================================

        /// <summary>
        /// 계산된 가격이 허용 범위 내에 있는지 검증하고 제한을 적용합니다.
        /// 가격 변동: 기본가격의 50% ~ 300%, 주기당 최대 ±10%
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <param name="calculatedPrice">계산된 가격</param>
        /// <param name="currentPrice">현재 가격</param>
        /// <returns>제한이 적용된 최종 가격</returns>
        decimal ApplyPriceLimits(string itemId, decimal calculatedPrice, decimal currentPrice);

        /// <summary>
        /// 아이템의 최소/최대 가격 범위를 가져옵니다.
        /// 기본가격의 50%~300% 범위로 설정됩니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>최소가격, 최대가격 튜플</returns>
        Task<(decimal minPrice, decimal maxPrice)> GetPriceLimitsAsync(string itemId);

        // ============================================================================
        // 6. 가격 예측 및 분석 메소드
        // ============================================================================

        /// <summary>
        /// 현재 시장 동향을 기반으로 향후 가격을 예측합니다.
        /// 단기(10분), 중기(1시간), 장기(6시간) 예측을 제공합니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>가격 예측 정보</returns>
        Task<PricePrediction> PredictPriceAsync(string itemId);

        /// <summary>
        /// 특정 아이템의 가격 변동성을 분석합니다.
        /// 과거 24시간 데이터를 기반으로 변동성 지표를 계산합니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>가격 변동성 정보</returns>
        Task<PriceVolatilityInfo> AnalyzePriceVolatilityAsync(string itemId);

        // ============================================================================
        // 7. 디버깅 및 모니터링 메소드
        // ============================================================================

        /// <summary>
        /// 특정 아이템의 상세한 가격 계산 과정을 반환합니다.
        /// 디버깅 및 모니터링 목적으로 사용됩니다.
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>가격 계산 상세 정보</returns>
        Task<PriceCalculationDetail> GetPriceCalculationDetailAsync(string itemId);

        /// <summary>
        /// 현재 시장 전체의 건강도를 평가합니다.
        /// 가격 안정성, 거래량, 압력 분포 등을 종합적으로 분석합니다.
        /// </summary>
        /// <returns>시장 건강도 정보</returns>
        Task<MarketHealthInfo> GetMarketHealthAsync();
    }

    // ============================================================================
    // 지원 클래스 및 열거형
    // ============================================================================

    /// <summary>
    /// 시장 압력 정보를 담는 클래스
    /// </summary>
    public class MarketPressureInfo
    {
        public double DemandPressure { get; set; }      // 수요 압력
        public double SupplyPressure { get; set; }      // 공급 압력
        public double NetPressure { get; set; }         // 순 압력 (수요 - 공급)
        public DateTime CalculatedAt { get; set; }      // 계산 시간
        public int OnlinePlayerCount { get; set; }      // 계산 시점 접속자 수
    }

    /// <summary>
    /// 가격 예측 정보를 담는 클래스
    /// </summary>
    public class PricePrediction
    {
        public decimal CurrentPrice { get; set; }       // 현재 가격
        public decimal ShortTermPrice { get; set; }     // 단기 예측 (10분 후)
        public decimal MediumTermPrice { get; set; }    // 중기 예측 (1시간 후)
        public decimal LongTermPrice { get; set; }      // 장기 예측 (6시간 후)
        public double Confidence { get; set; }          // 예측 신뢰도 (0.0~1.0)
        public DateTime PredictedAt { get; set; }       // 예측 생성 시간
    }

    /// <summary>
    /// 가격 변동성 정보를 담는 클래스
    /// </summary>
    public class PriceVolatilityInfo
    {
        public decimal StandardDeviation { get; set; }  // 가격 표준편차
        public decimal AverageChange { get; set; }      // 평균 변동률
        public decimal MaxChange { get; set; }          // 최대 변동률
        public decimal MinChange { get; set; }          // 최소 변동률
        public int UpdateCount { get; set; }            // 업데이트 횟수 (24시간)
        public DateTime AnalyzedAt { get; set; }        // 분석 시간
    }

    /// <summary>
    /// 가격 계산 상세 정보를 담는 클래스 (디버깅용)
    /// </summary>
    public class PriceCalculationDetail
    {
        public string ItemId { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }          // 기본 가격
        public decimal CurrentPrice { get; set; }       // 현재 가격
        public double DemandPressure { get; set; }      // 수요 압력
        public double SupplyPressure { get; set; }      // 공급 압력
        public double PlayerCorrectionFactor { get; set; } // 접속자 보정 계수
        public double TimeWeightFactor { get; set; }    // 시간대 가중치
        public decimal CalculatedPrice { get; set; }    // 계산된 가격 (제한 적용 전)
        public decimal FinalPrice { get; set; }         // 최종 가격 (제한 적용 후)
        public bool WasLimited { get; set; }            // 가격 제한 적용 여부
        public DateTime CalculatedAt { get; set; }      // 계산 시간
    }

    /// <summary>
    /// 시장 건강도 정보를 담는 클래스
    /// </summary>
    public class MarketHealthInfo
    {
        public double OverallStability { get; set; }    // 전체 안정성 지수 (0.0~1.0)
        public int ActiveItems { get; set; }            // 활성 아이템 수
        public double AverageVolatility { get; set; }   // 평균 변동성
        public int TotalTransactions24h { get; set; }   // 24시간 총 거래 수
        public decimal TotalVolume24h { get; set; }     // 24시간 총 거래액
        public int HighVolatilityItems { get; set; }    // 고변동성 아이템 수
        public DateTime AnalyzedAt { get; set; }        // 분석 시간
    }
}