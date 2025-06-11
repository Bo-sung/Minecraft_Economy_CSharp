using HarvestCraft2.Economy.API.Models;
using HarvestCraft2.Economy.API.Services;
using HarvestCraft2.Economy.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace HarvestCraft2.Economy.API.Controllers
{
    /// <summary>
    /// 가격 변동 모니터링 및 시장 분석 API 컨트롤러
    /// 실시간 가격 추적, 예측, 차트 데이터를 제공합니다.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiKey")]
    [Produces("application/json")]
    public class PriceController : ControllerBase
    {
        private readonly IPriceService _priceService;
        private readonly IShopService _shopService;  // 추가
        private readonly IRedisService _redisService;
        private readonly ILogger<PriceController> _logger;

        public PriceController(IPriceService priceService, IShopService shopService, IRedisService redisService, ILogger<PriceController> logger)
        {
            _priceService = priceService;
            _shopService = shopService;
            _redisService = redisService;
            _logger = logger;
        }

        // ============================================================================
        // 1. 실시간 가격 조회 API
        // ============================================================================

        /// <summary>
        /// 모든 아이템의 현재 가격 목록을 조회합니다.
        /// </summary>
        [HttpGet("current")]
        [ProducesResponseType(typeof(ApiResponse<Dictionary<string, decimal>>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetAllCurrentPrices()
        {
            try
            {
                var prices = await _redisService.GetAllCurrentPricesAsync();

                return Ok(new ApiResponse<Dictionary<string, decimal>>
                {
                    Success = true,
                    Message = $"{prices.Count}개 아이템의 현재 가격을 조회했습니다.",
                    Data = prices
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "전체 가격 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 특정 아이템의 상세 가격 정보를 조회합니다.
        /// </summary>
        [HttpGet("{itemId}/detail")]
        [ProducesResponseType(typeof(ApiResponse<PriceCalculationDetail>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetPriceDetail([FromRoute] string itemId)
        {
            try
            {
                if (string.IsNullOrEmpty(itemId))
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "아이템 ID가 필요합니다."
                    });
                }

                var detail = await _priceService.GetPriceCalculationDetailAsync(itemId);

                if (string.IsNullOrEmpty(detail.ItemId))
                {
                    return NotFound(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "해당 아이템의 가격 정보를 찾을 수 없습니다."
                    });
                }

                return Ok(new ApiResponse<PriceCalculationDetail>
                {
                    Success = true,
                    Message = "가격 상세 정보 조회 성공",
                    Data = detail
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 상세 정보 조회 API 오류: {ItemId}", itemId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 여러 아이템의 가격을 배치로 조회합니다.
        /// </summary>
        [HttpPost("batch")]
        [ProducesResponseType(typeof(ApiResponse<Dictionary<string, decimal>>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetBatchPrices([FromBody] BatchPriceRequest request)
        {
            try
            {
                if (!ModelState.IsValid || !request.ItemIds.Any())
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "아이템 목록이 비어있거나 유효하지 않습니다."
                    });
                }

                if (request.ItemIds.Count > 100)
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "한 번에 최대 100개의 아이템만 조회할 수 있습니다."
                    });
                }

                var prices = await _priceService.CalculateBatchPricesAsync(
                    request.ItemIds,
                    request.TransactionType);

                return Ok(new ApiResponse<Dictionary<string, decimal>>
                {
                    Success = true,
                    Message = $"{prices.Count}개 아이템의 가격을 조회했습니다.",
                    Data = prices
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "배치 가격 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 2. 시장 압력 조회 API
        // ============================================================================

        /// <summary>
        /// 특정 아이템의 시장 압력 정보를 조회합니다.
        /// </summary>
        [HttpGet("{itemId}/pressure")]
        [ProducesResponseType(typeof(ApiResponse<MarketPressureInfo>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetMarketPressure([FromRoute] string itemId)
        {
            try
            {
                if (string.IsNullOrEmpty(itemId))
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "아이템 ID가 필요합니다."
                    });
                }

                var pressure = await _priceService.CalculateMarketPressureAsync(itemId);

                if (pressure.CalculatedAt == DateTime.MinValue)
                {
                    return NotFound(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "해당 아이템의 시장 압력 정보를 찾을 수 없습니다."
                    });
                }

                return Ok(new ApiResponse<MarketPressureInfo>
                {
                    Success = true,
                    Message = "시장 압력 조회 성공",
                    Data = pressure
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시장 압력 조회 API 오류: {ItemId}", itemId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 모든 아이템의 시장 압력을 조회합니다.
        /// </summary>
        [HttpGet("pressure/all")]
        [ProducesResponseType(typeof(ApiResponse<Dictionary<string, (decimal demand, decimal supply)>>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetAllMarketPressures()
        {
            try
            {
                var pressures = await _redisService.GetAllMarketPressuresAsync();

                return Ok(new ApiResponse<Dictionary<string, (decimal demand, decimal supply)>>
                {
                    Success = true,
                    Message = $"{pressures.Count}개 아이템의 시장 압력을 조회했습니다.",
                    Data = pressures
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "전체 시장 압력 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 3. 가격 예측 및 분석 API
        // ============================================================================

        /// <summary>
        /// 특정 아이템의 가격을 예측합니다.
        /// </summary>
        [HttpGet("{itemId}/predict")]
        [ProducesResponseType(typeof(ApiResponse<PricePrediction>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> PredictPrice([FromRoute] string itemId)
        {
            try
            {
                if (string.IsNullOrEmpty(itemId))
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "아이템 ID가 필요합니다."
                    });
                }

                var prediction = await _priceService.PredictPriceAsync(itemId);

                return Ok(new ApiResponse<PricePrediction>
                {
                    Success = true,
                    Message = "가격 예측 완료",
                    Data = prediction
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 예측 API 오류: {ItemId}", itemId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 특정 아이템의 가격 변동성을 분석합니다.
        /// </summary>
        [HttpGet("{itemId}/volatility")]
        [ProducesResponseType(typeof(ApiResponse<PriceVolatilityInfo>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> AnalyzeVolatility([FromRoute] string itemId)
        {
            try
            {
                if (string.IsNullOrEmpty(itemId))
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "아이템 ID가 필요합니다."
                    });
                }

                var volatility = await _priceService.AnalyzePriceVolatilityAsync(itemId);

                return Ok(new ApiResponse<PriceVolatilityInfo>
                {
                    Success = true,
                    Message = "변동성 분석 완료",
                    Data = volatility
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "변동성 분석 API 오류: {ItemId}", itemId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 4. 차트 데이터 API
        // ============================================================================

        /// <summary>
        /// 특정 아이템의 가격 차트 데이터를 조회합니다.
        /// </summary>
        [HttpGet("{itemId}/chart")]
        [ProducesResponseType(typeof(ApiResponse<List<PriceChartData>>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetPriceChart(
            [FromRoute] string itemId,
            [FromQuery] TimePeriod period = TimePeriod.DAY)
        {
            try
            {
                if (string.IsNullOrEmpty(itemId))
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "아이템 ID가 필요합니다."
                    });
                }

                // _priceService 대신 _shopService 사용
                var chartData = await _shopService.GetPriceChartDataAsync(itemId, period);

                return Ok(new ApiResponse<List<PriceChartData>>
                {
                    Success = true,
                    Message = $"{period} 기간의 가격 차트 데이터를 조회했습니다.",
                    Data = chartData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 차트 데이터 조회 API 오류: {ItemId}", itemId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 5. 시스템 관리 API
        // ============================================================================

        /// <summary>
        /// 모든 아이템의 가격을 수동으로 업데이트합니다. (관리자 전용)
        /// </summary>
        [HttpPost("admin/update")]
        [ProducesResponseType(typeof(ApiResponse<PriceUpdateResult>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> ForceUpdatePrices()
        {
            try
            {
                _logger.LogInformation("관리자 가격 수동 업데이트 시작");

                var startTime = DateTime.Now;
                var updatedCount = await _priceService.UpdateAllPricesAsync();
                var endTime = DateTime.Now;
                var duration = endTime - startTime;

                var result = new PriceUpdateResult
                {
                    UpdatedItemCount = updatedCount,
                    UpdateStartTime = startTime,
                    UpdateEndTime = endTime,
                    UpdateDuration = duration,
                    Success = updatedCount > 0
                };

                _logger.LogInformation("관리자 가격 수동 업데이트 완료: {Count}개 아이템, {Duration}ms",
                    updatedCount, duration.TotalMilliseconds);

                return Ok(new ApiResponse<PriceUpdateResult>
                {
                    Success = true,
                    Message = $"{updatedCount}개 아이템의 가격이 업데이트되었습니다.",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "관리자 가격 업데이트 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 전체 시장 건강도를 조회합니다.
        /// </summary>
        [HttpGet("market/health")]
        [ProducesResponseType(typeof(ApiResponse<MarketHealthInfo>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetMarketHealth()
        {
            try
            {
                var health = await _priceService.GetMarketHealthAsync();

                return Ok(new ApiResponse<MarketHealthInfo>
                {
                    Success = true,
                    Message = "시장 건강도 분석 완료",
                    Data = health
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시장 건강도 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }
    }

    // ============================================================================
    // API 요청/응답 모델
    // ============================================================================

    public class BatchPriceRequest
    {
        [Required(ErrorMessage = "아이템 목록은 필수입니다.")]
        [MinLength(1, ErrorMessage = "최소 1개의 아이템이 필요합니다.")]
        [MaxLength(100, ErrorMessage = "최대 100개의 아이템만 가능합니다.")]
        public List<string> ItemIds { get; set; } = new();

        [Required(ErrorMessage = "거래 타입은 필수입니다.")]
        public TransactionType TransactionType { get; set; }
    }

    public class PriceUpdateResult
    {
        public int UpdatedItemCount { get; set; }
        public DateTime UpdateStartTime { get; set; }
        public DateTime UpdateEndTime { get; set; }
        public TimeSpan UpdateDuration { get; set; }
        public bool Success { get; set; }
    }
}