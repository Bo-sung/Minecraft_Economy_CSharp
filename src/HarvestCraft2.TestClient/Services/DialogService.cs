using System.Windows;
using Microsoft.Win32;
using Serilog;

namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// 다이얼로그 서비스 구현
    /// </summary>
    public class DialogService : IDialogService
    {
        public async Task<bool> ShowConfirmationAsync(string title, string message, string? confirmText = null, string? cancelText = null)
        {
            try
            {
                var result = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var messageBoxResult = MessageBox.Show(
                        message,
                        title,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    return messageBoxResult == MessageBoxResult.Yes;
                });

                Log.Information("확인 다이얼로그 결과: {Result} - {Title}", result, title);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "확인 다이얼로그 표시 중 오류가 발생했습니다: {Title}", title);
                return false;
            }
        }

        public async Task ShowInfoAsync(string title, string message)
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                });

                Log.Information("정보 다이얼로그를 표시했습니다: {Title}", title);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "정보 다이얼로그 표시 중 오류가 발생했습니다: {Title}", title);
            }
        }

        public async Task ShowWarningAsync(string title, string message)
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                });

                Log.Information("경고 다이얼로그를 표시했습니다: {Title}", title);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "경고 다이얼로그 표시 중 오류가 발생했습니다: {Title}", title);
            }
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                });

                Log.Information("오류 다이얼로그를 표시했습니다: {Title}", title);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "오류 다이얼로그 표시 중 오류가 발생했습니다: {Title}", title);
            }
        }

        public async Task<string?> ShowInputAsync(string title, string message, string? defaultValue = null)
        {
            try
            {
                // 간단한 입력 다이얼로그 (향후 커스텀 윈도우로 교체 가능)
                var result = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var inputDialog = new InputDialog(title, message, defaultValue);
                    var dialogResult = inputDialog.ShowDialog();
                    return dialogResult == true ? inputDialog.InputText : null;
                });

                Log.Information("입력 다이얼로그 결과: {HasValue} - {Title}", result != null, title);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "입력 다이얼로그 표시 중 오류가 발생했습니다: {Title}", title);
                return null;
            }
        }

        public async Task<string?> ShowOpenFileDialogAsync(string? title = null, string? filter = null, string? initialDirectory = null)
        {
            try
            {
                var result = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var openFileDialog = new OpenFileDialog
                    {
                        Title = title ?? "파일 열기",
                        Filter = filter ?? "모든 파일 (*.*)|*.*",
                        InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    };

                    return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
                });

                Log.Information("파일 열기 다이얼로그 결과: {HasFile}", result != null);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "파일 열기 다이얼로그 표시 중 오류가 발생했습니다.");
                return null;
            }
        }

        public async Task<string?> ShowSaveFileDialogAsync(string? title = null, string? filter = null, string? initialDirectory = null, string? defaultFileName = null)
        {
            try
            {
                var result = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var saveFileDialog = new SaveFileDialog
                    {
                        Title = title ?? "파일 저장",
                        Filter = filter ?? "모든 파일 (*.*)|*.*",
                        InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        FileName = defaultFileName ?? string.Empty
                    };

                    return saveFileDialog.ShowDialog() == true ? saveFileDialog.FileName : null;
                });

                Log.Information("파일 저장 다이얼로그 결과: {HasFile}", result != null);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "파일 저장 다이얼로그 표시 중 오류가 발생했습니다.");
                return null;
            }
        }

        public async Task<string?> ShowFolderDialogAsync(string? title = null, string? initialDirectory = null)
        {
            try
            {
                // .NET 8 이상에서는 더 나은 폴더 선택 다이얼로그 사용 가능
                var result = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var folderDialog = new OpenFolderDialog
                    {
                        Title = title ?? "폴더 선택",
                        InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    };

                    return folderDialog.ShowDialog() == true ? folderDialog.FolderName : null;
                });

                Log.Information("폴더 선택 다이얼로그 결과: {HasFolder}", result != null);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "폴더 선택 다이얼로그 표시 중 오류가 발생했습니다.");
                return null;
            }
        }

        public async Task<DialogResult> ShowCustomDialogAsync(DialogInfo dialogInfo)
        {
            try
            {
                var result = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var messageBoxButton = GetMessageBoxButton(dialogInfo.Buttons);
                    var messageBoxImage = GetMessageBoxImage(dialogInfo.Type);

                    var messageBoxResult = MessageBox.Show(
                        dialogInfo.Message,
                        dialogInfo.Title,
                        messageBoxButton,
                        messageBoxImage);

                    return ConvertMessageBoxResult(messageBoxResult);
                });

                Log.Information("사용자 정의 다이얼로그 결과: {Result} - {Title}", result, dialogInfo.Title);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "사용자 정의 다이얼로그 표시 중 오류가 발생했습니다: {Title}", dialogInfo.Title);
                return DialogResult.None;
            }
        }

        public async Task<IProgressDialog> ShowProgressDialogAsync(string title, string message, bool isCancellable = false)
        {
            try
            {
                var progressDialog = new ProgressDialogImpl(title, message, isCancellable);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    progressDialog.Show();
                });

                Log.Information("진행률 다이얼로그를 표시했습니다: {Title}", title);
                return progressDialog;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "진행률 다이얼로그 표시 중 오류가 발생했습니다: {Title}", title);
                return new ProgressDialogImpl(title, message, isCancellable);
            }
        }

        private static MessageBoxButton GetMessageBoxButton(List<DialogButton> buttons)
        {
            if (buttons.Count == 0) return MessageBoxButton.OK;

            // 간단한 매핑 (향후 확장 가능)
            return buttons.Count switch
            {
                1 => MessageBoxButton.OK,
                2 => MessageBoxButton.YesNo,
                _ => MessageBoxButton.YesNoCancel
            };
        }

        private static MessageBoxImage GetMessageBoxImage(DialogType type)
        {
            return type switch
            {
                DialogType.Info => MessageBoxImage.Information,
                DialogType.Warning => MessageBoxImage.Warning,
                DialogType.Error => MessageBoxImage.Error,
                DialogType.Question => MessageBoxImage.Question,
                _ => MessageBoxImage.None
            };
        }

        private static DialogResult ConvertMessageBoxResult(MessageBoxResult result)
        {
            return result switch
            {
                MessageBoxResult.OK => DialogResult.OK,
                MessageBoxResult.Cancel => DialogResult.Cancel,
                MessageBoxResult.Yes => DialogResult.Yes,
                MessageBoxResult.No => DialogResult.No,
                _ => DialogResult.None
            };
        }
    }

    // 간단한 입력 다이얼로그 클래스 (향후 별도 파일로 분리 가능)
    internal class InputDialog : Window
    {
        public string InputText { get; private set; } = string.Empty;

        public InputDialog(string title, string message, string? defaultValue)
        {
            Title = title;
            Width = 400;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            // 향후 XAML로 구현
        }
    }

    // 진행률 다이얼로그 구현
    internal class ProgressDialogImpl : IProgressDialog
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed;

        public bool IsCancellationRequested => _cancellationTokenSource.IsCancellationRequested;
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public ProgressDialogImpl(string title, string message, bool isCancellable)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            // 향후 실제 진행률 다이얼로그 구현
        }

        public void UpdateProgress(int percentage, string? message = null)
        {
            UpdateProgress(percentage / 100.0, message);
        }

        public void UpdateProgress(double percentage, string? message = null)
        {
            // 향후 UI 업데이트 구현
        }

        public void UpdateMessage(string message)
        {
            // 향후 UI 업데이트 구현
        }

        public void Show()
        {
            // 향후 다이얼로그 표시 구현
        }

        public void Close()
        {
            // 향후 다이얼로그 닫기 구현
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _cancellationTokenSource?.Dispose();
                _isDisposed = true;
            }
        }
    }
}