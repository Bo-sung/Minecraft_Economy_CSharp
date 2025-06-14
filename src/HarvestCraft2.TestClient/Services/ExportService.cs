using Serilog;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;

namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// 데이터 내보내기 서비스 구현
    /// </summary>
    public class ExportService : IExportService
    {
        public event EventHandler<ExportProgressEventArgs>? ExportProgress;
        public event EventHandler<ExportCompletedEventArgs>? ExportCompleted;

        public async Task<bool> ExportToCsvAsync<T>(IEnumerable<T> data, string filePath, ExportOptions? options = null)
        {
            var exportOptions = options ?? new ExportOptions();
            var startTime = DateTime.Now;

            try
            {
                var dataList = data.ToList();
                if (!dataList.Any())
                {
                    Log.Warning("내보낼 데이터가 없습니다: {FilePath}", filePath);
                    return false;
                }

                // 진행률 이벤트 발생
                RaiseProgressEvent(filePath, 0, "CSV 내보내기 시작", 0, dataList.Count, startTime);

                var csvContent = new StringBuilder();
                var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                // 헤더 추가
                if (exportOptions.IncludeHeaders)
                {
                    var headers = properties.Select(p => p.Name);
                    csvContent.AppendLine(string.Join(exportOptions.Delimiter, headers));
                }

                // 데이터 행 추가
                for (int i = 0; i < dataList.Count; i++)
                {
                    var item = dataList[i];
                    var values = properties.Select(p => FormatValue(p.GetValue(item), exportOptions));
                    csvContent.AppendLine(string.Join(exportOptions.Delimiter, values));

                    // 진행률 업데이트 (100개마다)
                    if (i % 100 == 0 || i == dataList.Count - 1)
                    {
                        var progress = (int)((i + 1) * 100.0 / dataList.Count);
                        RaiseProgressEvent(filePath, progress, $"처리 중: {i + 1}/{dataList.Count}", i + 1, dataList.Count, startTime);
                    }
                }

                // 파일 저장
                await File.WriteAllTextAsync(filePath, csvContent.ToString(), Encoding.GetEncoding(exportOptions.Encoding));

                var duration = DateTime.Now - startTime;
                var fileInfo = new FileInfo(filePath);

                RaiseCompletedEvent(filePath, ExportFormat.CSV, true, null, dataList.Count, duration, fileInfo.Length);

                Log.Information("CSV 내보내기 완료: {FilePath}, {Count}개 항목, {Duration}ms",
                    filePath, dataList.Count, duration.TotalMilliseconds);

                return true;
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                RaiseCompletedEvent(filePath, ExportFormat.CSV, false, ex.Message, 0, duration, 0);

                Log.Error(ex, "CSV 내보내기 중 오류가 발생했습니다: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> ExportToJsonAsync<T>(T data, string filePath, ExportOptions? options = null)
        {
            var exportOptions = options ?? new ExportOptions();
            var startTime = DateTime.Now;

            try
            {
                RaiseProgressEvent(filePath, 0, "JSON 내보내기 시작", 0, 1, startTime);

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                // 사용자 정의 속성 적용
                if (exportOptions.CustomProperties.TryGetValue("IndentedJson", out var indented) && indented is bool isIndented)
                {
                    jsonOptions.WriteIndented = isIndented;
                }

                var jsonContent = JsonSerializer.Serialize(data, jsonOptions);

                RaiseProgressEvent(filePath, 50, "JSON 직렬화 완료", 1, 1, startTime);

                await File.WriteAllTextAsync(filePath, jsonContent, Encoding.GetEncoding(exportOptions.Encoding));

                var duration = DateTime.Now - startTime;
                var fileInfo = new FileInfo(filePath);

                RaiseCompletedEvent(filePath, ExportFormat.JSON, true, null, 1, duration, fileInfo.Length);

                Log.Information("JSON 내보내기 완료: {FilePath}, {Duration}ms", filePath, duration.TotalMilliseconds);

                return true;
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                RaiseCompletedEvent(filePath, ExportFormat.JSON, false, ex.Message, 0, duration, 0);

                Log.Error(ex, "JSON 내보내기 중 오류가 발생했습니다: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> ExportToXmlAsync<T>(T data, string filePath, ExportOptions? options = null)
        {
            var exportOptions = options ?? new ExportOptions();
            var startTime = DateTime.Now;

            try
            {
                RaiseProgressEvent(filePath, 0, "XML 내보내기 시작", 0, 1, startTime);

                var serializer = new XmlSerializer(typeof(T));

                using var fileStream = new FileStream(filePath, FileMode.Create);
                using var writer = new StreamWriter(fileStream, Encoding.GetEncoding(exportOptions.Encoding));

                serializer.Serialize(writer, data);

                RaiseProgressEvent(filePath, 100, "XML 직렬화 완료", 1, 1, startTime);

                var duration = DateTime.Now - startTime;
                var fileInfo = new FileInfo(filePath);

                RaiseCompletedEvent(filePath, ExportFormat.XML, true, null, 1, duration, fileInfo.Length);

                Log.Information("XML 내보내기 완료: {FilePath}, {Duration}ms", filePath, duration.TotalMilliseconds);

                return true;
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                RaiseCompletedEvent(filePath, ExportFormat.XML, false, ex.Message, 0, duration, 0);

                Log.Error(ex, "XML 내보내기 중 오류가 발생했습니다: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> ExportChartToImageAsync(object chartData, string filePath, ImageFormat format = ImageFormat.PNG)
        {
            var startTime = DateTime.Now;

            try
            {
                // 향후 차트 라이브러리와 연동하여 구현
                Log.Warning("차트 이미지 내보내기는 아직 구현되지 않았습니다: {FilePath}", filePath);

                await Task.Delay(100); // 비동기 패턴 유지
                return false;
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                RaiseCompletedEvent(filePath, ExportFormat.PNG, false, ex.Message, 0, duration, 0);

                Log.Error(ex, "차트 이미지 내보내기 중 오류가 발생했습니다: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> ExportToPdfAsync<T>(T data, string filePath, string templateName, ExportOptions? options = null)
        {
            var startTime = DateTime.Now;

            try
            {
                // 향후 PDF 라이브러리와 연동하여 구현
                Log.Warning("PDF 내보내기는 아직 구현되지 않았습니다: {FilePath}", filePath);

                await Task.Delay(100); // 비동기 패턴 유지
                return false;
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                RaiseCompletedEvent(filePath, ExportFormat.PDF, false, ex.Message, 0, duration, 0);

                Log.Error(ex, "PDF 내보내기 중 오류가 발생했습니다: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> ExportToExcelAsync<T>(IEnumerable<T> data, string filePath, ExportOptions? options = null)
        {
            var startTime = DateTime.Now;

            try
            {
                // 향후 Excel 라이브러리와 연동하여 구현
                Log.Warning("Excel 내보내기는 아직 구현되지 않았습니다: {FilePath}", filePath);

                await Task.Delay(100); // 비동기 패턴 유지
                return false;
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                RaiseCompletedEvent(filePath, ExportFormat.Excel, false, ex.Message, 0, duration, 0);

                Log.Error(ex, "Excel 내보내기 중 오류가 발생했습니다: {FilePath}", filePath);
                return false;
            }
        }

        public List<ExportFormat> GetSupportedFormats()
        {
            return new List<ExportFormat>
            {
                ExportFormat.CSV,
                ExportFormat.JSON,
                ExportFormat.XML
                // 향후 구현: ExportFormat.PDF, ExportFormat.Excel, ExportFormat.PNG
            };
        }

        private string FormatValue(object? value, ExportOptions options)
        {
            if (value == null) return string.Empty;

            return value switch
            {
                DateTime dateTime => dateTime.ToString(options.DateFormat, CultureInfo.InvariantCulture),
                decimal or double or float => ((IFormattable)value).ToString(options.NumberFormat, CultureInfo.InvariantCulture),
                string str => $"\"{str.Replace("\"", "\"\"")}\"", // CSV 이스케이프
                _ => value.ToString() ?? string.Empty
            };
        }

        private void RaiseProgressEvent(string fileName, int percentage, string step, long processed, long total, DateTime startTime)
        {
            var elapsed = DateTime.Now - startTime;
            var estimatedTotal = processed > 0 ? TimeSpan.FromTicks(elapsed.Ticks * total / processed) : TimeSpan.Zero;
            var remaining = estimatedTotal - elapsed;

            ExportProgress?.Invoke(this, new ExportProgressEventArgs
            {
                FileName = fileName,
                ProgressPercentage = percentage,
                CurrentStep = step,
                ProcessedItems = processed,
                TotalItems = total,
                StartTime = startTime,
                EstimatedTimeRemaining = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero
            });
        }

        private void RaiseCompletedEvent(string fileName, ExportFormat format, bool success, string? error, long items, TimeSpan duration, long fileSize)
        {
            ExportCompleted?.Invoke(this, new ExportCompletedEventArgs
            {
                FileName = fileName,
                Format = format,
                Success = success,
                ErrorMessage = error,
                TotalItems = items,
                Duration = duration,
                FileSizeBytes = fileSize
            });
        }
    }
}