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
    public partial class MarketViewModel : ObservableObject
    {
        private readonly IApiService _apiService;
        private readonly IChartService _chartService;
        private readonly ILogger<MarketViewModel> _logger;
        private readonly Timer _marketUpdateTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public MarketViewModel(IApiService apiService, IChartService chartService, ILogger<MarketViewModel> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _chartService = chartService ?? throw new ArgumentNullException(nameof(chartService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cancellationTokenSource = new CancellationTokenSource();

            InitializeFilterOptions();
            _marketUpdateTimer = new Timer(async state => await RefreshMarketDataAsync(state),
                null, TimeSpan.Zero, TimeSpan.FromSeconds(RefreshInterval));

            PropertyChanged += OnPropertyChanged;
            _logger.LogDebug("MarketViewModel 초기화 완료");
        }

        // ============================================================================
        // Observable Properties - CommunityToolkit.Mvvm 사용
        // ============================================================================

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isAutoRefreshEnabled = true;

        [ObservableProperty]
        private int refreshInterval = 10;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private MarketDashboardResponse? marketDashboard;

        // 개별 대시보드 속성들 - 이게 에러의 원인이었습니다
        [ObservableProperty]
        private int totalOnlinePlayers;

        [ObservableProperty]
        private long totalTransactions24h;

        [ObservableProperty]
        private decimal totalVolume24h;

        [ObservableProperty]
        private int activeItems;

        [ObservableProperty]
        private decimal averagePrice;

        [ObservableProperty]
        private DateTime lastUpdated;

        [ObservableProperty]
        private int trendingItemsLimit = 10;

        [ObservableProperty]
        private int volatileItemsLimit = 10;

        [ObservableProperty]
        private string selectedCategory = "All";

        [ObservableProperty]
        private string selectedSortBy = "TransactionCount";

        // ============================================================================
        // Collections
        // ============================================================================

        public ObservableCollection<PopularItemResponse> PopularItems { get; } = new();
        public ObservableCollection<VolatileItemResponse> VolatileItems { get; } = new();
        public ObservableCollection<CategoryStatsResponse> CategoryStats { get; } = new();
        public ObservableCollection<MarketTrendData> TrendData { get; } = new();
        public ObservableCollection<ChartDataPoint> PriceChartData { get; } = new();
        public ObservableCollection<ChartData_Category> CategoryChartData { get; } = new();
        public ObservableCollection<string> AvailableCategories { get; } = new();
        public ObservableCollection<string> AvailableSortOptions { get; } = new();

        // ============================================================================
        // Commands
        // ============================================================================

        [RelayCommand]
        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "시장 데이터를 새로고침하는 중...";

                var cancellationToken = _cancellationTokenSource.Token;
                await LoadMarketDashboardAsync(cancellationToken);
                await LoadPopularItemsAsync(cancellationToken);
                await LoadVolatileItemsAsync(cancellationToken);
                await LoadCategoryStatsAsync(cancellationToken);

                StatusMessage = "시장 데이터 새로고침 완료";
                _logger.LogInformation("시장 데이터 새로고침 완료");
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "데이터 새로고침이 취소되었습니다.";
                _logger.LogWarning("시장 데이터 새로고침 취소됨");
            }
            catch (Exception ex)
            {
                StatusMessage = $"새로고침 실패: {ex.Message}";
                _logger.LogError(ex, "시장 데이터 새로고침 실패");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ToggleAutoRefresh()
        {
            IsAutoRefreshEnabled = !IsAutoRefreshEnabled;

            if (IsAutoRefreshEnabled)
            {
                StartAutoRefresh();
                StatusMessage = "자동 새로고침이 활성화되었습니다.";
            }
            else
            {
                _marketUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                StatusMessage = "자동 새로고침이 비활성화되었습니다.";
            }

            _logger.LogInformation("자동 새로고침: {IsEnabled}", IsAutoRefreshEnabled);
        }

        // ============================================================================
        // Private Methods
        // ============================================================================

        private void InitializeFilterOptions()
        {
            AvailableCategories.Add("All");
            AvailableCategories.Add("Food");
            AvailableCategories.Add("Crops");
            AvailableCategories.Add("Tools");
            AvailableCategories.Add("Materials");

            AvailableSortOptions.Add("TransactionCount");
            AvailableSortOptions.Add("Price");
            AvailableSortOptions.Add("PriceChange");
            AvailableSortOptions.Add("Volatility");
        }

        private void StartAutoRefresh()
        {
            var interval = TimeSpan.FromSeconds(RefreshInterval);
            _marketUpdateTimer?.Change(TimeSpan.Zero, interval);
        }

        private async Task LoadMarketDashboardAsync(CancellationToken cancellationToken)
        {
            try
            {
                var dashboard = await _apiService.GetMarketDashboardAsync(cancellationToken);
                if (dashboard != null)
                {
                    MarketDashboard = dashboard;

                    // Observable Properties 직접 설정 - 이게 핵심!
                    TotalOnlinePlayers = dashboard.TotalOnlinePlayers;
                    TotalTransactions24h = dashboard.TotalTransactions24h;
                    TotalVolume24h = dashboard.TotalVolume24h;
                    ActiveItems = dashboard.ActiveItems;
                    AveragePrice = dashboard.AveragePrice;
                    LastUpdated = DateTime.Now;

                    UpdateTrendData(dashboard);
                    _logger.LogDebug("시장 대시보드 로드 완료");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시장 대시보드 로드 실패");
                throw;
            }
        }

        private async Task LoadPopularItemsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var popularItems = await _apiService.GetPopularItemsAsync(TrendingItemsLimit, cancellationToken);

                PopularItems.Clear();
                if (popularItems != null)
                {
                    foreach (var item in popularItems)
                    {
                        PopularItems.Add(item);
                    }
                }

                _logger.LogDebug("인기 아이템 로드 완료: {Count}개", PopularItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "인기 아이템 로드 실패");
                throw;
            }
        }

        private async Task LoadVolatileItemsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var volatileItems = await _apiService.GetVolatileItemsAsync(VolatileItemsLimit, cancellationToken);

                VolatileItems.Clear();
                if (volatileItems != null)
                {
                    foreach (var item in volatileItems)
                    {
                        VolatileItems.Add(item);
                    }
                }

                _logger.LogDebug("변동성 아이템 로드 완료: {Count}개", VolatileItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "변동성 아이템 로드 실패");
                throw;
            }
        }

        private async Task LoadCategoryStatsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var categoryStats = await _apiService.GetCategoryStatsAsync(cancellationToken);

                CategoryStats.Clear();
                CategoryChartData.Clear();

                if (categoryStats != null)
                {
                    foreach (var stat in categoryStats)
                    {
                        CategoryStats.Add(stat);

                        // CategoryStatsResponse의 실제 속성만 사용
                        CategoryChartData.Add(new ChartData_Category
                        {
                            CategoryName = stat.Category ?? "Unknown",
                            ItemCount = stat.ItemCount,
                            AveragePrice = 0m, // CategoryStatsResponse에 AveragePrice 없음 - 기본값 설정
                            TotalVolume = stat.TotalVolume, // long → decimal 변환
                            MostPopularItem = "데이터 없음" // CategoryStatsResponse에 MostPopularItem 없음 - 기본값 설정
                        });
                    }
                }

                _logger.LogDebug("카테고리 통계 로드 완료: {Count}개", CategoryStats.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "카테고리 통계 로드 실패");
                throw;
            }
        }

        private async Task RefreshMarketDataAsync(object? state)
        {
            if (!IsAutoRefreshEnabled || IsLoading) return;

            try
            {
                var cancellationToken = _cancellationTokenSource.Token;
                await LoadMarketDashboardAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 정상적인 취소는 로그하지 않음
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시장 데이터 자동 새로고침 실패");
            }
        }

        private void UpdateTrendData(MarketDashboardResponse dashboard)
        {
            try
            {
                var trendPoint = new MarketTrendData
                {
                    Timestamp = DateTime.Now,
                    OnlinePlayerCount = dashboard.TotalOnlinePlayers,
                    ActiveItemCount = dashboard.ActiveItems,
                    AveragePrice = dashboard.AveragePrice,
                    TotalTransactions = dashboard.TotalTransactions24h,
                    TotalVolume = dashboard.TotalVolume24h
                };

                TrendData.Add(trendPoint);

                while (TrendData.Count > 100)
                {
                    TrendData.RemoveAt(0);
                }

                UpdateChartData();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "트렌드 데이터 업데이트 실패");
            }
        }

        private void UpdateChartData()
        {
            try
            {
                PriceChartData.Clear();

                foreach (var trend in TrendData.TakeLast(50))
                {
                    PriceChartData.Add(new ChartDataPoint
                    {
                        Timestamp = trend.Timestamp,
                        Value = (double)trend.AveragePrice,
                        Volume = (int)trend.TotalTransactions
                    });
                }

                _logger.LogDebug("차트 데이터 업데이트 완료: {Count}개 포인트", PriceChartData.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "차트 데이터 업데이트 실패");
            }
        }

        private async void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(RefreshInterval):
                    if (IsAutoRefreshEnabled)
                    {
                        StartAutoRefresh();
                    }
                    break;

                case nameof(TrendingItemsLimit):
                    if (!IsLoading)
                    {
                        await LoadPopularItemsAsync(_cancellationTokenSource.Token);
                    }
                    break;

                case nameof(VolatileItemsLimit):
                    if (!IsLoading)
                    {
                        await LoadVolatileItemsAsync(_cancellationTokenSource.Token);
                    }
                    break;
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _marketUpdateTimer?.Dispose();
        }

        // ============================================================================
        // Market 전용 차트 데이터 클래스들 (중복 방지)
        // ============================================================================

        public class MarketTrendData
        {
            public DateTime Timestamp { get; set; }
            public int OnlinePlayerCount { get; set; }
            public int ActiveItemCount { get; set; }
            public decimal AveragePrice { get; set; }
            public long TotalTransactions { get; set; }
            public decimal TotalVolume { get; set; }
        }

        public class ChartData_Category
        {
            public string CategoryName { get; set; } = string.Empty;
            public int ItemCount { get; set; }
            public decimal AveragePrice { get; set; }
            public long TotalVolume { get; set; }
            public string MostPopularItem { get; set; } = string.Empty;
        }

        public class ChartDataPoint
        {
            public DateTime Timestamp { get; set; }
            public double Value { get; set; }
            public int Volume { get; set; }
        }
    }
}