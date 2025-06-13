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
    public partial class AdminViewModel : ObservableObject
    {
        private readonly IApiService _apiService;
        private readonly ILogger<AdminViewModel> _logger;
        private readonly Timer _statusUpdateTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;

        // ============================================================================
        // Observable Properties - 시스템 모니터링
        // ============================================================================

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isAutoRefreshEnabled = true;

        [ObservableProperty]
        private int refreshInterval = 15;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private SystemMetricsResponse? systemMetrics;

        [ObservableProperty]
        private bool isSystemHealthy = true;

        [ObservableProperty]
        private DateTime lastSystemCheck = DateTime.Now;

        // ============================================================================
        // Observable Properties - 아이템 관리  
        // ============================================================================

        [ObservableProperty]
        private string selectedItemId = string.Empty;

        [ObservableProperty]
        private ItemResponse? selectedItem;

        [ObservableProperty]
        private decimal newBasePrice;

        [ObservableProperty]
        private string priceAdjustmentReason = string.Empty;

        [ObservableProperty]
        private bool isItemUpdateMode;

        // ============================================================================
        // Observable Properties - 데이터 정리
        // ============================================================================

        [ObservableProperty]
        private DateTime cleanupBeforeDate = DateTime.Now.AddDays(-30);

        [ObservableProperty]
        private int retentionDays = 30;

        [ObservableProperty]
        private bool isCleanupInProgress;

        [ObservableProperty]
        private string lastCleanupResult = string.Empty;

        // ============================================================================
        // Collections
        // ============================================================================

        public ObservableCollection<ItemResponse> Items { get; } = new();
        public ObservableCollection<SystemLogEntry> SystemLogs { get; } = new();
        public ObservableCollection<AdminAction> RecentActions { get; } = new();
        public ObservableCollection<PriceAdjustmentHistory> PriceAdjustments { get; } = new();

        public ObservableCollection<int> RetentionDayOptions { get; } = new()
        {
            7, 14, 30, 60, 90, 180, 365
        };

        public ObservableCollection<string> PriceAdjustmentReasons { get; } = new()
        {
            "시장 불균형 조정", "이벤트 가격 조정", "밸런스 패치", "오류 수정",
            "수동 개입", "테스트 목적", "기타"
        };

        public AdminViewModel(IApiService apiService, ILogger<AdminViewModel> logger)
        {
            _apiService = apiService;
            _logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();

            _statusUpdateTimer = new Timer(AutoRefreshCallback, null, Timeout.Infinite, Timeout.Infinite);

            PropertyChanged += OnPropertyChanged;
            _apiService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _apiService.ApiError += OnApiError;
        }

        #region Commands - 시스템 관리

        [RelayCommand]
        private async Task LoadAdminDataAsync()
        {
            IsLoading = true;
            StatusMessage = "관리자 데이터 로딩 중...";

            try
            {
                var cancellationToken = _cancellationTokenSource.Token;

                var tasks = new List<Task>
                {
                    LoadSystemMetricsAsync(cancellationToken),
                    LoadItemsAsync(cancellationToken),
                    LoadRecentActionsAsync()
                };

                await Task.WhenAll(tasks);

                StatusMessage = "관리자 데이터 로드 완료";
                _logger.LogInformation("관리자 데이터 로드 완료");
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "데이터 로드 취소됨";
                _logger.LogInformation("관리자 데이터 로드 취소");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "관리자 데이터 로드 실패");
                StatusMessage = $"데이터 로드 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshSystemMetricsAsync()
        {
            try
            {
                var cancellationToken = _cancellationTokenSource.Token;
                await LoadSystemMetricsAsync(cancellationToken);
                AddRecentAction("시스템 메트릭 새로고침", "시스템 상태 수동 새로고침");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시스템 메트릭 새로고침 실패");
                StatusMessage = $"시스템 메트릭 새로고침 실패: {ex.Message}";
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
                AddRecentAction("자동 새로고침 활성화", $"{RefreshInterval}초 간격으로 설정");
            }
            else
            {
                StopAutoRefresh();
                StatusMessage = "자동 새로고침 중지";
                AddRecentAction("자동 새로고침 비활성화", "수동 새로고침 모드로 전환");
            }
        }

        [RelayCommand]
        private async Task TestApiConnectionAsync()
        {
            IsLoading = true;
            StatusMessage = "API 연결 테스트 중...";

            try
            {
                var cancellationToken = _cancellationTokenSource.Token;
                var isConnected = await _apiService.TestConnectionAsync(cancellationToken);

                if (isConnected)
                {
                    StatusMessage = "API 연결 성공";
                    IsSystemHealthy = true;
                    AddRecentAction("API 연결 테스트", "연결 성공");
                }
                else
                {
                    StatusMessage = "API 연결 실패";
                    IsSystemHealthy = false;
                    AddRecentAction("API 연결 테스트", "연결 실패");
                }

                LastSystemCheck = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API 연결 테스트 실패");
                StatusMessage = $"API 연결 테스트 실패: {ex.Message}";
                IsSystemHealthy = false;
                AddRecentAction("API 연결 테스트", $"오류: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Commands - 아이템 관리

        [RelayCommand]
        private async Task SelectItemAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            SelectedItemId = itemId;
            SelectedItem = Items.FirstOrDefault(i => i.ItemId == itemId);

            if (SelectedItem != null)
            {
                NewBasePrice = SelectedItem.CurrentPrice; // BasePrice → CurrentPrice
                StatusMessage = $"아이템 선택: {SelectedItem.DisplayName}";
            }
        }

        [RelayCommand]
        private async Task UpdateItemAsync()
        {
            if (SelectedItem == null || string.IsNullOrEmpty(SelectedItemId))
            {
                StatusMessage = "아이템을 먼저 선택해주세요.";
                return;
            }

            IsLoading = true;
            StatusMessage = "아이템 정보 업데이트 중...";

            try
            {
                var cancellationToken = _cancellationTokenSource.Token;

                var updateRequest = new UpdateItemRequest
                {
                    DisplayName = SelectedItem.DisplayName,
                    Category = SelectedItem.Category,
                    IsEnabled = SelectedItem.IsEnabled,
                    MinPrice = SelectedItem.MinPrice,
                    MaxPrice = SelectedItem.MaxPrice
                };

                var updatedItem = await _apiService.UpdateItemAsync(SelectedItemId, updateRequest, cancellationToken);

                if (updatedItem != null)
                {
                    var index = Items.ToList().FindIndex(i => i.ItemId == SelectedItemId);
                    if (index >= 0)
                    {
                        Items[index] = updatedItem;
                        SelectedItem = updatedItem;
                    }

                    StatusMessage = $"아이템 정보 업데이트 완료: {updatedItem.DisplayName}";
                    AddRecentAction("아이템 정보 업데이트", $"{updatedItem.DisplayName} 정보 변경");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 업데이트 실패: {ItemId}", SelectedItemId);
                StatusMessage = $"아이템 업데이트 실패: {ex.Message}";
                AddRecentAction("아이템 업데이트 실패", $"{SelectedItemId}: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AdjustItemPriceAsync()
        {
            if (string.IsNullOrEmpty(SelectedItemId) || NewBasePrice <= 0)
            {
                StatusMessage = "아이템과 올바른 가격을 설정해주세요.";
                return;
            }

            IsLoading = true;
            StatusMessage = "가격 조정 중...";

            try
            {
                var cancellationToken = _cancellationTokenSource.Token;
                var reason = string.IsNullOrEmpty(PriceAdjustmentReason) ? "관리자 수동 조정" : PriceAdjustmentReason;

                var adjustedPrice = await _apiService.AdjustPriceAsync(SelectedItemId, NewBasePrice, reason, cancellationToken);

                if (adjustedPrice != null)
                {
                    StatusMessage = $"가격 조정 완료: {SelectedItemId} → {NewBasePrice:C}";

                    PriceAdjustments.Insert(0, new PriceAdjustmentHistory
                    {
                        ItemId = SelectedItemId,
                        ItemName = SelectedItem?.DisplayName ?? SelectedItemId,
                        OldPrice = SelectedItem?.CurrentPrice ?? 0m, // BasePrice → CurrentPrice
                        NewPrice = NewBasePrice,
                        Reason = reason,
                        AdjustedAt = DateTime.Now,
                        AdjustedBy = "Admin"
                    });

                    AddRecentAction("가격 수동 조정", $"{SelectedItemId}: {NewBasePrice:C} (사유: {reason})");
                    await LoadItemsAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 조정 실패: {ItemId}", SelectedItemId);
                StatusMessage = $"가격 조정 실패: {ex.Message}";
                AddRecentAction("가격 조정 실패", $"{SelectedItemId}: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshItemsAsync()
        {
            try
            {
                var cancellationToken = _cancellationTokenSource.Token;
                await LoadItemsAsync(cancellationToken);
                AddRecentAction("아이템 목록 새로고침", "전체 아이템 정보 갱신");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 목록 새로고침 실패");
                StatusMessage = $"아이템 목록 새로고침 실패: {ex.Message}";
            }
        }

        #endregion

        #region Commands - 데이터 관리

        [RelayCommand]
        private async Task ExecuteDataCleanupAsync()
        {
            IsCleanupInProgress = true;
            StatusMessage = "데이터 정리 실행 중...";

            try
            {
                var cancellationToken = _cancellationTokenSource.Token;
                var beforeDate = DateTime.Now.AddDays(-RetentionDays);

                var cleanupResult = await _apiService.CleanupDataAsync(beforeDate, cancellationToken);

                if (cleanupResult != null && cleanupResult.Success)
                {
                    var totalDeleted = cleanupResult.DeletedTransactions + cleanupResult.DeletedPriceHistory;
                    LastCleanupResult = $"정리 완료: 거래 {cleanupResult.DeletedTransactions}건, 가격히스토리 {cleanupResult.DeletedPriceHistory}건 삭제";
                    StatusMessage = "데이터 정리 완료";

                    AddRecentAction("데이터 정리 실행", $"{RetentionDays}일 이전 데이터 정리: 총 {totalDeleted}건 삭제");
                    AddSystemLog("INFO", "데이터 정리 완료", LastCleanupResult);
                }
                else
                {
                    LastCleanupResult = $"정리 실패: {cleanupResult?.ErrorMessage ?? "알 수 없는 오류"}";
                    StatusMessage = LastCleanupResult;
                    AddRecentAction("데이터 정리 실패", cleanupResult?.ErrorMessage ?? "알 수 없는 오류");
                    AddSystemLog("ERROR", "데이터 정리 실패", LastCleanupResult);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "데이터 정리 실패");
                LastCleanupResult = $"정리 실패: {ex.Message}";
                StatusMessage = $"데이터 정리 실패: {ex.Message}";
                AddRecentAction("데이터 정리 실패", ex.Message);
                AddSystemLog("ERROR", "데이터 정리 실패", ex.Message);
            }
            finally
            {
                IsCleanupInProgress = false;
            }
        }

        [RelayCommand]
        private void SetRetentionDays(int days)
        {
            RetentionDays = days;
            CleanupBeforeDate = DateTime.Now.AddDays(-days);
            StatusMessage = $"보존 기간 설정: {days}일";
        }

        [RelayCommand]
        private async Task ExportSystemLogsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "시스템 로그 내보내기 중...";

                var logData = GenerateSystemLogReport();

                StatusMessage = "시스템 로그 내보내기 완료";
                AddRecentAction("시스템 로그 내보내기", $"{SystemLogs.Count}건의 로그 내보내기");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시스템 로그 내보내기 실패");
                StatusMessage = $"로그 내보내기 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Data Loading

        private async Task LoadSystemMetricsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var metrics = await _apiService.GetSystemMetricsAsync(cancellationToken);

                if (metrics != null)
                {
                    SystemMetrics = metrics;
                    EvaluateSystemHealth(metrics);
                    LastSystemCheck = DateTime.Now;

                    _logger.LogDebug("시스템 메트릭 로드 완료");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시스템 메트릭 로드 실패");
                IsSystemHealthy = false;
                throw;
            }
        }

        private async Task LoadItemsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var items = await _apiService.GetItemsAsync(cancellationToken);

                Items.Clear();
                foreach (var item in items.OrderBy(i => i.DisplayName))
                {
                    Items.Add(item);
                }

                _logger.LogDebug("아이템 목록 로드 완료: {Count}개", Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 목록 로드 실패");
                throw;
            }
        }

        private async Task LoadRecentActionsAsync()
        {
            try
            {
                _logger.LogDebug("최근 관리자 작업 로드 완료: {Count}개", RecentActions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "최근 작업 로드 실패");
            }
        }

        #endregion

        #region Auto Refresh

        private void StartAutoRefresh()
        {
            StopAutoRefresh();
            _statusUpdateTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(RefreshInterval));
        }

        private void StopAutoRefresh()
        {
            _statusUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async void AutoRefreshCallback(object? state)
        {
            if (!IsAutoRefreshEnabled || IsLoading) return;

            try
            {
                var cancellationToken = _cancellationTokenSource.Token;
                await LoadSystemMetricsAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "관리자 데이터 자동 새로고침 실패");
            }
        }

        #endregion

        #region Helper Methods

        private void EvaluateSystemHealth(SystemMetricsResponse metrics)
        {
            try
            {
                // 실제 SystemMetricsResponse 속성 사용
                var healthIndicators = new List<bool>
                {
                    metrics.Performance.CpuUsage < 80.0,      // Performance.CpuUsage
                    metrics.Performance.MemoryUsage < 90.0,   // Performance.MemoryUsage (long 타입)
                    metrics.Performance.AverageResponseTime < 500, // AverageResponseTime
                    metrics.ActivePlayers > 0,                // 활성 플레이어가 있는지
                    metrics.TotalItems > 0                     // 아이템이 등록되어 있는지
                };

                IsSystemHealthy = healthIndicators.All(x => x);

                if (!IsSystemHealthy)
                {
                    AddSystemLog("WARN", "시스템 건강도 경고", "시스템 성능 지표가 임계값을 초과했습니다.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시스템 건강도 평가 실패");
                IsSystemHealthy = false;
            }
        }

        private void AddRecentAction(string actionType, string description)
        {
            RecentActions.Insert(0, new AdminAction
            {
                ActionType = actionType,
                Description = description,
                Timestamp = DateTime.Now,
                User = "Admin"
            });

            while (RecentActions.Count > 50)
            {
                RecentActions.RemoveAt(RecentActions.Count - 1);
            }
        }

        private void AddSystemLog(string level, string category, string message)
        {
            SystemLogs.Insert(0, new SystemLogEntry
            {
                Level = level,
                Category = category,
                Message = message,
                Timestamp = DateTime.Now
            });

            while (SystemLogs.Count > 100)
            {
                SystemLogs.RemoveAt(SystemLogs.Count - 1);
            }
        }

        private string GenerateSystemLogReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== HarvestCraft 2 시스템 로그 리포트 ===");
            report.AppendLine($"생성 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"로그 기간: {SystemLogs.LastOrDefault()?.Timestamp:yyyy-MM-dd HH:mm:ss} ~ {SystemLogs.FirstOrDefault()?.Timestamp:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"총 로그 수: {SystemLogs.Count}건");
            report.AppendLine();

            foreach (var log in SystemLogs.Take(100))
            {
                report.AppendLine($"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}] [{log.Level}] {log.Category}: {log.Message}");
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
                        AddRecentAction("새로고침 간격 변경", $"{RefreshInterval}초로 변경");
                    }
                    break;

                case nameof(RetentionDays):
                    CleanupBeforeDate = DateTime.Now.AddDays(-RetentionDays);
                    break;
            }
        }

        private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
        {
            try
            {
                if (e.IsConnected)
                {
                    StatusMessage = "API 서버 연결됨";
                    IsSystemHealthy = true;
                    if (IsAutoRefreshEnabled)
                    {
                        StartAutoRefresh();
                    }
                    AddSystemLog("INFO", "연결", "API 서버 연결 성공");
                }
                else
                {
                    StatusMessage = $"API 서버 연결 끊어짐: {e.ErrorMessage}";
                    IsSystemHealthy = false;
                    StopAutoRefresh();
                    AddSystemLog("ERROR", "연결", $"API 서버 연결 실패: {e.ErrorMessage}");
                }

                _logger.LogInformation("API 연결 상태 변경: {IsConnected}", e.IsConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "연결 상태 변경 이벤트 처리 실패");
            }
        }

        private void OnApiError(object? sender, ApiErrorEventArgs e)
        {
            try
            {
                var errorMessage = $"API 오류: {e.Method} {e.Endpoint} - {e.ErrorMessage}";
                StatusMessage = errorMessage;
                AddSystemLog("ERROR", "API", errorMessage);
                AddRecentAction("API 오류", $"{e.Method} {e.Endpoint}: {e.StatusCode}");

                _logger.LogError("API 오류 이벤트: {Method} {Endpoint} {StatusCode} {Message}",
                    e.Method, e.Endpoint, e.StatusCode, e.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API 오류 이벤트 처리 실패");
            }
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _statusUpdateTimer?.Dispose();

            if (_apiService != null)
            {
                _apiService.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _apiService.ApiError -= OnApiError;
            }
        }

        #endregion
    }

    // ============================================================================
    // 보조 클래스들
    // ============================================================================

    public class AdminAction
    {
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string User { get; set; } = string.Empty;
    }

    public class SystemLogEntry
    {
        public string Level { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class PriceAdjustmentHistory
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime AdjustedAt { get; set; }
        public string AdjustedBy { get; set; } = string.Empty;
    }
}