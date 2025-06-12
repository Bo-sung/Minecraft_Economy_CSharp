using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HarvestCraft2.TestClient.Models;

namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// 실시간 가격 차트 데이터 관리 및 시각화를 담당하는 서비스 인터페이스
    /// </summary>
    public interface IChartService
    {
        // ============================================================================
        // 차트 데이터 관리
        // ============================================================================

        /// <summary>
        /// 현재 모니터링 중인 아이템 목록
        /// </summary>
        IReadOnlyList<string> MonitoredItems { get; }

        /// <summary>
        /// 실시간 업데이트 활성화 여부
        /// </summary>
        bool IsRealTimeUpdateEnabled { get; set; }

        /// <summary>
        /// 업데이트 간격 (초 단위)
        /// </summary>
        int UpdateIntervalSeconds { get; set; }

        /// <summary>
        /// 아이템 모니터링 시작
        /// </summary>
        Task StartMonitoringAsync(string itemId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 아이템 모니터링 중지
        /// </summary>
        Task StopMonitoringAsync(string itemId);

        /// <summary>
        /// 모든 모니터링 중지
        /// </summary>
        Task StopAllMonitoringAsync();

        /// <summary>
        /// 차트 데이터 수동 새로고침
        /// </summary>
        Task RefreshChartDataAsync(string itemId, CancellationToken cancellationToken = default);

        // ============================================================================
        // 가격 차트 데이터
        // ============================================================================

        /// <summary>
        /// 실시간 가격 라인 차트 데이터 조회
        /// </summary>
        Task<PriceChartData> GetPriceLineChartAsync(string itemId, TimeRange timeRange, CancellationToken cancellationToken = default);

        /// <summary>
        /// 캔들스틱 차트 데이터 조회
        /// </summary>
        Task<CandlestickChartData> GetCandlestickChartAsync(string itemId, TimeRange timeRange, TimeInterval interval, CancellationToken cancellationToken = default);

        /// <summary>
        /// 볼륨 차트 데이터 조회
        /// </summary>
        Task<VolumeChartData> GetVolumeChartAsync(string itemId, TimeRange timeRange, CancellationToken cancellationToken = default);

        /// <summary>
        /// 가격 분포 히스토그램 데이터 조회
        /// </summary>
        Task<HistogramChartData> GetPriceDistributionAsync(string itemId, TimeRange timeRange, CancellationToken cancellationToken = default);

        /// <summary>
        /// 여러 아이템 가격 비교 차트 데이터
        /// </summary>
        Task<MultiItemChartData> GetMultiItemComparisonAsync(List<string> itemIds, TimeRange timeRange, CancellationToken cancellationToken = default);

        // ============================================================================
        // 시장 분석 차트
        // ============================================================================

        /// <summary>
        /// 시장 압력 차트 데이터 (수요/공급)
        /// </summary>
        Task<MarketPressureChartData> GetMarketPressureChartAsync(string itemId, TimeRange timeRange, CancellationToken cancellationToken = default);

        /// <summary>
        /// 가격 변동성 차트 데이터
        /// </summary>
        Task<VolatilityChartData> GetVolatilityChartAsync(string itemId, TimeRange timeRange, CancellationToken cancellationToken = default);

        /// <summary>
        /// 거래량 트렌드 차트 데이터
        /// </summary>
        Task<TradingVolumeChartData> GetTradingVolumeChartAsync(string itemId, TimeRange timeRange, CancellationToken cancellationToken = default);

        /// <summary>
        /// 카테고리별 시장 점유율 파이 차트 데이터
        /// </summary>
        Task<PieChartData> GetCategoryMarketShareAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 시간대별 거래 활동 히트맵 데이터
        /// </summary>
        Task<HeatmapChartData> GetTradingActivityHeatmapAsync(TimeRange timeRange, CancellationToken cancellationToken = default);

        // ============================================================================
        // 예측 및 분석 차트
        // ============================================================================

        /// <summary>
        /// 가격 예측 차트 데이터
        /// </summary>
        Task<PredictionChartData> GetPricePredictionChartAsync(string itemId, int forecastDays = 7, CancellationToken cancellationToken = default);

        /// <summary>
        /// 이동평균선 차트 데이터
        /// </summary>
        Task<MovingAverageChartData> GetMovingAverageChartAsync(string itemId, TimeRange timeRange, List<int> periods, CancellationToken cancellationToken = default);

        /// <summary>
        /// 상관관계 매트릭스 차트 데이터
        /// </summary>
        Task<CorrelationMatrixData> GetItemCorrelationMatrixAsync(List<string> itemIds, TimeRange timeRange, CancellationToken cancellationToken = default);

        // ============================================================================
        // 차트 설정 및 유틸리티
        // ============================================================================

        /// <summary>
        /// 차트 테마 설정
        /// </summary>
        ChartTheme CurrentTheme { get; set; }

        /// <summary>
        /// 차트 색상 팔레트 조회
        /// </summary>
        List<ChartColor> GetColorPalette(ChartColorScheme scheme = ChartColorScheme.Default);

        /// <summary>
        /// 데이터 집계 및 샘플링
        /// </summary>
        Task<ChartDataPoints> AggregateDataAsync(List<PriceHistoryResponse> rawData, TimeInterval interval, AggregationType aggregationType);

        /// <summary>
        /// 차트 데이터 내보내기
        /// </summary>
        Task<bool> ExportChartDataAsync(string itemId, TimeRange timeRange, ExportFormat format, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 차트 이미지 생성 및 저장
        /// </summary>
        Task<bool> SaveChartImageAsync(ChartType chartType, string itemId, string filePath, ChartImageSettings settings, CancellationToken cancellationToken = default);

        // ============================================================================
        // 실시간 데이터 스트리밍
        // ============================================================================

        /// <summary>
        /// 실시간 가격 데이터 스트림 시작
        /// </summary>
        Task StartRealTimeStreamAsync(string itemId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 실시간 데이터 스트림 중지
        /// </summary>
        Task StopRealTimeStreamAsync(string itemId);

        /// <summary>
        /// 버퍼된 실시간 데이터 조회
        /// </summary>
        ChartDataBuffer GetRealTimeBuffer(string itemId);

        /// <summary>
        /// 실시간 데이터 버퍼 크기 설정
        /// </summary>
        void SetBufferSize(int maxDataPoints);

        // ============================================================================
        // 이벤트
        // ============================================================================

        /// <summary>
        /// 차트 데이터 업데이트됨
        /// </summary>
        event EventHandler<ChartDataUpdatedEventArgs> ChartDataUpdated;

        /// <summary>
        /// 실시간 가격 데이터 수신됨
        /// </summary>
        event EventHandler<RealTimePriceEventArgs> RealTimePriceReceived;

        /// <summary>
        /// 차트 오류 발생
        /// </summary>
        event EventHandler<ChartErrorEventArgs> ChartError;

        /// <summary>
        /// 모니터링 상태 변경됨
        /// </summary>
        event EventHandler<MonitoringStatusChangedEventArgs> MonitoringStatusChanged;
    }

    // ============================================================================
    // 차트 데이터 모델들
    // ============================================================================

    /// <summary>
    /// 기본 차트 데이터 포인트
    /// </summary>
    public class ChartDataPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Value { get; set; }
        public object? AdditionalData { get; set; }
    }

    /// <summary>
    /// 차트 데이터 포인트 컬렉션
    /// </summary>
    public class ChartDataPoints : List<ChartDataPoint>
    {
        public string ItemId { get; set; } = string.Empty;
        public TimeRange TimeRange { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// 가격 라인 차트 데이터
    /// </summary>
    public class PriceChartData
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public List<ChartDataPoint> PricePoints { get; set; } = new();
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal AveragePrice { get; set; }
        public TimeRange TimeRange { get; set; } = new();
    }

    /// <summary>
    /// 캔들스틱 차트 데이터
    /// </summary>
    public class CandlestickChartData
    {
        public string ItemId { get; set; } = string.Empty;
        public List<CandlestickDataPoint> Candles { get; set; } = new();
        public TimeRange TimeRange { get; set; } = new();
        public TimeInterval Interval { get; set; }
    }

    public class CandlestickDataPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
    }

    /// <summary>
    /// 볼륨 차트 데이터
    /// </summary>
    public class VolumeChartData
    {
        public string ItemId { get; set; } = string.Empty;
        public List<VolumeDataPoint> VolumePoints { get; set; } = new();
        public long TotalVolume { get; set; }
        public long MaxVolume { get; set; }
        public TimeRange TimeRange { get; set; } = new();
    }

    public class VolumeDataPoint
    {
        public DateTime Timestamp { get; set; }
        public long Volume { get; set; }
        public decimal WeightedPrice { get; set; }
    }

    /// <summary>
    /// 히스토그램 차트 데이터
    /// </summary>
    public class HistogramChartData
    {
        public string ItemId { get; set; } = string.Empty;
        public List<HistogramBin> Bins { get; set; } = new();
        public int TotalCount { get; set; }
        public decimal MeanValue { get; set; }
        public decimal StandardDeviation { get; set; }
    }

    public class HistogramBin
    {
        public decimal RangeStart { get; set; }
        public decimal RangeEnd { get; set; }
        public int Count { get; set; }
        public double Frequency { get; set; }
    }

    /// <summary>
    /// 다중 아이템 비교 차트 데이터
    /// </summary>
    public class MultiItemChartData
    {
        public List<string> ItemIds { get; set; } = new();
        public Dictionary<string, List<ChartDataPoint>> ItemSeries { get; set; } = new();
        public TimeRange TimeRange { get; set; } = new();
        public bool IsNormalized { get; set; } // 정규화 여부 (0-100% 스케일)
    }

    /// <summary>
    /// 시장 압력 차트 데이터
    /// </summary>
    public class MarketPressureChartData
    {
        public string ItemId { get; set; } = string.Empty;
        public List<MarketPressurePoint> PressurePoints { get; set; } = new();
        public TimeRange TimeRange { get; set; } = new();
    }

    public class MarketPressurePoint
    {
        public DateTime Timestamp { get; set; }
        public decimal DemandPressure { get; set; }
        public decimal SupplyPressure { get; set; }
        public decimal NetPressure => DemandPressure - SupplyPressure;
        public decimal Price { get; set; }
    }

    /// <summary>
    /// 변동성 차트 데이터
    /// </summary>
    public class VolatilityChartData
    {
        public string ItemId { get; set; } = string.Empty;
        public List<VolatilityPoint> VolatilityPoints { get; set; } = new();
        public decimal AverageVolatility { get; set; }
        public TimeRange TimeRange { get; set; } = new();
    }

    public class VolatilityPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Volatility { get; set; }
        public decimal Price { get; set; }
    }

    /// <summary>
    /// 거래량 트렌드 차트 데이터
    /// </summary>
    public class TradingVolumeChartData
    {
        public string ItemId { get; set; } = string.Empty;
        public List<TradingVolumePoint> VolumePoints { get; set; } = new();
        public List<ChartDataPoint> TrendLine { get; set; } = new();
        public TimeRange TimeRange { get; set; } = new();
    }

    public class TradingVolumePoint
    {
        public DateTime Timestamp { get; set; }
        public long BuyVolume { get; set; }
        public long SellVolume { get; set; }
        public long NetVolume => BuyVolume - SellVolume;
        public long TotalVolume => BuyVolume + SellVolume;
    }

    /// <summary>
    /// 파이 차트 데이터
    /// </summary>
    public class PieChartData
    {
        public string Title { get; set; } = string.Empty;
        public List<PieSlice> Slices { get; set; } = new();
        public decimal Total { get; set; }
    }

    public class PieSlice
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    /// <summary>
    /// 히트맵 차트 데이터
    /// </summary>
    public class HeatmapChartData
    {
        public string Title { get; set; } = string.Empty;
        public List<string> XLabels { get; set; } = new(); // 시간대
        public List<string> YLabels { get; set; } = new(); // 요일
        public decimal[,] Values { get; set; } = new decimal[0, 0];
        public decimal MinValue { get; set; }
        public decimal MaxValue { get; set; }
        public TimeRange TimeRange { get; set; } = new();
    }

    /// <summary>
    /// 예측 차트 데이터
    /// </summary>
    public class PredictionChartData
    {
        public string ItemId { get; set; } = string.Empty;
        public List<ChartDataPoint> HistoricalData { get; set; } = new();
        public List<PredictionPoint> PredictionData { get; set; } = new();
        public double ConfidenceLevel { get; set; }
        public string Model { get; set; } = string.Empty;
    }

    public class PredictionPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal PredictedValue { get; set; }
        public decimal LowerBound { get; set; }
        public decimal UpperBound { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>
    /// 이동평균선 차트 데이터
    /// </summary>
    public class MovingAverageChartData
    {
        public string ItemId { get; set; } = string.Empty;
        public List<ChartDataPoint> PriceData { get; set; } = new();
        public Dictionary<int, List<ChartDataPoint>> MovingAverages { get; set; } = new(); // Period -> MA Data
        public TimeRange TimeRange { get; set; } = new();
    }

    /// <summary>
    /// 상관관계 매트릭스 데이터
    /// </summary>
    public class CorrelationMatrixData
    {
        public List<string> ItemIds { get; set; } = new();
        public double[,] CorrelationMatrix { get; set; } = new double[0, 0];
        public TimeRange TimeRange { get; set; } = new();
    }

    /// <summary>
    /// 실시간 데이터 버퍼
    /// </summary>
    public class ChartDataBuffer
    {
        public string ItemId { get; set; } = string.Empty;
        public Queue<ChartDataPoint> Buffer { get; set; } = new();
        public int MaxSize { get; set; } = 1000;
        public DateTime LastUpdate { get; set; }

        public void AddDataPoint(ChartDataPoint point)
        {
            Buffer.Enqueue(point);
            if (Buffer.Count > MaxSize)
            {
                Buffer.Dequeue();
            }
            LastUpdate = DateTime.UtcNow;
        }
    }

    // ============================================================================
    // 설정 및 열거형
    // ============================================================================

    public class TimeRange
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan Duration => EndDate - StartDate;
    }

    public enum TimeInterval
    {
        Minute1,
        Minute5,
        Minute15,
        Minute30,
        Hour1,
        Hour4,
        Hour12,
        Day1,
        Week1,
        Month1
    }

    public enum AggregationType
    {
        Average,
        OHLC, // Open, High, Low, Close
        Sum,
        Count,
        WeightedAverage
    }

    public enum ChartType
    {
        Line,
        Candlestick,
        Volume,
        Histogram,
        Pie,
        Heatmap,
        Scatter,
        Area
    }

    public enum ChartTheme
    {
        Light,
        Dark,
        HighContrast,
        Custom
    }

    public enum ChartColorScheme
    {
        Default,
        Financial,
        Categorical,
        Sequential,
        Diverging
    }

    public enum ExportFormat
    {
        CSV,
        JSON,
        Excel,
        PDF
    }

    public class ChartColor
    {
        public string Name { get; set; } = string.Empty;
        public string HexCode { get; set; } = string.Empty;
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public byte A { get; set; } = 255;
    }

    public class ChartImageSettings
    {
        public int Width { get; set; } = 800;
        public int Height { get; set; } = 600;
        public int DPI { get; set; } = 96;
        public string Format { get; set; } = "PNG";
        public bool IncludeTitle { get; set; } = true;
        public bool IncludeLegend { get; set; } = true;
        public ChartTheme Theme { get; set; } = ChartTheme.Light;
    }

    // ============================================================================
    // 이벤트 인수 클래스들
    // ============================================================================

    public class ChartDataUpdatedEventArgs : EventArgs
    {
        public string ItemId { get; set; } = string.Empty;
        public ChartType ChartType { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int DataPointCount { get; set; }
    }

    public class RealTimePriceEventArgs : EventArgs
    {
        public string ItemId { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime Timestamp { get; set; }
        public long Volume { get; set; }
        public decimal Change { get; set; }
        public decimal ChangePercent { get; set; }
    }

    public class ChartErrorEventArgs : EventArgs
    {
        public string ItemId { get; set; } = string.Empty;
        public ChartType ChartType { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    public class MonitoringStatusChangedEventArgs : EventArgs
    {
        public string ItemId { get; set; } = string.Empty;
        public bool IsMonitoring { get; set; }
        public DateTime ChangedAt { get; set; }
        public string? Reason { get; set; }
    }
}