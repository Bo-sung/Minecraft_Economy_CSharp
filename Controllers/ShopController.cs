using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HarvestCraft2.Economy.API.Services.Interfaces;
using HarvestCraft2.Economy.API.Models;
using System.ComponentModel.DataAnnotations;

namespace HarvestCraft2.Economy.API.Controllers
{
    /// <summary>
    /// 상점 거래 처리 API 컨트롤러
    /// 플레이어와 NPC 간의 모든 거래를 관리합니다.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiKey")]
    [Produces("application/json")]
    public class ShopController : ControllerBase
    {
        private readonly IShopService _shopService;
        private readonly IPriceService _priceService;
        private readonly ILogger<ShopController> _logger;

        public ShopController(
            IShopService shopService,
            IPriceService priceService,
            ILogger<ShopController> logger)
        {
            _shopService = shopService;
            _priceService = priceService;
            _logger = logger;
        }

        // ============================================================================
        // 1. 핵심 거래 API
        // ============================================================================

        /// <summary>
        /// 플레이어가 NPC에게 아이템을 판매합니다.
        /// </summary>
        [HttpPost("sell")]
        [ProducesResponseType(typeof(ApiResponse<TransactionResult>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> SellToNpc([FromBody] SellRequest request)
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

                var result = await _shopService.SellToNpcAsync(
                    request.PlayerId,
                    request.ItemId,
                    request.Quantity);

                if (result.Success)
                {
                    _logger.LogInformation("판매 거래 성공: {PlayerId} - {ItemId} x{Quantity} = {Amount}",
                        request.PlayerId, request.ItemId, request.Quantity, result.TotalAmount);

                    return Ok(new ApiResponse<TransactionResult>
                    {
                        Success = true,
                        Message = "거래가 성공적으로 완료되었습니다.",
                        Data = result
                    });
                }
                else
                {
                    _logger.LogWarning("판매 거래 실패: {PlayerId} - {Message}",
                        request.PlayerId, result.Message);

                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = result.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "판매 API 처리 중 오류: {PlayerId} - {ItemId}",
                    request?.PlayerId ?? "Unknown", request?.ItemId ?? "Unknown");

                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 플레이어가 NPC에게서 아이템을 구매합니다.
        /// </summary>
        [HttpPost("buy")]
        [ProducesResponseType(typeof(ApiResponse<TransactionResult>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> BuyFromNpc([FromBody] BuyRequest request)
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

                var result = await _shopService.BuyFromNpcAsync(
                    request.PlayerId,
                    request.ItemId,
                    request.Quantity);

                if (result.Success)
                {
                    _logger.LogInformation("구매 거래 성공: {PlayerId} - {ItemId} x{Quantity} = {Amount}",
                        request.PlayerId, request.ItemId, request.Quantity, result.TotalAmount);

                    return Ok(new ApiResponse<TransactionResult>
                    {
                        Success = true,
                        Message = "거래가 성공적으로 완료되었습니다.",
                        Data = result
                    });
                }
                else
                {
                    _logger.LogWarning("구매 거래 실패: {PlayerId} - {Message}",
                        request.PlayerId, result.Message);

                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = result.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "구매 API 처리 중 오류: {PlayerId} - {ItemId}",
                    request?.PlayerId ?? "Unknown", request?.ItemId ?? "Unknown");

                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 여러 아이템을 한번에 거래합니다 (배치 처리).
        /// </summary>
        [HttpPost("batch")]
        [ProducesResponseType(typeof(ApiResponse<List<TransactionResult>>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> BatchTransaction([FromBody] BatchTransactionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || !request.Transactions.Any())
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "거래 목록이 비어있거나 유효하지 않습니다."
                    });
                }

                if (request.Transactions.Count > 50)
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "한 번에 최대 50개의 거래만 처리할 수 있습니다."
                    });
                }

                var results = await _shopService.ProcessBatchTransactionsAsync(
                    request.PlayerId,
                    request.Transactions);

                var successCount = results.Count(r => r.Success);
                var message = $"배치 거래 완료: {successCount}/{results.Count}개 성공";

                _logger.LogInformation("배치 거래 처리: {PlayerId} - {SuccessCount}/{TotalCount}",
                    request.PlayerId, successCount, results.Count);

                return Ok(new ApiResponse<List<TransactionResult>>
                {
                    Success = true,
                    Message = message,
                    Data = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "배치 거래 API 처리 중 오류: {PlayerId}",
                    request?.PlayerId ?? "Unknown");

                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 2. 플레이어 정보 API
        // ============================================================================

        /// <summary>
        /// 플레이어의 현재 잔액을 조회합니다.
        /// </summary>
        [HttpGet("balance/{playerId}")]
        [ProducesResponseType(typeof(ApiResponse<PlayerBalanceInfo>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetPlayerBalance([FromRoute] string playerId)
        {
            try
            {
                if (string.IsNullOrEmpty(playerId))
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "플레이어 ID가 필요합니다."
                    });
                }

                var balance = await _shopService.GetPlayerBalanceAsync(playerId);

                var balanceInfo = new PlayerBalanceInfo
                {
                    PlayerId = playerId,
                    Balance = balance,
                    LastUpdated = DateTime.Now
                };

                return Ok(new ApiResponse<PlayerBalanceInfo>
                {
                    Success = true,
                    Message = "잔액 조회 성공",
                    Data = balanceInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "플레이어 잔액 조회 API 오류: {PlayerId}", playerId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 플레이어의 거래 히스토리를 조회합니다.
        /// </summary>
        [HttpGet("history/{playerId}")]
        [ProducesResponseType(typeof(ApiResponse<TransactionHistory>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetTransactionHistory(
            [FromRoute] string playerId,
            [FromQuery] int page = 1,
            [FromQuery] int size = 20,
            [FromQuery] TransactionType? type = null)
        {
            try
            {
                if (string.IsNullOrEmpty(playerId))
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "플레이어 ID가 필요합니다."
                    });
                }

                if (page < 1) page = 1;
                if (size < 1 || size > 100) size = 20;

                var history = await _shopService.GetPlayerTransactionHistoryAsync(
                    playerId, page, size, type);

                return Ok(new ApiResponse<TransactionHistory>
                {
                    Success = true,
                    Message = "거래 히스토리 조회 성공",
                    Data = history
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "거래 히스토리 조회 API 오류: {PlayerId}", playerId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 3. 상점 정보 API
        // ============================================================================

        /// <summary>
        /// 상점에서 판매 중인 모든 아이템 목록을 조회합니다.
        /// </summary>
        [HttpGet("items")]
        [ProducesResponseType(typeof(ApiResponse<List<ShopItemInfo>>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetAvailableItems([FromQuery] string? category = null)
        {
            try
            {
                var items = await _shopService.GetAvailableItemsAsync(category);

                return Ok(new ApiResponse<List<ShopItemInfo>>
                {
                    Success = true,
                    Message = $"{items.Count}개의 아이템을 찾았습니다.",
                    Data = items
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "상점 아이템 목록 조회 API 오류");
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        /// <summary>
        /// 특정 아이템의 상세 정보를 조회합니다.
        /// </summary>
        [HttpGet("items/{itemId}")]
        [ProducesResponseType(typeof(ApiResponse<ShopItemDetail>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetItemDetail([FromRoute] string itemId)
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

                var itemDetail = await _shopService.GetItemDetailAsync(itemId);

                if (string.IsNullOrEmpty(itemDetail.DisplayName))
                {
                    return NotFound(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "해당 아이템을 찾을 수 없습니다."
                    });
                }

                return Ok(new ApiResponse<ShopItemDetail>
                {
                    Success = true,
                    Message = "아이템 상세 정보 조회 성공",
                    Data = itemDetail
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 상세 정보 조회 API 오류: {ItemId}", itemId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 4. 실시간 가격 API
        // ============================================================================

        /// <summary>
        /// 특정 아이템의 현재 판매/구매 가격을 조회합니다.
        /// </summary>
        [HttpGet("price/{itemId}")]
        [ProducesResponseType(typeof(ApiResponse<ItemPriceInfo>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> GetItemPrice([FromRoute] string itemId)
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

                var buyPrice = await _priceService.CalculateCurrentPriceAsync(itemId, TransactionType.BuyFromNpc);
                var sellPrice = await _priceService.CalculateCurrentPriceAsync(itemId, TransactionType.SellToNpc);

                if (buyPrice <= 0 && sellPrice <= 0)
                {
                    return NotFound(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "해당 아이템의 가격 정보를 찾을 수 없습니다."
                    });
                }

                var priceInfo = new ItemPriceInfo
                {
                    ItemId = itemId,
                    BuyPrice = buyPrice,
                    SellPrice = sellPrice,
                    LastUpdated = DateTime.Now
                };

                return Ok(new ApiResponse<ItemPriceInfo>
                {
                    Success = true,
                    Message = "가격 조회 성공",
                    Data = priceInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 가격 조회 API 오류: {ItemId}", itemId);
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "서버 오류가 발생했습니다."
                });
            }
        }

        // ============================================================================
        // 5. 관리자 API (선택적)
        // ============================================================================

        /// <summary>
        /// 플레이어 잔액을 설정합니다. (관리자 전용)
        /// </summary>
        [HttpPut("admin/balance")]
        [ProducesResponseType(typeof(ApiResponse<string>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 500)]
        public async Task<IActionResult> SetPlayerBalance([FromBody] SetBalanceRequest request)
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

                var success = await _shopService.SetPlayerBalanceAsync(request.PlayerId, request.NewBalance);

                if (success)
                {
                    _logger.LogInformation("관리자 잔액 설정: {PlayerId} = {Balance}",
                        request.PlayerId, request.NewBalance);

                    var message = $"플레이어 잔액이 {request.NewBalance:F2} Gold로 설정되었습니다.";
                    return Ok(new ApiResponse<string>
                    {
                        Success = true,
                        Message = "잔액 설정 성공",
                        Data = message
                    });
                }
                else
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "잔액 설정에 실패했습니다."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "관리자 잔액 설정 API 오류: {PlayerId}", request?.PlayerId);
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

    public class SellRequest
    {
        [Required(ErrorMessage = "플레이어 ID는 필수입니다.")]
        [StringLength(36, ErrorMessage = "플레이어 ID는 36자 이하여야 합니다.")]
        public string PlayerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "아이템 ID는 필수입니다.")]
        [StringLength(100, ErrorMessage = "아이템 ID는 100자 이하여야 합니다.")]
        public string ItemId { get; set; } = string.Empty;

        [Required(ErrorMessage = "수량은 필수입니다.")]
        [Range(1, 10000, ErrorMessage = "수량은 1개 이상 10,000개 이하여야 합니다.")]
        public int Quantity { get; set; }
    }

    public class BuyRequest
    {
        [Required(ErrorMessage = "플레이어 ID는 필수입니다.")]
        [StringLength(36, ErrorMessage = "플레이어 ID는 36자 이하여야 합니다.")]
        public string PlayerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "아이템 ID는 필수입니다.")]
        [StringLength(100, ErrorMessage = "아이템 ID는 100자 이하여야 합니다.")]
        public string ItemId { get; set; } = string.Empty;

        [Required(ErrorMessage = "수량은 필수입니다.")]
        [Range(1, 10000, ErrorMessage = "수량은 1개 이상 10,000개 이하여야 합니다.")]
        public int Quantity { get; set; }
    }

    public class BatchTransactionRequest
    {
        [Required(ErrorMessage = "플레이어 ID는 필수입니다.")]
        public string PlayerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "거래 목록은 필수입니다.")]
        [MinLength(1, ErrorMessage = "최소 1개의 거래가 필요합니다.")]
        [MaxLength(50, ErrorMessage = "최대 50개의 거래만 가능합니다.")]
        public List<TransactionRequest> Transactions { get; set; } = new();
    }

    public class SetBalanceRequest
    {
        [Required(ErrorMessage = "플레이어 ID는 필수입니다.")]
        public string PlayerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "새 잔액은 필수입니다.")]
        [Range(0, 1000000, ErrorMessage = "잔액은 0 이상 1,000,000 이하여야 합니다.")]
        public decimal NewBalance { get; set; }
    }

    public class PlayerBalanceInfo
    {
        public string PlayerId { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class ItemPriceInfo
    {
        public string ItemId { get; set; } = string.Empty;
        public decimal BuyPrice { get; set; }
        public decimal SellPrice { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// 표준화된 API 응답 형식
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public object? Errors { get; set; }
    }
}