using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows;
using HarvestCraft2.TestClient.Services;
using HarvestCraft2.TestClient.Models;
using Serilog;

namespace HarvestCraft2.TestClient.ViewModels
{
    /// <summary>
    /// 플레이어 관리 뷰모델
    /// </summary>
    public class PlayerViewModel : ViewModelBase
    {
        private readonly IPlayerService _playerService;
        private readonly INotificationService _notificationService;
        private readonly IDialogService _dialogService;

        private VirtualPlayer? _selectedPlayer;
        private string _searchText = string.Empty;
        private bool _isAutoTradingEnabled;

        public PlayerViewModel(IPlayerService playerService, INotificationService notificationService, IDialogService dialogService)
        {
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            Players = new ObservableCollection<VirtualPlayer>();
            FilteredPlayers = new ObservableCollection<VirtualPlayer>();

            InitializeCommands();
            SubscribeToEvents();

            _ = LoadPlayersAsync();
        }

        #region Properties

        /// <summary>
        /// 전체 플레이어 목록
        /// </summary>
        public ObservableCollection<VirtualPlayer> Players { get; }

        /// <summary>
        /// 필터링된 플레이어 목록
        /// </summary>
        public ObservableCollection<VirtualPlayer> FilteredPlayers { get; }

        /// <summary>
        /// 선택된 플레이어
        /// </summary>
        public VirtualPlayer? SelectedPlayer
        {
            get => _selectedPlayer;
            set
            {
                if (SetProperty(ref _selectedPlayer, value))
                {
                    OnSelectedPlayerChanged();
                }
            }
        }

        public bool IsLoading
        {
            get => IsBusy;
            set => IsBusy = value;
        }

        /// <summary>
        /// 검색 텍스트
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>
        /// 자동 거래 활성화 상태
        /// </summary>
        public bool IsAutoTradingEnabled
        {
            get => _isAutoTradingEnabled;
            set
            {
                if (SetProperty(ref _isAutoTradingEnabled, value))
                {
                    _ = UpdateAutoTradingStatusAsync(value);
                }
            }
        }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand AddPlayerCommand { get; private set; } = null!;
        public ICommand RemovePlayerCommand { get; private set; } = null!;
        public ICommand EditPlayerCommand { get; private set; } = null!;
        public ICommand StartAutoTradingCommand { get; private set; } = null!;
        public ICommand StopAutoTradingCommand { get; private set; } = null!;
        public ICommand ViewPlayerDetailsCommand { get; private set; } = null!;
        public ICommand ClearSearchCommand { get; private set; } = null!;

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            RefreshCommand = new AsyncRelayCommand(LoadPlayersAsync);
            AddPlayerCommand = new AsyncRelayCommand(AddPlayerAsync);
            RemovePlayerCommand = new AsyncRelayCommand(RemoveSelectedPlayerAsync);
            EditPlayerCommand = new AsyncRelayCommand(EditSelectedPlayerAsync);
            StartAutoTradingCommand = new AsyncRelayCommand(StartAutoTradingAsync);
            StopAutoTradingCommand = new AsyncRelayCommand(StopAutoTradingAsync);
            ViewPlayerDetailsCommand = new AsyncRelayCommand(ViewPlayerDetailsAsync);
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        }

        private void SubscribeToEvents()
        {
            _playerService.PlayerAdded += OnPlayerAdded;
            _playerService.PlayerRemoved += OnPlayerRemoved;
            _playerService.PlayerUpdated += OnPlayerUpdated;
            _playerService.AutoTradingStatusChanged += OnAutoTradingStatusChanged;
        }

        private async Task LoadPlayersAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "플레이어 목록을 로드하고 있습니다...";

                // IPlayerService의 Players 속성을 사용
                await _playerService.RefreshAllPlayersAsync();
                var players = _playerService.Players;

                Players.Clear();
                foreach (var player in players)
                {
                    Players.Add(player);
                }

                ApplyFilter();
                StatusMessage = $"{Players.Count}명의 플레이어가 로드되었습니다.";

                Log.Information("플레이어 목록 로드 완료: {Count}명", Players.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "플레이어 목록 로드 중 오류가 발생했습니다.");
                StatusMessage = "플레이어 목록 로드에 실패했습니다.";
                await _notificationService.ShowErrorAsync("오류", "플레이어 목록을 로드할 수 없습니다.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AddPlayerAsync()
        {
            try
            {
                var playerName = await _dialogService.ShowInputAsync("플레이어 추가", "새 플레이어의 이름을 입력하세요:");

                if (string.IsNullOrWhiteSpace(playerName))
                    return;

                var newPlayer = await _playerService.CreatePlayerAsync(playerName.Trim());

                await _notificationService.ShowSuccessAsync("성공", $"플레이어 '{playerName}'이 추가되었습니다.");
                StatusMessage = $"플레이어 '{playerName}'이 추가되었습니다.";

                // 플레이어 목록 새로고침
                await LoadPlayersAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "플레이어 추가 중 오류가 발생했습니다.");
                await _notificationService.ShowErrorAsync("오류", "플레이어 추가 중 오류가 발생했습니다.");
            }
        }

        private async Task RemoveSelectedPlayerAsync()
        {
            if (SelectedPlayer == null) return;

            try
            {
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    "플레이어 삭제",
                    $"정말로 플레이어 '{SelectedPlayer.PlayerName}'을(를) 삭제하시겠습니까?");

                if (!confirmed) return;

                var success = await _playerService.RemovePlayerAsync(SelectedPlayer.PlayerId);

                if (success)
                {
                    await _notificationService.ShowSuccessAsync("성공", $"플레이어 '{SelectedPlayer.PlayerName}'이 삭제되었습니다.");
                    StatusMessage = $"플레이어 '{SelectedPlayer.PlayerName}'이 삭제되었습니다.";
                    SelectedPlayer = null;
                    await LoadPlayersAsync(); // 목록 새로고침
                }
                else
                {
                    await _notificationService.ShowErrorAsync("실패", "플레이어 삭제에 실패했습니다.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "플레이어 삭제 중 오류가 발생했습니다.");
                await _notificationService.ShowErrorAsync("오류", "플레이어 삭제 중 오류가 발생했습니다.");
            }
        }

        private async Task EditSelectedPlayerAsync()
        {
            if (SelectedPlayer == null) return;

            try
            {
                // 현재 IPlayerService에는 플레이어 정보 직접 업데이트 메서드가 없으므로
                // 플레이어 정보를 새로고침하는 것으로 대체
                await _playerService.RefreshPlayerAsync(SelectedPlayer);

                await _notificationService.ShowSuccessAsync("성공", "플레이어 정보가 새로고침되었습니다.");
                StatusMessage = "플레이어 정보가 새로고침되었습니다.";

                // 목록 새로고침
                await LoadPlayersAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "플레이어 정보 새로고침 중 오류가 발생했습니다.");
                await _notificationService.ShowErrorAsync("오류", "플레이어 정보 새로고침 중 오류가 발생했습니다.");
            }
        }

        private async Task StartAutoTradingAsync()
        {
            if (SelectedPlayer == null) return;

            try
            {
                // 개별 플레이어 자동 거래 설정
                var defaultBehavior = new PlayerTradingBehavior
                {
                    TradingIntervalSeconds = 30, // 30초마다
                    PreferredItems = new List<string> { "minecraft:wheat", "minecraft:bread" },
                    BuyProbability = 0.6, // double 타입
                    MinQuantity = 1,
                    MaxQuantity = 5,
                    TradingBudgetRatio = 0.1, // 잔액의 10%
                    RiskProfile = RiskProfile.Moderate,
                    UsePriceBasedDecision = true
                };

                await _playerService.SetPlayerAutoTradingAsync(SelectedPlayer.PlayerId, defaultBehavior);

                await _notificationService.ShowSuccessAsync("성공", $"플레이어 '{SelectedPlayer.PlayerName}'의 자동 거래가 설정되었습니다.");
                StatusMessage = "개별 플레이어 자동 거래가 설정되었습니다.";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "자동 거래 설정 중 오류가 발생했습니다.");
                await _notificationService.ShowErrorAsync("오류", "자동 거래 설정 중 오류가 발생했습니다.");
            }
        }

        private async Task StopAutoTradingAsync()
        {
            try
            {
                // 전역 자동 거래 중지
                await _playerService.StopAutoTradingAsync();

                await _notificationService.ShowSuccessAsync("성공", "자동 거래가 중지되었습니다.");
                StatusMessage = "자동 거래가 중지되었습니다.";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "자동 거래 중지 중 오류가 발생했습니다.");
                await _notificationService.ShowErrorAsync("오류", "자동 거래 중지 중 오류가 발생했습니다.");
            }
        }

        private async Task ViewPlayerDetailsAsync()
        {
            if (SelectedPlayer == null) return;

            try
            {
                var details = $"플레이어: {SelectedPlayer.PlayerName}\n" +
                             $"ID: {SelectedPlayer.PlayerId}\n" +
                             $"잔액: {SelectedPlayer.Balance:C}\n" +
                             $"온라인 상태: {(SelectedPlayer.IsOnline ? "온라인" : "오프라인")}\n" +
                             $"자동 거래: {(SelectedPlayer.IsAutoTradingEnabled ? "활성" : "비활성")}\n" +
                             $"생성일: {SelectedPlayer.CreatedAt:yyyy-MM-dd HH:mm:ss}\n" +
                             $"마지막 업데이트: {SelectedPlayer.LastUpdated:yyyy-MM-dd HH:mm:ss}";

                await _dialogService.ShowInfoAsync("플레이어 상세 정보", details);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "플레이어 상세 정보 표시 중 오류가 발생했습니다.");
                await _notificationService.ShowErrorAsync("오류", "플레이어 상세 정보를 표시할 수 없습니다.");
            }
        }

        private async Task UpdateAutoTradingStatusAsync(bool enabled)
        {
            try
            {
                if (enabled)
                {
                    // 전역 자동 거래 시작
                    var globalSettings = new AutoTradingSettings
                    {
                        EnableGlobalAutoTrading = true,
                        GlobalTradingIntervalMs = 10000, // 10초마다
                        MaxConcurrentTrades = 5,
                        EnableMarketPressureSimulation = true,
                        AllowedItems = new List<string>(), // 빈 목록 = 모든 아이템 허용
                        RestrictedItems = new List<string>
                        { 
                            // 위험한 아이템들은 제외
                            "minecraft:diamond",
                            "minecraft:netherite_ingot"
                        }
                    };

                    await _playerService.StartAutoTradingAsync(globalSettings);
                }
                else
                {
                    await _playerService.StopAutoTradingAsync();
                }

                StatusMessage = $"전역 자동 거래가 {(enabled ? "활성화" : "비활성화")}되었습니다.";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "자동 거래 상태 업데이트 중 오류가 발생했습니다.");
                // 상태를 원래대로 되돌림
                _isAutoTradingEnabled = !enabled;
                OnPropertyChanged(nameof(IsAutoTradingEnabled));
            }
        }

        private void ApplyFilter()
        {
            FilteredPlayers.Clear();

            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? Players
                : Players.Where(p => p.PlayerName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            foreach (var player in filtered)
            {
                FilteredPlayers.Add(player);
            }
        }

        private void OnSelectedPlayerChanged()
        {
            // 명령어 상태는 AsyncRelayCommand에서 자동으로 관리됨
        }

        #endregion

        #region Event Handlers

        private void OnPlayerAdded(object? sender, PlayerAddedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Players.Add(e.Player);
                ApplyFilter();
                StatusMessage = $"플레이어 '{e.Player.PlayerName}'이 추가되었습니다.";
            });
        }

        private void OnPlayerRemoved(object? sender, PlayerRemovedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var player = Players.FirstOrDefault(p => p.PlayerId == e.PlayerId);
                if (player != null)
                {
                    Players.Remove(player);
                    ApplyFilter();
                    StatusMessage = $"플레이어 '{e.PlayerName}'이 제거되었습니다.";

                    if (SelectedPlayer?.PlayerId == e.PlayerId)
                    {
                        SelectedPlayer = null;
                    }
                }
            });
        }

        private void OnPlayerUpdated(object? sender, PlayerUpdatedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var index = Players.ToList().FindIndex(p => p.PlayerId == e.Player.PlayerId);
                if (index >= 0)
                {
                    Players[index] = e.Player;
                    ApplyFilter();
                    StatusMessage = $"플레이어 '{e.Player.PlayerName}'이 업데이트되었습니다.";

                    if (SelectedPlayer?.PlayerId == e.Player.PlayerId)
                    {
                        SelectedPlayer = e.Player;
                    }
                }
            });
        }

        private void OnAutoTradingStatusChanged(object? sender, AutoTradingStatusChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(e.PlayerId)) // 전역 상태 변경
                {
                    // IPlayerService의 IsAutoTradingEnabled 속성을 사용
                    IsAutoTradingEnabled = _playerService.IsAutoTradingEnabled;
                    StatusMessage = $"전역 자동 거래 상태: {e.NewStatus}";
                }
            });
        }

        #endregion
    }
}