using System.Collections.Concurrent;
using System.Windows;
using Serilog;

namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// 알림 서비스 구현
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly ConcurrentDictionary<string, NotificationInfo> _activeNotifications;
        private bool _isEnabled = true;

        public event EventHandler<NotificationClickedEventArgs>? NotificationClicked;

        public NotificationService()
        {
            _activeNotifications = new ConcurrentDictionary<string, NotificationInfo>();
        }

        public async Task ShowInfoAsync(string title, string message, TimeSpan? duration = null)
        {
            var notification = new NotificationInfo
            {
                Title = title,
                Message = message,
                Type = NotificationType.Info,
                Duration = duration ?? TimeSpan.FromSeconds(5)
            };

            await ShowCustomAsync(notification);
        }

        public async Task ShowSuccessAsync(string title, string message, TimeSpan? duration = null)
        {
            var notification = new NotificationInfo
            {
                Title = title,
                Message = message,
                Type = NotificationType.Success,
                Duration = duration ?? TimeSpan.FromSeconds(3)
            };

            await ShowCustomAsync(notification);
        }

        public async Task ShowWarningAsync(string title, string message, TimeSpan? duration = null)
        {
            var notification = new NotificationInfo
            {
                Title = title,
                Message = message,
                Type = NotificationType.Warning,
                Duration = duration ?? TimeSpan.FromSeconds(7)
            };

            await ShowCustomAsync(notification);
        }

        public async Task ShowErrorAsync(string title, string message, TimeSpan? duration = null)
        {
            var notification = new NotificationInfo
            {
                Title = title,
                Message = message,
                Type = NotificationType.Error,
                Duration = duration ?? TimeSpan.FromSeconds(10)
            };

            await ShowCustomAsync(notification);
        }

        public async Task ShowCustomAsync(NotificationInfo notification)
        {
            if (!_isEnabled)
            {
                Log.Debug("알림이 비활성화되어 있어 표시하지 않습니다: {Title}", notification.Title);
                return;
            }

            try
            {
                // 활성 알림 목록에 추가
                _activeNotifications[notification.Id] = notification;

                // UI 스레드에서 실행
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // WPF MessageBox로 간단 구현 (향후 커스텀 토스트로 교체 가능)
                    var icon = GetMessageBoxIcon(notification.Type);
                    var result = MessageBox.Show(
                        notification.Message,
                        notification.Title,
                        MessageBoxButton.OK,
                        icon);

                    // 클릭 이벤트 발생
                    NotificationClicked?.Invoke(this, new NotificationClickedEventArgs
                    {
                        NotificationId = notification.Id,
                        Title = notification.Title,
                        Message = notification.Message,
                        Type = notification.Type
                    });
                });

                Log.Information("알림을 표시했습니다: {Title} - {Message}", notification.Title, notification.Message);

                // 자동 제거 (지정된 시간 후)
                _ = Task.Delay(notification.Duration).ContinueWith(task =>
                {
                    _activeNotifications.TryRemove(notification.Id, out var removedNotification);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "알림 표시 중 오류가 발생했습니다: {Title}", notification.Title);
            }
        }

        public async Task ShowTrayNotificationAsync(string title, string message, NotificationIcon icon = NotificationIcon.Info)
        {
            if (!_isEnabled) return;

            try
            {
                // 향후 시스템 트레이 기능 구현 시 사용
                // 현재는 일반 알림으로 대체
                await ShowInfoAsync(title, message);

                Log.Debug("트레이 알림을 표시했습니다: {Title}", title);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "트레이 알림 표시 중 오류가 발생했습니다: {Title}", title);
            }
        }

        public async Task ClearAllAsync()
        {
            try
            {
                _activeNotifications.Clear();
                Log.Information("모든 알림을 지웠습니다.");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "알림 지우기 중 오류가 발생했습니다.");
            }
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            Log.Information("알림 서비스가 {Status}되었습니다.", enabled ? "활성화" : "비활성화");
        }

        private static MessageBoxImage GetMessageBoxIcon(NotificationType type)
        {
            return type switch
            {
                NotificationType.Info => MessageBoxImage.Information,
                NotificationType.Success => MessageBoxImage.Information,
                NotificationType.Warning => MessageBoxImage.Warning,
                NotificationType.Error => MessageBoxImage.Error,
                _ => MessageBoxImage.None
            };
        }
    }
}