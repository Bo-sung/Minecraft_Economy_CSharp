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
    public partial class PriceViewModel : ObservableObject
    {
        private readonly IApiService _apiService;
        private readonly IChartService _chartService;
        private readonly ILogger<PriceViewModel> _logger;
        private readonly Timer _priceUpdateTimer;

        // ============================================================================
        // Observable Properties
        // ============================================================================

        [ObservableProperty]
        private string selectedItemId = string.Empty;

        [ObservableProperty]
        private PriceResponse? selectedItemPrice;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isAutoRefreshEnabled = true;

        [ObservableProperty]
        private int autoRefreshInterval = 5; // 초

        [ObservableProperty]
        private DateTime selectedStartDate = DateTime.Now.AddDays(-7);

        [ObservableProperty]
        private DateTime selectedEndDate = DateTime.Now;

        [ObservableProperty]
        private int predictionDays = 7;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private decimal currentPrice;

        [ObservableProperty]
        private decimal priceChange;

        [ObservableProperty]
        private decimal priceChangePercent;

        [ObservableProperty]
        private string priceChangeDirection = "None"; // Up, Down, None

        // ============================================================================
        // Collections
        // ============================================================================

        public ObservableCollection<PriceResponse> AllPrices { get; } = new();
        public ObservableCollection<PriceHistoryResponse> PriceHistory { get; } = new();
        public ObservableCollection<PricePredictionResponse> PricePredictions { get; } = new();
        public ObservableCollection<ChartDataPoint> ChartData { get; } = new();

        // 차트 관련 데이터
        public ObservableCollection<string> ChartCategories { get; } = new();
        public ObservableCollection<decimal> ChartValues { get; } = new();

        // 감시 대상 아이템 목록
        public ObservableCollection<string> WatchedItems { get; } = new()
        {
            "minecraft:wheat", "minecraft:carrot", "minecraft:potato", "minecraft:apple",
            "pamhc2foodcore:rice", "pamhc2foodcore:corn", "pamhc2foodcore:tomato"
        };

        // 사용 가능한 시간 범위
        public ObservableCollection<TimeRangeOption> TimeRangeOptions { get; } = new()
        {
            new() { DisplayName = "최근 1시간", Hours = 1 },
            new() { DisplayName = "최근 6시간", Hours = 6 },
            new() { DisplayName = "최근 24시간", Hours = 24 },
            new() { DisplayName = "최근 3일", Hours = 72 },
            new() { DisplayName = "최근 7일", Hours = 168 },
            new() { DisplayName = "최근 30일", Hours = 720 }
        };

        public PriceViewModel(IApiService apiService, IChartService chartService, ILogger<PriceViewModel> logger)
        {
            _apiService = apiService;
            _chartService = chartService;
            _logger = logger;

            // 자동 새로고침 타이머 설정
            _priceUpdateTimer = new Timer(AutoRefreshCallback, null, Timeout.Infinite, Timeout.Infinite);

            // 속성 변경 감지
            PropertyChanged += OnPropertyChanged;

            // API 이벤트 구독
            _apiService.PriceChanged += OnPriceChanged;
        }

        #region Commands

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            IsLoading = true;
            StatusMessage = "가격 데이터 로딩 중...";

            try
            {
                await LoadAllPricesAsync();

                if (!string.IsNullOrEmpty(SelectedItemId))
                {
                    await LoadItemDetailsAsync();
                }

                StatusMessage = "가격 데이터 로드 완료";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 데이터 로드 실패");
                StatusMessage = $"데이터 로드 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SelectItemAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || itemId == SelectedItemId) return;

            SelectedItemId = itemId;
            await LoadItemDetailsAsync();
        }

        [RelayCommand]
        private async Task LoadPriceHistoryAsync()
        {
            if (string.IsNullOrEmpty(SelectedItemId)) return;

            IsLoading = true;
            StatusMessage = "가격 히스토리 로딩 중...";

            try
            {
                var history = await _apiService.GetPriceHistoryAsync(
                    SelectedItemId,
                    SelectedStartDate,
                    SelectedEndDate);

                PriceHistory.Clear();
                ChartData.Clear();

                foreach (var record in history.OrderBy(h => h.Timestamp))
                {
                    PriceHistory.Add(record);
                    ChartData.Add(new ChartDataPoint
                    {
                        Timestamp = record.Timestamp,
                        BuyPrice = record.BuyPrice,
                        SellPrice = record.SellPrice,
                        Volume = record.Volume
                    });
                }

                // 차트 데이터 업데이트
                await UpdateChartDataAsync();

                StatusMessage = $"가격 히스토리 로드 완료: {PriceHistory.Count}건";
                _logger.LogDebug("가격 히스토리 로드: {ItemId}, {Count}건", SelectedItemId, PriceHistory.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 히스토리 로드 실패: {ItemId}", SelectedItemId);
                StatusMessage = $"히스토리 로드 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task LoadPricePredictionAsync()
        {
            if (string.IsNullOrEmpty(SelectedItemId)) return;

            IsLoading = true;
            StatusMessage = "가격 예측 로딩 중...";

            try
            {
                var prediction = await _apiService.GetPricePredictionAsync(SelectedItemId, PredictionDays);

                PricePredictions.Clear();
                PricePredictions.Add(prediction);

                StatusMessage = $"가격 예측 완료: {PredictionDays}일 예측";
                _logger.LogDebug("가격 예측 로드: {ItemId}, {Days}일", SelectedItemId, PredictionDays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 예측 실패: {ItemId}", SelectedItemId);
                StatusMessage = $"예측 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshCurrentPriceAsync()
        {
            if (string.IsNullOrEmpty(SelectedItemId)) return;

            try
            {
                var oldPrice = CurrentPrice;
                var priceResponse = await _apiService.GetItemPriceAsync(SelectedItemId);

                if (priceResponse != null)
                {
                    SelectedItemPrice = priceResponse;
                    CurrentPrice = priceResponse.BuyPrice;

                    // 가격 변동 계산
                    CalculatePriceChange(oldPrice, CurrentPrice);

                    StatusMessage = $"가격 업데이트: {SelectedItemId} - {CurrentPrice:C}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 새로고침 실패: {ItemId}", SelectedItemId);
                StatusMessage = $"가격 새로고침 실패: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ToggleAutoRefresh()
        {
            IsAutoRefreshEnabled = !IsAutoRefreshEnabled;

            if (IsAutoRefreshEnabled)
            {
                StartAutoRefresh();
                StatusMessage = $"자동 새로고침 시작 ({AutoRefreshInterval}초 간격)";
            }
            else
            {
                StopAutoRefresh();
                StatusMessage = "자동 새로고침 중지";
            }
        }

        [RelayCommand]
        private async Task SetTimeRangeAsync(TimeRangeOption timeRange)
        {
            SelectedEndDate = DateTime.Now;
            SelectedStartDate = DateTime.Now.AddHours(-timeRange.Hours);

            await LoadPriceHistoryAsync();
        }

        [RelayCommand]
        private async Task ExportDataAsync()
        {
            if (PriceHistory.Count == 0)
            {
                StatusMessage = "내보낼 데이터가 없습니다.";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "데이터 내보내기 중...";

                // CSV 형태로 데이터 내보내기 (실제 구현에서는 파일 다이얼로그 사용)
                var csvData = GenerateCsvData();

                // 임시로 클립보드에 복사 (실제로는 파일 저장)
                // Clipboard.SetText(csvData);

                StatusMessage = $"데이터 내보내기 완료: {PriceHistory.Count}건";
                _logger.LogInformation("가격 데이터 내보내기 완료: {ItemId}, {Count}건", SelectedItemId, PriceHistory.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "데이터 내보내기 실패");
                StatusMessage = $"내보내기 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Data Loading

        private async Task LoadAllPricesAsync()
        {
            try
            {
                var priceList = new List<PriceResponse>();

                // 감시 대상 아이템들의 가격 조회
                foreach (var itemId in WatchedItems)
                {
                    try
                    {
                        var price = await _apiService.GetItemPriceAsync(itemId);
                        if (price != null)
                        {
                            priceList.Add(price);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "아이템 가격 로드 실패: {ItemId}", itemId);
                    }
                }

                AllPrices.Clear();
                foreach (var price in priceList.OrderBy(p => p.ItemDisplayName))
                {
                    AllPrices.Add(price);
                }

                _logger.LogDebug("전체 가격 정보 로드 완료: {Count}개", AllPrices.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "전체 가격 정보 로드 실패");
                throw;
            }
        }

        private async Task LoadItemDetailsAsync()
        {
            if (string.IsNullOrEmpty(SelectedItemId)) return;

            try
            {
                // 선택된 아이템의 상세 정보 로드
                await RefreshCurrentPriceAsync();
                await LoadPriceHistoryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 상세 정보 로드 실패: {ItemId}", SelectedItemId);
                throw;
            }
        }

        private async Task UpdateChartDataAsync()
        {
            try
            {
                ChartCategories.Clear();
                ChartValues.Clear();

                foreach (var data in ChartData.TakeLast(50)) // 최근 50개 데이터포인트
                {
                    ChartCategories.Add(data.Timestamp.ToString("MM/dd HH:mm"));
                    ChartValues.Add(data.BuyPrice);
                }

                _logger.LogDebug("차트 데이터 업데이트 완료: {Count}개 포인트", ChartData.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "차트 데이터 업데이트 실패");
            }
        }

        #endregion

        #region Auto Refresh

        private void StartAutoRefresh()
        {
            StopAutoRefresh();
            _priceUpdateTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(AutoRefreshInterval));
        }

        private void StopAutoRefresh()
        {
            _priceUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async void AutoRefreshCallback(object? state)
        {
            if (!IsAutoRefreshEnabled || IsLoading) return;

            try
            {
                await LoadAllPricesAsync();

                if (!string.IsNullOrEmpty(SelectedItemId))
                {
                    await RefreshCurrentPriceAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "자동 새로고침 실패");
            }
        }

        #endregion

        #region Price Analysis

        private void CalculatePriceChange(decimal oldPrice, decimal newPrice)
        {
            if (oldPrice <= 0)
            {
                PriceChange = 0;
                PriceChangePercent = 0;
                PriceChangeDirection = "None";
                return;
            }

            PriceChange = newPrice - oldPrice;
            PriceChangePercent = (PriceChange / oldPrice) * 100;

            PriceChangeDirection = PriceChange switch
            {
                > 0 => "Up",
                < 0 => "Down",
                _ => "None"
            };
        }

        private string GenerateCsvData()
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Timestamp,BuyPrice,SellPrice,Volume");

            foreach (var record in PriceHistory)
            {
                csv.AppendLine($"{record.Timestamp:yyyy-MM-dd HH:mm:ss},{record.BuyPrice},{record.SellPrice},{record.Volume}");
            }

            return csv.ToString();
        }

        #endregion

        #region Event Handlers

        private async void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(AutoRefreshInterval):
                    if (IsAutoRefreshEnabled)
                    {
                        StartAutoRefresh();
                    }
                    break;

                case nameof(SelectedItemId):
                    if (!string.IsNullOrEmpty(SelectedItemId))
                    {
                        await LoadItemDetailsAsync();
                    }
                    break;
            }
        }

        private async void OnPriceChanged(object? sender, PriceChangedEventArgs e)
        {
            try
            {
                // 가격 변동 이벤트 처리
                if (e.ItemId == SelectedItemId)
                {
                    var oldPrice = CurrentPrice;
                    CurrentPrice = e.NewPrice;
                    CalculatePriceChange(oldPrice, e.NewPrice);

                    StatusMessage = $"가격 변동 감지: {e.ItemId} {e.OldPrice:C} → {e.NewPrice:C}";
                    _logger.LogInformation("가격 변동: {ItemId} {OldPrice} → {NewPrice}", e.ItemId, e.OldPrice, e.NewPrice);
                }

                // 전체 가격 목록에서도 업데이트
                var existingItem = AllPrices.FirstOrDefault(p => p.ItemId == e.ItemId);
                if (existingItem != null)
                {
                    await LoadAllPricesAsync(); // 전체 목록 새로고침
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 변동 이벤트 처리 실패");
            }
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            _priceUpdateTimer?.Dispose();
            if (_apiService != null)
            {
                _apiService.PriceChanged -= OnPriceChanged;
            }
        }

        #endregion
    }

    // ============================================================================
    // 보조 클래스들
    // ============================================================================

    public class TimeRangeOption
    {
        public string DisplayName { get; set; } = string.Empty;
        public int Hours { get; set; }
    }

    public class ChartDataPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal SellPrice { get; set; }
        public int Volume { get; set; }
    }
}