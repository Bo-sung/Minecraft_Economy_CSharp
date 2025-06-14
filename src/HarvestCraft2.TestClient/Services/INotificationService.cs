namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// 알림 서비스 인터페이스
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// 정보 알림을 표시합니다.
        /// </summary>
        Task ShowInfoAsync(string title, string message, TimeSpan? duration = null);

        /// <summary>
        /// 성공 알림을 표시합니다.
        /// </summary>
        Task ShowSuccessAsync(string title, string message, TimeSpan? duration = null);

        /// <summary>
        /// 경고 알림을 표시합니다.
        /// </summary>
        Task ShowWarningAsync(string title, string message, TimeSpan? duration = null);

        /// <summary>
        /// 오류 알림을 표시합니다.
        /// </summary>
        Task ShowErrorAsync(string title, string message, TimeSpan? duration = null);

        /// <summary>
        /// 사용자 정의 알림을 표시합니다.
        /// </summary>
        Task ShowCustomAsync(NotificationInfo notification);

        /// <summary>
        /// 시스템 트레이 알림을 표시합니다.
        /// </summary>
        Task ShowTrayNotificationAsync(string title, string message, NotificationIcon icon = NotificationIcon.Info);

        /// <summary>
        /// 모든 알림을 지웁니다.
        /// </summary>
        Task ClearAllAsync();

        /// <summary>
        /// 알림 활성화 상태를 설정합니다.
        /// </summary>
        void SetEnabled(bool enabled);

        /// <summary>
        /// 알림 클릭 이벤트
        /// </summary>
        event EventHandler<NotificationClickedEventArgs> NotificationClicked;
    }

    /// <summary>
    /// 알림 정보 클래스
    /// </summary>
    public class NotificationInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; } = NotificationType.Info;
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(5);
        public string? ActionText { get; set; }
        public Action? ActionCallback { get; set; }
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 알림 타입 열거형
    /// </summary>
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// 알림 아이콘 열거형
    /// </summary>
    public enum NotificationIcon
    {
        None,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 알림 클릭 이벤트 인수
    /// </summary>
    public class NotificationClickedEventArgs : EventArgs
    {
        public string NotificationId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public DateTime ClickedAt { get; set; } = DateTime.Now;
    }
}