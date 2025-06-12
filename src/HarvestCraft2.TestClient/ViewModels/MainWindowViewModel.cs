using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HarvestCraft2.TestClient.ViewModels
{
    /// <summary>
    /// 메인 윈도우의 ViewModel
    /// 기존 MainWindow.xaml 구조에 맞게 구현
    /// </summary>
    public class MainWindowViewModel : ViewModelBase
    {
        #region 필드

        private readonly IConfiguration _configuration;
        private readonly ILogger<MainWindowViewModel> _logger;
        private System.Timers.Timer? _statusTimer;

        #endregion

        #region 생성자

        public MainWindowViewModel(IConfiguration configuration, ILogger<MainWindowViewModel> logger)
        {
            _configuration = configuration;
            _logger = logger;

            InitializeCommands();
            InitializeTimer();
            LoadConfiguration();
        }

        #endregion

        #region 속성

        #region 연결 상태

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                SetProperty(ref _isConnected, value);
                ConnectionStatus = value ? "연결됨" : "연결 안됨";
                ConnectionColor = value ? "#4CAF50" : "#F44336";
            }
        }

        private string _connectionStatus = "연결 안됨";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        private string _connectionColor = "#F44336";
        public string ConnectionColor
        {
            get => _connectionColor;
            set => SetProperty(ref _connectionColor, value);
        }

        private string _serverUrl = "localhost:7001";
        public string ServerUrl
        {
            get => _serverUrl;
            set => SetProperty(ref _serverUrl, value);
        }

        private string _apiVersion = "v1.0";
        public string ApiVersion
        {
            get => _apiVersion;
            set => SetProperty(ref _apiVersion, value);
        }

        #endregion

        #region 탭 관리

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                SetProperty(ref _selectedTabIndex, value);
                OnTabChanged();
            }
        }

        #endregion

        #region 상태바

        private string _progressText = "Phase 1: 기본 구조 (67%)";
        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        private string _currentTime = DateTime.Now.ToString("HH:mm:ss");
        public string CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        #endregion

        #region 대시보드 데이터

        private int _totalItems = 120;
        public int TotalItems
        {
            get => _totalItems;
            set => SetProperty(ref _totalItems, value);
        }

        private int _onlinePlayers = 5;
        public int OnlinePlayers
        {
            get => _onlinePlayers;
            set => SetProperty(ref _onlinePlayers, value);
        }

        private decimal _totalVolume = 15420.50m;
        public decimal TotalVolume
        {
            get => _totalVolume;
            set => SetProperty(ref _totalVolume, value);
        }

        private decimal _avgPrice = 12.75m;
        public decimal AvgPrice
        {
            get => _avgPrice;
            set => SetProperty(ref _avgPrice, value);
        }

        #endregion

        #region 설정

        private bool _autoRefresh = true;
        public bool AutoRefresh
        {
            get => _autoRefresh;
            set => SetProperty(ref _autoRefresh, value);
        }

        private bool _showNotifications = true;
        public bool ShowNotifications
        {
            get => _showNotifications;
            set => SetProperty(ref _showNotifications, value);
        }

        private bool _showAdvancedFeatures = false;
        public bool ShowAdvancedFeatures
        {
            get => _showAdvancedFeatures;
            set => SetProperty(ref _showAdvancedFeatures, value);
        }

        #endregion

        #endregion

        #region 명령어

        public ICommand ConnectCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand SettingsCommand { get; private set; } = null!;

        #endregion

        #region 초기화

        private void InitializeCommands()
        {
            ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !IsBusy);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
            SettingsCommand = new RelayCommand(OpenSettings);
        }

        private void InitializeTimer()
        {
            _statusTimer = new System.Timers.Timer(1000); // 1초마다
            _statusTimer.Elapsed += (s, e) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            _statusTimer.Start();
        }

        private void LoadConfiguration()
        {
            try
            {
                var apiSettings = _configuration.GetSection("ApiSettings");
                ServerUrl = apiSettings.GetValue<string>("BaseUrl") ?? "localhost:7001";

                var uiSettings = _configuration.GetSection("UISettings");
                AutoRefresh = uiSettings.GetValue<bool>("EnableAnimations");
                ShowNotifications = uiSettings.GetValue<bool>("ShowNotifications");

                StatusMessage = "설정을 로드했습니다.";
                _logger.LogInformation("MainWindow 설정이 로드되었습니다.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"설정 로드 실패: {ex.Message}";
                _logger.LogError(ex, "설정 로드 중 오류가 발생했습니다.");
            }
        }

        #endregion

        #region 메서드

        private async Task ConnectAsync()
        {
            await ExecuteAsync(async () =>
            {
                StatusMessage = "서버에 연결 중...";
                _logger.LogInformation("API 서버 연결을 시도합니다: {ServerUrl}", ServerUrl);

                // 실제 연결 로직은 Phase 2에서 구현
                await Task.Delay(2000); // 시뮬레이션

                IsConnected = !IsConnected; // 테스트용 토글

                if (IsConnected)
                {
                    StatusMessage = "서버에 연결되었습니다.";
                    await RefreshDashboardData();
                }
                else
                {
                    StatusMessage = "서버 연결이 해제되었습니다.";
                }

                _logger.LogInformation("연결 상태가 변경되었습니다: {IsConnected}", IsConnected);
            });
        }

        private async Task RefreshAsync()
        {
            await ExecuteAsync(async () =>
            {
                StatusMessage = "데이터를 새로고침하는 중...";
                _logger.LogInformation("데이터 새로고침을 시작합니다.");

                await RefreshDashboardData();

                StatusMessage = $"데이터 새로고침 완료 - {DateTime.Now:HH:mm:ss}";
                _logger.LogInformation("데이터 새로고침이 완료되었습니다.");
            });
        }

        private async Task RefreshDashboardData()
        {
            if (!IsConnected) return;

            // 실제 API 호출은 Phase 2에서 구현
            await Task.Delay(1000); // 시뮬레이션

            // 테스트 데이터 업데이트
            var random = new Random();
            OnlinePlayers = random.Next(1, 20);
            TotalVolume = (decimal)(random.NextDouble() * 50000);
            AvgPrice = (decimal)(random.NextDouble() * 100);
        }

        private void OnTabChanged()
        {
            _logger.LogDebug("탭이 변경되었습니다: {SelectedTabIndex}", SelectedTabIndex);

            // 탭별 초기화 로직 (Phase 3에서 구현)
            StatusMessage = $"탭 {SelectedTabIndex + 1}이 선택되었습니다.";
        }

        private void OpenSettings()
        {
            SelectedTabIndex = 5; // 설정 탭으로 이동
            StatusMessage = "설정 탭을 열었습니다.";
        }

        #endregion

        #region IDisposable

        protected override void Finalize()
        {
            _statusTimer?.Stop();
            _statusTimer?.Dispose();
        }

        #endregion
    }
}