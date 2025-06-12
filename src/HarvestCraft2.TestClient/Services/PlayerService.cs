using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HarvestCraft2.TestClient.Models;

namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// 가상 플레이어 관리 및 자동 거래 시뮬레이션 서비스 구현체
    /// </summary>
    public class PlayerService : IPlayerService, IDisposable
    {
        private readonly IApiService _apiService;
        private readonly ILogger<PlayerService> _logger;
        private readonly ConcurrentDictionary<string, VirtualPlayer> _players;
        private readonly Random _random;
        private readonly Timer _autoTradingTimer;
        private readonly SemaphoreSlim _autoTradingSemaphore;

        private VirtualPlayer? _selectedPlayer;
        private bool _isAutoTradingEnabled = false;
        private AutoTradingSettings _autoTradingSettings;
        private CancellationTokenSource? _autoTradingCancellation;

        // ============================================================================
        // 생성자 및 초기화
        // ============================================================================

        public PlayerService(IApiService apiService, ILogger<PlayerService> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _players = new ConcurrentDictionary<string, VirtualPlayer>();
            _random = new Random();
            _autoTradingSettings = new AutoTradingSettings();
            _autoTradingSemaphore = new SemaphoreSlim(10, 10); // 최대 10개 동시 거래

            // 자동 거래 타이머 (5초마다 실행)
            _autoTradingTimer = new Timer(ExecuteAutoTradingCycle, null, Timeout.Infinite, Timeout.Infinite);

            _logger.LogInformation("PlayerService 초기화 완료");
        }

        // ============================================================================
        // 속성 및 이벤트
        // ============================================================================

        public IReadOnlyList<VirtualPlayer> Players => _players.Values.ToList();

        public VirtualPlayer? SelectedPlayer
        {
            get => _selectedPlayer;
            set
            {
                if (_selectedPlayer != value)
                {
                    _selectedPlayer = value;
                    _logger.LogInformation("선택된 플레이어 변경: {PlayerName}", value?.PlayerName ?? "없음");
                }
            }
        }

        public bool IsAutoTradingEnabled => _isAutoTradingEnabled;

        // 이벤트 정의
        public event EventHandler<PlayerAddedEventArgs>? PlayerAdded;
        public event EventHandler<PlayerRemovedEventArgs>? PlayerRemoved;
        public event EventHandler<PlayerUpdatedEventArgs>? PlayerUpdated;
        public event EventHandler<AutoTradingStatusChangedEventArgs>? AutoTradingStatusChanged;
        public event EventHandler<TradeExecutedEventArgs>? TradeExecuted;

        // ============================================================================
        // 플레이어 관리
        // ============================================================================

        public async Task<VirtualPlayer> CreatePlayerAsync(string playerName, decimal initialBalance = 1000m, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("가상 플레이어 생성 시작: {PlayerName}, 초기잔액: {Balance}", playerName, initialBalance);

                // API를 통해 실제 플레이어 생성
                var apiPlayer = await _apiService.CreatePlayerAsync(playerName, initialBalance, cancellationToken);

                var virtualPlayer = new VirtualPlayer
                {
                    PlayerId = apiPlayer.PlayerId,
                    PlayerName = apiPlayer.PlayerName,
                    Balance = apiPlayer.Balance,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    IsOnline = true,
                    IsApiPlayer = true,
                    TradingBehavior = CreateDefaultTradingBehavior(),
                    Stats = new PlayerStats()
                };

                if (_players.TryAdd(virtualPlayer.PlayerId, virtualPlayer))
                {
                    PlayerAdded?.Invoke(this, new PlayerAddedEventArgs
                    {
                        Player = virtualPlayer,
                        AddedAt = DateTime.UtcNow
                    });

                    _logger.LogInformation("가상 플레이어 생성 완료: {PlayerId}", virtualPlayer.PlayerId);
                    return virtualPlayer;
                }
                else
                {
                    throw new InvalidOperationException($"플레이어 추가 실패: {virtualPlayer.PlayerId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가상 플레이어 생성 실패: {PlayerName}", playerName);
                throw;
            }
        }

        public async Task<VirtualPlayer> LoadPlayerAsync(string playerId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("기존 플레이어 로드 시작: {PlayerId}", playerId);

                var apiPlayer = await _apiService.GetPlayerAsync(playerId, cancellationToken);

                var virtualPlayer = new VirtualPlayer
                {
                    PlayerId = apiPlayer.PlayerId,
                    PlayerName = apiPlayer.PlayerName,
                    Balance = apiPlayer.Balance,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    IsOnline = apiPlayer.IsOnline,
                    IsApiPlayer = true,
                    TradingBehavior = CreateDefaultTradingBehavior(),
                    Stats = new PlayerStats
                    {
                        TotalTransactions = apiPlayer.TotalTransactions,
                        TotalSpent = apiPlayer.TotalSpent,
                        TotalEarned = apiPlayer.TotalEarned
                    }
                };

                _players.AddOrUpdate(virtualPlayer.PlayerId, virtualPlayer, (key, existing) => virtualPlayer);

                PlayerAdded?.Invoke(this, new PlayerAddedEventArgs
                {
                    Player = virtualPlayer,
                    AddedAt = DateTime.UtcNow
                });

                _logger.LogInformation("기존 플레이어 로드 완료: {PlayerId}", playerId);
                return virtualPlayer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "기존 플레이어 로드 실패: {PlayerId}", playerId);
                throw;
            }
        }

        public async Task RefreshPlayerAsync(VirtualPlayer player, CancellationToken cancellationToken = default)
        {
            try
            {
                var apiPlayer = await _apiService.GetPlayerAsync(player.PlayerId, cancellationToken);
                var changedProperties = new List<string>();

                if (player.Balance != apiPlayer.Balance)
                {
                    player.Balance = apiPlayer.Balance;
                    changedProperties.Add(nameof(player.Balance));
                }

                if (player.IsOnline != apiPlayer.IsOnline)
                {
                    player.IsOnline = apiPlayer.IsOnline;
                    changedProperties.Add(nameof(player.IsOnline));
                }

                player.LastUpdated = DateTime.UtcNow;

                if (changedProperties.Count > 0)
                {
                    PlayerUpdated?.Invoke(this, new PlayerUpdatedEventArgs
                    {
                        Player = player,
                        ChangedProperties = changedProperties,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                _logger.LogDebug("플레이어 정보 새로고침 완료: {PlayerId}", player.PlayerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "플레이어 정보 새로고침 실패: {PlayerId}", player.PlayerId);
            }
        }

        public async Task<bool> RemovePlayerAsync(string playerId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_players.TryRemove(playerId, out var removedPlayer))
                {
                    // 자동 거래 중지
                    if (removedPlayer.IsAutoTradingEnabled)
                    {
                        removedPlayer.IsAutoTradingEnabled = false;
                        removedPlayer.AutoTradingStatus = AutoTradingStatus.Stopped;
                    }

                    PlayerRemoved?.Invoke(this, new PlayerRemovedEventArgs
                    {
                        PlayerId = playerId,
                        PlayerName = removedPlayer.PlayerName,
                        RemovedAt = DateTime.UtcNow
                    });

                    _logger.LogInformation("플레이어 제거 완료: {PlayerId}", playerId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "플레이어 제거 실패: {PlayerId}", playerId);
                return false;
            }
        }

        public async Task RefreshAllPlayersAsync(CancellationToken cancellationToken = default)
        {
            var refreshTasks = _players.Values.Select(player => RefreshPlayerAsync(player, cancellationToken));
            await Task.WhenAll(refreshTasks);
            _logger.LogInformation("모든 플레이어 정보 새로고침 완료");
        }

        // ============================================================================
        // 잔액 관리
        // ============================================================================

        public async Task<decimal> GetPlayerBalanceAsync(string playerId, CancellationToken cancellationToken = default)
        {
            var balance = await _apiService.GetPlayerBalanceAsync(playerId, cancellationToken);

            // 로컬 플레이어 정보도 업데이트
            if (_players.TryGetValue(playerId, out var player))
            {
                player.Balance = balance.Balance;
                player.LastUpdated = DateTime.UtcNow;
            }

            return balance.Balance;
        }

        public async Task<bool> SetPlayerBalanceAsync(string playerId, decimal amount, CancellationToken cancellationToken = default)
        {
            try
            {
                await _apiService.SetPlayerBalanceAsync(playerId, amount, cancellationToken);

                if (_players.TryGetValue(playerId, out var player))
                {
                    player.Balance = amount;
                    player.LastUpdated = DateTime.UtcNow;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "플레이어 잔액 설정 실패: {PlayerId}, {Amount}", playerId, amount);
                return false;
            }
        }

        public async Task<bool> AddMoneyAsync(string playerId, decimal amount, CancellationToken cancellationToken = default)
        {
            if (_players.TryGetValue(playerId, out var player))
            {
                var newBalance = player.Balance + amount;
                return await SetPlayerBalanceAsync(playerId, newBalance, cancellationToken);
            }
            return false;
        }

        public async Task<bool> DeductMoneyAsync(string playerId, decimal amount, CancellationToken cancellationToken = default)
        {
            if (_players.TryGetValue(playerId, out var player))
            {
                var newBalance = Math.Max(0, player.Balance - amount);
                return await SetPlayerBalanceAsync(playerId, newBalance, cancellationToken);
            }
            return false;
        }

        // ============================================================================
        // 거래 관리
        // ============================================================================

        public async Task<List<TransactionResponse>> GetPlayerTransactionsAsync(string playerId, int page = 1, int size = 50, CancellationToken cancellationToken = default)
        {
            return await _apiService.GetPlayerTransactionsAsync(playerId, page, size, cancellationToken);
        }

        public async Task<PurchaseResponse> PurchaseItemAsync(string playerId, string itemId, int quantity, CancellationToken cancellationToken = default)
        {
            var result = await _apiService.PurchaseItemAsync(playerId, itemId, quantity, cancellationToken);

            if (result.Success && _players.TryGetValue(playerId, out var player))
            {
                player.Balance = result.NewBalance;
                player.LastTradeTime = DateTime.UtcNow;
                player.Stats.TotalTransactions++;
                player.Stats.TotalSpent += result.TotalCost;

                TradeExecuted?.Invoke(this, new TradeExecutedEventArgs
                {
                    Result = new TransactionResult
                    {
                        Success = true,
                        TransactionId = result.TransactionId,
                        PlayerId = playerId,
                        ItemId = itemId,
                        Quantity = quantity,
                        Amount = result.TotalCost,
                        IsPurchase = true,
                        ExecutedAt = DateTime.UtcNow
                    },
                    IsAutoTrade = false,
                    ExecutedAt = DateTime.UtcNow
                });
            }

            return result;
        }

        public async Task<SellResponse> SellItemAsync(string playerId, string itemId, int quantity, CancellationToken cancellationToken = default)
        {
            var result = await _apiService.SellItemAsync(playerId, itemId, quantity, cancellationToken);

            if (result.Success && _players.TryGetValue(playerId, out var player))
            {
                player.Balance = result.NewBalance;
                player.LastTradeTime = DateTime.UtcNow;
                player.Stats.TotalTransactions++;
                player.Stats.TotalEarned += result.TotalEarned;

                TradeExecuted?.Invoke(this, new TradeExecutedEventArgs
                {
                    Result = new TransactionResult
                    {
                        Success = true,
                        TransactionId = result.TransactionId,
                        PlayerId = playerId,
                        ItemId = itemId,
                        Quantity = quantity,
                        Amount = result.TotalEarned,
                        IsPurchase = false,
                        ExecutedAt = DateTime.UtcNow
                    },
                    IsAutoTrade = false,
                    ExecutedAt = DateTime.UtcNow
                });
            }

            return result;
        }

        public async Task<BatchTradeResponse> BatchTradeAsync(string playerId, List<TradeRequest> trades, CancellationToken cancellationToken = default)
        {
            return await _apiService.BatchTradeAsync(playerId, trades, cancellationToken);
        }

        // ============================================================================
        // 자동 거래 시뮬레이션
        // ============================================================================

        public async Task StartAutoTradingAsync(AutoTradingSettings settings, CancellationToken cancellationToken = default)
        {
            try
            {
                _autoTradingSettings = settings;
                _autoTradingCancellation = new CancellationTokenSource();
                _isAutoTradingEnabled = true;

                // 타이머 시작
                _autoTradingTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(settings.GlobalTradingIntervalMs));

                AutoTradingStatusChanged?.Invoke(this, new AutoTradingStatusChangedEventArgs
                {
                    OldStatus = AutoTradingStatus.Stopped,
                    NewStatus = AutoTradingStatus.Running,
                    ChangedAt = DateTime.UtcNow
                });

                _logger.LogInformation("자동 거래 시작됨");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "자동 거래 시작 실패");
                throw;
            }
        }

        public async Task StopAutoTradingAsync()
        {
            try
            {
                _isAutoTradingEnabled = false;
                _autoTradingTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _autoTradingCancellation?.Cancel();

                // 모든 플레이어의 자동 거래 중지
                foreach (var player in _players.Values)
                {
                    if (player.IsAutoTradingEnabled)
                    {
                        player.IsAutoTradingEnabled = false;
                        player.AutoTradingStatus = AutoTradingStatus.Stopped;
                    }
                }

                AutoTradingStatusChanged?.Invoke(this, new AutoTradingStatusChangedEventArgs
                {
                    OldStatus = AutoTradingStatus.Running,
                    NewStatus = AutoTradingStatus.Stopped,
                    ChangedAt = DateTime.UtcNow
                });

                _logger.LogInformation("자동 거래 중지됨");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "자동 거래 중지 실패");
            }
        }

        public async Task SetPlayerAutoTradingAsync(string playerId, PlayerTradingBehavior behavior, CancellationToken cancellationToken = default)
        {
            if (_players.TryGetValue(playerId, out var player))
            {
                player.TradingBehavior = behavior;
                player.IsAutoTradingEnabled = true;
                player.AutoTradingStatus = _isAutoTradingEnabled ? AutoTradingStatus.Running : AutoTradingStatus.Stopped;

                _logger.LogInformation("플레이어 자동 거래 설정 완료: {PlayerId}", playerId);
            }
        }

        public async Task<TransactionResult> ExecuteRandomTradeAsync(string playerId, CancellationToken cancellationToken = default)
        {
            if (!_players.TryGetValue(playerId, out var player))
            {
                return new TransactionResult { Success = false, ErrorMessage = "플레이어를 찾을 수 없음" };
            }

            try
            {
                var behavior = player.TradingBehavior;
                var isPurchase = _random.NextDouble() < behavior.BuyProbability;
                var quantity = _random.Next(behavior.MinQuantity, behavior.MaxQuantity + 1);

                // 랜덤 아이템 선택 (실제 구현에서는 아이템 목록을 가져와야 함)
                var testItems = new[] { "harvestcraft:apple", "harvestcraft:bread", "harvestcraft:rice", "harvestcraft:cheese", "harvestcraft:milk" };
                var itemId = testItems[_random.Next(testItems.Length)];

                if (isPurchase)
                {
                    var result = await _apiService.PurchaseItemAsync(playerId, itemId, quantity, cancellationToken);
                    return new TransactionResult
                    {
                        Success = result.Success,
                        TransactionId = result.TransactionId,
                        PlayerId = playerId,
                        ItemId = itemId,
                        Quantity = quantity,
                        Amount = result.TotalCost,
                        IsPurchase = true,
                        ErrorMessage = result.ErrorMessage,
                        ExecutedAt = DateTime.UtcNow
                    };
                }
                else
                {
                    var result = await _apiService.SellItemAsync(playerId, itemId, quantity, cancellationToken);
                    return new TransactionResult
                    {
                        Success = result.Success,
                        TransactionId = result.TransactionId,
                        PlayerId = playerId,
                        ItemId = itemId,
                        Quantity = quantity,
                        Amount = result.TotalEarned,
                        IsPurchase = false,
                        ErrorMessage = result.ErrorMessage,
                        ExecutedAt = DateTime.UtcNow
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "랜덤 거래 실행 실패: {PlayerId}", playerId);
                return new TransactionResult
                {
                    Success = false,
                    PlayerId = playerId,
                    ErrorMessage = ex.Message,
                    ExecutedAt = DateTime.UtcNow
                };
            }
        }

        // ============================================================================
        // 통계 및 분석
        // ============================================================================

        public async Task<PlayerStats> GetPlayerStatsAsync(string playerId, CancellationToken cancellationToken = default)
        {
            if (_players.TryGetValue(playerId, out var player))
            {
                // API에서 최신 거래 정보 가져와서 통계 업데이트
                var transactions = await GetPlayerTransactionsAsync(playerId, 1, 100, cancellationToken);

                var stats = new PlayerStats
                {
                    TotalTransactions = transactions.Count,
                    TotalSpent = transactions.Where(t => t.IsPurchase).Sum(t => t.TotalAmount),
                    TotalEarned = transactions.Where(t => !t.IsPurchase).Sum(t => t.TotalAmount),
                    UniqueItemsTraded = transactions.Select(t => t.ItemId).Distinct().Count(),
                    FirstTradeTime = transactions.OrderBy(t => t.TransactionTime).FirstOrDefault()?.TransactionTime,
                    LastTradeTime = transactions.OrderByDescending(t => t.TransactionTime).FirstOrDefault()?.TransactionTime,
                    MostTradedItem = transactions.GroupBy(t => t.ItemId)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault()?.Key ?? string.Empty
                };

                player.Stats = stats;
                return stats;
            }

            return new PlayerStats();
        }

        public async Task<OverallPlayerStats> GetOverallStatsAsync(CancellationToken cancellationToken = default)
        {
            var players = _players.Values.ToList();

            return new OverallPlayerStats
            {
                TotalPlayers = players.Count,
                ActivePlayers = players.Count(p => p.IsOnline),
                AutoTradingPlayers = players.Count(p => p.IsAutoTradingEnabled),
                TotalBalance = players.Sum(p => p.Balance),
                AverageBalance = players.Count > 0 ? players.Average(p => p.Balance) : 0,
                TotalTransactions = players.Sum(p => p.Stats.TotalTransactions),
                TotalVolume = players.Sum(p => p.Stats.TotalSpent + p.Stats.TotalEarned),
                MostActivePlayer = players.OrderByDescending(p => p.Stats.TotalTransactions).FirstOrDefault(),
                MostProfitablePlayer = players.OrderByDescending(p => p.Stats.NetProfit).FirstOrDefault()
            };
        }

        public async Task<ProfitabilityAnalysis> AnalyzeProfitabilityAsync(string playerId, CancellationToken cancellationToken = default)
        {
            var stats = await GetPlayerStatsAsync(playerId, cancellationToken);

            return new ProfitabilityAnalysis
            {
                PlayerId = playerId,
                TotalProfit = stats.NetProfit,
                ProfitMargin = stats.ProfitMargin,
                Recommendation = new TradingRecommendation
                {
                    SuggestedRiskProfile = stats.NetProfit > 0 ? RiskProfile.Moderate : RiskProfile.Conservative,
                    Reason = stats.NetProfit > 0 ? "수익성이 좋으므로 현재 전략 유지 추천" : "손실이 발생하고 있으므로 보수적 전략 추천"
                }
            };
        }

        // ============================================================================
        // 가상 플레이어 프리셋
        // ============================================================================

        public async Task CreateDefaultPlayersAsync(CancellationToken cancellationToken = default)
        {
            var defaultPlayers = new[]
            {
                ("보수적 거래자", RiskProfile.Conservative, 2000m),
                ("일반 거래자", RiskProfile.Moderate, 1500m),
                ("공격적 거래자", RiskProfile.Aggressive, 1000m),
                ("테스트 플레이어", RiskProfile.Moderate, 5000m)
            };

            foreach (var (name, risk, balance) in defaultPlayers)
            {
                try
                {
                    var player = await CreatePlayerAsync(name, balance, cancellationToken);
                    player.TradingBehavior.RiskProfile = risk;
                    player.TradingBehavior.TradingIntervalSeconds = risk switch
                    {
                        RiskProfile.Conservative => 600, // 10분
                        RiskProfile.Moderate => 300,     // 5분
                        RiskProfile.Aggressive => 120,   // 2분
                        _ => 300
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "기본 플레이어 생성 실패: {Name}", name);
                }
            }

            _logger.LogInformation("기본 테스트 플레이어들 생성 완료");
        }

        public async Task<VirtualPlayer> CreateBehaviorPlayerAsync(string baseName, PlayerTradingBehavior behavior, CancellationToken cancellationToken = default)
        {
            var player = await CreatePlayerAsync(baseName, 1000m, cancellationToken);
            player.TradingBehavior = behavior;
            return player;
        }

        public async Task<List<VirtualPlayer>> CreateBulkPlayersAsync(int count, string namePrefix = "TestPlayer", CancellationToken cancellationToken = default)
        {
            var players = new List<VirtualPlayer>();
            var tasks = new List<Task<VirtualPlayer>>();

            for (int i = 1; i <= count; i++)
            {
                var playerName = $"{namePrefix}{i:D3}";
                tasks.Add(CreatePlayerAsync(playerName, 1000m, cancellationToken));
            }

            try
            {
                var results = await Task.WhenAll(tasks);
                players.AddRange(results);
                _logger.LogInformation("대량 플레이어 생성 완료: {Count}명", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "대량 플레이어 생성 중 오류 발생");
            }

            return players;
        }

        // ============================================================================
        // 내부 메서드들
        // ============================================================================

        private PlayerTradingBehavior CreateDefaultTradingBehavior()
        {
            return new PlayerTradingBehavior
            {
                TradingIntervalSeconds = 300, // 5분
                BuyProbability = 0.5,
                MinQuantity = 1,
                MaxQuantity = 5,
                TradingBudgetRatio = 0.1,
                RiskProfile = RiskProfile.Moderate,
                UsePriceBasedDecision = true
            };
        }

        private async void ExecuteAutoTradingCycle(object? state)
        {
            if (!_isAutoTradingEnabled || _autoTradingCancellation?.Token.IsCancellationRequested == true)
                return;

            try
            {
                var activePlayers = _players.Values
                    .Where(p => p.IsAutoTradingEnabled && p.AutoTradingStatus == AutoTradingStatus.Running)
                    .ToList();

                var tasks = activePlayers.Select(async player =>
                {
                    await _autoTradingSemaphore.WaitAsync(_autoTradingCancellation.Token);
                    try
                    {
                        // 거래 간격 확인
                        var timeSinceLastTrade = DateTime.UtcNow - (player.LastTradeTime ?? DateTime.MinValue);
                        if (timeSinceLastTrade.TotalSeconds >= player.TradingBehavior.TradingIntervalSeconds)
                        {
                            var result = await ExecuteRandomTradeAsync(player.PlayerId, _autoTradingCancellation.Token);
                            if (result.Success)
                            {
                                TradeExecuted?.Invoke(this, new TradeExecutedEventArgs
                                {
                                    Result = result,
                                    IsAutoTrade = true,
                                    ExecutedAt = DateTime.UtcNow
                                });
                            }
                        }
                    }
                    finally
                    {
                        _autoTradingSemaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // 정상적인 취소
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "자동 거래 사이클 실행 중 오류 발생");
            }
        }

        // ============================================================================
        // IDisposable 구현
        // ============================================================================

        public void Dispose()
        {
            _autoTradingTimer?.Dispose();
            _autoTradingCancellation?.Cancel();
            _autoTradingCancellation?.Dispose();
            _autoTradingSemaphore?.Dispose();
            _logger.LogInformation("PlayerService 리소스 정리 완료");
        }
    }
}