// ============================================================================
// ViewModels/PriceViewModel.cs - 실시간 가격 모니터링 뷰모델 (최종 수정 버전)
// ============================================================================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HarvestCraft2.TestClient.Models;
using HarvestCraft2.TestClient.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HarvestCraft2.TestClient.ViewModels
{
    /// <summary>
    /// 가격 모니터링 뷰모델 (기본 기능만)
    /// </summary>
    public partial class PriceViewModel : ObservableObject, INotifyPropertyChanged, IDisposable
    {
        private readonly IApiService _apiService;
        private readonly IChartService _chartService;
        private readonly ILogger<PriceViewModel> _logger;
        private readonly Timer _priceUpdateTimer;

        // ============================================================================
        // 기본 속성들
        // ============================================================================

        [ObservableProperty]
        private string _selectedItemId = string.Empty;

        [ObservableProperty]
        private string _selectedItemName = string.Empty;

        [ObservableProperty]
        private decimal _currentPrice;

        [ObservableProperty]
        private decimal _previousPrice;

        [ObservableProperty]
        private decimal _priceChange;

        [ObservableProperty]
        private decimal _priceChangePercent;

        [ObservableProperty]
        private string _priceChangeDirection = "None";

        [ObservableProperty]
        private decimal _highPrice;

        [ObservableProperty]
        private decimal _lowPrice;

        [ObservableProperty]
        private decimal _averagePrice;

        [ObservableProperty]
        private int _totalVolume;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "준비됨";

        [ObservableProperty]
        private bool _isAutoRefreshEnabled = true;

        [ObservableProperty]
        private int _autoRefreshInterval = 30;

        [ObservableProperty]
        private DateTime _selectedStartDate = DateTime.Now.AddDays(-1);

        [ObservableProperty]
        private DateTime _selectedEndDate = DateTime.Now;

        [ObservableProperty]
        private int _predictionDays = 7;

        // ============================================================================
        // 컬렉션들 (실제 모델명 사용)
        // ============================================================================

        public ObservableCollection<PriceResponse> AllPrices { get; } = new();
        public ObservableCollection<PriceHistoryResponse> PriceHistory { get; } = new();
        public ObservableCollection<PricePredictionResponse> PricePredictions { get; } = new();
        public ObservableCollection<ChartDataPoint> ChartData { get; } = new();
        public ObservableCollection<string> ChartCategories { get; } = new();
        public ObservableCollection<decimal> ChartValues { get; } = new();
        public ObservableCollection<TimeRangeOption> TimeRangeOptions { get; } = new();

        [ObservableProperty]
        private TimeRangeOption? _selectedTimeRange;

        // ============================================================================
        // 생성자 및 초기화
        // ============================================================================

        public PriceViewModel(
            IApiService apiService,
            IChartService chartService,
            ILogger<PriceViewModel> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _chartService = chartService ?? throw new ArgumentNullException(nameof(chartService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // 자동 새로고침 타이머 초기화
            _priceUpdateTimer = new Timer(AutoRefreshCallback, null, Timeout.Infinite, Timeout.Infinite);

            // 초기화
            InitializeViewModel();

            // 이벤트 구독
            PropertyChanged += OnPropertyChanged;

            _logger.LogInformation("PriceViewModel 초기화 완료");
        }

        private void InitializeViewModel()
        {
            // 시간 범위 옵션 설정
            TimeRangeOptions.Clear();
            TimeRangeOptions.Add(new TimeRangeOption { DisplayName = "1시간", Hours = 1 });
            TimeRangeOptions.Add(new TimeRangeOption { DisplayName = "6시간", Hours = 6 });
            TimeRangeOptions.Add(new TimeRangeOption { DisplayName = "24시간", Hours = 24 });
            TimeRangeOptions.Add(new TimeRangeOption { DisplayName = "3일", Hours = 72 });
            TimeRangeOptions.Add(new TimeRangeOption { DisplayName = "1주일", Hours = 168 });
            TimeRangeOptions.Add(new TimeRangeOption { DisplayName = "1개월", Hours = 720 });

            SelectedTimeRange = TimeRangeOptions.FirstOrDefault(t => t.Hours == 24);

            // 초기 데이터 로드
            _ = LoadAllPricesAsync();
        }

        // ============================================================================
        // 명령어들
        // ============================================================================

        [RelayCommand]
        private async Task LoadAllPricesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "가격 정보 로딩 중...";

                // 실제 API 시그니처에 맞게 수정
                // GetItemPricesAsync(List<string> itemIds)이므로 빈 리스트 전달 (전체 조회)
                var prices = await _apiService.GetItemPricesAsync(new List<string>());

                AllPrices.Clear();
                foreach (var price in prices.OrderBy(p => p.ItemName))
                {
                    AllPrices.Add(price);
                }

                StatusMessage = $"가격 정보 로드 완료: {AllPrices.Count}개 아이템";
                _logger.LogDebug("전체 가격 로드 완료: {Count}개", AllPrices.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 정보 로드 실패");
                StatusMessage = $"가격 로드 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            try
            {
                await LoadAllPricesAsync();

                if (!string.IsNullOrEmpty(SelectedItemId))
                {
                    await LoadItemDetailsAsync();
                    await LoadPriceHistoryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "데이터 새로고침 실패");
                StatusMessage = $"새로고침 실패: {ex.Message}";
            }
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

                foreach (var record in history.OrderBy(h => h.Date))
                {
                    PriceHistory.Add(record);
                    ChartData.Add(new ChartDataPoint
                    {
                        Timestamp = record.Date,
                        BuyPrice = record.Price,
                        SellPrice = record.Price,
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
                _logger.LogError(ex, "가격 예측 로드 실패: {ItemId}", SelectedItemId);
                StatusMessage = $"예측 로드 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ExportDataAsync()
        {
            try
            {
                StatusMessage = "데이터 내보내기 기능은 향후 구현 예정입니다.";
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "데이터 내보내기 실패");
                StatusMessage = "내보내기 실패";
            }
        }

        // ============================================================================
        // 헬퍼 메서드들
        // ============================================================================

        private async Task LoadItemDetailsAsync()
        {
            if (string.IsNullOrEmpty(SelectedItemId)) return;

            try
            {
                var priceResponse = await _apiService.GetItemPriceAsync(SelectedItemId);
                if (priceResponse != null)
                {
                    SelectedItemName = priceResponse.ItemName;
                    CurrentPrice = priceResponse.CurrentPrice;

                    // 이전 가격과 비교하여 변동 계산
                    CalculatePriceChange(priceResponse.PreviousPrice, priceResponse.CurrentPrice);

                    _logger.LogDebug("아이템 상세 정보 로드: {ItemId} - {ItemName}", SelectedItemId, SelectedItemName);
                }
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
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "차트 데이터 업데이트 실패");
            }
        }

        private async Task RefreshCurrentPriceAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(SelectedItemId)) return;

                var currentData = await _apiService.GetItemPriceAsync(SelectedItemId);
                if (currentData != null)
                {
                    var oldPrice = CurrentPrice;
                    CurrentPrice = currentData.CurrentPrice;
                    CalculatePriceChange(oldPrice, CurrentPrice);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "현재 가격 새로고침 실패: {ItemId}", SelectedItemId);
            }
        }

        // ============================================================================
        // 자동 새로고침
        // ============================================================================

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

        // ============================================================================
        // 가격 분석
        // ============================================================================

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

        // ============================================================================
        // 이벤트 핸들러
        // ============================================================================

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

        // ============================================================================
        // Dispose
        // ============================================================================

        public void Dispose()
        {
            _priceUpdateTimer?.Dispose();
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
}