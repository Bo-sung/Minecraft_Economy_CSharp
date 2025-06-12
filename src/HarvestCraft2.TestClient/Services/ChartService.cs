using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HarvestCraft2.TestClient.Models;

namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// 실시간 차트 데이터 관리 및 시각화 서비스 구현체
    /// </summary>
    public class ChartService : IChartService, IDisposable
    {
        private readonly IApiService _apiService;
        private readonly ILogger<ChartService> _logger;
        private readonly ConcurrentDictionary<string, ChartDataBuffer> _dataBuffers;
        private readonly ConcurrentDictionary<string, Timer> _monitoringTimers;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _monitoringCancellations;
        private readonly Timer _globalUpdateTimer;

        private readonly HashSet<string> _monitoredItems;
        private readonly object _monitoredItemsLock = new();

        private bool _isRealTimeUpdateEnabled = true;
        private int _updateIntervalSeconds = 30;
        private int _maxBufferSize = 1000;
        private ChartTheme _currentTheme = ChartTheme.Light;

        // ============================================================================
        // 생성자 및 초기화
        // ============================================================================

        public ChartService(IApiService apiService, ILogger<ChartService> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _dataBuffers = new ConcurrentDictionary<string, ChartDataBuffer>();
            _monitoringTimers = new ConcurrentDictionary<string, Timer>();
            _monitoringCancellations = new ConcurrentDictionary<string, CancellationTokenSource>();
            _monitoredItems = new HashSet<string>();

            // 글로벌 업데이트 타이머 (모든 모니터링 아이템 일괄 업데이트)
            _globalUpdateTimer = new Timer(GlobalUpdateCallback, null,
                TimeSpan.FromSeconds(_updateIntervalSeconds),
                TimeSpan.FromSeconds(_updateIntervalSeconds));

            _logger.LogInformation("ChartService 초기화 완료");
        }

        // ============================================================================
        // 속성 및 이벤트
        // ============================================================================

        public IReadOnlyList<string> MonitoredItems
        {
            get
            {
                lock (_monitoredItemsLock)
                {
                    return _monitoredItems.ToList();
                }
            }
        }

        public bool IsRealTimeUpdateEnabled
        {
            get => _isRealTimeUpdateEnabled;
            set
            {
                if (_isRealTimeUpdateEnabled != value)
                {
                    _isRealTimeUpdateEnabled = value;
                    _logger.LogInformation("실시간 업데이트 상태 변경: {Enabled}", value);

                    if (value)
                        _globalUpdateTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(_updateIntervalSeconds));
                    else
                        _globalUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }

        public int UpdateIntervalSeconds
        {
            get => _updateIntervalSeconds;
            set
            {
                if (_updateIntervalSeconds != value && value > 0)
                {
                    _updateIntervalSeconds = value;
                    _logger.LogInformation("업데이트 간격 변경: {Seconds}초", value);

                    if (_isRealTimeUpdateEnabled)
                    {
                        _globalUpdateTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(value));
                    }
                }
            }
        }

        public ChartTheme CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    _logger.LogInformation("차트 테마 변경: {Theme}", value);
                }
            }
        }

        // 이벤트 정의
        public event EventHandler<ChartDataUpdatedEventArgs>? ChartDataUpdated;
        public event EventHandler<RealTimePriceEventArgs>? RealTimePriceReceived;
        public event EventHandler<ChartErrorEventArgs>? ChartError;
        public event EventHandler<MonitoringStatusChangedEventArgs>? MonitoringStatusChanged;

        // ============================================================================
        // 차트 데이터 관리
        // ============================================================================

        public async Task StartMonitoringAsync(string itemId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("아이템 모니터링 시작: {ItemId}", itemId);

                lock (_monitoredItemsLock)
                {
                    if (_monitoredItems.Contains(itemId))
                    {
                        _logger.LogWarning("이미 모니터링 중인 아이템: {ItemId}", itemId);
                        return;
                    }
                    _monitoredItems.Add(itemId);
                }

                // 데이터 버퍼 초기화
                var buffer = new ChartDataBuffer
                {
                    ItemId = itemId,
                    MaxSize = _maxBufferSize
                };
                _dataBuffers.TryAdd(itemId, buffer);

                // 개별 아이템 모니터링 타이머 생성 (더 자주 업데이트)
                var cancellationSource = new CancellationTokenSource();
                _monitoringCancellations.TryAdd(itemId, cancellationSource);

                var timer = new Timer(async _ => await UpdateItemDataAsync(itemId, cancellationSource.Token),
                    null, TimeSpan.Zero, TimeSpan.FromSeconds(10)); // 10초마다
                _monitoringTimers.TryAdd(itemId, timer);

                MonitoringStatusChanged?.Invoke(this, new MonitoringStatusChangedEventArgs
                {
                    ItemId = itemId,
                    IsMonitoring = true,
                    ChangedAt = DateTime.UtcNow
                });

                _logger.LogInformation("아이템 모니터링 시작 완료: {ItemId}", itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 모니터링 시작 실패: {ItemId}", itemId);
                ChartError?.Invoke(this, new ChartErrorEventArgs
                {
                    ItemId = itemId,
                    ErrorMessage = ex.Message,
                    Exception = ex,
                    OccurredAt = DateTime.UtcNow
                });
            }
        }

        public async Task StopMonitoringAsync(string itemId)
        {
            try
            {
                _logger.LogInformation("아이템 모니터링 중지: {ItemId}", itemId);

                lock (_monitoredItemsLock)
                {
                    _monitoredItems.Remove(itemId);
                }

                // 타이머 정리
                if (_monitoringTimers.TryRemove(itemId, out var timer))
                {
                    timer.Dispose();
                }

                // 취소 토큰 정리
                if (_monitoringCancellations.TryRemove(itemId, out var cancellation))
                {
                    cancellation.Cancel();
                    cancellation.Dispose();
                }

                // 버퍼는 유지 (히스토리 데이터로 활용)

                MonitoringStatusChanged?.Invoke(this, new MonitoringStatusChangedEventArgs
                {
                    ItemId = itemId,
                    IsMonitoring = false,
                    ChangedAt = DateTime.UtcNow
                });

                _logger.LogInformation("아이템 모니터링 중지 완료: {ItemId}", itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 모니터링 중지 실패: {ItemId}", itemId);
            }
        }

        public async Task StopAllMonitoringAsync()
        {
            var monitoredItems = MonitoredItems.ToList();
            foreach (var itemId in monitoredItems)
            {
                await StopMonitoringAsync(itemId);
            }
            _logger.LogInformation("모든 모니터링 중지 완료");
        }

        public async Task RefreshChartDataAsync(string itemId, CancellationToken cancellationToken = default)
        {
            await UpdateItemDataAsync(itemId, cancellationToken);
        }

        // ============================================================================
        // 가격 차트 데이터
        // ============================================================================

        public async Task<PriceChartData> GetPriceLineChartAsync(string itemId, TimeRange timeRange, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("가격 라인 차트 데이터 조회: {ItemId}", itemId);

                var historyData = await _apiService.GetPriceHistoryAsync(itemId, timeRange.StartDate, timeRange.EndDate, cancellationToken);
                var pricePoints = historyData.Select(h => new ChartDataPoint
                {
                    Timestamp = h.Date,
                    Value = h.Price
                }).ToList();

                var result = new PriceChartData
                {
                    ItemId = itemId,
                    ItemName = await GetItemNameAsync(itemId),
                    PricePoints = pricePoints,
                    MinPrice = pricePoints.Count > 0 ? pricePoints.Min(p => p.Value) : 0,
                    MaxPrice = pricePoints.Count > 0 ? pricePoints.Max(p => p.Value) : 0,
                    AveragePrice = pricePoints.Count > 0 ? pricePoints.Average(p => p.Value) : 0,
                    TimeRange = timeRange
                };

                ChartDataUpdated?.Invoke(this, new ChartDataUpdatedEventArgs
                {
                    ItemId = itemId,
                    ChartType = ChartType.Line,
                    UpdatedAt = DateTime.UtcNow,
                    DataPointCount = pricePoints.Count
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 라인 차트 데이터 조회 실패: {ItemId}", itemId);
                throw;
            }
        }

        public async Task<CandlestickChartData> GetCandlestickChartAsync(string itemId, TimeRange timeRange, TimeInterval interval, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("캔들스틱 차트 데이터 조회: {ItemId}, {Interval}", itemId, interval);

                var historyData = await _apiService.GetPriceHistoryAsync(itemId, timeRange.StartDate, timeRange.EndDate, cancellationToken);
                var aggregatedData = await AggregateDataAsync(historyData, interval, AggregationType.OHLC);

                var candles = ConvertToOHLCData(aggregatedData.ToList(), interval);

                var result = new CandlestickChartData
                {
                    ItemId = itemId,
                    Candles = candles,
                    TimeRange = timeRange,
                    Interval = interval
                };

                ChartDataUpdated?.Invoke(this, new ChartDataUpdatedEventArgs
                {
                    ItemId = itemId,
                    ChartType = ChartType.Candlestick,
                    UpdatedAt = DateTime.UtcNow,
                    DataPointCount = candles.Count
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "캔들스틱 차트 데이터 조회 실패: {ItemId}", itemId);
                throw;
            }
        }

        public async Task<VolumeChartData> GetVolumeChartAsync(string itemId, TimeRange timeRange, CancellationToken cancellationToken = default)
        {
            try
            {
                var historyData = await _apiService.GetPriceHistoryAsync(itemId, timeRange.StartDate, timeRange.EndDate, cancellationToken);

                var volumePoints = historyData.Select(h => new VolumeDataPoint
                {
                    Timestamp = h.Date,
                    Volume = h.Volume,
                    WeightedPrice = h.Price
                }).ToList();

                var result = new VolumeChartData
                {
                    ItemId = itemId,
                    VolumePoints = volumePoints,
                    TotalVolume = volumePoints.Sum(v => v.Volume),
                    MaxVolume = volumePoints.Count > 0 ? volumePoints.Max(v => v.Volume) : 0,
                    TimeRange = timeRange
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "볼륨 차트 데이터 조회 실패: {ItemId}", itemId);
                throw;
            }
        }

        public async Task<HistogramChartData> GetPriceDistributionAsync(string itemId, TimeRange timeRange, CancellationToken cancellationToken = default)
        {
            try
            {
                var historyData = await _apiService.GetPriceHistoryAsync(itemId, timeRange.StartDate, timeRange.EndDate, cancellationToken);
                var prices = historyData.Select(h => h.Price).ToList();

                if (prices.Count == 0)
                {
                    return new HistogramChartData { ItemId = itemId };
                }

                var min = prices.Min();
                var max = prices.Max();
                var binCount = Math.Min(20, prices.Count / 5); // 최대 20개 구간
                var binWidth = (max - min) / binCount;

                var bins = new List<HistogramBin>();
                for (int i = 0; i < binCount; i++)
                {
                    var rangeStart = min + (i * binWidth);
                    var rangeEnd = min + ((i + 1) * binWidth);
                    var count = prices.Count(p => p >= rangeStart && (i == binCount - 1 ? p <= rangeEnd : p < rangeEnd));

                    bins.Add(new HistogramBin
                    {
                        RangeStart = rangeStart,
                        RangeEnd = rangeEnd,
                        Count = count,
                        Frequency = (double)count / prices.Count
                    });
                }

                var mean = prices.Average();
                var variance = prices.Select(p => Math.Pow((double)(p - mean), 2)).Average();
                var stdDev = (decimal)Math.Sqrt(variance);

                return new HistogramChartData
                {
                    ItemId = itemId,
                    Bins = bins,
                    TotalCount = prices.Count,
                    MeanValue = mean,
                    StandardDeviation = stdDev
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 분포 히스토그램 조회 실패: {ItemId}", itemId);
                throw;
            }
        }

        public async Task<MultiItemChartData> GetMultiItemComparisonAsync(List<string> itemIds, TimeRange timeRange, CancellationToken cancellationToken = default)
        {
            try
            {
                var itemSeries = new Dictionary<string, List<ChartDataPoint>>();

                foreach (var itemId in itemIds)
                {
                    var historyData = await _apiService.GetPriceHistoryAsync(itemId, timeRange.StartDate, timeRange.EndDate, cancellationToken);
                    var dataPoints = historyData.Select(h => new ChartDataPoint
                    {
                        Timestamp = h.Date,
                        Value = h.Price
                    }).ToList();

                    itemSeries[itemId] = dataPoints;
                }

                return new MultiItemChartData
                {
                    ItemIds = itemIds,
                    ItemSeries = itemSeries,
                    TimeRange = timeRange,
                    IsNormalized = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "다중 아이템 비교 차트 조회 실패");
                throw;
            }
        }

        // ============================================================================
        // 시장 분석 차트
        // ============================================================================

        public async Task<MarketPressureChartData> GetMarketPressureChartAsync(string itemId, TimeRange timeRange, CancellationToken cancellationToken = default)
        {
            try
            {
                var historyData = await _apiService.GetPriceHistoryAsync(itemId, timeRange.StartDate, timeRange.EndDate, cancellationToken);

                var pressurePoints = historyData.Select(h => new MarketPressurePoint
                {
                    Timestamp = h.Date,
                    DemandPressure = h.DemandPressure,
                    SupplyPressure = h.SupplyPressure,
                    Price = h.Price
                }).ToList();

                return new MarketPressureChartData
                {
                    ItemId = itemId,
                    PressurePoints = pressurePoints,
                    TimeRange = timeRange
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시장 압력 차트 조회 실패: {ItemId}", itemId);
                throw;
            }
        }

        public async Task<VolatilityChartData> GetVolatilityChartAsync(string itemId, TimeRange timeRange, CancellationToken cancellationToken = default)
        {
            try
            {
                var historyData = await _apiService.GetPriceHistoryAsync(itemId, timeRange.StartDate, timeRange.EndDate, cancellationToken);

                var volatilityPoints = new List<VolatilityPoint>();
                var window = 10; // 10개 데이터 포인트 기준 변동성 계산

                for (int i = window; i < historyData.Count; i++)
                {
                    var windowData = historyData.Skip(i - window).Take(window).ToList();
                    var returns = windowData.Zip(windowData.Skip(1), (current, next) =>
                        Math.Log((double)next.Price / (double)current.Price)).ToList();

                    var volatility = (decimal)Math.Sqrt(returns.Select(r => Math.Pow(r, 2)).Average());

                    volatilityPoints.Add(new VolatilityPoint
                    {
                        Timestamp = historyData[i].Date,
                        Volatility = volatility,
                        Price = historyData[i].Price
                    });
                }

                return new VolatilityChartData
                {
                    ItemId = itemId,
                    VolatilityPoints = volatilityPoints,
                    AverageVolatility = volatilityPoints.Count > 0 ? volatilityPoints.Average(v => v.Volatility) : 0,
                    TimeRange = timeRange
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "변동성 차트 조회 실패: {ItemId}", itemId);
                throw;
            }
        }

        public async Task<TradingVolumeChartData> GetTradingVolumeChartAsync(string itemId, TimeRange timeRange, CancellationToken cancellationToken = default)
        {
            try
            {
                var historyData = await _apiService.GetPriceHistoryAsync(itemId, timeRange.StartDate, timeRange.EndDate, cancellationToken);

                // 실제 구현에서는 매수/매도 볼륨을 구분해야 함 (현재는 임시 분할)
                var volumePoints = historyData.Select(h => new TradingVolumePoint
                {
                    Timestamp = h.Date,
                    BuyVolume = h.Volume / 2, // 임시: 절반은 매수
                    SellVolume = h.Volume / 2  // 임시: 절반은 매도
                }).ToList();

                // 간단한 선형 트렌드 라인 계산
                var trendLine = CalculateLinearTrend(volumePoints.Select(v => new ChartDataPoint
                {
                    Timestamp = v.Timestamp,
                    Value = v.TotalVolume
                }).ToList());

                return new TradingVolumeChartData
                {
                    ItemId = itemId,
                    VolumePoints = volumePoints,
                    TrendLine = trendLine,
                    TimeRange = timeRange
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "거래량 트렌드 차트 조회 실패: {ItemId}", itemId);
                throw;
            }
        }

        public async Task<PieChartData> GetCategoryMarketShareAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var categoryStats = await _apiService.GetCategoryStatsAsync(cancellationToken);
                var total = categoryStats.Sum(c => c.TotalValue);

                var slices = categoryStats.Select((c, index) => new PieSlice
                {
                    Label = c.Category,
                    Value = c.TotalValue,
                    Percentage = total > 0 ? (double)(c.TotalValue / total) * 100 : 0,
                    Color = GetCategoryColor(index)
                }).ToList();

                return new PieChartData
                {
                    Title = "카테고리별 시장 점유율",
                    Slices = slices,
                    Total = total
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "카테고리 시장 점유율 차트 조회 실패");
                throw;
            }
        }

        public async Task<HeatmapChartData> GetTradingActivityHeatmapAsync(TimeRange timeRange, CancellationToken cancellationToken = default)
        {
            try
            {
                // 실제 구현에서는 시간대별 거래 데이터를 조회해야 함
                // 현재는 샘플 데이터 생성
                var xLabels = Enumerable.Range(0, 24).Select(h => $"{h:D2}:00").ToList();
                var yLabels = new List<string> { "일", "월", "화", "수", "목", "금", "토" };

                var values = new decimal[7, 24];
                var random = new Random();
                var minValue = decimal.MaxValue;
                var maxValue = decimal.MinValue;

                for (int day = 0; day < 7; day++)
                {
                    for (int hour = 0; hour < 24; hour++)
                    {
                        // 샘플 데이터: 낮 시간대에 더 높은 활동
                        var baseActivity = hour >= 9 && hour <= 18 ? 100 : 30;
                        values[day, hour] = baseActivity + (decimal)(random.NextDouble() * 50);

                        minValue = Math.Min(minValue, values[day, hour]);
                        maxValue = Math.Max(maxValue, values[day, hour]);
                    }
                }

                return new HeatmapChartData
                {
                    Title = "시간대별 거래 활동",
                    XLabels = xLabels,
                    YLabels = yLabels,
                    Values = values,
                    MinValue = minValue,
                    MaxValue = maxValue,
                    TimeRange = timeRange
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "거래 활동 히트맵 조회 실패");
                throw;
            }
        }

        // ============================================================================
        // 예측 및 분석 차트
        // ============================================================================

        public async Task<PredictionChartData> GetPricePredictionChartAsync(string itemId, int forecastDays = 7, CancellationToken cancellationToken = default)
        {
            try
            {
                var prediction = await _apiService.GetPricePredictionAsync(itemId, forecastDays, cancellationToken);
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddDays(-30); // 최근 30일 히스토리

                var historyData = await _apiService.GetPriceHistoryAsync(itemId, startDate, endDate, cancellationToken);
                var historicalPoints = historyData.Select(h => new ChartDataPoint
                {
                    Timestamp = h.Date,
                    Value = h.Price
                }).ToList();

                var predictionPoints = prediction.Predictions.Select(p => new PredictionPoint
                {
                    Timestamp = p.Date,
                    PredictedValue = p.Price,
                    LowerBound = p.LowerBound,
                    UpperBound = p.UpperBound,
                    Confidence = prediction.Confidence
                }).ToList();

                return new PredictionChartData
                {
                    ItemId = itemId,
                    HistoricalData = historicalPoints,
                    PredictionData = predictionPoints,
                    ConfidenceLevel = prediction.Confidence,
                    Model = prediction.Model
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 예측 차트 조회 실패: {ItemId}", itemId);
                throw;
            }
        }

        public async Task<MovingAverageChartData> GetMovingAverageChartAsync(string itemId, TimeRange timeRange, List<int> periods, CancellationToken cancellationToken = default)
        {
            try
            {
                var historyData = await _apiService.GetPriceHistoryAsync(itemId, timeRange.StartDate, timeRange.EndDate, cancellationToken);
                var priceData = historyData.Select(h => new ChartDataPoint
                {
                    Timestamp = h.Date,
                    Value = h.Price
                }).ToList();

                var movingAverages = new Dictionary<int, List<ChartDataPoint>>();

                foreach (var period in periods)
                {
                    var maData = CalculateMovingAverage(priceData, period);
                    movingAverages[period] = maData;
                }

                return new MovingAverageChartData
                {
                    ItemId = itemId,
                    PriceData = priceData,
                    MovingAverages = movingAverages,
                    TimeRange = timeRange
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "이동평균선 차트 조회 실패: {ItemId}", itemId);
                throw;
            }
        }

        public async Task<CorrelationMatrixData> GetItemCorrelationMatrixAsync(List<string> itemIds, TimeRange timeRange, CancellationToken cancellationToken = default)
        {
            try
            {
                var priceDataMatrix = new Dictionary<string, List<decimal>>();

                // 각 아이템의 가격 데이터 수집
                foreach (var itemId in itemIds)
                {
                    var historyData = await _apiService.GetPriceHistoryAsync(itemId, timeRange.StartDate, timeRange.EndDate, cancellationToken);
                    priceDataMatrix[itemId] = historyData.Select(h => h.Price).ToList();
                }

                // 상관관계 매트릭스 계산
                var correlationMatrix = CalculateCorrelationMatrix(priceDataMatrix, itemIds);

                return new CorrelationMatrixData
                {
                    ItemIds = itemIds,
                    CorrelationMatrix = correlationMatrix,
                    TimeRange = timeRange
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "상관관계 매트릭스 조회 실패");
                throw;
            }
        }

        // ============================================================================
        // 차트 설정 및 유틸리티
        // ============================================================================

        public List<ChartColor> GetColorPalette(ChartColorScheme scheme = ChartColorScheme.Default)
        {
            return scheme switch
            {
                ChartColorScheme.Financial => GetFinancialColorPalette(),
                ChartColorScheme.Categorical => GetCategoricalColorPalette(),
                ChartColorScheme.Sequential => GetSequentialColorPalette(),
                ChartColorScheme.Diverging => GetDivergingColorPalette(),
                _ => GetDefaultColorPalette()
            };
        }

        public async Task<ChartDataPoints> AggregateDataAsync(List<PriceHistoryResponse> rawData, TimeInterval interval, AggregationType aggregationType)
        {
            var groupedData = GroupDataByInterval(rawData, interval);
            var aggregatedPoints = new List<ChartDataPoint>();

            foreach (var group in groupedData)
            {
                var aggregatedValue = aggregationType switch
                {
                    AggregationType.Average => group.Value.Average(d => d.Price),
                    AggregationType.Sum => group.Value.Sum(d => d.Price),
                    AggregationType.Count => group.Value.Count,
                    AggregationType.WeightedAverage => CalculateWeightedAverage(group.Value),
                    AggregationType.OHLC => group.Value.First().Price, // OHLC는 별도 처리 필요
                    _ => group.Value.Average(d => d.Price)
                };

                aggregatedPoints.Add(new ChartDataPoint
                {
                    Timestamp = group.Key,
                    Value = aggregatedValue
                });
            }

            return new ChartDataPoints
            {
                ItemId = rawData.FirstOrDefault()?.ToString() ?? string.Empty,
                LastUpdated = DateTime.UtcNow
            };
        }

        public async Task<bool> ExportChartDataAsync(string itemId, TimeRange timeRange, ExportFormat format, string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var chartData = await GetPriceLineChartAsync(itemId, timeRange, cancellationToken);

                return format switch
                {
                    ExportFormat.CSV => await ExportToCsvAsync(chartData, filePath),
                    ExportFormat.JSON => await ExportToJsonAsync(chartData, filePath),
                    ExportFormat.Excel => await ExportToExcelAsync(chartData, filePath),
                    ExportFormat.PDF => await ExportToPdfAsync(chartData, filePath),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "차트 데이터 내보내기 실패: {ItemId}, {Format}", itemId, format);
                return false;
            }
        }

        public async Task<bool> SaveChartImageAsync(ChartType chartType, string itemId, string filePath, ChartImageSettings settings, CancellationToken cancellationToken = default)
        {
            try
            {
                // 실제 구현에서는 차트 라이브러리를 사용하여 이미지 생성
                _logger.LogWarning("차트 이미지 저장 기능은 아직 구현되지 않음");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "차트 이미지 저장 실패: {ChartType}, {ItemId}", chartType, itemId);
                return false;
            }
        }

        // ============================================================================
        // 실시간 데이터 스트리밍
        // ============================================================================

        public async Task StartRealTimeStreamAsync(string itemId, CancellationToken cancellationToken = default)
        {
            if (!_dataBuffers.ContainsKey(itemId))
            {
                await StartMonitoringAsync(itemId, cancellationToken);
            }

            _logger.LogInformation("실시간 스트림 시작: {ItemId}", itemId);
        }

        public async Task StopRealTimeStreamAsync(string itemId)
        {
            await StopMonitoringAsync(itemId);
            _logger.LogInformation("실시간 스트림 중지: {ItemId}", itemId);
        }

        public ChartDataBuffer GetRealTimeBuffer(string itemId)
        {
            _dataBuffers.TryGetValue(itemId, out var buffer);
            return buffer ?? new ChartDataBuffer { ItemId = itemId };
        }

        public void SetBufferSize(int maxDataPoints)
        {
            _maxBufferSize = maxDataPoints;
            foreach (var buffer in _dataBuffers.Values)
            {
                buffer.MaxSize = maxDataPoints;
            }
            _logger.LogInformation("버퍼 크기 변경: {Size}", maxDataPoints);
        }

        // ============================================================================
        // 내부 헬퍼 메서드들
        // ============================================================================

        private async void GlobalUpdateCallback(object? state)
        {
            if (!_isRealTimeUpdateEnabled) return;

            var monitoredItems = MonitoredItems.ToList();
            var updateTasks = monitoredItems.Select(itemId => UpdateItemDataAsync(itemId, CancellationToken.None));

            try
            {
                await Task.WhenAll(updateTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "글로벌 업데이트 실행 중 오류 발생");
            }
        }

        private async Task UpdateItemDataAsync(string itemId, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            try
            {
                var priceData = await _apiService.GetItemPriceAsync(itemId, cancellationToken);

                if (_dataBuffers.TryGetValue(itemId, out var buffer))
                {
                    var dataPoint = new ChartDataPoint
                    {
                        Timestamp = DateTime.UtcNow,
                        Value = priceData.CurrentPrice
                    };

                    buffer.AddDataPoint(dataPoint);

                    // 실시간 가격 이벤트 발생
                    RealTimePriceReceived?.Invoke(this, new RealTimePriceEventArgs
                    {
                        ItemId = itemId,
                        Price = priceData.CurrentPrice,
                        Timestamp = DateTime.UtcNow,
                        Volume = priceData.TotalVolume24h,
                        Change = priceData.CurrentPrice - priceData.PreviousPrice,
                        ChangePercent = priceData.ChangePercent
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // 정상적인 취소
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "아이템 데이터 업데이트 실패: {ItemId}", itemId);
            }
        }

        private async Task<string> GetItemNameAsync(string itemId)
        {
            try
            {
                // 실제 구현에서는 아이템 정보 API 호출
                return itemId.Split(':').LastOrDefault() ?? itemId;
            }
            catch
            {
                return itemId;
            }
        }

        private List<CandlestickDataPoint> ConvertToOHLCData(List<ChartDataPoint> data, TimeInterval interval)
        {
            var groupedData = GroupDataPointsByInterval(data, interval);
            return groupedData.Select(group => new CandlestickDataPoint
            {
                Timestamp = group.Key,
                Open = group.Value.First().Value,
                High = group.Value.Max(d => d.Value),
                Low = group.Value.Min(d => d.Value),
                Close = group.Value.Last().Value,
                Volume = group.Value.Count // 임시: 실제로는 볼륨 데이터 필요
            }).ToList();
        }

        private List<ChartDataPoint> CalculateMovingAverage(List<ChartDataPoint> data, int period)
        {
            var result = new List<ChartDataPoint>();

            for (int i = period - 1; i < data.Count; i++)
            {
                var window = data.Skip(i - period + 1).Take(period);
                var average = window.Average(d => d.Value);

                result.Add(new ChartDataPoint
                {
                    Timestamp = data[i].Timestamp,
                    Value = average
                });
            }

            return result;
        }

        private List<ChartDataPoint> CalculateLinearTrend(List<ChartDataPoint> data)
        {
            if (data.Count < 2) return new List<ChartDataPoint>();

            // 간단한 선형 회귀
            var n = data.Count;
            var sumX = 0.0;
            var sumY = 0.0;
            var sumXY = 0.0;
            var sumX2 = 0.0;

            for (int i = 0; i < n; i++)
            {
                var x = i;
                var y = (double)data[i].Value;
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            var intercept = (sumY - slope * sumX) / n;

            return data.Select((d, i) => new ChartDataPoint
            {
                Timestamp = d.Timestamp,
                Value = (decimal)(slope * i + intercept)
            }).ToList();
        }

        private double[,] CalculateCorrelationMatrix(Dictionary<string, List<decimal>> priceData, List<string> itemIds)
        {
            var n = itemIds.Count;
            var matrix = new double[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j)
                    {
                        matrix[i, j] = 1.0;
                    }
                    else
                    {
                        var correlation = CalculateCorrelation(
                            priceData[itemIds[i]].Select(p => (double)p).ToList(),
                            priceData[itemIds[j]].Select(p => (double)p).ToList()
                        );
                        matrix[i, j] = correlation;
                    }
                }
            }

            return matrix;
        }

        private double CalculateCorrelation(List<double> x, List<double> y)
        {
            var n = Math.Min(x.Count, y.Count);
            if (n < 2) return 0.0;

            var meanX = x.Take(n).Average();
            var meanY = y.Take(n).Average();

            var numerator = x.Take(n).Zip(y.Take(n), (xi, yi) => (xi - meanX) * (yi - meanY)).Sum();
            var denomX = Math.Sqrt(x.Take(n).Select(xi => Math.Pow(xi - meanX, 2)).Sum());
            var denomY = Math.Sqrt(y.Take(n).Select(yi => Math.Pow(yi - meanY, 2)).Sum());

            return denomX * denomY != 0 ? numerator / (denomX * denomY) : 0.0;
        }

        private Dictionary<DateTime, List<PriceHistoryResponse>> GroupDataByInterval(List<PriceHistoryResponse> data, TimeInterval interval)
        {
            return interval switch
            {
                TimeInterval.Minute1 => data.GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, d.Date.Day, d.Date.Hour, d.Date.Minute, 0)).ToDictionary(g => g.Key, g => g.ToList()),
                TimeInterval.Hour1 => data.GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, d.Date.Day, d.Date.Hour, 0, 0)).ToDictionary(g => g.Key, g => g.ToList()),
                TimeInterval.Day1 => data.GroupBy(d => d.Date.Date).ToDictionary(g => g.Key, g => g.ToList()),
                _ => data.GroupBy(d => d.Date.Date).ToDictionary(g => g.Key, g => g.ToList())
            };
        }

        private Dictionary<DateTime, List<ChartDataPoint>> GroupDataPointsByInterval(List<ChartDataPoint> data, TimeInterval interval)
        {
            return interval switch
            {
                TimeInterval.Minute1 => data.GroupBy(d => new DateTime(d.Timestamp.Year, d.Timestamp.Month, d.Timestamp.Day, d.Timestamp.Hour, d.Timestamp.Minute, 0)).ToDictionary(g => g.Key, g => g.ToList()),
                TimeInterval.Hour1 => data.GroupBy(d => new DateTime(d.Timestamp.Year, d.Timestamp.Month, d.Timestamp.Day, d.Timestamp.Hour, 0, 0)).ToDictionary(g => g.Key, g => g.ToList()),
                TimeInterval.Day1 => data.GroupBy(d => d.Timestamp.Date).ToDictionary(g => g.Key, g => g.ToList()),
                _ => data.GroupBy(d => d.Timestamp.Date).ToDictionary(g => g.Key, g => g.ToList())
            };
        }

        private decimal CalculateWeightedAverage(List<PriceHistoryResponse> data)
        {
            var totalVolume = data.Sum(d => d.Volume);
            return totalVolume > 0 ? data.Sum(d => d.Price * d.Volume) / totalVolume : data.Average(d => d.Price);
        }

        private string GetCategoryColor(int index)
        {
            var colors = new[] { "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7", "#DDA0DD", "#F0A3FF", "#0075DC" };
            return colors[index % colors.Length];
        }

        // 색상 팔레트 메서드들
        private List<ChartColor> GetDefaultColorPalette() => new()
        {
            new ChartColor { Name = "Blue", HexCode = "#007ACC", R = 0, G = 122, B = 204 },
            new ChartColor { Name = "Green", HexCode = "#28A745", R = 40, G = 167, B = 69 },
            new ChartColor { Name = "Red", HexCode = "#DC3545", R = 220, G = 53, B = 69 },
            new ChartColor { Name = "Orange", HexCode = "#FD7E14", R = 253, G = 126, B = 20 },
            new ChartColor { Name = "Purple", HexCode = "#6F42C1", R = 111, G = 66, B = 193 }
        };

        private List<ChartColor> GetFinancialColorPalette() => new()
        {
            new ChartColor { Name = "Bull Green", HexCode = "#26A69A", R = 38, G = 166, B = 154 },
            new ChartColor { Name = "Bear Red", HexCode = "#EF5350", R = 239, G = 83, B = 80 },
            new ChartColor { Name = "Neutral Gray", HexCode = "#78909C", R = 120, G = 144, B = 156 }
        };

        private List<ChartColor> GetCategoricalColorPalette() => new()
        {
            new ChartColor { Name = "Category 1", HexCode = "#1F77B4", R = 31, G = 119, B = 180 },
            new ChartColor { Name = "Category 2", HexCode = "#FF7F0E", R = 255, G = 127, B = 14 },
            new ChartColor { Name = "Category 3", HexCode = "#2CA02C", R = 44, G = 160, B = 44 },
            new ChartColor { Name = "Category 4", HexCode = "#D62728", R = 214, G = 39, B = 40 },
            new ChartColor { Name = "Category 5", HexCode = "#9467BD", R = 148, G = 103, B = 189 }
        };

        private List<ChartColor> GetSequentialColorPalette() => new()
        {
            new ChartColor { Name = "Light", HexCode = "#E8F4FD", R = 232, G = 244, B = 253 },
            new ChartColor { Name = "Medium Light", HexCode = "#B3D9F2", R = 179, G = 217, B = 242 },
            new ChartColor { Name = "Medium", HexCode = "#7BBDE6", R = 123, G = 189, B = 230 },
            new ChartColor { Name = "Medium Dark", HexCode = "#4A90E2", R = 74, G = 144, B = 226 },
            new ChartColor { Name = "Dark", HexCode = "#2E5C8A", R = 46, G = 92, B = 138 }
        };

        private List<ChartColor> GetDivergingColorPalette() => new()
        {
            new ChartColor { Name = "Low", HexCode = "#D73027", R = 215, G = 48, B = 39 },
            new ChartColor { Name = "Medium Low", HexCode = "#FC8D59", R = 252, G = 141, B = 89 },
            new ChartColor { Name = "Neutral", HexCode = "#FFFFBF", R = 255, G = 255, B = 191 },
            new ChartColor { Name = "Medium High", HexCode = "#91BFDB", R = 145, G = 191, B = 219 },
            new ChartColor { Name = "High", HexCode = "#4575B4", R = 69, G = 117, B = 180 }
        };

        // 내보내기 메서드들 (간단한 구현)
        private async Task<bool> ExportToCsvAsync(PriceChartData chartData, string filePath)
        {
            try
            {
                var lines = new List<string> { "Timestamp,Price" };
                lines.AddRange(chartData.PricePoints.Select(p => $"{p.Timestamp:yyyy-MM-dd HH:mm:ss},{p.Value}"));
                await File.WriteAllLinesAsync(filePath, lines);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ExportToJsonAsync(PriceChartData chartData, string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(chartData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ExportToExcelAsync(PriceChartData chartData, string filePath)
        {
            _logger.LogWarning("Excel 내보내기는 아직 구현되지 않음");
            return false;
        }

        private async Task<bool> ExportToPdfAsync(PriceChartData chartData, string filePath)
        {
            _logger.LogWarning("PDF 내보내기는 아직 구현되지 않음");
            return false;
        }

        // ============================================================================
        // IDisposable 구현
        // ============================================================================

        public void Dispose()
        {
            _globalUpdateTimer?.Dispose();

            foreach (var timer in _monitoringTimers.Values)
            {
                timer?.Dispose();
            }
            _monitoringTimers.Clear();

            foreach (var cancellation in _monitoringCancellations.Values)
            {
                cancellation?.Cancel();
                cancellation?.Dispose();
            }
            _monitoringCancellations.Clear();

            _logger.LogInformation("ChartService 리소스 정리 완료");
        }
    }
}