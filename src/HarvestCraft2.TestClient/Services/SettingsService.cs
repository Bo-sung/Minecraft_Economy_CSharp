using HarvestCraft2.TestClient.Models;
using Microsoft.Extensions.Options;
using Serilog;
using System.IO;
using System.Text.Json;

namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// 애플리케이션 설정 관리 서비스 구현
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;
        private ApiSettings _apiSettings;
        private UiSettings _uiSettings;

        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        public SettingsService(IOptions<ApiSettings> apiOptions, IOptions<UiSettings> uiOptions)
        {
            _settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HarvestCraft2.TestClient",
                "settings.json");

            _apiSettings = apiOptions.Value;
            _uiSettings = uiOptions.Value;

            // 설정 파일이 있으면 로드
            LoadSettingsFromFile();
        }

        public ApiSettings GetApiSettings()
        {
            return _apiSettings;
        }

        public UiSettings GetUiSettings()
        {
            return _uiSettings;
        }

        public async Task SaveApiSettingsAsync(ApiSettings settings)
        {
            var oldSettings = _apiSettings;
            _apiSettings = settings;

            await SaveSettingsToFileAsync();

            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs
            {
                SettingType = nameof(ApiSettings),
                OldValue = oldSettings,
                NewValue = settings
            });

            Log.Information("API 설정이 저장되었습니다.");
        }

        public async Task SaveUiSettingsAsync(UiSettings settings)
        {
            var oldSettings = _uiSettings;
            _uiSettings = settings;

            await SaveSettingsToFileAsync();

            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs
            {
                SettingType = nameof(UiSettings),
                OldValue = oldSettings,
                NewValue = settings
            });

            Log.Information("UI 설정이 저장되었습니다.");
        }

        public async Task ResetToDefaultsAsync()
        {
            var oldApiSettings = _apiSettings;
            var oldUiSettings = _uiSettings;

            _apiSettings = new ApiSettings();
            _uiSettings = new UiSettings();

            await SaveSettingsToFileAsync();

            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs
            {
                SettingType = "Reset",
                OldValue = new { Api = oldApiSettings, Ui = oldUiSettings },
                NewValue = new { Api = _apiSettings, Ui = _uiSettings }
            });

            Log.Information("설정이 기본값으로 재설정되었습니다.");
        }

        private void LoadSettingsFromFile()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    Log.Information("설정 파일이 없습니다. 기본 설정을 사용합니다: {FilePath}", _settingsFilePath);
                    return;
                }

                var json = File.ReadAllText(_settingsFilePath);
                var settingsContainer = JsonSerializer.Deserialize<SettingsContainer>(json);

                if (settingsContainer != null)
                {
                    _apiSettings = settingsContainer.ApiSettings ?? new ApiSettings();
                    _uiSettings = settingsContainer.UiSettings ?? new UiSettings();
                    Log.Information("설정 파일을 로드했습니다: {FilePath}", _settingsFilePath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "설정 파일 로드 중 오류가 발생했습니다: {FilePath}", _settingsFilePath);
            }
        }

        private async Task SaveSettingsToFileAsync()
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var settingsContainer = new SettingsContainer
                {
                    ApiSettings = _apiSettings,
                    UiSettings = _uiSettings
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(settingsContainer, options);
                await File.WriteAllTextAsync(_settingsFilePath, json);

                Log.Debug("설정이 파일에 저장되었습니다: {FilePath}", _settingsFilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "설정 파일 저장 중 오류가 발생했습니다: {FilePath}", _settingsFilePath);
                throw;
            }
        }
    }

    /// <summary>
    /// 설정 컨테이너 클래스 (JSON 직렬화용)
    /// </summary>
    internal class SettingsContainer
    {
        public ApiSettings? ApiSettings { get; set; }
        public UiSettings? UiSettings { get; set; }
    }
}