using HarvestCraft2.TestClient.Models;
using HarvestCraft2.TestClient.Services;
using Serilog;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace HarvestCraft2.TestClient.ViewModels
{
    /// <summary>
    /// 설정 관리 뷰모델
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly INotificationService _notificationService;
        private readonly IDialogService _dialogService;

        private ApiSettings _apiSettings = new();
        private UiSettings _uiSettings = new();
        private bool _hasUnsavedChanges;

        public SettingsViewModel(ISettingsService settingsService, INotificationService notificationService, IDialogService dialogService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            InitializeCommands();
            SubscribeToEvents();

            _ = LoadSettingsAsync();
        }

        #region Properties

        /// <summary>
        /// API 설정
        /// </summary>
        public ApiSettings ApiSettings
        {
            get => _apiSettings;
            set
            {
                if (SetProperty(ref _apiSettings, value))
                {
                    HasUnsavedChanges = true;
                }
            }
        }

        /// <summary>
        /// UI 설정
        /// </summary>
        public UiSettings UiSettings
        {
            get => _uiSettings;
            set
            {
                if (SetProperty(ref _uiSettings, value))
                {
                    HasUnsavedChanges = true;
                }
            }
        }

        /// <summary>
        /// API 기본 URL
        /// </summary>
        public string ApiBaseUrl
        {
            get => _apiSettings.BaseUrl;
            set
            {
                if (_apiSettings.BaseUrl != value)
                {
                    _apiSettings.BaseUrl = value;
                    OnPropertyChanged();
                    HasUnsavedChanges = true;
                }
            }
        }

        /// <summary>
        /// API 키
        /// </summary>
        public string ApiKey
        {
            get => _apiSettings.ApiKey;
            set
            {
                if (_apiSettings.ApiKey != value)
                {
                    _apiSettings.ApiKey = value;
                    OnPropertyChanged();
                    HasUnsavedChanges = true;
                }
            }
        }

        /// <summary>
        /// API 타임아웃 (초)
        /// </summary>
        public int ApiTimeoutSeconds
        {
            get => _apiSettings.TimeoutSeconds;
            set
            {
                if (_apiSettings.TimeoutSeconds != value && value > 0 && value <= 300)
                {
                    _apiSettings.TimeoutSeconds = value;
                    OnPropertyChanged();
                    HasUnsavedChanges = true;
                    ValidateApiSettings();
                }
            }
        }

        /// <summary>
        /// API 재시도 횟수
        /// </summary>
        public int ApiRetryCount
        {
            get => _apiSettings.RetryCount;
            set
            {
                if (_apiSettings.RetryCount != value && value >= 0 && value <= 10)
                {
                    _apiSettings.RetryCount = value;
                    OnPropertyChanged();
                    HasUnsavedChanges = true;
                }
            }
        }

        /// <summary>
        /// HTTPS 사용 여부
        /// </summary>
        public bool UseHttps
        {
            get => _apiSettings.UseHttps;
            set
            {
                if (_apiSettings.UseHttps != value)
                {
                    _apiSettings.UseHttps = value;
                    OnPropertyChanged();
                    HasUnsavedChanges = true;
                }
            }
        }

        /// <summary>
        /// UI 테마
        /// </summary>
        public string Theme
        {
            get => _uiSettings.Theme;
            set
            {
                if (_uiSettings.Theme != value)
                {
                    _uiSettings.Theme = value;
                    OnPropertyChanged();
                    HasUnsavedChanges = true;
                }
            }
        }

        /// <summary>
        /// 언어 설정
        /// </summary>
        public string Language
        {
            get => _uiSettings.Language;
            set
            {
                if (_uiSettings.Language != value)
                {
                    _uiSettings.Language = value;
                    OnPropertyChanged();
                    HasUnsavedChanges = true;
                }
            }
        }

        /// <summary>
        /// 알림 표시 여부
        /// </summary>
        public bool ShowNotifications
        {
            get => _uiSettings.ShowNotifications;
            set
            {
                if (_uiSettings.ShowNotifications != value)
                {
                    _uiSettings.ShowNotifications = value;
                    OnPropertyChanged();
                    HasUnsavedChanges = true;
                }
            }
        }

        /// <summary>
        /// 자동 새로고침 여부
        /// </summary>
        public bool AutoRefresh
        {
            get => _uiSettings.AutoRefresh;
            set
            {
                if (_uiSettings.AutoRefresh != value)
                {
                    _uiSettings.AutoRefresh = value;
                    OnPropertyChanged();
                    HasUnsavedChanges = true;
                }
            }
        }

        /// <summary>
        /// 새로고침 간격 (초)
        /// </summary>
        public int RefreshIntervalSeconds
        {
            get => _uiSettings.RefreshIntervalSeconds;
            set
            {
                if (_uiSettings.RefreshIntervalSeconds != value && value >= 5 && value <= 3600)
                {
                    _uiSettings.RefreshIntervalSeconds = value;
                    OnPropertyChanged();
                    HasUnsavedChanges = true;
                }
            }
        }

        /// <summary>
        /// 고급 기능 표시 여부
        /// </summary>
        public bool ShowAdvancedFeatures
        {
            get => _uiSettings.ShowAdvancedFeatures;
            set
            {
                if (_uiSettings.ShowAdvancedFeatures != value)
                {
                    _uiSettings.ShowAdvancedFeatures = value;
                    OnPropertyChanged();
                    HasUnsavedChanges = true;
                }
            }
        }

        /// <summary>
        /// API 설정 유효성 검사 결과
        /// </summary>
        private string _apiValidationMessage = string.Empty;
        public string ApiValidationMessage
        {
            get => _apiValidationMessage;
            set => SetProperty(ref _apiValidationMessage, value);
        }

        /// <summary>
        /// API 설정이 유효한지 여부
        /// </summary>
        private bool _isApiSettingsValid = true;
        public bool IsApiSettingsValid
        {
            get => _isApiSettingsValid;
            set => SetProperty(ref _isApiSettingsValid, value);
        }

        /// <summary>
        /// 저장되지 않은 변경사항 여부
        /// </summary>
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => SetProperty(ref _hasUnsavedChanges, value);
        }

        /// <summary>
        /// 로딩 상태
        /// </summary>
        public bool IsLoading
        {
            get => IsBusy;
            set => IsBusy = value;
        }

        /// <summary>
        /// 테마 옵션 목록
        /// </summary>
        public List<string> ThemeOptions { get; } = new() { "Light", "Dark", "Auto" };

        /// <summary>
        /// 언어 옵션 목록
        /// </summary>
        public List<string> LanguageOptions { get; } = new() { "ko-KR", "en-US", "ja-JP" };

        #endregion

        #region Commands

        public ICommand SaveCommand { get; private set; } = null!;
        public ICommand CancelCommand { get; private set; } = null!;
        public ICommand ResetToDefaultsCommand { get; private set; } = null!;
        public ICommand TestConnectionCommand { get; private set; } = null!;
        public ICommand ImportSettingsCommand { get; private set; } = null!;
        public ICommand ExportSettingsCommand { get; private set; } = null!;

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);
            CancelCommand = new AsyncRelayCommand(CancelChangesAsync);
            ResetToDefaultsCommand = new AsyncRelayCommand(ResetToDefaultsAsync);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
            ImportSettingsCommand = new AsyncRelayCommand(ImportSettingsAsync);
            ExportSettingsCommand = new AsyncRelayCommand(ExportSettingsAsync);
        }

        private void SubscribeToEvents()
        {
            _settingsService.SettingsChanged += OnSettingsChanged;
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "설정을 로드하고 있습니다...";

                _apiSettings = _settingsService.GetApiSettings();
                _uiSettings = _settingsService.GetUiSettings();

                // 모든 속성 변경 알림
                OnPropertyChanged(nameof(ApiSettings));
                OnPropertyChanged(nameof(UiSettings));
                OnPropertyChanged(nameof(ApiBaseUrl));
                OnPropertyChanged(nameof(ApiKey));
                OnPropertyChanged(nameof(ApiTimeoutSeconds));
                OnPropertyChanged(nameof(ApiRetryCount));
                OnPropertyChanged(nameof(UseHttps));
                OnPropertyChanged(nameof(Theme));
                OnPropertyChanged(nameof(Language));
                OnPropertyChanged(nameof(ShowNotifications));
                OnPropertyChanged(nameof(AutoRefresh));
                OnPropertyChanged(nameof(RefreshIntervalSeconds));
                OnPropertyChanged(nameof(ShowAdvancedFeatures));

                HasUnsavedChanges = false;
                StatusMessage = "설정이 로드되었습니다.";

                Log.Information("설정이 로드되었습니다.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "설정 로드 중 오류가 발생했습니다.");
                StatusMessage = "설정 로드에 실패했습니다.";
                await _notificationService.ShowErrorAsync("오류", "설정을 로드할 수 없습니다.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "설정을 저장하고 있습니다...";

                await _settingsService.SaveApiSettingsAsync(_apiSettings);
                await _settingsService.SaveUiSettingsAsync(_uiSettings);

                HasUnsavedChanges = false;
                StatusMessage = "설정이 저장되었습니다.";

                await _notificationService.ShowSuccessAsync("성공", "설정이 저장되었습니다.");

                Log.Information("설정이 저장되었습니다.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "설정 저장 중 오류가 발생했습니다.");
                StatusMessage = "설정 저장에 실패했습니다.";
                await _notificationService.ShowErrorAsync("오류", "설정을 저장할 수 없습니다.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CancelChangesAsync()
        {
            try
            {
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    "변경사항 취소",
                    "저장되지 않은 변경사항이 있습니다. 정말로 취소하시겠습니까?");

                if (!confirmed) return;

                await LoadSettingsAsync();
                StatusMessage = "변경사항이 취소되었습니다.";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "변경사항 취소 중 오류가 발생했습니다.");
                await _notificationService.ShowErrorAsync("오류", "변경사항 취소 중 오류가 발생했습니다.");
            }
        }

        private async Task ResetToDefaultsAsync()
        {
            try
            {
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    "기본값으로 재설정",
                    "모든 설정을 기본값으로 재설정하시겠습니까? 이 작업은 되돌릴 수 없습니다.");

                if (!confirmed) return;

                await _settingsService.ResetToDefaultsAsync();
                await LoadSettingsAsync();

                StatusMessage = "설정이 기본값으로 재설정되었습니다.";
                await _notificationService.ShowSuccessAsync("성공", "설정이 기본값으로 재설정되었습니다.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "설정 재설정 중 오류가 발생했습니다.");
                await _notificationService.ShowErrorAsync("오류", "설정 재설정 중 오류가 발생했습니다.");
            }
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "API 연결을 테스트하고 있습니다...";

                // API 설정 유효성 검사
                if (!ValidateApiSettings())
                {
                    StatusMessage = "API 설정이 유효하지 않습니다.";
                    await _notificationService.ShowWarningAsync("설정 오류", ApiValidationMessage);
                    return;
                }

                // 실제 연결 테스트
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_apiSettings.TimeoutSeconds);

                if (!string.IsNullOrEmpty(_apiSettings.ApiKey))
                {
                    httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiSettings.ApiKey);
                }

                var response = await httpClient.GetAsync($"{_apiSettings.BaseUrl}/health");

                if (response.IsSuccessStatusCode)
                {
                    StatusMessage = "API 연결이 성공했습니다.";
                    await _notificationService.ShowSuccessAsync("연결 성공", "API 서버에 성공적으로 연결되었습니다.");
                }
                else
                {
                    StatusMessage = $"API 연결에 실패했습니다. (상태 코드: {response.StatusCode})";
                    await _notificationService.ShowWarningAsync("연결 실패", $"API 서버 연결에 실패했습니다.\n상태 코드: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "API 연결 테스트 중 오류가 발생했습니다.");
                StatusMessage = "API 연결 테스트에 실패했습니다.";
                await _notificationService.ShowErrorAsync("연결 오류", $"API 연결 테스트 중 오류가 발생했습니다:\n{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// API 설정 유효성 검사
        /// </summary>
        private bool ValidateApiSettings()
        {
            var validationErrors = new List<string>();

            // Base URL 검증
            if (string.IsNullOrWhiteSpace(ApiBaseUrl))
            {
                validationErrors.Add("API URL은 필수입니다.");
            }
            else if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri) ||
                     (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                validationErrors.Add("올바른 URL 형식이 아닙니다. (http:// 또는 https://로 시작)");
            }

            // API Key 검증 (선택사항이지만 형식 확인)
            if (!string.IsNullOrWhiteSpace(ApiKey) && ApiKey.Length < 10)
            {
                validationErrors.Add("API 키가 너무 짧습니다.");
            }

            // 타임아웃 검증
            if (ApiTimeoutSeconds < 5 || ApiTimeoutSeconds > 300)
            {
                validationErrors.Add("타임아웃은 5초에서 300초 사이여야 합니다.");
            }

            // 재시도 횟수 검증
            if (ApiRetryCount < 0 || ApiRetryCount > 10)
            {
                validationErrors.Add("재시도 횟수는 0회에서 10회 사이여야 합니다.");
            }

            // 결과 설정
            IsApiSettingsValid = !validationErrors.Any();
            ApiValidationMessage = validationErrors.Any()
                ? string.Join("\n", validationErrors)
                : "API 설정이 유효합니다.";

            return IsApiSettingsValid;
        }

        /// <summary>
        /// UI 설정 유효성 검사
        /// </summary>
        private bool ValidateUiSettings()
        {
            var validationErrors = new List<string>();

            // 새로고침 간격 검증
            if (RefreshIntervalSeconds < 5 || RefreshIntervalSeconds > 3600)
            {
                validationErrors.Add("새로고침 간격은 5초에서 3600초 사이여야 합니다.");
            }

            // 테마 검증
            if (!ThemeOptions.Contains(Theme))
            {
                validationErrors.Add("지원하지 않는 테마입니다.");
            }

            // 언어 검증
            if (!LanguageOptions.Contains(Language))
            {
                validationErrors.Add("지원하지 않는 언어입니다.");
            }

            return !validationErrors.Any();
        }

        /// <summary>
        /// 모든 설정 유효성 검사
        /// </summary>
        public bool ValidateAllSettings()
        {
            return ValidateApiSettings() && ValidateUiSettings();
        }

        private async Task ImportSettingsAsync()
        {
            try
            {
                var filePath = await _dialogService.ShowOpenFileDialogAsync(
                    "설정 파일 가져오기",
                    "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*");

                if (string.IsNullOrEmpty(filePath)) return;

                IsLoading = true;
                StatusMessage = "설정을 가져오는 중입니다...";

                // 파일 읽기
                var jsonContent = await System.IO.File.ReadAllTextAsync(filePath);

                // JSON 파싱
                var importedSettings = System.Text.Json.JsonSerializer.Deserialize<SettingsImportExport>(jsonContent);

                if (importedSettings == null)
                {
                    await _notificationService.ShowErrorAsync("오류", "설정 파일을 읽을 수 없습니다.");
                    return;
                }

                // 확인 대화상자
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    "설정 가져오기",
                    "현재 설정을 가져온 설정으로 덮어쓰시겠습니까?\n이 작업은 되돌릴 수 없습니다.");

                if (!confirmed) return;

                // 설정 적용
                if (importedSettings.ApiSettings != null)
                {
                    _apiSettings = importedSettings.ApiSettings;
                }

                if (importedSettings.UiSettings != null)
                {
                    _uiSettings = importedSettings.UiSettings;
                }

                // UI 업데이트
                await LoadSettingsAsync();

                StatusMessage = "설정 가져오기가 완료되었습니다.";
                await _notificationService.ShowSuccessAsync("성공", "설정을 성공적으로 가져왔습니다.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "설정 가져오기 중 오류가 발생했습니다.");
                StatusMessage = "설정 가져오기에 실패했습니다.";
                await _notificationService.ShowErrorAsync("오류", $"설정 가져오기 중 오류가 발생했습니다:\n{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportSettingsAsync()
        {
            try
            {
                var filePath = await _dialogService.ShowSaveFileDialogAsync(
                    "설정 파일 내보내기",
                    "JSON 파일 (*.json)|*.json",
                    defaultFileName: $"harvestcraft2-settings-{DateTime.Now:yyyyMMdd-HHmmss}.json");

                if (string.IsNullOrEmpty(filePath)) return;

                IsLoading = true;
                StatusMessage = "설정을 내보내는 중입니다...";

                // 내보낼 설정 구성
                var exportSettings = new SettingsImportExport
                {
                    ApiSettings = _apiSettings,
                    UiSettings = _uiSettings,
                    ExportInfo = new ExportInfo
                    {
                        ExportedAt = DateTime.Now,
                        ExportedBy = Environment.UserName,
                        ApplicationVersion = "1.0.0" // 실제 버전으로 교체
                    }
                };

                // JSON 직렬화
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var jsonContent = System.Text.Json.JsonSerializer.Serialize(exportSettings, options);

                // 파일 저장
                await System.IO.File.WriteAllTextAsync(filePath, jsonContent);

                StatusMessage = "설정 내보내기가 완료되었습니다.";
                await _notificationService.ShowSuccessAsync("성공", $"설정을 성공적으로 내보냈습니다:\n{filePath}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "설정 내보내기 중 오류가 발생했습니다.");
                StatusMessage = "설정 내보내기에 실패했습니다.";
                await _notificationService.ShowErrorAsync("오류", $"설정 내보내기 중 오류가 발생했습니다:\n{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Event Handlers

        private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            // 외부에서 설정이 변경된 경우 UI 업데이트
            Application.Current.Dispatcher.Invoke(async () =>
            {
                await LoadSettingsAsync();
                StatusMessage = $"설정이 외부에서 변경되었습니다: {e.SettingType}";
            });
        }

        #endregion
    }

    /// <summary>
    /// 설정 파일 import/export용 데이터 클래스
    /// </summary>
    public class SettingsImportExport
    {
        public ApiSettings? ApiSettings { get; set; }
        public UiSettings? UiSettings { get; set; }
        public ExportInfo? ExportInfo { get; set; }
    }

    /// <summary>
    /// 내보내기 메타데이터
    /// </summary>
    public class ExportInfo
    {
        public DateTime ExportedAt { get; set; }
        public string ExportedBy { get; set; } = string.Empty;
        public string ApplicationVersion { get; set; } = string.Empty;
        public string Description { get; set; } = "HarvestCraft2 TestClient Settings";
    }
}