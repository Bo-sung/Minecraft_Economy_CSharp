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

        public ShopViewModel(IApiService apiService, IPlayerService playerService, ILogger<ShopViewModel> logger)
        {
            _apiService = apiService;
            _playerService = playerService;
            _logger = logger;

            // 속성 변경 시 가격 자동 계산
            PropertyChanged += OnPropertyChanged;
        }

        #region Commands

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
                // 기존 IApiService의 PurchaseItemAsync 사용
                var response = await _apiService.PurchaseItemAsync(SelectedPlayerId, SelectedItemId, Quantity);

                if (response != null)
                {
                    StatusMessage = $"구매 완료! 총 비용: {response.TotalCost:C}, 잔액: {response.PlayerBalance:C}";

                    // 거래 내역 갱신
                    await LoadTransactionHistoryAsync();

                    // 아이템 가격 갱신 (시장 압력 반영)
                    await UpdateItemPriceAsync();

                    // 수량 초기화
                    Quantity = 1;
                }
                else
                {
                    StatusMessage = "구매 실패: 응답을 받지 못했습니다.";
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
                // 기존 IApiService의 SellItemAsync 사용
                var response = await _apiService.SellItemAsync(SelectedPlayerId, SelectedItemId, Quantity);

                if (response != null)
                {
                    StatusMessage = $"판매 완료! 총 수익: {response.TotalRevenue:C}, 잔액: {response.PlayerBalance:C}";

                    // 거래 내역 갱신
                    await LoadTransactionHistoryAsync();

                    // 아이템 가격 갱신 (시장 압력 반영)
                    await UpdateItemPriceAsync();

                    // 수량 초기화
                    Quantity = 1;
                }
                else
                {
                    StatusMessage = "판매 실패: 응답을 받지 못했습니다.";
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
                // 기존 IApiService의 CreatePlayerAsync 사용 (초기 잔액 1000원)
                var response = await _apiService.CreatePlayerAsync(playerName, 1000m);

                if (response != null)
                {
                    await LoadPlayersAsync();
                    SelectedPlayerId = response.PlayerId;
                    StatusMessage = $"테스트 플레이어 생성 완료: {playerName} (잔액: {response.Balance:C})";
                }
                else
                {
                    StatusMessage = "플레이어 생성 실패: 응답을 받지 못했습니다.";
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

        #endregion

        #region Data Loading

        private async Task LoadPlayersAsync()
        {
            try
            {
                // 기존 IApiService의 GetOnlinePlayersAsync 사용
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
                // 기존 IApiService의 GetPlayerTransactionsAsync 사용 (최근 20건)
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

        #endregion

        #region Price Calculation

        private void CalculateEstimatedValues()
        {
            if (SelectedItemPrice == null || Quantity <= 0)
            {
                EstimatedCost = 0;
                EstimatedRevenue = 0;
                return;
            }

            EstimatedCost = SelectedItemPrice.BuyPrice * Quantity;
            EstimatedRevenue = SelectedItemPrice.SellPrice * Quantity;
        }

        #endregion

        #region Event Handlers

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

        #endregion

        #region Command Validation

        private bool CanExecuteTransaction()
        {
            return !IsLoading &&
                   !string.IsNullOrEmpty(SelectedPlayerId) &&
                   !string.IsNullOrEmpty(SelectedItemId) &&
                   Quantity > 0 &&
                   SelectedItemPrice != null;
        }

        #endregion

        #region Batch Operations

        [RelayCommand]
        private async Task ExecuteBatchTransactionsAsync()
        {
            // 대량 거래 테스트를 위한 배치 처리
            if (!CanExecuteTransaction()) return;

            IsLoading = true;
            StatusMessage = "배치 거래 실행 중...";

            try
            {
                var trades = new List<TradeRequest>();
                var batchSize = 5; // 5개씩 배치 처리

                for (int i = 0; i < batchSize; i++)
                {
                    trades.Add(new TradeRequest
                    {
                        ItemId = SelectedItemId,
                        Quantity = 1,
                        IsPurchase = i % 2 == 0 // 구매와 판매 번갈아 실행
                    });
                }

                // 기존 IApiService의 BatchTradeAsync 사용
                var response = await _apiService.BatchTradeAsync(SelectedPlayerId, trades);

                if (response != null)
                {
                    StatusMessage = $"배치 거래 완료: {response.SuccessCount}건 성공, {response.FailureCount}건 실패";

                    // 데이터 갱신
                    await LoadTransactionHistoryAsync();
                    await UpdateItemPriceAsync();
                }
                else
                {
                    StatusMessage = "배치 거래 실패: 응답을 받지 못했습니다.";
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

        #endregion
    }
}