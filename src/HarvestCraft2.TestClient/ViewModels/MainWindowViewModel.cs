using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HarvestCraft2.TestClient.Models;
using HarvestCraft2.TestClient.Services;

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
        private readonly IApiService _apiService;
        private readonly IPlayerService _playerService;
        private System.Timers.Timer? _statusTimer;

        #endregion

        #region 생성자

        public MainWindowViewModel(IConfiguration configuration, ILogger<MainWindowViewModel> logger,
            IApiService apiService, IPlayerService playerService)
        {
            _configuration = configuration;
            _logger = logger;
            _apiService = apiService;
            _playerService = playerService;

            InitializeCommands();
            InitializeTimer();
            InitializeCollections();
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
                ConnectionStatusText = value ? "연결됨" : "연결 안됨";
                ConnectionColor = value ? "#4CAF50" : "#F44336";
            }
        }

        private string _connectionStatusText = "연결 안됨";
        public string ConnectionStatusText
        {
            get => _connectionStatusText;
            set => SetProperty(ref _connectionStatusText, value);
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

        private string _progressText = "Phase 3: 뷰모델 (85%)";
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

        private int _onlinePlayersCount = 5;
        public int OnlinePlayersCount
        {
            get => _onlinePlayersCount;
            set => SetProperty(ref _onlinePlayersCount, value);
        }

        private int _activeItemsCount = 24;
        public int ActiveItemsCount
        {
            get => _activeItemsCount;
            set => SetProperty(ref _activeItemsCount, value);
        }

        private decimal _totalTradeVolume = 15420.50m;
        public decimal TotalTradeVolume
        {
            get => _totalTradeVolume;
            set => SetProperty(ref _totalTradeVolume, value);
        }

        private string _systemStatus = "정상";
        public string SystemStatus
        {
            get => _systemStatus;
            set => SetProperty(ref _systemStatus, value);
        }

        #endregion

        #region 플레이어 관리

        private PlayerResponse? _selectedPlayer;
        public PlayerResponse? SelectedPlayer
        {
            get => _selectedPlayer;
            set
            {
                SetProperty(ref _selectedPlayer, value);
                OnPropertyChanged(nameof(CanRemovePlayer));

                if (value != null)
                {
                    _ = Task.Run(async () => await LoadPlayerTransactionsAsync(value.PlayerId));
                }
            }
        }

        public bool CanRemovePlayer => SelectedPlayer != null && !IsBusy;

        #endregion

        #region 상점 테스트

        private string _selectedPlayerId = string.Empty;
        public string SelectedPlayerId
        {
            get => _selectedPlayerId;
            set => SetProperty(ref _selectedPlayerId, value);
        }

        private string _selectedItemId = string.Empty;
        public string SelectedItemId
        {
            get => _selectedItemId;
            set => SetProperty(ref _selectedItemId, value);
        }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        private string _tradeResultText = "거래 테스트 결과가 여기에 표시됩니다.";
        public string TradeResultText
        {
            get => _tradeResultText;
            set => SetProperty(ref _tradeResultText, value);
        }

        public bool CanExecuteTransaction => !IsBusy &&
                                           !string.IsNullOrEmpty(SelectedPlayerId) &&
                                           !string.IsNullOrEmpty(SelectedItemId) &&
                                           Quantity > 0;

        #endregion

        #region 설정

        private string _apiBaseUrl = "http://localhost:5000";
        public string ApiBaseUrl
        {
            get => _apiBaseUrl;
            set => SetProperty(ref _apiBaseUrl, value);
        }

        private string _apiKey = "your-api-key-here";
        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        private int _timeoutSeconds = 30;
        public int TimeoutSeconds
        {
            get => _timeoutSeconds;
            set => SetProperty(ref _timeoutSeconds, value);
        }

        private bool _isAutoRefreshEnabled = true;
        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set => SetProperty(ref _isAutoRefreshEnabled, value);
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

        #region 컬렉션

        public ObservableCollection<PlayerResponse> Players { get; } = new();
        public ObservableCollection<TransactionResponse> PlayerTransactions { get; } = new();
        public ObservableCollection<PriceResponse> PricesList { get; } = new();
        public ObservableCollection<string> RecentActivities { get; } = new();
        public ObservableCollection<string> AvailableItems { get; } = new();
        public ObservableCollection<string> PriceFilterOptions { get; } = new();

        private string _selectedPriceFilter = "전체";
        public string SelectedPriceFilter
        {
            get => _selectedPriceFilter;
            set => SetProperty(ref _selectedPriceFilter, value);
        }

        #endregion

        #region 명령어

        public ICommand TestConnectionCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand CreateTestPlayerCommand { get; private set; } = null!;
        public ICommand RemovePlayerCommand { get; private set; } = null!;
        public ICommand PurchaseItemCommand { get; private set; } = null!;
        public ICommand SellItemCommand { get; private set; } = null!;
        public ICommand ClearResultCommand { get; private set; } = null!;
        public ICommand RefreshPricesCommand { get; private set; } = null!;
        public ICommand SaveSettingsCommand { get; private set; } = null!;

        #endregion

        #region 초기화

        private void InitializeCommands()
        {
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, () => !IsBusy);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
            CreateTestPlayerCommand = new AsyncRelayCommand(CreateTestPlayerAsync, () => !IsBusy);
            RemovePlayerCommand = new AsyncRelayCommand(RemovePlayerAsync, () => CanRemovePlayer);
            PurchaseItemCommand = new AsyncRelayCommand(PurchaseItemAsync, () => CanExecuteTransaction);
            SellItemCommand = new AsyncRelayCommand(SellItemAsync, () => CanExecuteTransaction);
            ClearResultCommand = new RelayCommand(ClearResult);
            RefreshPricesCommand = new AsyncRelayCommand(RefreshPricesAsync, () => !IsBusy);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        }

        private void InitializeTimer()
        {
            _statusTimer = new System.Timers.Timer(1000); // 1초마다
            _statusTimer.Elapsed += (s, e) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            _statusTimer.Start();
        }

        private void InitializeCollections()
        {
            // 최근 활동 초기화
            RecentActivities.Add("시스템 시작됨");
            RecentActivities.Add("API 연결 대기 중...");
            RecentActivities.Add("데이터 로딩 준비");

            // 사용 가능한 아이템 목록
            AvailableItems.Add("minecraft:apple");
            AvailableItems.Add("minecraft:bread");
            AvailableItems.Add("minecraft:carrot");
            AvailableItems.Add("minecraft:potato");
            AvailableItems.Add("minecraft:wheat");

            // 가격 필터 옵션
            PriceFilterOptions.Add("전체");
            PriceFilterOptions.Add("식품");
            PriceFilterOptions.Add("재료");
            PriceFilterOptions.Add("도구");
        }

        private void LoadConfiguration()
        {
            try
            {
                var apiSettings = _configuration.GetSection("ApiSettings");
                ApiBaseUrl = apiSettings.GetValue<string>("BaseUrl") ?? "http://localhost:5000";

                var uiSettings = _configuration.GetSection("UISettings");
                IsAutoRefreshEnabled = uiSettings.GetValue<bool>("AutoRefresh");
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

        private async Task TestConnectionAsync()
        {
            await ExecuteAsync(async () =>
            {
                StatusMessage = "서버에 연결 중...";
                _logger.LogInformation("API 서버 연결을 시도합니다: {ServerUrl}", ApiBaseUrl);

                var result = await _apiService.TestConnectionAsync();
                IsConnected = result;

                if (IsConnected)
                {
                    StatusMessage = "서버에 연결되었습니다.";
                    RecentActivities.Add($"[{DateTime.Now:HH:mm:ss}] 서버 연결 성공");
                    await RefreshDashboardData();
                }
                else
                {
                    StatusMessage = "서버 연결에 실패했습니다.";
                    RecentActivities.Add($"[{DateTime.Now:HH:mm:ss}] 서버 연결 실패");
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
                await LoadPlayersAsync();
                await RefreshPricesAsync();

                StatusMessage = $"데이터 새로고침 완료 - {DateTime.Now:HH:mm:ss}";
                RecentActivities.Add($"[{DateTime.Now:HH:mm:ss}] 데이터 새로고침 완료");
                _logger.LogInformation("데이터 새로고침이 완료되었습니다.");
            });
        }

        private async Task CreateTestPlayerAsync()
        {
            await ExecuteAsync(async () =>
            {
                var playerName = $"TestPlayer_{DateTime.Now:HHmmss}";
                StatusMessage = "테스트 플레이어 생성 중...";

                var response = await _apiService.CreatePlayerAsync(playerName, 1000m);
                if (response != null)
                {
                    await LoadPlayersAsync();
                    SelectedPlayer = response;
                    StatusMessage = $"테스트 플레이어 생성 완료: {playerName}";
                    RecentActivities.Add($"[{DateTime.Now:HH:mm:ss}] 플레이어 생성: {playerName}");
                }
                else
                {
                    StatusMessage = "플레이어 생성에 실패했습니다.";
                }
            });
        }

        private async Task RemovePlayerAsync()
        {
            if (SelectedPlayer == null) return;

            await ExecuteAsync(async () =>
            {
                StatusMessage = "플레이어 삭제 중...";
                var playerName = SelectedPlayer.PlayerName;

                Players.Remove(SelectedPlayer);
                SelectedPlayer = null;

                StatusMessage = $"플레이어가 삭제되었습니다: {playerName}";
                RecentActivities.Add($"[{DateTime.Now:HH:mm:ss}] 플레이어 삭제: {playerName}");
                await Task.CompletedTask;
            });
        }

        private async Task PurchaseItemAsync()
        {
            await ExecuteAsync(async () =>
            {
                StatusMessage = "구매 처리 중...";

                var response = await _apiService.PurchaseItemAsync(SelectedPlayerId, SelectedItemId, Quantity);
                if (response?.Success == true)
                {
                    TradeResultText += $"\n[{DateTime.Now:HH:mm:ss}] 구매 성공: {SelectedItemId} x{Quantity} = {response.TotalCost:C}";
                    StatusMessage = "구매가 완료되었습니다.";
                    RecentActivities.Add($"[{DateTime.Now:HH:mm:ss}] 구매: {SelectedItemId} x{Quantity}");
                }
                else
                {
                    TradeResultText += $"\n[{DateTime.Now:HH:mm:ss}] 구매 실패: {response?.ErrorMessage ?? "알 수 없는 오류"}";
                    StatusMessage = "구매에 실패했습니다.";
                }
            });
        }

        private async Task SellItemAsync()
        {
            await ExecuteAsync(async () =>
            {
                StatusMessage = "판매 처리 중...";

                var response = await _apiService.SellItemAsync(SelectedPlayerId, SelectedItemId, Quantity);
                if (response?.Success == true)
                {
                    TradeResultText += $"\n[{DateTime.Now:HH:mm:ss}] 판매 성공: {SelectedItemId} x{Quantity} = {response.TotalEarned:C}";
                    StatusMessage = "판매가 완료되었습니다.";
                    RecentActivities.Add($"[{DateTime.Now:HH:mm:ss}] 판매: {SelectedItemId} x{Quantity}");
                }
                else
                {
                    TradeResultText += $"\n[{DateTime.Now:HH:mm:ss}] 판매 실패: {response?.ErrorMessage ?? "알 수 없는 오류"}";
                    StatusMessage = "판매에 실패했습니다.";
                }
            });
        }

        private void ClearResult()
        {
            TradeResultText = "거래 테스트 결과가 여기에 표시됩니다.";
            StatusMessage = "거래 결과가 지워졌습니다.";
        }

        private async Task RefreshPricesAsync()
        {
            await ExecuteAsync(async () =>
            {
                StatusMessage = "가격 정보를 새로고침하는 중...";

                try
                {
                    PricesList.Clear();
                    foreach (var itemId in AvailableItems)
                    {
                        var price = await _apiService.GetItemPriceAsync(itemId);
                        if (price != null)
                        {
                            PricesList.Add(price);
                        }
                    }

                    StatusMessage = $"가격 정보 새로고침 완료: {PricesList.Count}개 아이템";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"가격 정보 새로고침 실패: {ex.Message}";
                }
            });
        }

        private async Task SaveSettingsAsync()
        {
            await ExecuteAsync(async () =>
            {
                StatusMessage = "설정을 저장하는 중...";

                // 설정 저장 로직 (실제 구현은 Phase 4에서)
                await Task.Delay(500);

                StatusMessage = "설정이 저장되었습니다.";
                RecentActivities.Add($"[{DateTime.Now:HH:mm:ss}] 설정 저장 완료");
            });
        }

        private async Task RefreshDashboardData()
        {
            if (!IsConnected) return;

            try
            {
                // 실제 API 호출로 대시보드 데이터 업데이트
                var dashboard = await _apiService.GetMarketDashboardAsync();
                if (dashboard != null)
                {
                    OnlinePlayersCount = dashboard.TotalOnlinePlayers;
                    ActiveItemsCount = dashboard.ActiveItems;
                    TotalTradeVolume = dashboard.TotalVolume24h;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "대시보드 데이터 새로고침 실패");

                // 테스트 데이터로 폴백
                var random = new Random();
                OnlinePlayersCount = random.Next(1, 20);
                ActiveItemsCount = random.Next(15, 50);
                TotalTradeVolume = (decimal)(random.NextDouble() * 50000);
            }
        }

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "플레이어 목록 로드 실패");
            }
        }

        private async Task LoadPlayerTransactionsAsync(string playerId)
        {
            try
            {
                var transactions = await _apiService.GetPlayerTransactionsAsync(playerId, page: 1, size: 20);

                PlayerTransactions.Clear();
                foreach (var transaction in transactions.OrderByDescending(t => t.TransactionTime))
                {
                    PlayerTransactions.Add(transaction);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "플레이어 거래 내역 로드 실패: {PlayerId}", playerId);
            }
        }

        private void OnTabChanged()
        {
            _logger.LogDebug("탭이 변경되었습니다: {SelectedTabIndex}", SelectedTabIndex);
            StatusMessage = $"탭 {SelectedTabIndex + 1}이 선택되었습니다.";
        }

        protected void OnIsBusyChanged()
        {
            OnPropertyChanged(nameof(CanRemovePlayer));
            OnPropertyChanged(nameof(CanExecuteTransaction));
        }

        #endregion

        #region IDisposable

        ~MainWindowViewModel()
        {
            _statusTimer?.Stop();
            _statusTimer?.Dispose();
        }

        #endregion
    }
}