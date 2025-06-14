namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// 데이터 내보내기 서비스 인터페이스
    /// </summary>
    public interface IExportService
    {
        /// <summary>
        /// 데이터를 CSV 형식으로 내보냅니다.
        /// </summary>
        Task<bool> ExportToCsvAsync<T>(IEnumerable<T> data, string filePath, ExportOptions? options = null);

        /// <summary>
        /// 데이터를 JSON 형식으로 내보냅니다.
        /// </summary>
        Task<bool> ExportToJsonAsync<T>(T data, string filePath, ExportOptions? options = null);

        /// <summary>
        /// 데이터를 XML 형식으로 내보냅니다.
        /// </summary>
        Task<bool> ExportToXmlAsync<T>(T data, string filePath, ExportOptions? options = null);

        /// <summary>
        /// 차트 데이터를 이미지로 내보냅니다.
        /// </summary>
        Task<bool> ExportChartToImageAsync(object chartData, string filePath, ImageFormat format = ImageFormat.PNG);

        /// <summary>
        /// 데이터를 PDF 보고서로 내보냅니다.
        /// </summary>
        Task<bool> ExportToPdfAsync<T>(T data, string filePath, string templateName, ExportOptions? options = null);

        /// <summary>
        /// 데이터를 Excel 형식으로 내보냅니다.
        /// </summary>
        Task<bool> ExportToExcelAsync<T>(IEnumerable<T> data, string filePath, ExportOptions? options = null);

        /// <summary>
        /// 지원되는 내보내기 형식 목록을 가져옵니다.
        /// </summary>
        List<ExportFormat> GetSupportedFormats();

        /// <summary>
        /// 내보내기 진행률 이벤트
        /// </summary>
        event EventHandler<ExportProgressEventArgs> ExportProgress;

        /// <summary>
        /// 내보내기 완료 이벤트
        /// </summary>
        event EventHandler<ExportCompletedEventArgs> ExportCompleted;
    }

    /// <summary>
    /// 내보내기 옵션 클래스
    /// </summary>
    public class ExportOptions
    {
        public bool IncludeHeaders { get; set; } = true;
        public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
        public string NumberFormat { get; set; } = "F2";
        public string Encoding { get; set; } = "UTF-8";
        public string Delimiter { get; set; } = ",";
        public bool OverwriteExisting { get; set; } = false;
        public bool ShowProgress { get; set; } = true;
        public Dictionary<string, object> CustomProperties { get; set; } = new();
    }

    /// <summary>
    /// 이미지 형식 열거형
    /// </summary>
    public enum ImageFormat
    {
        PNG,
        JPEG,
        BMP,
        SVG
    }

    /// <summary>
    /// 내보내기 진행률 이벤트 인수
    /// </summary>
    public class ExportProgressEventArgs : EventArgs
    {
        public string FileName { get; set; } = string.Empty;
        public int ProgressPercentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public long ProcessedItems { get; set; }
        public long TotalItems { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// 내보내기 완료 이벤트 인수
    /// </summary>
    public class ExportCompletedEventArgs : EventArgs
    {
        public string FileName { get; set; } = string.Empty;
        public ExportFormat Format { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public long TotalItems { get; set; }
        public TimeSpan Duration { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime CompletedAt { get; set; } = DateTime.Now;
    }
}