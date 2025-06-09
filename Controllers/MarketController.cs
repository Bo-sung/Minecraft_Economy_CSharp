using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HarvestCraft2.Economy.API.Services.Interfaces;
using HarvestCraft2.Economy.API.Models;
using System.ComponentModel.DataAnnotations;

namespace HarvestCraft2.Economy.API.Controllers
{
    /// <summary>
    /// 시장 분석 및 대시보드 API 컨트롤러
    /// 전체 경제 동향, 통계, 리포트를 제공합니다.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiKey")]
    [Produces("application/json")]
    public class MarketController : ControllerBase
    {
        private readonly IShopService _shopService;
        private readonly IPriceService _priceService;
        private readonly IRedisService _redisService;
        private readonly ILogger<MarketController> _logger;

        public MarketController(
            IShopService shopService,
            IPriceService priceService,
            IRedisService redisService,
            ILogger<MarketController> logger)
        {
            _shopService = shopService;
            _priceService = priceService;
            _redisService = redisService;
            _logger = logger;
        }

        // ============================================================================
        // 1. 대시보드 API
        // ============================================================================

        /// <summary>
        /// 메인 대시보드 데이터를 조회합니다.
        /// </summary>
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(ApiResponse<ShopDashboardData>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var dashboardData = await _shopService.GetDashboardDataAsync();

                return Ok(new ApiResponse<ShopDashboardData>
                {
                    Success = true,
                    Message = "대시보드 데이터 조회 성공",
                    Data = dashboardData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "대시보드 데이터 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 경제 시스템 건강도 리포트를 조회합니다.
        /// </summary>
        [HttpGet("health")]
        [ProducesResponseType(typeof(ApiResponse<EconomyHealthReport>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetEconomyHealth()
        {
            try
            {
                var healthReport = await _shopService.GetEconomyHealthReportAsync();

                return Ok(new ApiResponse<EconomyHealthReport>
                {
                    Success = true,
                    Message = "경제 건강도 분석 완료",
                    Data = healthReport
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "경제 건강도 분석 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 2. 시장 개요 API
        // ============================================================================

        /// <summary>
        /// 시장 전체 개요 정보를 조회합니다.
        /// </summary>
        [HttpGet("overview")]
        [ProducesResponseType(typeof(ApiResponse<MarketOverview>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetMarketOverview()
        {
            try
            {
                var onlinePlayerCount = await _redisService.GetOnlinePlayerCountAsync();
                var allPrices = await _redisService.GetAllCurrentPricesAsync();
                var allPressures = await _redisService.GetAllMarketPressuresAsync();

                // 시장 스냅샷 생성
                var snapshot = await _redisService.CreateMarketSnapshotAsync();

                var overview = new MarketOverview
                {
                    OnlinePlayerCount = onlinePlayerCount,
                    ActiveItemCount = allPrices.Count,
                    AveragePrice = allPrices.Values.Any() ? allPrices.Values.Average() : 0m,
                    HighestPrice = allPrices.Values.Any() ? allPrices.Values.Max() : 0m,
                    LowestPrice = allPrices.Values.Any() ? allPrices.Values.Min() : 0m,
                    HighVolatilityItemCount = snapshot?.HighVolatilityItems.Count ?? 0,
                    HighActivityItemCount = snapshot?.HighActivityItems.Count ?? 0,
                    TotalMarketPressure = allPressures.Values.Sum(p => Math.Abs((double)(p.demand - p.supply))),
                    LastUpdated = DateTime.Now
                };

                return Ok(new ApiResponse<MarketOverview>
                {
                    Success = true,
                    Message = "시장 개요 조회 성공",
                    Data = overview
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시장 개요 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 3. 인기 아이템 및 랭킹 API
        // ============================================================================

        /// <summary>
        /// 인기 거래 아이템 순위를 조회합니다.
        /// </summary>
        [HttpGet("trending")]
        [ProducesResponseType(typeof(ApiResponse<List<TrendingItem>>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetTrendingItems(
            [FromQuery] int limit = 10,
            [FromQuery] TrendingPeriod period = TrendingPeriod.Day)
        {
            try
            {
                if (limit < 1 || limit > 50)
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Limit은 1-50 사이여야 합니다."
                    });
                }

                var trendingItems = await GetTrendingItemsInternalAsync(limit, period);

                return Ok(new ApiResponse<List<TrendingItem>>
                {
                    Success = true,
                    Message = $"{period} 기간 인기 아이템 {trendingItems.Count}개를 조회했습니다.",
                    Data = trendingItems
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "인기 아이템 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 가격 변동이 큰 아이템 순위를 조회합니다.
        /// </summary>
        [HttpGet("volatile")]
        [ProducesResponseType(typeof(ApiResponse<List<VolatileItem>>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetVolatileItems([FromQuery] int limit = 10)
        {
            try
            {
                if (limit < 1 || limit > 50)
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Limit은 1-50 사이여야 합니다."
                    });
                }

                var volatileItems = await GetVolatileItemsInternalAsync(limit);

                return Ok(new ApiResponse<List<VolatileItem>>
                {
                    Success = true,
                    Message = $"변동성 높은 아이템 {volatileItems.Count}개를 조회했습니다.",
                    Data = volatileItems
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "변동성 아이템 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 4. 통계 분석 API
        // ============================================================================

        /// <summary>
        /// 카테고리별 시장 통계를 조회합니다.
        /// </summary>
        [HttpGet("stats/category")]
        [ProducesResponseType(typeof(ApiResponse<List<CategoryStats>>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetCategoryStats()
        {
            try
            {
                var categoryStats = await GetCategoryStatsInternalAsync();

                return Ok(new ApiResponse<List<CategoryStats>>
                {
                    Success = true,
                    Message = "카테고리별 통계 조회 성공",
                    Data = categoryStats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "카테고리 통계 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 시간대별 거래 활동 통계를 조회합니다.
        /// </summary>
        [HttpGet("stats/activity")]
        [ProducesResponseType(typeof(ApiResponse<List<HourlyActivity>>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetActivityStats()
        {
            try
            {
                var activityStats = await GetActivityStatsInternalAsync();

                return Ok(new ApiResponse<List<HourlyActivity>>
                {
                    Success = true,
                    Message = "시간대별 활동 통계 조회 성공",
                    Data = activityStats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "활동 통계 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 5. 실시간 모니터링 API
        // ============================================================================

        /// <summary>
        /// 실시간 시장 스냅샷을 생성합니다.
        /// </summary>
        [HttpGet("snapshot")]
        [ProducesResponseType(typeof(ApiResponse<MarketSnapshot>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> CreateMarketSnapshot()
        {
            try
            {
                var snapshot = await _redisService.CreateMarketSnapshotAsync();

                if (snapshot == null)
                {
                    return StatusCode(500, new ApiResponse<string>
                    {
                        Success = false,
                        Message = "스냅샷 생성에 실패했습니다."
                    });
                }

                return Ok(new ApiResponse<MarketSnapshot>
                {
                    Success = true,
                    Message = "시장 스냅샷 생성 완료",
                    Data = snapshot
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시장 스냅샷 생성 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 현재 시스템 상태를 조회합니다.
        /// </summary>
        [HttpGet("status")]
        [ProducesResponseType(typeof(ApiResponse<SystemStatus>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetSystemStatus()
        {
            try
            {
                var redisConnected = await _redisService.IsConnectedAsync();
                var redisInfo = await _redisService.GetServerInfoAsync();
                var keyCount = await _redisService.GetKeyCountAsync();

                var status = new SystemStatus
                {
                    RedisConnected = redisConnected,
                    RedisInfo = redisInfo,
                    TotalCachedKeys = keyCount,
                    ServerTime = DateTime.Now,
                    UptimeHours = Environment.TickCount64 / (1000 * 60 * 60),
                    MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024)
                };

                return Ok(new ApiResponse<SystemStatus>
                {
                    Success = true,
                    Message = "시스템 상태 조회 성공",
                    Data = status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시스템 상태 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 내부 헬퍼 메소드
        // ============================================================================

        private async Task<List<TrendingItem>> GetTrendingItemsInternalAsync(int limit, TrendingPeriod period)
        {
            // 실제 구현에서는 데이터베이스에서 거래량 기준으로 조회
            // 현재는 샘플 데이터 반환
            var trendingItems = new List<TrendingItem>();

            try
            {
                var activeItems = await _shopService.GetAvailableItemsAsync();
                var sortedItems = activeItems
                    .OrderByDescending(i => i.TransactionVolume24h)
                    .Take(limit);

                foreach (var item in sortedItems)
                {
                    trendingItems.Add(new TrendingItem
                    {
                        ItemId = item.ItemId,
                        DisplayName = item.DisplayName,
                        Category = item.Category,
                        TransactionCount = item.TransactionVolume24h,
                        CurrentPrice = item.CurrentBuyPrice,
                        PriceChangePercent = item.PriceChangePercent24h,
                        Rank = trendingItems.Count + 1
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "인기 아이템 내부 조회 오류");
            }

            return trendingItems;
        }

        private async Task<List<VolatileItem>> GetVolatileItemsInternalAsync(int limit)
        {
            var volatileItems = new List<VolatileItem>();

            try
            {
                var activeItems = await _shopService.GetAvailableItemsAsync();
                var sortedItems = activeItems
                    .OrderByDescending(i => Math.Abs(i.PriceChangePercent24h))
                    .Take(limit);

                foreach (var item in sortedItems)
                {
                    volatileItems.Add(new VolatileItem
                    {
                        ItemId = item.ItemId,
                        DisplayName = item.DisplayName,
                        Category = item.Category,
                        CurrentPrice = item.CurrentBuyPrice,
                        PriceChangePercent = item.PriceChangePercent24h,
                        VolatilityScore = Math.Abs(item.PriceChangePercent24h),
                        Rank = volatileItems.Count + 1
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "변동성 아이템 내부 조회 오류");
            }

            return volatileItems;
        }

        private async Task<List<CategoryStats>> GetCategoryStatsInternalAsync()
        {
            var categoryStats = new List<CategoryStats>();

            try
            {
                var categories = new[] { "FOOD_CORE", "CROPS", "FOOD_EXTENDED", "VANILLA", "TOOLS" };

                foreach (var category in categories)
                {
                    var items = await _shopService.GetAvailableItemsAsync(category);

                    categoryStats.Add(new CategoryStats
                    {
                        CategoryName = category,
                        ItemCount = items.Count,
                        AveragePrice = items.Any() ? items.Average(i => i.CurrentBuyPrice) : 0m,
                        TotalVolume24h = items.Sum(i => i.TransactionVolume24h),
                        MostPopularItem = items.OrderByDescending(i => i.TransactionVolume24h).FirstOrDefault()?.DisplayName ?? "없음"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "카테고리 통계 내부 조회 오류");
            }

            return categoryStats;
        }

        private async Task<List<HourlyActivity>> GetActivityStatsInternalAsync()
        {
            var activityStats = new List<HourlyActivity>();

            try
            {
                // 24시간 시간대별 활동 통계 (샘플 데이터)
                for (int hour = 0; hour < 24; hour++)
                {
                    activityStats.Add(new HourlyActivity
                    {
                        Hour = hour,
                        TransactionCount = Random.Shared.Next(10, 100),
                        AverageOnlinePlayers = Random.Shared.Next(5, 50),
                        TotalVolume = Random.Shared.Next(1000, 10000)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "활동 통계 내부 조회 오류");
            }

            return activityStats;
        }
    }

    // ============================================================================
    // API 응답 모델
    // ============================================================================

    public class MarketOverview
    {
        public int OnlinePlayerCount { get; set; }
        public int ActiveItemCount { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal HighestPrice { get; set; }
        public decimal LowestPrice { get; set; }
        public int HighVolatilityItemCount { get; set; }
        public int HighActivityItemCount { get; set; }
        public double TotalMarketPressure { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class TrendingItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal CurrentPrice { get; set; }
        public double PriceChangePercent { get; set; }
        public int Rank { get; set; }
    }

    public class VolatileItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public double PriceChangePercent { get; set; }
        public double VolatilityScore { get; set; }
        public int Rank { get; set; }
    }

    public class CategoryStats
    {
        public string CategoryName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public decimal AveragePrice { get; set; }
        public int TotalVolume24h { get; set; }
        public string MostPopularItem { get; set; } = string.Empty;
    }

    public class HourlyActivity
    {
        public int Hour { get; set; }
        public int TransactionCount { get; set; }
        public int AverageOnlinePlayers { get; set; }
        public decimal TotalVolume { get; set; }
    }

    public class SystemStatus
    {
        public bool RedisConnected { get; set; }
        public string RedisInfo { get; set; } = string.Empty;
        public long TotalCachedKeys { get; set; }
        public DateTime ServerTime { get; set; }
        public long UptimeHours { get; set; }
        public long MemoryUsageMB { get; set; }
    }

    public enum TrendingPeriod
    {
        Hour,
        Day,
        Week
    }
}