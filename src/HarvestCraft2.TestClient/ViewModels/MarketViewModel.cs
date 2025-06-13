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

        // ============================================================================
        // Observable Properties - 시장 대시보드 (기존 구조에 맞춤)
        // ============================================================================

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isAutoRefreshEnabled = true;

        [ObservableProperty]
        private int refreshInterval = 10; // 10초 간격

        [ObservableProperty]
        private string statusMessage = string.Empty;

        // 기존 MarketDashboardResponse 구조에 맞춤 (Data 속성 없음)
        [ObservableProperty]
        private MarketDashboardResponse? marketDashboard;

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

        // ============================================================================
        // Observable Properties - 필터 및 설정
        // ============================================================================

        [ObservableProperty]
        private int trendingItemsLimit = 10;

        [ObservableProperty]
        private int volatileItemsLimit = 10;

        [ObservableProperty]
        private string selectedCategory = "All";

        [ObservableProperty]
        private string selectedSortBy = "TransactionCount";

        // ============================================================================
        // Collections (기존 응답 모델 사용)
        // ============================================================================

        public ObservableCollection<PopularItemResponse> PopularItems { get; } = new();
        public ObservableCollection<VolatileItemResponse> VolatileItems { get; } = new();
        public ObservableCollection<CategoryStatsResponse> CategoryStats { get; } = new();
        public ObservableCollection<MarketTrendData> TrendData { get; } = new();

        // 차트 데이터
        public ObservableCollection<ChartDataPoint> PriceChartData { get; } = new();
        public ObservableCollection<CategoryChartData> CategoryChartData { get; } = new();

        // 필터 옵션
        public ObservableCollection<string> AvailableCategories { get; } = new()
        {
            "All", "FOOD_CORE", "CROPS", "FOOD_EXTENDED", "VANILLA", "TOOLS"
        };

        public ObservableCollection<string> SortOptions { get; } = new()
        {
            "TransactionCount", "Price", "Volatility", "Rank"
        };

        public MarketViewModel(IApiService apiService, IChartService chartService, ILogger<MarketViewModel> logger)
        {
            _apiService = apiService;
            _chartService = chartService;
            _logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();

            // 자동 새로고침 타이머 설정
            _marketUpdateTimer = new Timer(AutoRefreshCallback, null, Timeout.Infinite, Timeout.Infinite);

            // 속성 변경 감지
            PropertyChanged += OnPropertyChanged;
        }

        #region Commands

        [RelayCommand]
        private async Task LoadMarketDataAsync()
        {
            IsLoading = true;
            StatusMessage = "시장 데이터 로딩 중...";

            try
            {
                var cancellationToken = _cancellationTokenSource.Token;

                // 병렬로 모든 시장 데이터 로드
                var tasks = new List<Task>
                {
                    LoadMarketDashboardAsync(cancellationToken),
                    LoadPopularItemsAsync(cancellationToken),
                    LoadVolatileItemsAsync(cancellationToken),
                    LoadCategoryStatsAsync(cancellationToken)
                };

                await Task.WhenAll(tasks);

                StatusMessage = "시장 데이터 로드 완료";
                _logger.LogInformation("시장 데이터 로드 완료");
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "시장 데이터 로드 취소됨";
                _logger.LogInformation("시장 데이터 로드 취소");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시장 데이터 로드 실패");
                StatusMessage = $"시장 데이터 로드 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshMarketDashboardAsync()
        {
            try
            {
                var cancellationToken = _cancellationTokenSource.Token;
                await LoadMarketDashboardAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시장 대시보드 새로고침 실패");
                StatusMessage = $"새로고침 실패: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RefreshPopularItemsAsync()
        {
            try
            {
                var cancellationToken = _cancellationTokenSource.Token;
                await LoadPopularItemsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "인기 아이템 새로고침 실패");
                StatusMessage = $"인기 아이템 새로고침 실패: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RefreshVolatileItemsAsync()
        {
            try
            {
                var cancellationToken = _cancellationTokenSource.Token;
                await LoadVolatileItemsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "변동성 아이템 새로고침 실패");
                StatusMessage = $"변동성 아이템 새로고침 실패: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RefreshCategoryStatsAsync()
        {
            try
            {
                var cancellationToken = _cancellationTokenSource.Token;
                await LoadCategoryStatsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "카테고리 통계 새로고침 실패");
                StatusMessage = $"카테고리 통계 새로고침 실패: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ToggleAutoRefresh()
        {
            IsAutoRefreshEnabled = !IsAutoRefreshEnabled;

            if (IsAutoRefreshEnabled)
            {
                StartAutoRefresh();
                StatusMessage = $"자동 새로고침 시작 ({RefreshInterval}초 간격)";
            }
            else
            {
                StopAutoRefresh();
                StatusMessage = "자동 새로고침 중지";
            }
        }

        [RelayCommand]
        private async Task ApplyFiltersAsync()
        {
            IsLoading = true;
            StatusMessage = "필터 적용 중...";

            try
            {
                var cancellationToken = _cancellationTokenSource.Token;

                // 필터가 적용된 데이터 다시 로드
                await LoadPopularItemsAsync(cancellationToken);
                await LoadVolatileItemsAsync(cancellationToken);

                if (SelectedCategory != "All")
                {
                    await LoadCategoryStatsAsync(cancellationToken);
                }

                StatusMessage = $"필터 적용 완료: {SelectedCategory}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "필터 적용 실패");
                StatusMessage = $"필터 적용 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ExportMarketReportAsync()
        {
            if (MarketDashboard == null)
            {
                StatusMessage = "내보낼 시장 데이터가 없습니다.";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "시장 리포트 생성 중...";

                var report = GenerateMarketReport();

                StatusMessage = "시장 리포트 생성 완료";
                _logger.LogInformation("시장 리포트 생성 완료");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시장 리포트 생성 실패");
                StatusMessage = $"리포트 생성 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ShowItemDetailsAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            try
            {
                StatusMessage = $"아이템 상세 정보: {itemId}";
                _logger.LogDebug("아이템 상세 정보 요청: {ItemId}", itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 상세 정보 표시 실패: {ItemId}", itemId);
                StatusMessage = $"아이템 정보 표시 실패: {ex.Message}";
            }
        }

        #endregion

        #region Data Loading (기존 IApiService 메서드 구조에 맞춤)

        private async Task LoadMarketDashboardAsync(CancellationToken cancellationToken)
        {
            try
            {
                // 기존 IApiService: GetMarketDashboardAsync() → MarketDashboardResponse 직접 반환
                var dashboard = await _apiService.GetMarketDashboardAsync(cancellationToken);

                if (dashboard != null)
                {
                    MarketDashboard = dashboard;

                    // MarketDashboardResponse의 실제 속성들 사용
                    TotalOnlinePlayers = dashboard.TotalOnlinePlayers;
                    TotalTransactions24h = dashboard.TotalTransactions24h;
                    TotalVolume24h = dashboard.TotalVolume24h;
                    ActiveItems = dashboard.ActiveItems;
                    AveragePrice = dashboard.AveragePrice;
                    LastUpdated = DateTime.Now;

                    // 트렌드 데이터 업데이트
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
                // 기존 IApiService: GetPopularItemsAsync() → List<PopularItemResponse> 직접 반환
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
                // 기존 IApiService: GetVolatileItemsAsync() → List<VolatileItemResponse> 직접 반환
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
                // 기존 IApiService: GetCategoryStatsAsync() → List<CategoryStatsResponse> 직접 반환
                var categoryStats = await _apiService.GetCategoryStatsAsync(cancellationToken);

                CategoryStats.Clear();
                CategoryChartData.Clear();

                if (categoryStats != null)
                {
                    foreach (var stat in categoryStats)
                    {
                        CategoryStats.Add(stat);

                        // 기존 CategoryStatsResponse 속성 사용 (실제 정의에 맞춤)
                        CategoryChartData.Add(new CategoryChartData
                        {
                            CategoryName = stat.Category ?? "Unknown", // 'Category' 속성 사용
                            ItemCount = stat.ItemCount,
                            AveragePrice = stat.AveragePrice,
                            TotalVolume = stat.TotalVolume, // long 타입이므로 decimal로 변환
                            MostPopularItem = "해당없음" // 임시값 - 실제 속성 확인 필요
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

        #endregion

        #region Auto Refresh

        private void StartAutoRefresh()
        {
            StopAutoRefresh();
            _marketUpdateTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(RefreshInterval));
        }

        private void StopAutoRefresh()
        {
            _marketUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async void AutoRefreshCallback(object? state)
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

        #endregion

        #region Data Analysis

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

                // 최근 100개 데이터포인트만 유지 (메모리 관리)
                while (TrendData.Count > 100)
                {
                    TrendData.RemoveAt(0);
                }

                // 차트 데이터 업데이트
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

                foreach (var trend in TrendData.TakeLast(50)) // 최근 50개 포인트
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

        private string GenerateMarketReport()
        {
            if (MarketDashboard == null) return string.Empty;

            var report = new System.Text.StringBuilder();
            report.AppendLine("=== HarvestCraft 2 시장 분석 리포트 ===");
            report.AppendLine($"생성 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            report.AppendLine("## 시장 개요");
            report.AppendLine($"온라인 플레이어: {TotalOnlinePlayers:N0}명");
            report.AppendLine($"활성 아이템: {ActiveItems:N0}개");
            report.AppendLine($"24시간 거래량: {TotalTransactions24h:N0}건");
            report.AppendLine($"24시간 거래액: {TotalVolume24h:C}");
            report.AppendLine($"평균 가격: {AveragePrice:C}");
            report.AppendLine($"마지막 업데이트: {LastUpdated:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            report.AppendLine("## 인기 아이템 TOP 10");
            foreach (var item in PopularItems.Take(10))
            {
                report.AppendLine($"- {item.ItemName ?? item.ItemId}: {item.Volume24h}회 거래, {item.Price:C}");
            }
            report.AppendLine();

            report.AppendLine("## 변동성 큰 아이템 TOP 10");
            foreach (var item in VolatileItems.Take(10))
            {
                report.AppendLine($"- {item.ItemName ?? item.ItemId}: {item.Volatility:P2} 변동, {item.CurrentPrice:C}");
            }

            return report.ToString();
        }

        #endregion

        #region Event Handlers

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

        #endregion

        #region Dispose

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _marketUpdateTimer?.Dispose();
        }

        #endregion
    }

    // ============================================================================
    // 보조 클래스들 (기존 구조에 맞춤)
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

    public class CategoryChartData
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