using HarvestCraft2.Economy.API.Data;
using HarvestCraft2.Economy.API.Models;
using HarvestCraft2.Economy.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace HarvestCraft2.Economy.API.Services
{
    /// <summary>
    /// 상점 거래 처리 시스템의 핵심 구현체
    /// 플레이어와 NPC 간의 모든 거래를 안전하고 효율적으로 처리합니다.
    /// </summary>
    public class ShopService : IShopService
    {
        private readonly IRedisService _redisService;
        private readonly IPriceService _priceService;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ShopService> _logger;

        // 시스템 상수
        private const decimal DEFAULT_STARTING_BALANCE = 1000.00m;  // 신규 플레이어 기본 잔액
        private const int MAX_TRANSACTION_QUANTITY = 10000;         // 최대 거래 수량
        private const decimal MAX_TRANSACTION_AMOUNT = 1000000.00m; // 최대 거래 금액
        private const int INFINITE_STOCK = -1;                      // 무한 재고 표시

        public ShopService(
            IRedisService redisService,
            IPriceService priceService,
            ApplicationDbContext dbContext,
            ILogger<ShopService> logger)
        {
            _redisService = redisService;
            _priceService = priceService;
            _dbContext = dbContext;
            _logger = logger;
        }

        // ============================================================================
        // 1. 핵심 거래 처리 메소드
        // ============================================================================

        public async Task<TransactionResult> SellToNpcAsync(string playerId, string itemId, int quantity)
        {
            try
            {
                _logger.LogDebug("판매 거래 시작: {PlayerId} → {ItemId} x{Quantity}", playerId, itemId, quantity);

                // 1. 거래 검증
                var validation = await ValidateSellTransactionAsync(playerId, itemId, quantity);
                if (!validation.IsValid)
                {
                    return new TransactionResult
                    {
                        Success = false,
                        Message = validation.ErrorMessage,
                        TransactionTime = DateTime.Now
                    };
                }

                // 2. 현재 판매 가격 계산
                var unitPrice = await _priceService.CalculateCurrentPriceAsync(itemId, TransactionType.SellToNpc);
                var totalAmount = unitPrice * quantity;

                // 3. 플레이어 잔액 업데이트
                var newBalance = await UpdatePlayerBalanceAsync(playerId, totalAmount);

                // 4. NPC 재고 업데이트 (NPC가 아이템을 구매하므로 재고 증가)
                var newStock = await UpdateItemStockAsync(itemId, quantity);

                // 5. 실시간 거래량 반영
                await UpdateRealTimeVolumeAsync(itemId, TransactionType.SellToNpc, quantity, playerId);

                // 6. 거래 기록 생성 및 저장
                var transaction = new ShopTransaction
                {
                    PlayerId = playerId,
                    ItemId = itemId,
                    TransactionType = TransactionType.SellToNpc,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalAmount = totalAmount,
                    DemandPressure = 0, // 나중에 시장 압력 추가
                    SupplyPressure = 0,
                    CreatedAt = DateTime.Now
                };

                // 백그라운드에서 DB 저장
                _ = Task.Run(async () => await RecordTransactionAsync(transaction));

                var result = new TransactionResult
                {
                    Success = true,
                    Message = $"{quantity}개 아이템을 {totalAmount:F2} Gold에 판매했습니다.",
                    UnitPrice = unitPrice,
                    TotalAmount = totalAmount,
                    PlayerBalanceAfter = newBalance,
                    ItemStockAfter = newStock,
                    TransactionTime = DateTime.Now,
                    TransactionId = Guid.NewGuid().ToString()
                };

                _logger.LogInformation("판매 거래 성공: {PlayerId} - {ItemId} x{Quantity} = {Amount} Gold",
                    playerId, itemId, quantity, totalAmount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "판매 거래 중 오류 발생: {PlayerId} - {ItemId}", playerId, itemId);
                return new TransactionResult
                {
                    Success = false,
                    Message = "거래 처리 중 오류가 발생했습니다.",
                    TransactionTime = DateTime.Now
                };
            }
        }

        public async Task<TransactionResult> BuyFromNpcAsync(string playerId, string itemId, int quantity)
        {
            try
            {
                _logger.LogDebug("구매 거래 시작: {PlayerId} ← {ItemId} x{Quantity}", playerId, itemId, quantity);

                // 1. 거래 검증
                var validation = await ValidateBuyTransactionAsync(playerId, itemId, quantity);
                if (!validation.IsValid)
                {
                    return new TransactionResult
                    {
                        Success = false,
                        Message = validation.ErrorMessage,
                        TransactionTime = DateTime.Now
                    };
                }

                // 2. 현재 구매 가격 계산
                var unitPrice = await _priceService.CalculateCurrentPriceAsync(itemId, TransactionType.BuyFromNpc);
                var totalAmount = unitPrice * quantity;

                // 3. 플레이어 잔액 차감
                var newBalance = await UpdatePlayerBalanceAsync(playerId, -totalAmount);

                // 4. NPC 재고 감소 (무한 재고가 아닌 경우)
                var newStock = await UpdateItemStockAsync(itemId, -quantity);

                // 5. 실시간 거래량 반영
                await UpdateRealTimeVolumeAsync(itemId, TransactionType.BuyFromNpc, quantity, playerId);

                // 6. 거래 기록 생성 및 저장
                var transaction = new ShopTransaction
                {
                    PlayerId = playerId,
                    ItemId = itemId,
                    TransactionType = TransactionType.BuyFromNpc,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalAmount = totalAmount,
                    DemandPressure = 0,
                    SupplyPressure = 0,
                    CreatedAt = DateTime.Now
                };

                _ = Task.Run(async () => await RecordTransactionAsync(transaction));

                var result = new TransactionResult
                {
                    Success = true,
                    Message = $"{quantity}개 아이템을 {totalAmount:F2} Gold에 구매했습니다.",
                    UnitPrice = unitPrice,
                    TotalAmount = totalAmount,
                    PlayerBalanceAfter = newBalance,
                    ItemStockAfter = newStock,
                    TransactionTime = DateTime.Now,
                    TransactionId = Guid.NewGuid().ToString()
                };

                _logger.LogInformation("구매 거래 성공: {PlayerId} - {ItemId} x{Quantity} = {Amount} Gold",
                    playerId, itemId, quantity, totalAmount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "구매 거래 중 오류 발생: {PlayerId} - {ItemId}", playerId, itemId);
                return new TransactionResult
                {
                    Success = false,
                    Message = "거래 처리 중 오류가 발생했습니다.",
                    TransactionTime = DateTime.Now
                };
            }
        }

        public async Task<List<TransactionResult>> ProcessBatchTransactionsAsync(string playerId, List<TransactionRequest> transactions)
        {
            var results = new List<TransactionResult>();

            try
            {
                _logger.LogDebug("배치 거래 시작: {PlayerId} - {Count}개 거래", playerId, transactions.Count);

                // 1. 전체 배치 검증
                var batchValidation = await ValidateBatchTransactionsAsync(playerId, transactions);
                if (!batchValidation.IsValid)
                {
                    // 전체 실패 시 모든 거래에 동일한 오류 반환
                    foreach (var transaction in transactions)
                    {
                        results.Add(new TransactionResult
                        {
                            Success = false,
                            Message = batchValidation.ErrorMessage,
                            TransactionTime = DateTime.Now
                        });
                    }
                    return results;
                }

                // 2. 개별 거래 처리
                foreach (var transactionRequest in transactions)
                {
                    try
                    {
                        TransactionResult result;

                        if (transactionRequest.TransactionType == TransactionType.SellToNpc)
                        {
                            result = await SellToNpcAsync(playerId, transactionRequest.ItemId, transactionRequest.Quantity);
                        }
                        else
                        {
                            result = await BuyFromNpcAsync(playerId, transactionRequest.ItemId, transactionRequest.Quantity);
                        }

                        results.Add(result);

                        // 실패한 거래가 있으면 로그 기록
                        if (!result.Success)
                        {
                            _logger.LogWarning("배치 거래 중 개별 실패: {ItemId} - {Message}",
                                transactionRequest.ItemId, result.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "배치 거래 중 개별 오류: {ItemId}", transactionRequest.ItemId);
                        results.Add(new TransactionResult
                        {
                            Success = false,
                            Message = "개별 거래 처리 중 오류가 발생했습니다.",
                            TransactionTime = DateTime.Now
                        });
                    }
                }

                var successCount = results.Count(r => r.Success);
                _logger.LogInformation("배치 거래 완료: {PlayerId} - {Success}/{Total}개 성공",
                    playerId, successCount, transactions.Count);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "배치 거래 처리 중 오류: {PlayerId}", playerId);

                // 전체 실패 시 빈 결과 또는 오류 결과 반환
                foreach (var transaction in transactions)
                {
                    results.Add(new TransactionResult
                    {
                        Success = false,
                        Message = "배치 거래 처리 중 시스템 오류가 발생했습니다.",
                        TransactionTime = DateTime.Now
                    });
                }

                return results;
            }
        }

        // ============================================================================
        // 2. 플레이어 잔액 관리 메소드
        // ============================================================================

        public async Task<decimal> GetPlayerBalanceAsync(string playerId)
        {
            try
            {
                var balance = await GetPlayerBalanceInternalAsync(playerId);

                if (balance == null)
                {
                    // 신규 플레이어인 경우 기본 잔액 설정
                    await SetPlayerBalanceInternalAsync(playerId, DEFAULT_STARTING_BALANCE);
                    _logger.LogInformation("신규 플레이어 기본 잔액 설정: {PlayerId} = {Balance}",
                        playerId, DEFAULT_STARTING_BALANCE);
                    return DEFAULT_STARTING_BALANCE;
                }

                return balance.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "플레이어 잔액 조회 중 오류: {PlayerId}", playerId);
                return 0m;
            }
        }

        public async Task<decimal> UpdatePlayerBalanceAsync(string playerId, decimal amount)
        {
            try
            {
                var currentBalance = await GetPlayerBalanceAsync(playerId);
                var newBalance = currentBalance + amount;

                // 음수 잔액 방지
                if (newBalance < 0)
                {
                    _logger.LogWarning("음수 잔액 시도: {PlayerId} - {Current} + {Amount} = {New}",
                        playerId, currentBalance, amount, newBalance);
                    throw new InvalidOperationException("잔액이 부족합니다.");
                }

                await SetPlayerBalanceInternalAsync(playerId, newBalance);

                _logger.LogDebug("플레이어 잔액 업데이트: {PlayerId} {Old} → {New} ({Change:+#;-#;0})",
                    playerId, currentBalance, newBalance, amount);

                return newBalance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "플레이어 잔액 업데이트 중 오류: {PlayerId}", playerId);
                throw;
            }
        }

        public async Task<bool> SetPlayerBalanceAsync(string playerId, decimal newBalance)
        {
            try
            {
                if (newBalance < 0)
                {
                    _logger.LogWarning("음수 잔액 설정 시도: {PlayerId} = {Balance}", playerId, newBalance);
                    return false;
                }

                await SetPlayerBalanceAsync(playerId, newBalance);

                _logger.LogInformation("플레이어 잔액 설정: {PlayerId} = {Balance}", playerId, newBalance);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "플레이어 잔액 설정 중 오류: {PlayerId}", playerId);
                return false;
            }
        }

        // ============================================================================
        // 3. 아이템 재고 관리 메소드
        // ============================================================================

        public async Task<int> GetItemStockAsync(string itemId)
        {
            try
            {
                // Redis에서 재고 조회, 없으면 무한 재고로 간주
                var stock = await _redisService.GetHashFieldAsync($"item_stock", itemId);

                if (stock.HasValue && int.TryParse(stock.Value.ToString(), out int stockValue))
                {
                    return stockValue;
                }

                return INFINITE_STOCK; // 무한 재고
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 재고 조회 중 오류: {ItemId}", itemId);
                return INFINITE_STOCK;
            }
        }

        public async Task<int> UpdateItemStockAsync(string itemId, int quantityChange)
        {
            try
            {
                var currentStock = await GetItemStockAsync(itemId);

                // 무한 재고인 경우 변경하지 않음
                if (currentStock == INFINITE_STOCK)
                {
                    return INFINITE_STOCK;
                }

                var newStock = currentStock + quantityChange;

                // 음수 재고 방지
                if (newStock < 0)
                {
                    _logger.LogWarning("음수 재고 시도: {ItemId} - {Current} + {Change} = {New}",
                        itemId, currentStock, quantityChange, newStock);
                    newStock = 0;
                }

                await _redisService.SetHashFieldAsync("item_stock", itemId, newStock.ToString());

                _logger.LogDebug("아이템 재고 업데이트: {ItemId} {Old} → {New} ({Change:+#;-#;0})",
                    itemId, currentStock, newStock, quantityChange);

                return newStock;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 재고 업데이트 중 오류: {ItemId}", itemId);
                return 0;
            }
        }

        public async Task<Dictionary<string, int>> GetMultipleItemStocksAsync(IEnumerable<string> itemIds)
        {
            var result = new Dictionary<string, int>();

            try
            {
                var itemList = itemIds.ToList();
                var stockFields = await _redisService.GetHashFieldsAsync("item_stock", itemList.ToArray());

                foreach (var itemId in itemList)
                {
                    if (stockFields.ContainsKey(itemId) &&
                        int.TryParse(stockFields[itemId].ToString(), out int stock))
                    {
                        result[itemId] = stock;
                    }
                    else
                    {
                        result[itemId] = INFINITE_STOCK; // 기본값: 무한 재고
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "다중 아이템 재고 조회 중 오류");
                return result;
            }
        }

        // ============================================================================
        // 4. 거래 검증 메소드
        // ============================================================================

        public async Task<Interfaces.ValidationResult> ValidateSellTransactionAsync(string playerId, string itemId, int quantity)
        {
            try
            {
                // 1. 기본 유효성 검사
                if (string.IsNullOrEmpty(playerId))
                {
                    return new Interfaces.ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "플레이어 ID가 유효하지 않습니다.",
                        ErrorCode = "INVALID_PLAYER"
                    };
                }

                if (string.IsNullOrEmpty(itemId))
                {
                    return new Interfaces.ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "아이템 ID가 유효하지 않습니다.",
                        ErrorCode = "INVALID_ITEM"
                    };
                }

                if (quantity <= 0 || quantity > MAX_TRANSACTION_QUANTITY)
                {
                    return new Interfaces.ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"수량은 1개 이상 {MAX_TRANSACTION_QUANTITY}개 이하여야 합니다.",
                        ErrorCode = "INVALID_QUANTITY"
                    };
                }

                // 2. 아이템 존재 확인
                var priceInfo = await _redisService.GetCurrentPriceAsync(itemId);
                if (priceInfo == null)
                {
                    return new Interfaces.ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "해당 아이템은 거래할 수 없습니다.",
                        ErrorCode = "ITEM_NOT_TRADEABLE"
                    };
                }

                // 3. 거래 금액 제한 확인
                var unitPrice = await _priceService.CalculateCurrentPriceAsync(itemId, TransactionType.SellToNpc);
                var totalAmount = unitPrice * quantity;

                if (totalAmount > MAX_TRANSACTION_AMOUNT)
                {
                    return new Interfaces.ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"거래 금액이 한도({MAX_TRANSACTION_AMOUNT:F0} Gold)를 초과합니다.",
                        ErrorCode = "AMOUNT_LIMIT_EXCEEDED"
                    };
                }

                return new Interfaces.ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "판매 거래 검증 중 오류: {PlayerId} - {ItemId}", playerId, itemId);
                return new Interfaces.ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "거래 검증 중 오류가 발생했습니다.",
                    ErrorCode = "VALIDATION_ERROR"
                };
            }
        }

        public async Task<Interfaces.ValidationResult> ValidateBuyTransactionAsync(string playerId, string itemId, int quantity)
        {
            try
            {
                // 1. 기본 판매 검증 먼저 수행
                var basicValidation = await ValidateSellTransactionAsync(playerId, itemId, quantity);
                if (!basicValidation.IsValid)
                {
                    return basicValidation;
                }

                // 2. 플레이어 잔액 확인
                var playerBalance = await GetPlayerBalanceAsync(playerId);
                var unitPrice = await _priceService.CalculateCurrentPriceAsync(itemId, TransactionType.BuyFromNpc);
                var totalCost = unitPrice * quantity;

                if (playerBalance < totalCost)
                {
                    return new Interfaces.ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"잔액이 부족합니다. (필요: {totalCost:F2} Gold, 보유: {playerBalance:F2} Gold)",
                        ErrorCode = "INSUFFICIENT_BALANCE"
                    };
                }

                // 3. NPC 재고 확인
                var itemStock = await GetItemStockAsync(itemId);
                if (itemStock != INFINITE_STOCK && itemStock < quantity)
                {
                    return new Interfaces.ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"재고가 부족합니다. (요청: {quantity}개, 재고: {itemStock}개)",
                        ErrorCode = "INSUFFICIENT_STOCK"
                    };
                }

                return new Interfaces.ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "구매 거래 검증 중 오류: {PlayerId} - {ItemId}", playerId, itemId);
                return new Interfaces.ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "거래 검증 중 오류가 발생했습니다.",
                    ErrorCode = "VALIDATION_ERROR"
                };
            }
        }

        public async Task<BatchValidationResult> ValidateBatchTransactionsAsync(string playerId, List<TransactionRequest> transactions)
        {
            try
            {
                var result = new BatchValidationResult { IsValid = true };
                decimal totalCost = 0m;
                var playerBalance = await GetPlayerBalanceAsync(playerId);

                // 각 개별 거래 검증
                foreach (var transaction in transactions)
                {
                    Interfaces.ValidationResult individualResult;

                    if (transaction.TransactionType == TransactionType.SellToNpc)
                    {
                        individualResult = await ValidateSellTransactionAsync(playerId, transaction.ItemId, transaction.Quantity);
                    }
                    else
                    {
                        individualResult = await ValidateBuyTransactionAsync(playerId, transaction.ItemId, transaction.Quantity);

                        // 구매 거래인 경우 총 비용 계산
                        if (individualResult.IsValid)
                        {
                            var unitPrice = await _priceService.CalculateCurrentPriceAsync(transaction.ItemId, TransactionType.BuyFromNpc);
                            totalCost += unitPrice * transaction.Quantity;
                        }
                    }

                    result.IndividualResults.Add(individualResult);

                    if (!individualResult.IsValid)
                    {
                        result.IsValid = false;
                    }
                }

                // 전체 구매 비용이 잔액을 초과하는지 확인
                if (totalCost > playerBalance)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"전체 구매 비용이 잔액을 초과합니다. (필요: {totalCost:F2} Gold, 보유: {playerBalance:F2} Gold)";
                }

                result.TotalCost = totalCost;
                result.PlayerBalance = playerBalance;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "배치 거래 검증 중 오류: {PlayerId}", playerId);
                return new BatchValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "배치 거래 검증 중 오류가 발생했습니다."
                };
            }
        }

        // ============================================================================
        // 5. 거래 기록 및 히스토리 메소드
        // ============================================================================

        public async Task<TransactionHistory> GetPlayerTransactionHistoryAsync(
            string playerId,
            int pageNumber = 1,
            int pageSize = 20,
            TransactionType? transactionType = null)
        {
            try
            {
                var query = _dbContext.ShopTransactions
                    .Where(t => t.PlayerId == playerId);

                if (transactionType.HasValue)
                {
                    query = query.Where(t => t.TransactionType == transactionType.Value);
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var transactions = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var totalVolume = await query.SumAsync(t => t.TotalAmount);
                var averageTransactionSize = totalCount > 0 ? totalVolume / totalCount : 0m;

                return new TransactionHistory
                {
                    Transactions = transactions,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    TotalVolume = totalVolume,
                    AverageTransactionSize = averageTransactionSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "거래 히스토리 조회 중 오류: {PlayerId}", playerId);
                return new TransactionHistory();
            }
        }

        public async Task<ItemTransactionStats> GetItemTransactionStatsAsync(string itemId)
        {
            try
            {
                var now = DateTime.Now;
                var day1Ago = now.AddDays(-1);
                var day7Ago = now.AddDays(-7);
                var day30Ago = now.AddDays(-30);

                var item = await _dbContext.ShopItems.FirstOrDefaultAsync(i => i.ItemId == itemId);
                var itemName = item?.DisplayName ?? itemId;

                // 24시간 통계
                var stats24h = await _dbContext.ShopTransactions
                    .Where(t => t.ItemId == itemId && t.CreatedAt >= day1Ago)
                    .GroupBy(t => 1)
                    .Select(g => new
                    {
                        Count = g.Count(),
                        Volume = g.Sum(t => t.TotalAmount),
                        AvgPrice = g.Average(t => t.UnitPrice)
                    })
                    .FirstOrDefaultAsync();

                // 7일 통계
                var stats7d = await _dbContext.ShopTransactions
                    .Where(t => t.ItemId == itemId && t.CreatedAt >= day7Ago)
                    .GroupBy(t => 1)
                    .Select(g => new
                    {
                        Count = g.Count(),
                        Volume = g.Sum(t => t.TotalAmount),
                        AvgPrice = g.Average(t => t.UnitPrice)
                    })
                    .FirstOrDefaultAsync();

                // 30일 통계
                var stats30d = await _dbContext.ShopTransactions
                    .Where(t => t.ItemId == itemId && t.CreatedAt >= day30Ago)
                    .GroupBy(t => 1)
                    .Select(g => new
                    {
                        Count = g.Count(),
                        Volume = g.Sum(t => t.TotalAmount),
                        AvgPrice = g.Average(t => t.UnitPrice)
                    })
                    .FirstOrDefaultAsync();

                return new ItemTransactionStats
                {
                    ItemId = itemId,
                    DisplayName = itemName,

                    Transactions24h = stats24h?.Count ?? 0,
                    Volume24h = stats24h?.Volume ?? 0m,
                    AvgPrice24h = stats24h?.AvgPrice ?? 0m,

                    Transactions7d = stats7d?.Count ?? 0,
                    Volume7d = stats7d?.Volume ?? 0m,
                    AvgPrice7d = stats7d?.AvgPrice ?? 0m,

                    Transactions30d = stats30d?.Count ?? 0,
                    Volume30d = stats30d?.Volume ?? 0m,
                    AvgPrice30d = stats30d?.AvgPrice ?? 0m,

                    LastUpdated = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 거래 통계 조회 중 오류: {ItemId}", itemId);
                return new ItemTransactionStats
                {
                    ItemId = itemId,
                    LastUpdated = DateTime.Now
                };
            }
        }

        public async Task<bool> RecordTransactionAsync(ShopTransaction transaction)
        {
            try
            {
                _dbContext.ShopTransactions.Add(transaction);
                await _dbContext.SaveChangesAsync();

                _logger.LogDebug("거래 기록 저장 완료: {TransactionId}", transaction.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "거래 기록 저장 중 오류: {PlayerId} - {ItemId}",
                   transaction.PlayerId, transaction.ItemId);
                return false;
            }
        }

        // ============================================================================
        // 6. 실시간 거래량 반영 메소드
        // ============================================================================

        public async Task<bool> UpdateRealTimeVolumeAsync(string itemId, TransactionType transactionType, int quantity, string playerId)
        {
            try
            {
                // 현재 10분 구간 타임스탬프 생성
                var currentTimestamp = DateTime.Now.ToString("yyyyMMddHHmm");

                // 플레이어 세션 가중치 계산
                var sessionWeight = await _priceService.CalculateSessionWeightFactorAsync(playerId);
                var timeWeight = _priceService.CalculateTimeWeightFactor();
                var playerCorrectionFactor = await _priceService.CalculatePlayerCorrectionFactorAsync();

                // 가중치 적용된 거래량 계산
                var weightedQuantity = quantity * sessionWeight * timeWeight * playerCorrectionFactor;

                // Redis에 10분 구간 거래량 업데이트
                var key = $"trades_10min:{itemId}:{currentTimestamp}";

                if (transactionType == TransactionType.BuyFromNpc)
                {
                    await _redisService.SetHashFieldAsync(key, "buy", quantity.ToString());
                    await _redisService.SetHashFieldAsync(key, "weighted_buy", weightedQuantity.ToString("F2"));
                }
                else
                {
                    await _redisService.SetHashFieldAsync(key, "sell", quantity.ToString());
                    await _redisService.SetHashFieldAsync(key, "weighted_sell", weightedQuantity.ToString("F2"));
                }

                // TTL 설정 (1시간)
                await _redisService.SetStringAsync($"{key}:ttl", "1", TimeSpan.FromHours(1));

                _logger.LogDebug("실시간 거래량 반영: {ItemId} {Type} {Quantity} (가중치: {Weight:F2})",
                    itemId, transactionType, quantity, weightedQuantity);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "실시간 거래량 반영 중 오류: {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> AggregateVolumeDataAsync(string itemId, string timestamp)
        {
            try
            {
                // 해당 10분 구간의 모든 거래량 데이터 집계
                var key = $"trades_10min:{itemId}:{timestamp}";
                var fields = await _redisService.GetHashFieldsAsync(key,
                    "buy", "sell", "weighted_buy", "weighted_sell");

                var buyVolume = fields.ContainsKey("buy") ?
                    double.Parse(fields["buy"].ToString()) : 0.0;
                var sellVolume = fields.ContainsKey("sell") ?
                    double.Parse(fields["sell"].ToString()) : 0.0;
                var weightedBuyVolume = fields.ContainsKey("weighted_buy") ?
                    double.Parse(fields["weighted_buy"].ToString()) : 0.0;
                var weightedSellVolume = fields.ContainsKey("weighted_sell") ?
                    double.Parse(fields["weighted_sell"].ToString()) : 0.0;

                // 집계 결과를 별도 키에 저장
                var aggregateKey = $"volume_aggregate:{itemId}:{timestamp}";
                var aggregateData = new Dictionary<string, object>
                {
                    ["buy_volume"] = buyVolume,
                    ["sell_volume"] = sellVolume,
                    ["weighted_buy_volume"] = weightedBuyVolume,
                    ["weighted_sell_volume"] = weightedSellVolume,
                    ["total_volume"] = buyVolume + sellVolume,
                    ["net_volume"] = buyVolume - sellVolume,
                    ["aggregated_at"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                var jsonData = JsonSerializer.Serialize(aggregateData);
                await _redisService.SetStringAsync(aggregateKey, jsonData, TimeSpan.FromHours(6));

                _logger.LogDebug("거래량 집계 완료: {ItemId}:{Timestamp} - 구매: {Buy}, 판매: {Sell}",
                    itemId, timestamp, buyVolume, sellVolume);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "거래량 집계 중 오류: {ItemId}:{Timestamp}", itemId, timestamp);
                return false;
            }
        }

        // ============================================================================
        // 7. 상점 정보 및 설정 메소드
        // ============================================================================

        public async Task<List<ShopItemInfo>> GetAvailableItemsAsync(string? category = null)
        {
            try
            {
                var query = _dbContext.ShopItems.Where(i => i.IsActive);

                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(i => i.Category.ToString() == category);
                }

                var items = await query.ToListAsync();
                var result = new List<ShopItemInfo>();

                foreach (var item in items)
                {
                    try
                    {
                        var buyPrice = await _priceService.CalculateCurrentPriceAsync(item.ItemId, TransactionType.BuyFromNpc);
                        var sellPrice = await _priceService.CalculateCurrentPriceAsync(item.ItemId, TransactionType.SellToNpc);
                        var stock = await GetItemStockAsync(item.ItemId);

                        // 24시간 가격 변동률 계산 (간단 버전)
                        var priceChange = 0.0; // 실제로는 가격 히스토리에서 계산

                        // 24시간 거래량
                        var volume24h = await _dbContext.ShopTransactions
                            .Where(t => t.ItemId == item.ItemId && t.CreatedAt >= DateTime.Now.AddDays(-1))
                            .SumAsync(t => t.Quantity);

                        result.Add(new ShopItemInfo
                        {
                            ItemId = item.ItemId,
                            DisplayName = item.DisplayName,
                            Category = item.Category.ToString(),
                            CurrentBuyPrice = buyPrice,
                            CurrentSellPrice = sellPrice,
                            Stock = stock,
                            IsInfiniteStock = stock == INFINITE_STOCK,
                            PriceChangePercent24h = priceChange,
                            TransactionVolume24h = volume24h
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "아이템 정보 처리 중 오류: {ItemId}", item.ItemId);
                    }
                }

                _logger.LogDebug("상점 아이템 목록 조회 완료: {Count}개 (카테고리: {Category})",
                    result.Count, category ?? "전체");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "상점 아이템 목록 조회 중 오류");
                return new List<ShopItemInfo>();
            }
        }

        public async Task<ShopItemDetail> GetItemDetailAsync(string itemId)
        {
            try
            {
                var item = await _dbContext.ShopItems
                    .FirstOrDefaultAsync(i => i.ItemId == itemId && i.IsActive);

                if (item == null)
                {
                    return new ShopItemDetail { ItemId = itemId };
                }

                var buyPrice = await _priceService.CalculateCurrentPriceAsync(itemId, TransactionType.BuyFromNpc);
                var sellPrice = await _priceService.CalculateCurrentPriceAsync(itemId, TransactionType.SellToNpc);
                var stock = await GetItemStockAsync(itemId);
                var marketPressure = await _priceService.CalculateMarketPressureAsync(itemId);

                // 최근 가격 히스토리 (간단 버전)
                var recentHistory = new List<PriceChartData>();
                // 실제로는 Redis나 DB에서 최근 가격 데이터를 조회

                return new ShopItemDetail
                {
                    ItemId = item.ItemId,
                    DisplayName = item.DisplayName,
                    Category = item.Category.ToString(),
                    CurrentBuyPrice = buyPrice,
                    CurrentSellPrice = sellPrice,
                    BasePrice = item.BaseSellPrice,
                    Stock = stock,
                    IsInfiniteStock = stock == INFINITE_STOCK,
                    HungerRestore = item.HungerRestore,
                    SaturationRestore = item.SaturationRestore,
                    ComplexityLevel = item.ComplexityLevel.ToString(),
                    MinPrice = item.MinPrice,
                    MaxPrice = item.MaxPrice,
                    CurrentDemandPressure = marketPressure.DemandPressure,
                    CurrentSupplyPressure = marketPressure.SupplyPressure,
                    RecentPriceHistory = recentHistory
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 상세 정보 조회 중 오류: {ItemId}", itemId);
                return new ShopItemDetail { ItemId = itemId };
            }
        }

        public async Task<List<PriceChartData>> GetPriceChartDataAsync(string itemId, TimePeriod period)
        {
            try
            {
                // 실제로는 가격 히스토리 테이블이나 Redis에서 데이터 조회
                // 현재는 기본 구현만 제공
                var result = new List<PriceChartData>();

                var endTime = DateTime.Now;
                var startTime = period switch
                {
                    TimePeriod.HOUR => endTime.AddHours(-1),
                    TimePeriod.DAY => endTime.AddDays(-1),
                    TimePeriod.WEEK => endTime.AddDays(-7),
                    TimePeriod.MONTH => endTime.AddDays(-30),
                    _ => endTime.AddDays(-1)
                };

                // 샘플 데이터 생성 (실제로는 DB에서 조회)
                var interval = period switch
                {
                    TimePeriod.HOUR => TimeSpan.FromMinutes(10),
                    TimePeriod.DAY => TimeSpan.FromHours(1),
                    TimePeriod.WEEK => TimeSpan.FromHours(6),
                    TimePeriod.MONTH => TimeSpan.FromDays(1),
                    _ => TimeSpan.FromHours(1)
                };

                var currentTime = startTime;
                while (currentTime <= endTime)
                {
                    // 임시 데이터 - 실제로는 해당 시점의 가격과 거래량 조회
                    result.Add(new PriceChartData
                    {
                        Timestamp = currentTime,
                        Price = 10.0m, // 실제 가격 데이터
                        Volume = 100,   // 실제 거래량 데이터
                        MarketPressure = 0.0 // 실제 시장 압력 데이터
                    });

                    currentTime = currentTime.Add(interval);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 차트 데이터 조회 중 오류: {ItemId}", itemId);
                return new List<PriceChartData>();
            }
        }

        // ============================================================================
        // 8. 관리자 기능 메소드
        // ============================================================================

        public async Task<bool> AddItemToShopAsync(ShopItem shopItem)
        {
            try
            {
                _dbContext.ShopItems.Add(shopItem);
                await _dbContext.SaveChangesAsync();

                // Redis에 가격 정보 초기화
                var priceData = new Dictionary<string, object>
                {
                    ["current"] = shopItem.BaseSellPrice,
                    ["base"] = shopItem.BaseSellPrice,
                    ["updated"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                var jsonData = JsonSerializer.Serialize(priceData);
                await _redisService.SetStringAsync($"price:{shopItem.ItemId}", jsonData);

                // 활성 아이템 목록에 추가
                await _redisService.AddToSetAsync("active_items", shopItem.ItemId);

                _logger.LogInformation("새 아이템 상점 추가: {ItemId} - {Name}",
                    shopItem.ItemId, shopItem.DisplayName);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 상점 추가 중 오류: {ItemId}", shopItem.ItemId);
                return false;
            }
        }

        public async Task<bool> UpdateItemBasePriceAsync(string itemId, decimal newBasePrice)
        {
            try
            {
                var item = await _dbContext.ShopItems
                    .FirstOrDefaultAsync(i => i.ItemId == itemId);

                if (item == null)
                {
                    _logger.LogWarning("존재하지 않는 아이템: {ItemId}", itemId);
                    return false;
                }

                // DB 업데이트
                item.BaseSellPrice = newBasePrice;
                item.MinPrice = newBasePrice * 0.5m;
                item.MaxPrice = newBasePrice * 3.0m;
                item.UpdatedAt = DateTime.Now;

                await _dbContext.SaveChangesAsync();

                // Redis 가격 정보 업데이트
                var currentPriceInfo = await _redisService.GetCurrentPriceAsync(itemId);
                if (currentPriceInfo.HasValue)
                {
                    var priceData = new Dictionary<string, object>
                    {
                        ["current"] = currentPriceInfo.Value.currentPrice,
                        ["base"] = newBasePrice,
                        ["updated"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    };

                    var jsonData = JsonSerializer.Serialize(priceData);
                    await _redisService.SetStringAsync($"price:{itemId}", jsonData);
                }

                _logger.LogInformation("아이템 기본 가격 업데이트: {ItemId} = {NewPrice}",
                    itemId, newBasePrice);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 기본 가격 업데이트 중 오류: {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> SetItemActiveStatusAsync(string itemId, bool isActive)
        {
            try
            {
                var item = await _dbContext.ShopItems
                    .FirstOrDefaultAsync(i => i.ItemId == itemId);

                if (item == null)
                {
                    return false;
                }

                item.IsActive = isActive;
                item.UpdatedAt = DateTime.Now;

                await _dbContext.SaveChangesAsync();

                // Redis 활성 아이템 목록 업데이트
                if (isActive)
                {
                    await _redisService.AddToSetAsync("active_items", itemId);
                }
                else
                {
                    // Set에서 제거하는 메소드가 필요함 (RedisService에 추가 필요)
                    // await _redisService.RemoveFromSetAsync("active_items", itemId);
                }

                _logger.LogInformation("아이템 활성 상태 변경: {ItemId} = {Status}",
                    itemId, isActive ? "활성" : "비활성");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 활성 상태 변경 중 오류: {ItemId}", itemId);
                return false;
            }
        }

        // ============================================================================
        // 9. 대시보드 및 분석 메소드
        // ============================================================================

        public async Task<ShopDashboardData> GetDashboardDataAsync()
        {
            try
            {
                var yesterday = DateTime.Now.AddDays(-1);

                // 24시간 통계
                var transactions24h = await _dbContext.ShopTransactions
                    .Where(t => t.CreatedAt >= yesterday)
                    .CountAsync();

                var volume24h = await _dbContext.ShopTransactions
                    .Where(t => t.CreatedAt >= yesterday)
                    .SumAsync(t => t.TotalAmount);

                var activePlayers24h = await _dbContext.ShopTransactions
                    .Where(t => t.CreatedAt >= yesterday)
                    .Select(t => t.PlayerId)
                    .Distinct()
                    .CountAsync();

                var activeItems = await _dbContext.ShopItems
                    .Where(i => i.IsActive)
                    .CountAsync();

                var avgTransactionSize = transactions24h > 0 ? volume24h / transactions24h : 0m;

                // 인기 아이템 Top 5
                var topTradedItems = await GetTopTradedItemsAsync(5);

                // 변동성 높은 아이템 Top 5 (간단 구현)
                var volatileItems = await GetMostVolatileItemsAsync(5);

                // 전체 시장 안정성
                var marketHealth = await _priceService.GetMarketHealthAsync();

                return new ShopDashboardData
                {
                    TotalTransactions24h = transactions24h,
                    TotalVolume24h = volume24h,
                    ActivePlayers24h = activePlayers24h,
                    ActiveItems = activeItems,
                    AverageTransactionSize = avgTransactionSize,
                    TopTradedItems = topTradedItems,
                    MostVolatileItems = volatileItems,
                    OverallMarketStability = marketHealth.OverallStability,
                    LastUpdated = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "대시보드 데이터 조회 중 오류");
                return new ShopDashboardData
                {
                    LastUpdated = DateTime.Now
                };
            }
        }

        public async Task<EconomyHealthReport> GetEconomyHealthReportAsync()
        {
            try
            {
                // 기본 경제 건강도 분석 구현
                // 실제로는 더 복잡한 알고리즘 필요

                var marketHealth = await _priceService.GetMarketHealthAsync();
                var yesterday = DateTime.Now.AddDays(-1);

                // 거래 활성도 계산
                var recentTransactions = await _dbContext.ShopTransactions
                    .Where(t => t.CreatedAt >= yesterday)
                    .CountAsync();

                var tradingActivityScore = Math.Min(1.0, recentTransactions / 1000.0); // 임시 공식

                // 플레이어 참여도 계산
                var activePlayersCount = await _dbContext.ShopTransactions
                    .Where(t => t.CreatedAt >= yesterday)
                    .Select(t => t.PlayerId)
                    .Distinct()
                    .CountAsync();

                var participationScore = Math.Min(1.0, activePlayersCount / 50.0); // 임시 공식

                // 시장 다양성 계산
                var tradedItemsCount = await _dbContext.ShopTransactions
                    .Where(t => t.CreatedAt >= yesterday)
                    .Select(t => t.ItemId)
                    .Distinct()
                    .CountAsync();

                var diversityScore = Math.Min(1.0, tradedItemsCount / 20.0); // 임시 공식

                var overallScore = (marketHealth.OverallStability + tradingActivityScore +
                                  participationScore + diversityScore) / 4.0;

                // 문제점 및 권장사항 생성
                var issues = new List<string>();
                var recommendations = new List<string>();

                if (marketHealth.OverallStability < 0.7)
                {
                    issues.Add("가격 변동성이 높습니다.");
                    recommendations.Add("가격 안정화 메커니즘을 강화하세요.");
                }

                if (tradingActivityScore < 0.5)
                {
                    issues.Add("거래 활성도가 낮습니다.");
                    recommendations.Add("이벤트나 혜택을 통해 거래를 촉진하세요.");
                }

                return new EconomyHealthReport
                {
                    OverallHealthScore = overallScore,
                    PriceStabilityScore = marketHealth.OverallStability,
                    TradingActivityScore = tradingActivityScore,
                    MarketDiversityScore = diversityScore,
                    PlayerParticipationScore = participationScore,
                    HealthIssues = issues,
                    Recommendations = recommendations,
                    GeneratedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "경제 건강도 분석 중 오류");
                return new EconomyHealthReport
                {
                    OverallHealthScore = 0.5,
                    GeneratedAt = DateTime.Now
                };
            }
        }

        // ============================================================================
        // 내부 헬퍼 메소드
        // ============================================================================

        /// <summary>
        /// 플레이어 잔액을 조회합니다. (기존 메소드 래핑)
        /// </summary>
        private async Task<decimal?> GetPlayerBalanceInternalAsync(string playerId)
        {
            try
            {
                var balance = await _redisService.GetHashFieldAsync($"player:{playerId}", "balance");

                if (balance.HasValue && decimal.TryParse(balance.Value.ToString(), out decimal balanceValue))
                {
                    return balanceValue;
                }

                return null; // 잔액 정보 없음
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "플레이어 잔액 조회 중 오류: {PlayerId}", playerId);
                return null;
            }
        }

        /// <summary>
        /// 플레이어 잔액을 설정합니다. (기존 메소드 래핑)
        /// </summary>
        private async Task<bool> SetPlayerBalanceInternalAsync(string playerId, decimal newBalance)
        {
            try
            {
                await _redisService.SetHashFieldAsync($"player:{playerId}", "balance", newBalance.ToString("F2"));

                _logger.LogDebug("플레이어 잔액 설정: {PlayerId} = {Balance}", playerId, newBalance);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "플레이어 잔액 설정 중 오류: {PlayerId}", playerId);
                return false;
            }
        }
        private async Task<List<ShopItemInfo>> GetTopTradedItemsAsync(int count)
        {
            try
            {
                var yesterday = DateTime.Now.AddDays(-1);

                var topItems = await _dbContext.ShopTransactions
                    .Where(t => t.CreatedAt >= yesterday)
                    .GroupBy(t => t.ItemId)
                    .Select(g => new
                    {
                        ItemId = g.Key,
                        TotalVolume = g.Sum(t => t.Quantity),
                        TotalAmount = g.Sum(t => t.TotalAmount)
                    })
                    .OrderByDescending(x => x.TotalVolume)
                    .Take(count)
                    .ToListAsync();

                var result = new List<ShopItemInfo>();

                foreach (var item in topItems)
                {
                    var dbItem = await _dbContext.ShopItems
                        .FirstOrDefaultAsync(i => i.ItemId == item.ItemId);

                    if (dbItem != null)
                    {
                        var buyPrice = await _priceService.CalculateCurrentPriceAsync(item.ItemId, TransactionType.BuyFromNpc);
                        var sellPrice = await _priceService.CalculateCurrentPriceAsync(item.ItemId, TransactionType.SellToNpc);
                        var stock = await GetItemStockAsync(item.ItemId);

                        result.Add(new ShopItemInfo
                        {
                            ItemId = item.ItemId,
                            DisplayName = dbItem.DisplayName,
                            Category = dbItem.Category.ToString(),
                            CurrentBuyPrice = buyPrice,
                            CurrentSellPrice = sellPrice,
                            Stock = stock,
                            IsInfiniteStock = stock == INFINITE_STOCK,
                            TransactionVolume24h = item.TotalVolume
                        });
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "인기 아이템 조회 중 오류");
                return new List<ShopItemInfo>();
            }
        }

        private async Task<List<ShopItemInfo>> GetMostVolatileItemsAsync(int count)
        {
            try
            {
                // 간단한 변동성 계산 - 실제로는 가격 히스토리 분석 필요
                var activeItems = await _dbContext.ShopItems
                    .Where(i => i.IsActive)
                    .Take(count)
                    .ToListAsync();

                var result = new List<ShopItemInfo>();

                foreach (var item in activeItems)
                {
                    var buyPrice = await _priceService.CalculateCurrentPriceAsync(item.ItemId, TransactionType.BuyFromNpc);
                    var sellPrice = await _priceService.CalculateCurrentPriceAsync(item.ItemId, TransactionType.SellToNpc);
                    var stock = await GetItemStockAsync(item.ItemId);

                    result.Add(new ShopItemInfo
                    {
                        ItemId = item.ItemId,
                        DisplayName = item.DisplayName,
                        Category = item.Category.ToString(),
                        CurrentBuyPrice = buyPrice,
                        CurrentSellPrice = sellPrice,
                        Stock = stock,
                        IsInfiniteStock = stock == INFINITE_STOCK,
                        PriceChangePercent24h = 5.0 // 임시값
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "변동성 아이템 조회 중 오류");
                return new List<ShopItemInfo>();
            }
        }
    }
}