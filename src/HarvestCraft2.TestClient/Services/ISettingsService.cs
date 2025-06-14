using HarvestCraft2.TestClient.Models;

namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// 애플리케이션 설정 관리 서비스 인터페이스
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// API 설정을 가져옵니다.
        /// </summary>
        ApiSettings GetApiSettings();

        /// <summary>
        /// UI 설정을 가져옵니다.
        /// </summary>
        UiSettings GetUiSettings();

        /// <summary>
        /// API 설정을 저장합니다.
        /// </summary>
        Task SaveApiSettingsAsync(ApiSettings settings);

        /// <summary>
        /// UI 설정을 저장합니다.
        /// </summary>
        Task SaveUiSettingsAsync(UiSettings settings);

        /// <summary>
        /// 모든 설정을 기본값으로 재설정합니다.
        /// </summary>
        Task ResetToDefaultsAsync();

        /// <summary>
        /// 설정 변경 이벤트
        /// </summary>
        event EventHandler<SettingsChangedEventArgs> SettingsChanged;
    }

    /// <summary>
    /// 설정 변경 이벤트 인수
    /// </summary>
    public class SettingsChangedEventArgs : EventArgs
    {
        public string SettingType { get; set; } = string.Empty;
        public object OldValue { get; set; } = new();
        public object NewValue { get; set; } = new();
        public DateTime ChangedAt { get; set; } = DateTime.Now;
    }
}