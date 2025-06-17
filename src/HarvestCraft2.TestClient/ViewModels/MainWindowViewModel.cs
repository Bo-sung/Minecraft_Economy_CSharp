// ============================================================================
// ViewModels/MainWindowViewModel.cs - 메인 윈도우 뷰모델 (완전판)
// ============================================================================

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HarvestCraft2.TestClient.Models;
using HarvestCraft2.TestClient.Services;
using System.Text.Json;
using System.Text;
using System.Globalization;
using System.IO;
using System.Linq;

namespace HarvestCraft2.TestClient.ViewModels
{
    /// <summary>
    /// 메인 윈도우의 ViewModel
    /// 실시간 차트 및 시스템 상태 관리 포함
    /// </summary>
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        #region 필드

        private readonly IConfiguration _configuration;
        private readonly ILogger<MainWindowViewModel> _logger;
        private readonly IApiService _apiService;
        private readonly IPlayerService _playerService;
        private readonly IChartService _chartService;
        private System.Timers.Timer? _statusTimer;

        #endregion

        #region 생성자

        public MainWindowViewModel(
            IConfiguration configuration,
            ILogger<MainWindowViewModel> logger,
            IApiService apiService,
            IPlayerService playerService,
            IChartService chartService)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _chartService = chartService ?? throw new ArgumentNullException(nameof(chartService));

            // PriceViewModel 초기화
            PriceViewModel = new PriceViewModel(apiService, chartService, logger.CreateLogger<PriceViewModel>());

            InitializeCommands();
            InitializeTimer();
            InitializeCollections();
            LoadConfiguration();

            // ApiService 이벤트 구독
            SubscribeToApiServiceEvents();

            _logger.LogInformation("MainWindowViewModel 초기화 완료");
        }

        #endregion

        #region 뷰모델 속성

        /// <summary>
        /// 가격 모니터링 뷰모델
        /// </summary>
        public PriceViewModel PriceViewModel { get; }

        #endregion

        #region 연결 상태 속성

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                SetProperty(ref _isConnected, value);
                ConnectionStatusText = value ? "연결됨" : "연결 안됨";
                ConnectionColor = value ? "#4CAF50" : "#F44336";
                OnPropertyChanged(nameof(ConnectionStatusText));
                OnPropertyChanged(nameof(ConnectionColor));
            }
        }

        private string _connectionStatusText = "연결 확인 중";
        public string ConnectionStatusText
        {
            get => _connectionStatusText;
            set => SetProperty(ref _connectionStatusText, value);
        }

        private string _connectionColor = "#FFC107";
        public string ConnectionColor
        {
            get => _connectionColor;
            set => SetProperty(ref _connectionColor, value);
        }

        private string _apiEndpoint = string.Empty;
        public string ApiEndpoint
        {
            get => _apiEndpoint;
            set => SetProperty(ref _apiEndpoint, value);
        }

        private string _apiVersion = string.Empty;
        public string ApiVersion
        {
            get => _apiVersion;
            set => SetProperty(ref _apiVersion, value);
        }

        private TimeSpan _responseTime;
        public TimeSpan ResponseTime
        {
            get => _responseTime;
            set => SetProperty(ref _responseTime, value);
        }

        #endregion

        #region 시스템 상태 속성

        private string _systemStatus = "시스템 준비";
        public string SystemStatus
        {
            get => _systemStatus;
            set => SetProperty(ref _systemStatus, value);
        }

        private int _onlinePlayersCount;
        public int OnlinePlayersCount
        {
            get => _onlinePlayersCount;
            set => SetProperty(ref _onlinePlayersCount, value);
        }

        private int _totalItemsCount;
        public int TotalItemsCount
        {
            get => _totalItemsCount;
            set => SetProperty(ref _totalItemsCount, value);
        }

        private decimal _totalTransactionVolume;
        public decimal TotalTransactionVolume
        {
            get => _totalTransactionVolume;
            set => SetProperty(ref _totalTransactionVolume, value);
        }

        private DateTime _lastDataUpdate = DateTime.Now;
        public DateTime LastDataUpdate
        {
            get => _lastDataUpdate;
            set => SetProperty(ref _lastDataUpdate, value);
        }

        #endregion

        #region 플레이어 관리 속성

        private string _selectedPlayerId = string.Empty;
        public string SelectedPlayerId
        {
            get => _selectedPlayerId;
            set => SetProperty(ref _selectedPlayerId, value);
        }

        private string _selectedPlayerName = string.Empty;
        public string SelectedPlayerName
        {
            get => _selectedPlayerName;
            set => SetProperty(ref _selectedPlayerName, value);
        }

        private decimal _selectedPlayerBalance;
        public decimal SelectedPlayerBalance
        {
            get => _selectedPlayerBalance;
            set => SetProperty(ref _selectedPlayerBalance, value);
        }

        private string _newPlayerName = string.Empty;
        public string NewPlayerName
        {
            get => _newPlayerName;
            set => SetProperty(ref _newPlayerName, value);
        }

        private decimal _newPlayerBalance = 10000;
        public decimal NewPlayerBalance
        {
            get => _newPlayerBalance;
            set => SetProperty(ref _newPlayerBalance, value);
        }

        #endregion

        #region 컬렉션들

        public ObservableCollection<PlayerInfoResponse> AllPlayers { get; } = new();
        public ObservableCollection<string> RecentActivities { get; } = new();
        public ObservableCollection<SystemMetric> SystemMetrics { get; } = new();

        #endregion

        #region 명령어들

        public ICommand TestConnectionCommand { get; private set; } = null!;
        public ICommand RefreshDataCommand { get; private set; } = null!;
        public ICommand CreatePlayerCommand { get; private set; } = null!;
        public ICommand DeletePlayerCommand { get; private set; } = null!;
        public ICommand LoadPlayersCommand { get; private set; } = null!;
        public ICommand ClearActivitiesCommand { get; private set; } = null!;
        public ICommand ExportLogsCommand { get; private set; } = null!;

        #endregion

        #region 초기화 메서드들

        private void InitializeCommands()
        {
            TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync());
            RefreshDataCommand = new RelayCommand(async () => await RefreshAllDataAsync());
            CreatePlayerCommand = new RelayCommand(async () => await CreatePlayerAsync(),
                () => !string.IsNullOrWhiteSpace(NewPlayerName) && NewPlayerBalance > 0);
            DeletePlayerCommand = new RelayCommand(async () => await DeletePlayerAsync(),
                () => !string.IsNullOrEmpty(SelectedPlayerId));
            LoadPlayersCommand = new RelayCommand(async () => await LoadPlayersAsync());
            ClearActivitiesCommand = new RelayCommand(ClearActivities);
            ExportLogsCommand = new RelayCommand(async () => await ExportLogsAsync());
        }

        private void InitializeTimer()
        {
            _statusTimer = new System.Timers.Timer(TimeSpan.FromSeconds(30).TotalMilliseconds);
            _statusTimer.Elapsed += async (sender, e) => await UpdateSystemStatusAsync();
            _statusTimer.AutoReset = true;
            _statusTimer.Start();
        }

        private void InitializeCollections()
        {
            RecentActivities.Add($"{DateTime.Now:HH:mm:ss} - 애플리케이션 시작됨");
            RecentActivities.Add($"{DateTime.Now:HH:mm:ss} - API 연결 확인 중...");
        }

        private void LoadConfiguration()
        {
            try
            {
                ApiEndpoint = _configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
                ApiVersion = _configuration["ApiSettings:Version"] ?? "v1.0";

                AddActivity("설정 로드 완료");
                _logger.LogDebug("설정 로드: Endpoint={Endpoint}, Version={Version}", ApiEndpoint, ApiVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "설정 로드 실패");
                AddActivity("설정 로드 실패");
            }
        }

        private void SubscribeToApiServiceEvents()
        {
            try
            {
                if (_apiService != null)
                {
                    _apiService.ConnectionStatusChanged += OnConnectionStatusChanged;
                    _apiService.DataUpdated += OnDataUpdated;
                }

                _logger.LogDebug("API 이벤트 구독 완료");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API 이벤트 구독 실패");
            }
        }

        #endregion

        #region 명령어 구현들

        private async Task TestConnectionAsync()
        {
            await ExecuteAsync(async () =>
            {
                StatusMessage = "연결 테스트 중...";
                AddActivity("API 연결 테스트 시작");

                var startTime = DateTime.Now;

                try
                {
                    var result = await _apiService.TestConnectionAsync();
                    ResponseTime = DateTime.Now - startTime;

                    IsConnected = result;
                    SystemStatus = result ? "시스템 정상" : "연결 실패";

                    var message = result ? "연결 성공" : "연결 실패";
                    StatusMessage = $"{message} (응답시간: {ResponseTime.TotalMilliseconds:F0}ms)";
                    AddActivity($"API 연결 테스트: {message}");

                    _logger.LogInformation("연결 테스트 결과: {Result}, 응답시간: {ResponseTime}ms",
                        result, ResponseTime.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    IsConnected = false;
                    SystemStatus = "연결 오류";
                    StatusMessage = $"연결 테스트 실패: {ex.Message}";
                    AddActivity($"연결 테스트 오류: {ex.Message}");

                    _logger.LogError(ex, "연결 테스트 실패");
                }
            });
        }

        private async Task RefreshAllDataAsync()
        {
            await ExecuteAsync(async () =>
            {
                StatusMessage = "데이터 새로고침 중...";
                AddActivity("전체 데이터 새로고침 시작");

                try
                {
                    // 시스템 상태 업데이트
                    await UpdateSystemStatusAsync();

                    // 플레이어 정보 새로고침
                    await LoadPlayersAsync();

                    // 가격 데이터 새로고침 (PriceViewModel 통해)
                    await PriceViewModel.LoadAllPricesCommand.ExecuteAsync(null);

                    LastDataUpdate = DateTime.Now;
                    StatusMessage = "데이터 새로고침 완료";
                    AddActivity("전체 데이터 새로고침 완료");

                    _logger.LogInformation("전체 데이터 새로고침 완료");
                }
                catch (Exception ex)
                {
                    StatusMessage = $"데이터 새로고침 실패: {ex.Message}";
                    AddActivity($"데이터 새로고침 오류: {ex.Message}");

                    _logger.LogError(ex, "데이터 새로고침 실패");
                }
            });
        }

        private async Task CreatePlayerAsync()
        {
            await ExecuteAsync(async () =>
            {
                StatusMessage = $"플레이어 생성 중: {NewPlayerName}";

                try
                {
                    var request = new CreatePlayerRequest
                    {
                        PlayerName = NewPlayerName,
                        InitialBalance = NewPlayerBalance
                    };

                    var result = await _apiService.CreatePlayerAsync(request);

                    if (result != null)
                    {
                        await LoadPlayersAsync();
                        StatusMessage = $"플레이어 생성 완료: {result.PlayerName}";
                        AddActivity($"새 플레이어 생성: {result.PlayerName} (잔액: {result.Balance:C})");

                        // 입력 필드 초기화
                        NewPlayerName = string.Empty;
                        NewPlayerBalance = 10000;

                        _logger.LogInformation("플레이어 생성 완료: {PlayerName}, {Balance}",
                            result.PlayerName, result.Balance);
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"플레이어 생성 실패: {ex.Message}";
                    AddActivity($"플레이어 생성 오류: {ex.Message}");

                    _logger.LogError(ex, "플레이어 생성 실패: {PlayerName}", NewPlayerName);
                }
            });
        }

        private async Task DeletePlayerAsync()
        {
            await ExecuteAsync(async () =>
            {
                if (string.IsNullOrEmpty(SelectedPlayerId)) return;

                StatusMessage = $"플레이어 삭제 중: {SelectedPlayerName}";

                try
                {
                    await _apiService.DeletePlayerAsync(SelectedPlayerId);
                    await LoadPlayersAsync();

                    StatusMessage = $"플레이어 삭제 완료: {SelectedPlayerName}";
                    AddActivity($"플레이어 삭제: {SelectedPlayerName}");

                    // 선택 초기화
                    SelectedPlayerId = string.Empty;
                    SelectedPlayerName = string.Empty;
                    SelectedPlayerBalance = 0;

                    _logger.LogInformation("플레이어 삭제 완료: {PlayerName}", SelectedPlayerName);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"플레이어 삭제 실패: {ex.Message}";
                    AddActivity($"플레이어 삭제 오류: {ex.Message}");

                    _logger.LogError(ex, "플레이어 삭제 실패: {PlayerId}", SelectedPlayerId);
                }
            });
        }

        private async Task LoadPlayersAsync()
        {
            try
            {
                var players = await _apiService.GetAllPlayersAsync();

                AllPlayers.Clear();
                foreach (var player in players.OrderBy(p => p.PlayerName))
                {
                    AllPlayers.Add(player);
                }

                OnlinePlayersCount = AllPlayers.Count;
                AddActivity($"플레이어 정보 로드: {AllPlayers.Count}명");

                _logger.LogDebug("플레이어 로드 완료: {Count}명", AllPlayers.Count);
            }
            catch (Exception ex)
            {
                AddActivity($"플레이어 로드 오류: {ex.Message}");
                _logger.LogError(ex, "플레이어 로드 실패");
            }
        }

        private void ClearActivities()
        {
            Execute(() =>
            {
                RecentActivities.Clear();
                AddActivity("활동 로그 초기화됨");
                StatusMessage = "활동 로그가 초기화되었습니다.";
            });
        }

        private async Task ExportLogsAsync()
        {
            await ExecuteAsync(async () =>
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var fileName = $"HarvestCraft2_Logs_{timestamp}.json";
                    var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

                    var logData = new
                    {
                        ExportTime = DateTime.Now,
                        SystemStatus = new
                        {
                            IsConnected,
                            ConnectionStatusText,
                            ApiEndpoint,
                            ApiVersion,
                            ResponseTime = ResponseTime.TotalMilliseconds,
                            SystemStatus,
                            OnlinePlayersCount,
                            TotalItemsCount,
                            TotalTransactionVolume,
                            LastDataUpdate
                        },
                        RecentActivities = RecentActivities.ToList(),
                        Players = AllPlayers.ToList(),
                        SystemMetrics = SystemMetrics.ToList()
                    };

                    var json = JsonSerializer.Serialize(logData, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });

                    await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);

                    StatusMessage = $"로그 내보내기 완료: {fileName}";
                    AddActivity($"로그 내보내기: {fileName}");

                    _logger.LogInformation("로그 내보내기 완료: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"로그 내보내기 실패: {ex.Message}";
                    AddActivity($"로그 내보내기 오류: {ex.Message}");

                    _logger.LogError(ex, "로그 내보내기 실패");
                }
            });
        }

        #endregion

        #region 헬퍼 메서드들

        private async Task UpdateSystemStatusAsync()
        {
            try
            {
                if (!IsConnected) return;

                // 시스템 메트릭 업데이트
                var dashboardData = await _apiService.GetDashboardDataAsync();
                if (dashboardData != null)
                {
                    OnlinePlayersCount = dashboardData.OnlinePlayersCount;
                    TotalItemsCount = dashboardData.TotalItemsCount;
                    TotalTransactionVolume = dashboardData.TotalTransactionVolume;
                    LastDataUpdate = DateTime.Now;

                    SystemStatus = "시스템 정상";
                }

                _logger.LogDebug("시스템 상태 업데이트 완료");
            }
            catch (Exception ex)
            {
                SystemStatus = "상태 업데이트 실패";
                _logger.LogError(ex, "시스템 상태 업데이트 실패");
            }
        }

        private void AddActivity(string message)
        {
            var activity = $"{DateTime.Now:HH:mm:ss} - {message}";

            App.Current?.Dispatcher?.Invoke(() =>
            {
                RecentActivities.Insert(0, activity);

                // 최대 50개 활동만 유지
                while (RecentActivities.Count > 50)
                {
                    RecentActivities.RemoveAt(RecentActivities.Count - 1);
                }
            });
        }

        #endregion

        #region 이벤트 핸들러들

        private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
        {
            IsConnected = e.IsConnected;
            AddActivity($"연결 상태 변경: {(e.IsConnected ? "연결됨" : "연결 해제됨")}");

            _logger.LogInformation("연결 상태 변경: {IsConnected}", e.IsConnected);
        }

        private void OnDataUpdated(object? sender, DataUpdatedEventArgs e)
        {
            LastDataUpdate = e.UpdateTime;
            AddActivity($"데이터 업데이트: {e.DataType}");

            _logger.LogDebug("데이터 업데이트: {DataType} at {UpdateTime}", e.DataType, e.UpdateTime);
        }

        #endregion

        #region IDisposable 구현

        public void Dispose()
        {
            try
            {
                // Timer 해제
                _statusTimer?.Stop();
                _statusTimer?.Dispose();

                // PriceViewModel 해제
                PriceViewModel?.Dispose();

                // API 이벤트 구독 해제
                if (_apiService != null)
                {
                    _apiService.ConnectionStatusChanged -= OnConnectionStatusChanged;
                    _apiService.DataUpdated -= OnDataUpdated;
                }

                _logger.LogInformation("MainWindowViewModel 리소스 해제 완료");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainWindowViewModel Dispose 실패");
            }
        }

        #endregion

        #region 보조 클래스들

        /// <summary>
        /// 시스템 메트릭 데이터
        /// </summary>
        public class SystemMetric
        {
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// 연결 상태 변경 이벤트 인수
        /// </summary>
        public class ConnectionStatusChangedEventArgs : EventArgs
        {
            public bool IsConnected { get; set; }
            public string? Message { get; set; }
        }

        /// <summary>
        /// 데이터 업데이트 이벤트 인수
        /// </summary>
        public class DataUpdatedEventArgs : EventArgs
        {
            public string DataType { get; set; } = string.Empty;
            public DateTime UpdateTime { get; set; } = DateTime.Now;
        }

        #endregion
    }
}