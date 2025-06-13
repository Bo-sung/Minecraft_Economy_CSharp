using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using HarvestCraft2.TestClient.Models;
using HarvestCraft2.TestClient.Services;

namespace HarvestCraft2.TestClient.ViewModels
{
    public partial class ShopViewModel : ObservableObject
    {
        private readonly IApiService _apiService;
        private readonly IPlayerService _playerService;
        private readonly ILogger<ShopViewModel> _logger;

        // ============================================================================
        // Observable Properties
        // ============================================================================

        [ObservableProperty]
        private string selectedPlayerId = string.Empty;

        [ObservableProperty]
        private string selectedItemId = string.Empty;

        [ObservableProperty]
        private int quantity = 1;

        [ObservableProperty]
        private decimal estimatedCost;

        [ObservableProperty]
        private decimal estimatedRevenue;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private PriceResponse? selectedItemPrice;

        // ============================================================================
        // Collections
        // ============================================================================

        public ObservableCollection<PlayerResponse> Players { get; } = new();
        public ObservableCollection<PriceResponse> Items { get; } = new();
        public ObservableCollection<TransactionResponse> TransactionHistory { get; } = new();

        // 미리 정의된 아이템 목록 (HarvestCraft 2 기준)
        public ObservableCollection<string> AvailableItems { get; } = new()
        {
            "minecraft:wheat", "minecraft:carrot", "minecraft:potato", "minecraft:beetroot",
            "minecraft:apple", "minecraft:bread", "minecraft:cake", "minecraft:cookie",
            "minecraft:pumpkin", "minecraft:melon", "minecraft:sugar_cane", "minecraft:cocoa_beans",
            "pamhc2foodcore:rice", "pamhc2foodcore:corn", "pamhc2foodcore:tomato",
            "pamhc2foodcore:lettuce", "pamhc2foodcore:onion", "pamhc2foodcore:garlic"
        };

        // ============================================================================
        // Constructor
        // ============================================================================

        public ShopViewModel(IApiService apiService, IPlayerService playerService, ILogger<ShopViewModel> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // 속성 변경 시 가격 자동 계산
            PropertyChanged += OnPropertyChanged;

            _logger.LogDebug("ShopViewModel 초기화 완료");
        }

        // ============================================================================
        // Commands
        // ============================================================================

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            IsLoading = true;
            StatusMessage = "데이터 로딩 중...";

            try
            {
                // 플레이어 목록 로드
                await LoadPlayersAsync();

                // 아이템 가격 정보 로드
                await LoadItemPricesAsync();

                StatusMessage = "데이터 로드 완료";
                _logger.LogInformation("상점 데이터 로드 완료");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "데이터 로드 실패");
                StatusMessage = $"데이터 로드 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteTransaction))]
        private async Task PurchaseItemAsync()
        {
            if (!CanExecuteTransaction()) return;

            IsLoading = true;
            StatusMessage = "구매 처리 중...";

            try
            {
                var response = await _apiService.PurchaseItemAsync(SelectedPlayerId, SelectedItemId, Quantity);

                if (response?.Success == true)
                {
                    StatusMessage = $"구매 완료! 총 비용: {response.TotalCost:C}, 잔액: {response.NewBalance:C}";

                    // 거래 내역 갱신
                    await LoadTransactionHistoryAsync();

                    // 아이템 가격 갱신 (시장 압력 반영)
                    await UpdateItemPriceAsync();

                    // 수량 초기화
                    Quantity = 1;

                    _logger.LogInformation("구매 완료: {PlayerId} - {ItemId} x{Quantity}",
                        SelectedPlayerId, SelectedItemId, Quantity);
                }
                else
                {
                    StatusMessage = response?.ErrorMessage ?? "구매 실패: 응답을 받지 못했습니다.";
                    _logger.LogWarning("구매 실패: {PlayerId} - {Message}", SelectedPlayerId, StatusMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "구매 처리 중 오류 발생");
                StatusMessage = $"구매 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteTransaction))]
        private async Task SellItemAsync()
        {
            if (!CanExecuteTransaction()) return;

            IsLoading = true;
            StatusMessage = "판매 처리 중...";

            try
            {
                var response = await _apiService.SellItemAsync(SelectedPlayerId, SelectedItemId, Quantity);

                if (response?.Success == true)
                {
                    StatusMessage = $"판매 완료! 총 수익: {response.TotalEarned:C}, 잔액: {response.NewBalance:C}";

                    // 거래 내역 갱신
                    await LoadTransactionHistoryAsync();

                    // 아이템 가격 갱신 (시장 압력 반영)
                    await UpdateItemPriceAsync();

                    // 수량 초기화
                    Quantity = 1;

                    _logger.LogInformation("판매 완료: {PlayerId} - {ItemId} x{Quantity}",
                        SelectedPlayerId, SelectedItemId, Quantity);
                }
                else
                {
                    StatusMessage = response?.ErrorMessage ?? "판매 실패: 응답을 받지 못했습니다.";
                    _logger.LogWarning("판매 실패: {PlayerId} - {Message}", SelectedPlayerId, StatusMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "판매 처리 중 오류 발생");
                StatusMessage = $"판매 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task CreateTestPlayerAsync()
        {
            var playerName = $"TestPlayer_{DateTime.Now:HHmmss}";

            IsLoading = true;
            StatusMessage = "테스트 플레이어 생성 중...";

            try
            {
                var response = await _apiService.CreatePlayerAsync(playerName, 1000m);

                if (response != null)
                {
                    await LoadPlayersAsync();
                    SelectedPlayerId = response.PlayerId;
                    StatusMessage = $"테스트 플레이어 생성 완료: {playerName} (잔액: {1000m:C})";

                    _logger.LogInformation("테스트 플레이어 생성: {PlayerName} - {PlayerId}",
                        playerName, response.PlayerId);
                }
                else
                {
                    StatusMessage = "플레이어 생성 실패: 응답을 받지 못했습니다.";
                    _logger.LogWarning("플레이어 생성 실패: {PlayerName}", playerName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "테스트 플레이어 생성 실패");
                StatusMessage = $"플레이어 생성 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshTransactionHistoryAsync()
        {
            if (string.IsNullOrEmpty(SelectedPlayerId)) return;

            await LoadTransactionHistoryAsync();
        }

        [RelayCommand]
        private async Task RefreshItemPricesAsync()
        {
            await LoadItemPricesAsync();
        }

        [RelayCommand]
        private async Task ExecuteBatchTransactionsAsync()
        {
            if (!CanExecuteTransaction()) return;

            IsLoading = true;
            StatusMessage = "배치 거래 실행 중...";

            try
            {
                var trades = new List<Services.TradeRequest>(); // Services.TradeRequest 사용
                var batchSize = 5;

                for (int i = 0; i < batchSize; i++)
                {
                    trades.Add(new Services.TradeRequest
                    {
                        ItemId = SelectedItemId,
                        Quantity = 1,
                        IsPurchase = i % 2 == 0
                    });
                }

                var response = await _apiService.BatchTradeAsync(SelectedPlayerId, trades);

                if (response?.Success == true)
                {
                    var successCount = response.TransactionIds.Count;
                    var failureCount = response.Errors.Count;
                    StatusMessage = $"배치 거래 완료: {successCount}건 성공, {failureCount}건 실패";

                    await LoadTransactionHistoryAsync();
                    await UpdateItemPriceAsync();

                    _logger.LogInformation("배치 거래 완료: {SuccessCount}건 성공, {FailureCount}건 실패",
                        successCount, failureCount);
                }
                else
                {
                    StatusMessage = "배치 거래 실패: 응답을 받지 못했습니다.";
                    _logger.LogWarning("배치 거래 실패: {PlayerId}", SelectedPlayerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "배치 거래 실행 실패");
                StatusMessage = $"배치 거래 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ============================================================================
        // Data Loading
        // ============================================================================

        private async Task LoadPlayersAsync()
        {
            try
            {
                var players = await _apiService.GetOnlinePlayersAsync();

                Players.Clear();
                foreach (var player in players)
                {
                    Players.Add(player);
                }

                _logger.LogDebug("플레이어 목록 로드 완료: {Count}명", Players.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "플레이어 목록 로드 실패");
            }
        }

        private async Task LoadItemPricesAsync()
        {
            try
            {
                // 사용 가능한 아이템들의 가격 정보 로드
                var priceList = new List<PriceResponse>();

                foreach (var itemId in AvailableItems)
                {
                    try
                    {
                        var priceResponse = await _apiService.GetItemPriceAsync(itemId);
                        if (priceResponse != null)
                        {
                            priceList.Add(priceResponse);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "아이템 가격 로드 실패: {ItemId}", itemId);
                    }
                }

                Items.Clear();
                foreach (var item in priceList)
                {
                    Items.Add(item);
                }

                _logger.LogDebug("아이템 가격 정보 로드 완료: {Count}개", Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 가격 정보 로드 실패");
            }
        }

        private async Task LoadTransactionHistoryAsync()
        {
            if (string.IsNullOrEmpty(SelectedPlayerId)) return;

            try
            {
                var transactions = await _apiService.GetPlayerTransactionsAsync(SelectedPlayerId, page: 1, size: 20);

                TransactionHistory.Clear();
                foreach (var transaction in transactions.OrderByDescending(t => t.TransactionTime))
                {
                    TransactionHistory.Add(transaction);
                }

                _logger.LogDebug("거래 내역 로드 완료: {Count}건", TransactionHistory.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "거래 내역 로드 실패");
            }
        }

        private async Task UpdateItemPriceAsync()
        {
            if (string.IsNullOrEmpty(SelectedItemId)) return;

            try
            {
                var priceResponse = await _apiService.GetItemPriceAsync(SelectedItemId);

                if (priceResponse != null)
                {
                    SelectedItemPrice = priceResponse;

                    // Items 컬렉션에서도 업데이트
                    var existingItem = Items.FirstOrDefault(i => i.ItemId == SelectedItemId);
                    if (existingItem != null)
                    {
                        var index = Items.IndexOf(existingItem);
                        Items[index] = priceResponse;
                    }

                    // 예상 비용/수익 재계산
                    CalculateEstimatedValues();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 가격 업데이트 실패: {ItemId}", SelectedItemId);
            }
        }

        // ============================================================================
        // Price Calculation
        // ============================================================================

        private void CalculateEstimatedValues()
        {
            if (SelectedItemPrice == null || Quantity <= 0)
            {
                EstimatedCost = 0;
                EstimatedRevenue = 0;
                return;
            }

            // PriceResponse에는 CurrentPrice만 있음 - BuyPrice/SellPrice 없음
            EstimatedCost = SelectedItemPrice.CurrentPrice * Quantity;
            EstimatedRevenue = SelectedItemPrice.CurrentPrice * 0.8m * Quantity; // 판매가는 80% 가정
        }

        // ============================================================================
        // Event Handlers
        // ============================================================================

        private async void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SelectedItemId):
                    if (!string.IsNullOrEmpty(SelectedItemId))
                    {
                        SelectedItemPrice = Items.FirstOrDefault(i => i.ItemId == SelectedItemId);
                        CalculateEstimatedValues();
                    }
                    break;

                case nameof(Quantity):
                    CalculateEstimatedValues();
                    break;

                case nameof(SelectedPlayerId):
                    if (!string.IsNullOrEmpty(SelectedPlayerId))
                    {
                        await LoadTransactionHistoryAsync();
                    }
                    break;
            }
        }

        // ============================================================================
        // Command Validation
        // ============================================================================

        private bool CanExecuteTransaction()
        {
            return !IsLoading &&
                   !string.IsNullOrEmpty(SelectedPlayerId) &&
                   !string.IsNullOrEmpty(SelectedItemId) &&
                   Quantity > 0 &&
                   SelectedItemPrice != null;
        }

        // ============================================================================
        // 내부 클래스들 - ShopViewModel 전용
        // ============================================================================

        public class TradeRequest
        {
            public string ItemId { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public bool IsPurchase { get; set; }
        }
    }
}