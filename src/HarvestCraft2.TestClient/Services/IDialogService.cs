namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// 다이얼로그 서비스 인터페이스
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// 확인 다이얼로그를 표시합니다.
        /// </summary>
        Task<bool> ShowConfirmationAsync(string title, string message, string? confirmText = null, string? cancelText = null);

        /// <summary>
        /// 정보 다이얼로그를 표시합니다.
        /// </summary>
        Task ShowInfoAsync(string title, string message);

        /// <summary>
        /// 경고 다이얼로그를 표시합니다.
        /// </summary>
        Task ShowWarningAsync(string title, string message);

        /// <summary>
        /// 오류 다이얼로그를 표시합니다.
        /// </summary>
        Task ShowErrorAsync(string title, string message);

        /// <summary>
        /// 입력 다이얼로그를 표시합니다.
        /// </summary>
        Task<string?> ShowInputAsync(string title, string message, string? defaultValue = null);

        /// <summary>
        /// 파일 선택 다이얼로그를 표시합니다.
        /// </summary>
        Task<string?> ShowOpenFileDialogAsync(string? title = null, string? filter = null, string? initialDirectory = null);

        /// <summary>
        /// 파일 저장 다이얼로그를 표시합니다.
        /// </summary>
        Task<string?> ShowSaveFileDialogAsync(string? title = null, string? filter = null, string? initialDirectory = null, string? defaultFileName = null);

        /// <summary>
        /// 폴더 선택 다이얼로그를 표시합니다.
        /// </summary>
        Task<string?> ShowFolderDialogAsync(string? title = null, string? initialDirectory = null);

        /// <summary>
        /// 사용자 정의 다이얼로그를 표시합니다.
        /// </summary>
        Task<DialogResult> ShowCustomDialogAsync(DialogInfo dialogInfo);

        /// <summary>
        /// 진행률 다이얼로그를 표시합니다.
        /// </summary>
        Task<IProgressDialog> ShowProgressDialogAsync(string title, string message, bool isCancellable = false);
    }

    /// <summary>
    /// 다이얼로그 정보 클래스
    /// </summary>
    public class DialogInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DialogType Type { get; set; } = DialogType.Info;
        public List<DialogButton> Buttons { get; set; } = new();
        public string? DefaultButtonText { get; set; }
        public object? Tag { get; set; }
    }

    /// <summary>
    /// 다이얼로그 버튼 클래스
    /// </summary>
    public class DialogButton
    {
        public string Text { get; set; } = string.Empty;
        public DialogResult Result { get; set; } = DialogResult.None;
        public bool IsDefault { get; set; } = false;
        public bool IsCancel { get; set; } = false;
    }

    /// <summary>
    /// 다이얼로그 타입 열거형
    /// </summary>
    public enum DialogType
    {
        Info,
        Warning,
        Error,
        Question,
        Custom
    }

    /// <summary>
    /// 다이얼로그 결과 열거형
    /// </summary>
    public enum DialogResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No,
        Abort,
        Retry,
        Ignore,
        Custom
    }

    /// <summary>
    /// 진행률 다이얼로그 인터페이스
    /// </summary>
    public interface IProgressDialog : IDisposable
    {
        /// <summary>
        /// 진행률을 업데이트합니다 (0-100).
        /// </summary>
        void UpdateProgress(int percentage, string? message = null);

        /// <summary>
        /// 진행률을 업데이트합니다 (0.0-1.0).
        /// </summary>
        void UpdateProgress(double percentage, string? message = null);

        /// <summary>
        /// 상태 메시지를 업데이트합니다.
        /// </summary>
        void UpdateMessage(string message);

        /// <summary>
        /// 다이얼로그를 닫습니다.
        /// </summary>
        void Close();

        /// <summary>
        /// 취소 요청 여부를 확인합니다.
        /// </summary>
        bool IsCancellationRequested { get; }

        /// <summary>
        /// 취소 토큰을 가져옵니다.
        /// </summary>
        CancellationToken CancellationToken { get; }
    }
}