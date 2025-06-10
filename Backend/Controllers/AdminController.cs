using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HarvestCraft2.Economy.API.Services.Interfaces;
using HarvestCraft2.Economy.API.Models;
using HarvestCraft2.Economy.API.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HarvestCraft2.Economy.API.Controllers
{
    /// <summary>
    /// 관리자 전용 API 컨트롤러
    /// 시스템 관리, 아이템 관리, 설정 변경 등을 담당합니다.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiKey")]
    [Produces("application/json")]
    public class AdminController : ControllerBase
    {
        private readonly IShopService _shopService;
        private readonly IPriceService _priceService;
        private readonly IRedisService _redisService;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IShopService shopService,
            IPriceService priceService,
            IRedisService redisService,
            ApplicationDbContext dbContext,
            ILogger<AdminController> logger)
        {
            _shopService = shopService;
            _priceService = priceService;
            _redisService = redisService;
            _dbContext = dbContext;
            _logger = logger;
        }

        // ============================================================================
        // 1. 아이템 관리 API
        // ============================================================================

        /// <summary>
        /// 새로운 아이템을 상점에 추가합니다.
        /// </summary>
        [HttpPost("items")]
        [ProducesResponseType(typeof(ApiResponse<ShopItem>), 201)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> AddItem([FromBody] AddItemRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "요청 데이터가 유효하지 않습니다.",
                        Errors = ModelState
                    });
                }

                // 중복 체크
                var existingItem = await _dbContext.ShopItems
                    .FirstOrDefaultAsync(i => i.ItemId == request.ItemId);

                if (existingItem != null)
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "이미 존재하는 아이템 ID입니다."
                    });
                }

                var newItem = new ShopItem
                {
                    ItemId = request.ItemId,
                    DisplayName = request.DisplayName,
                    Category = request.Category,
                    HungerRestore = request.HungerRestore,
                    SaturationRestore = request.SaturationRestore,
                    ComplexityLevel = request.ComplexityLevel,
                    BaseSellPrice = request.BaseSellPrice,
                    BaseBuyPrice = request.BaseBuyPrice,
                    MinPrice = request.BaseSellPrice * 0.5m,
                    MaxPrice = request.BaseSellPrice * 3.0m,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                var success = await _shopService.AddItemToShopAsync(newItem);

                if (success)
                {
                    _logger.LogInformation("관리자가 새 아이템 추가: {ItemId} - {DisplayName}",
                        request.ItemId, request.DisplayName);

                    return CreatedAtAction(
                        nameof(GetItem),
                        new { itemId = newItem.ItemId },
                        new ApiResponse<ShopItem>
                        {
                            Success = true,
                            Message = "아이템이 성공적으로 추가되었습니다.",
                            Data = newItem
                        });
                }
                else
                {
                    return StatusCode(500, new ApiResponse<string>
                    {
                        Success = false,
                        Message = "아이템 추가 중 오류가 발생했습니다."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 추가 API 오류: {ItemId}", request?.ItemId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 기존 아이템 정보를 수정합니다.
        /// </summary>
        [HttpPut("items/{itemId}")]
        [ProducesResponseType(typeof(ApiResponse<ShopItem>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> UpdateItem(
            [FromRoute] string itemId,
            [FromBody] UpdateItemRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "요청 데이터가 유효하지 않습니다.",
                        Errors = ModelState
                    });
                }

                var existingItem = await _dbContext.ShopItems
                    .FirstOrDefaultAsync(i => i.ItemId == itemId);

                if (existingItem == null)
                {
                    return NotFound(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "해당 아이템을 찾을 수 없습니다."
                    });
                }

                // 업데이트
                existingItem.DisplayName = request.DisplayName ?? existingItem.DisplayName;
                existingItem.HungerRestore = request.HungerRestore ?? existingItem.HungerRestore;
                existingItem.SaturationRestore = request.SaturationRestore ?? existingItem.SaturationRestore;
                existingItem.ComplexityLevel = request.ComplexityLevel ?? existingItem.ComplexityLevel;
                existingItem.UpdatedAt = DateTime.Now;

                if (request.BaseSellPrice.HasValue)
                {
                    await _shopService.UpdateItemBasePriceAsync(itemId, request.BaseSellPrice.Value);
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("관리자가 아이템 수정: {ItemId} - {DisplayName}",
                    itemId, existingItem.DisplayName);

                return Ok(new ApiResponse<ShopItem>
                {
                    Success = true,
                    Message = "아이템이 성공적으로 수정되었습니다.",
                    Data = existingItem
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 수정 API 오류: {ItemId}", itemId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 아이템을 활성화/비활성화합니다.
        /// </summary>
        [HttpPatch("items/{itemId}/status")]
        [ProducesResponseType(typeof(ApiResponse<string>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> SetItemStatus(
            [FromRoute] string itemId,
            [FromBody] SetItemStatusRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "요청 데이터가 유효하지 않습니다.",
                        Errors = ModelState
                    });
                }

                var success = await _shopService.SetItemActiveStatusAsync(itemId, request.IsActive);

                if (success)
                {
                    var statusText = request.IsActive ? "활성화" : "비활성화";
                    _logger.LogInformation("관리자가 아이템 상태 변경: {ItemId} - {Status}",
                        itemId, statusText);

                    return Ok(new ApiResponse<string>
                    {
                        Success = true,
                        Message = $"아이템이 {statusText}되었습니다.",
                        Data = statusText
                    });
                }
                else
                {
                    return NotFound(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "해당 아이템을 찾을 수 없습니다."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 상태 변경 API 오류: {ItemId}", itemId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 특정 아이템 정보를 조회합니다.
        /// </summary>
        [HttpGet("items/{itemId}")]
        [ProducesResponseType(typeof(ApiResponse<ShopItem>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetItem([FromRoute] string itemId)
        {
            try
            {
                var item = await _dbContext.ShopItems
                    .FirstOrDefaultAsync(i => i.ItemId == itemId);

                if (item == null)
                {
                    return NotFound(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "해당 아이템을 찾을 수 없습니다."
                    });
                }

                return Ok(new ApiResponse<ShopItem>
                {
                    Success = true,
                    Message = "아이템 조회 성공",
                    Data = item
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "관리자 아이템 조회 API 오류: {ItemId}", itemId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 2. 가격 관리 API
        // ============================================================================

        /// <summary>
        /// 모든 아이템의 가격을 강제로 업데이트합니다.
        /// </summary>
        [HttpPost("prices/update")]
        [ProducesResponseType(typeof(ApiResponse<PriceUpdateSummary>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> ForceUpdateAllPrices()
        {
            try
            {
                _logger.LogInformation("관리자가 전체 가격 강제 업데이트 시작");

                var startTime = DateTime.Now;
                var updatedCount = await _priceService.UpdateAllPricesAsync();
                var endTime = DateTime.Now;

                var summary = new PriceUpdateSummary
                {
                    UpdatedItemCount = updatedCount,
                    StartTime = startTime,
                    EndTime = endTime,
                    Duration = endTime - startTime,
                    Initiator = "Admin"
                };

                _logger.LogInformation("관리자 전체 가격 업데이트 완료: {Count}개 아이템, {Duration}ms",
                    updatedCount, summary.Duration.TotalMilliseconds);

                return Ok(new ApiResponse<PriceUpdateSummary>
                {
                    Success = true,
                    Message = $"{updatedCount}개 아이템의 가격이 업데이트되었습니다.",
                    Data = summary
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
        /// 특정 아이템의 기본 가격을 설정합니다.
        /// </summary>
        [HttpPut("prices/{itemId}")]
        [ProducesResponseType(typeof(ApiResponse<string>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> SetItemBasePrice(
            [FromRoute] string itemId,
            [FromBody] SetBasePriceRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "요청 데이터가 유효하지 않습니다.",
                        Errors = ModelState
                    });
                }

                var success = await _shopService.UpdateItemBasePriceAsync(itemId, request.NewBasePrice);

                if (success)
                {
                    _logger.LogInformation("관리자가 아이템 기본 가격 설정: {ItemId} = {Price}",
                        itemId, request.NewBasePrice);

                    return Ok(new ApiResponse<string>
                    {
                        Success = true,
                        Message = $"아이템 기본 가격이 {request.NewBasePrice:F2} Gold로 설정되었습니다.",
                        Data = request.NewBasePrice.ToString("F2")
                    });
                }
                else
                {
                    return NotFound(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "해당 아이템을 찾을 수 없습니다."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 기본 가격 설정 API 오류: {ItemId}", itemId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 3. 시스템 관리 API
        // ============================================================================

        /// <summary>
        /// Redis 캐시를 정리합니다.
        /// </summary>
        [HttpDelete("cache/cleanup")]
        [ProducesResponseType(typeof(ApiResponse<CacheCleanupResult>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> CleanupCache()
        {
            try
            {
                _logger.LogInformation("관리자가 Redis 캐시 정리 시작");

                var deletedTradeData = await _redisService.CleanupOldTradeDataAsync();

                var result = new CacheCleanupResult
                {
                    DeletedTradeDataKeys = deletedTradeData,
                    CleanupTime = DateTime.Now,
                    Success = true
                };

                _logger.LogInformation("Redis 캐시 정리 완료: {DeletedKeys}개 키 삭제",
                    deletedTradeData);

                return Ok(new ApiResponse<CacheCleanupResult>
                {
                    Success = true,
                    Message = $"캐시 정리 완료: {deletedTradeData}개 키가 삭제되었습니다.",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "캐시 정리 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 서버 설정을 조회합니다.
        /// </summary>
        [HttpGet("config")]
        [ProducesResponseType(typeof(ApiResponse<List<ServerConfig>>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetServerConfig()
        {
            try
            {
                var configs = await _dbContext.ServerConfigs.ToListAsync();

                return Ok(new ApiResponse<List<ServerConfig>>
                {
                    Success = true,
                    Message = $"{configs.Count}개의 설정을 조회했습니다.",
                    Data = configs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "서버 설정 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 서버 설정을 업데이트합니다.
        /// </summary>
        [HttpPut("config/{configKey}")]
        [ProducesResponseType(typeof(ApiResponse<string>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> UpdateServerConfig(
            [FromRoute] string configKey,
            [FromBody] UpdateConfigRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "요청 데이터가 유효하지 않습니다.",
                        Errors = ModelState
                    });
                }

                var config = await _dbContext.ServerConfigs
                    .FirstOrDefaultAsync(c => c.ConfigKey == configKey);

                if (config == null)
                {
                    return NotFound(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "해당 설정을 찾을 수 없습니다."
                    });
                }

                config.ConfigValue = request.NewValue;
                config.UpdatedAt = DateTime.Now;

                await _dbContext.SaveChangesAsync();

                // Redis 캐시도 업데이트
                await _redisService.CacheServerConfigAsync(configKey, request.NewValue);

                _logger.LogInformation("관리자가 서버 설정 변경: {ConfigKey} = {Value}",
                    configKey, request.NewValue);

                return Ok(new ApiResponse<string>
                {
                    Success = true,
                    Message = "설정이 성공적으로 업데이트되었습니다.",
                    Data = request.NewValue
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "서버 설정 업데이트 API 오류: {ConfigKey}", configKey);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 4. 데이터베이스 관리 API
        // ============================================================================

        /// <summary>
        /// 데이터베이스 통계를 조회합니다.
        /// </summary>
        [HttpGet("database/stats")]
        [ProducesResponseType(typeof(ApiResponse<DatabaseStats>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetDatabaseStats()
        {
            try
            {
                var itemCount = await _dbContext.ShopItems.CountAsync();
                var activeItemCount = await _dbContext.ShopItems.CountAsync(i => i.IsActive);
                var transactionCount = await _dbContext.ShopTransactions.CountAsync();
                var priceHistoryCount = await _dbContext.PriceHistories.CountAsync();

                var recentTransactions = await _dbContext.ShopTransactions
                    .Where(t => t.CreatedAt >= DateTime.Now.AddDays(-1))
                    .CountAsync();

                var stats = new DatabaseStats
                {
                    TotalItems = itemCount,
                    ActiveItems = activeItemCount,
                    TotalTransactions = transactionCount,
                    TransactionsLast24h = recentTransactions,
                    PriceHistoryRecords = priceHistoryCount,
                    DatabaseSizeMB = 0, // 실제 구현에서는 DB 크기 조회 쿼리 필요
                    LastAnalyzed = DateTime.Now
                };

                return Ok(new ApiResponse<DatabaseStats>
                {
                    Success = true,
                    Message = "데이터베이스 통계 조회 성공",
                    Data = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "데이터베이스 통계 조회 API 오류");
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

    public class AddItemRequest
    {
        [Required(ErrorMessage = "아이템 ID는 필수입니다.")]
        [StringLength(100, ErrorMessage = "아이템 ID는 100자 이하여야 합니다.")]
        public string ItemId { get; set; } = string.Empty;

        [Required(ErrorMessage = "표시 이름은 필수입니다.")]
        [StringLength(100, ErrorMessage = "표시 이름은 100자 이하여야 합니다.")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "카테고리는 필수입니다.")]
        public ItemCategory Category { get; set; }

        [Range(0, 20, ErrorMessage = "허기 회복량은 0-20 사이여야 합니다.")]
        public int HungerRestore { get; set; }

        [Range(0, 20, ErrorMessage = "포만도 회복량은 0-20 사이여야 합니다.")]
        public decimal SaturationRestore { get; set; }

        [Required(ErrorMessage = "복잡도 레벨은 필수입니다.")]
        public ComplexityLevel ComplexityLevel { get; set; }

        [Required(ErrorMessage = "기본 판매 가격은 필수입니다.")]
        [Range(0.01, 100000, ErrorMessage = "기본 판매 가격은 0.01-100000 사이여야 합니다.")]
        public decimal BaseSellPrice { get; set; }

        [Range(0.01, 100000, ErrorMessage = "기본 구매 가격은 0.01-100000 사이여야 합니다.")]
        public decimal? BaseBuyPrice { get; set; }
    }

    public class UpdateItemRequest
    {
        [StringLength(100, ErrorMessage = "표시 이름은 100자 이하여야 합니다.")]
        public string? DisplayName { get; set; }

        [Range(0, 20, ErrorMessage = "허기 회복량은 0-20 사이여야 합니다.")]
        public int? HungerRestore { get; set; }

        [Range(0, 20, ErrorMessage = "포만도 회복량은 0-20 사이여야 합니다.")]
        public decimal? SaturationRestore { get; set; }

        public ComplexityLevel? ComplexityLevel { get; set; }

        [Range(0.01, 100000, ErrorMessage = "기본 판매 가격은 0.01-100000 사이여야 합니다.")]
        public decimal? BaseSellPrice { get; set; }
    }

    public class SetItemStatusRequest
    {
        [Required(ErrorMessage = "활성화 상태는 필수입니다.")]
        public bool IsActive { get; set; }
    }

    public class SetBasePriceRequest
    {
        [Required(ErrorMessage = "새로운 기본 가격은 필수입니다.")]
        [Range(0.01, 100000, ErrorMessage = "기본 가격은 0.01-100000 사이여야 합니다.")]
        public decimal NewBasePrice { get; set; }
    }

    public class UpdateConfigRequest
    {
        [Required(ErrorMessage = "새로운 값은 필수입니다.")]
        [StringLength(200, ErrorMessage = "값은 200자 이하여야 합니다.")]
        public string NewValue { get; set; } = string.Empty;
    }

    public class PriceUpdateSummary
    {
        public int UpdatedItemCount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string Initiator { get; set; } = string.Empty;
    }

    public class CacheCleanupResult
    {
        public long DeletedTradeDataKeys { get; set; }
        public DateTime CleanupTime { get; set; }
        public bool Success { get; set; }
    }

    public class DatabaseStats
    {
        public int TotalItems { get; set; }
        public int ActiveItems { get; set; }
        public int TotalTransactions { get; set; }
        public int TransactionsLast24h { get; set; }
        public int PriceHistoryRecords { get; set; }
        public long DatabaseSizeMB { get; set; }
        public DateTime LastAnalyzed { get; set; }
    }
}