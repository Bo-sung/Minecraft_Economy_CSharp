// ============================================================================
// Models/ChartModels.cs - 기존 ChartService와 호환되는 모델들
// ============================================================================

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace HarvestCraft2.TestClient.Models
{
    // ============================================================================
    // 기본 차트 데이터 구조 (기존 ChartService 호환)
    // ============================================================================

    /// <summary>
    /// 차트 데이터 포인트
    /// </summary>
    public class ChartDataPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Value { get; set; }
        public int Volume { get; set; } = 0;
    }

    /// <summary>
    /// 시간 범위
    /// </summary>
    public class TimeRange
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan Duration => EndDate - StartDate;
    }

    /// <summary>
    /// 가격 차트 데이터 (기존 ChartService의 PriceChartData와 호환)
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
    /// 캔들스틱 데이터 포인트
    /// </summary>
    public class CandlestickDataPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public int Volume { get; set; }
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

    /// <summary>
    /// 볼륨 데이터 포인트
    /// </summary>
    public class VolumeDataPoint
    {
        public DateTime Timestamp { get; set; }
        public int Volume { get; set; }
        public decimal WeightedPrice { get; set; }
    }

    /// <summary>
    /// 볼륨 차트 데이터
    /// </summary>
    public class VolumeChartData
    {
        public string ItemId { get; set; } = string.Empty;
        public List<VolumeDataPoint> VolumePoints { get; set; } = new();
        public long TotalVolume { get; set; }
        public int MaxVolume { get; set; }
        public TimeRange TimeRange { get; set; } = new();
    }

    /// <summary>
    /// 히스토그램 구간
    /// </summary>
    public class HistogramBin
    {
        public decimal RangeStart { get; set; }
        public decimal RangeEnd { get; set; }
        public int Count { get; set; }
        public double Frequency { get; set; }
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

    /// <summary>
    /// 다중 아이템 비교 차트 데이터
    /// </summary>
    public class MultiItemChartData
    {
        public List<string> ItemIds { get; set; } = new();
        public Dictionary<string, List<ChartDataPoint>> ItemSeries { get; set; } = new();
        public TimeRange TimeRange { get; set; } = new();
        public bool IsNormalized { get; set; }
    }

    // ============================================================================
    // 시장 분석 관련 모델들
    // ============================================================================

    /// <summary>
    /// 시장 압력 포인트
    /// </summary>
    public class MarketPressurePoint
    {
        public DateTime Timestamp { get; set; }
        public decimal DemandPressure { get; set; }
        public decimal SupplyPressure { get; set; }
        public decimal Price { get; set; }
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

    /// <summary>
    /// 변동성 포인트
    /// </summary>
    public class VolatilityPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Volatility { get; set; }
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

    /// <summary>
    /// 거래량 포인트
    /// </summary>
    public class TradingVolumePoint
    {
        public DateTime Timestamp { get; set; }
        public int BuyVolume { get; set; }
        public int SellVolume { get; set; }
        public int TotalVolume => BuyVolume + SellVolume;
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

    /// <summary>
    /// 파이 차트 슬라이스
    /// </summary>
    public class PieSlice
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = string.Empty;
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

    /// <summary>
    /// 히트맵 차트 데이터
    /// </summary>
    public class HeatmapChartData
    {
        public string Title { get; set; } = string.Empty;
        public List<string> XLabels { get; set; } = new();
        public List<string> YLabels { get; set; } = new();
        public decimal[,] Values { get; set; } = new decimal[0, 0];
        public decimal MinValue { get; set; }
        public decimal MaxValue { get; set; }
        public TimeRange TimeRange { get; set; } = new();
    }

    // ============================================================================
    // 예측 및 분석 모델들
    // ============================================================================

    /// <summary>
    /// 예측 포인트
    /// </summary>
    public class PredictionPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal PredictedValue { get; set; }
        public decimal LowerBound { get; set; }
        public decimal UpperBound { get; set; }
        public double Confidence { get; set; }
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

    /// <summary>
    /// 이동평균 차트 데이터
    /// </summary>
    public class MovingAverageChartData
    {
        public string ItemId { get; set; } = string.Empty;
        public List<ChartDataPoint> PriceData { get; set; } = new();
        public Dictionary<int, List<ChartDataPoint>> MovingAverages { get; set; } = new();
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

    // ============================================================================
    // 이벤트 인수 클래스들
    // ============================================================================

    /// <summary>
    /// 차트 데이터 업데이트 이벤트 인수
    /// </summary>
    public class ChartDataUpdatedEventArgs : EventArgs
    {
        public string ItemId { get; set; } = string.Empty;
        public ChartType ChartType { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int DataPointCount { get; set; }
    }

    /// <summary>
    /// 실시간 가격 수신 이벤트 인수
    /// </summary>
    public class RealTimePriceEventArgs : EventArgs
    {
        public string ItemId { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime Timestamp { get; set; }
        public int Volume { get; set; }
        public decimal Change { get; set; }
        public double ChangePercent { get; set; }
    }

    /// <summary>
    /// 모니터링 상태 변경 이벤트 인수
    /// </summary>
    public class MonitoringStatusChangedEventArgs : EventArgs
    {
        public string ItemId { get; set; } = string.Empty;
        public bool IsMonitoring { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    /// <summary>
    /// 차트 오류 이벤트 인수
    /// </summary>
    public class ChartErrorEventArgs : EventArgs
    {
        public string ItemId { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    // ============================================================================
    // 내부 데이터 구조들
    // ============================================================================

    /// <summary>
    /// 차트 데이터 버퍼 (실시간 데이터 저장용)
    /// </summary>
    public class ChartDataBuffer
    {
        private readonly Queue<ChartDataPoint> _buffer = new();
        private readonly object _lock = new();

        public string ItemId { get; set; } = string.Empty;
        public int MaxSize { get; set; } = 1000;

        public void AddDataPoint(ChartDataPoint point)
        {
            lock (_lock)
            {
                _buffer.Enqueue(point);
                while (_buffer.Count > MaxSize)
                {
                    _buffer.Dequeue();
                }
            }
        }

        public List<ChartDataPoint> GetDataPoints()
        {
            lock (_lock)
            {
                return new List<ChartDataPoint>(_buffer);
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _buffer.Count;
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _buffer.Clear();
            }
        }
    }

    /// <summary>
    /// 차트 데이터 포인트 컬렉션 (기존 ChartService 호환)
    /// </summary>
    public class ChartDataPoints
    {
        public string ItemId { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
        public List<ChartDataPoint> Points { get; set; } = new();
    }

    /// <summary>
    /// 차트 색상 정의
    /// </summary>
    public class ChartColor
    {
        public string Name { get; set; } = string.Empty;
        public string HexCode { get; set; } = string.Empty;
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
    }

    /// <summary>
    /// 차트 이미지 설정
    /// </summary>
    public class ChartImageSettings
    {
        public int Width { get; set; } = 800;
        public int Height { get; set; } = 600;
        public string Format { get; set; } = "PNG";
        public int Quality { get; set; } = 95;
        public bool IncludeLegend { get; set; } = true;
        public bool IncludeTitle { get; set; } = true;
    }

    // ============================================================================
    // 열거형 정의
    // ============================================================================

    /// <summary>
    /// 시간 간격
    /// </summary>
    public enum TimeInterval
    {
        Minute1,
        Minute5,
        Minute15,
        Hour1,
        Hour4,
        Day1,
        Week1
    }

    /// <summary>
    /// 차트 테마
    /// </summary>
    public enum ChartTheme
    {
        Light,
        Dark,
        Auto
    }

    /// <summary>
    /// 차트 타입
    /// </summary>
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

    /// <summary>
    /// 차트 색상 스키마
    /// </summary>
    public enum ChartColorScheme
    {
        Default,
        Financial,
        Categorical,
        Sequential,
        Diverging
    }

    /// <summary>
    /// 집계 타입
    /// </summary>
    public enum AggregationType
    {
        Average,
        Sum,
        Count,
        WeightedAverage,
        OHLC
    }

    /// <summary>
    /// 내보내기 형식
    /// </summary>
    public enum ExportFormat
    {
        CSV,
        JSON,
        Excel,
        PDF
    }

    // ============================================================================
    // LiveCharts 바인딩용 모델들
    // ============================================================================

    /// <summary>
    /// LiveCharts용 가격 포인트
    /// </summary>
    public class LiveChartsPricePoint
    {
        public double X { get; set; } // 시간 (OLE Automation Date)
        public double Y { get; set; } // 가격

        public static LiveChartsPricePoint FromChartDataPoint(ChartDataPoint point)
        {
            return new LiveChartsPricePoint
            {
                X = point.Timestamp.ToOADate(),
                Y = (double)point.Value
            };
        }
    }

    /// <summary>
    /// LiveCharts용 볼륨 포인트
    /// </summary>
    public class LiveChartsVolumePoint
    {
        public double X { get; set; } // 시간
        public double Y { get; set; } // 볼륨

        public static LiveChartsVolumePoint FromVolumeDataPoint(VolumeDataPoint point)
        {
            return new LiveChartsVolumePoint
            {
                X = point.Timestamp.ToOADate(),
                Y = point.Volume
            };
        }
    }
}