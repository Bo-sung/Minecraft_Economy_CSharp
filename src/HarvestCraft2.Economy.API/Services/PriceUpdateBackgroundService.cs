using HarvestCraft2.Economy.API.Services.Interfaces;

namespace HarvestCraft2.Economy.API.Services
{
    /// <summary>
    /// 10분 주기로 모든 아이템의 가격을 자동 업데이트하는 백그라운드 서비스
    /// </summary>
    public class PriceUpdateBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PriceUpdateBackgroundService> _logger;
        private readonly IConfiguration _configuration;

        // 시스템 상수
        private readonly TimeSpan _updateInterval;
        private readonly int _maxRetryAttempts;
        private readonly TimeSpan _retryDelay;

        public PriceUpdateBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<PriceUpdateBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;

            // 설정에서 업데이트 간격 읽기 (기본값: 10분)
            var intervalMinutes = _configuration.GetValue<int>("RedisConfig:PriceUpdateIntervalMinutes", 10);
            _updateInterval = TimeSpan.FromMinutes(intervalMinutes);

            _maxRetryAttempts = _configuration.GetValue<int>("PriceUpdate:MaxRetryAttempts", 3);
            _retryDelay = TimeSpan.FromSeconds(_configuration.GetValue<int>("PriceUpdate:RetryDelaySeconds", 30));

            _logger.LogInformation("가격 업데이트 서비스 초기화: 간격 {Interval}분, 최대 재시도 {MaxRetry}회",
                intervalMinutes, _maxRetryAttempts);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("가격 업데이트 백그라운드 서비스 시작");

            // 서비스 시작 시 1분 대기 (다른 서비스들이 초기화될 시간 확보)
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformPriceUpdateCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("가격 업데이트 서비스 종료 요청됨");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "가격 업데이트 사이클 중 치명적 오류 발생");

                    // 치명적 오류 시 5분 대기 후 재시도
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }

                // 다음 업데이트까지 대기
                await Task.Delay(_updateInterval, stoppingToken);
            }

            _logger.LogInformation("가격 업데이트 백그라운드 서비스 종료");
        }

        /// <summary>
        /// 하나의 가격 업데이트 사이클을 수행합니다.
        /// </summary>
        private async Task PerformPriceUpdateCycleAsync(CancellationToken cancellationToken)
        {
            var cycleStart = DateTime.UtcNow;
            var cycleId = Guid.NewGuid().ToString("N")[..8];

            _logger.LogInformation("가격 업데이트 사이클 시작: {CycleId}", cycleId);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var priceService = scope.ServiceProvider.GetRequiredService<IPriceService>();
                var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();

                // 1. 시스템 상태 확인
                await ValidateSystemHealthAsync(redisService, cancellationToken);

                // 2. 가격 업데이트 수행 (재시도 로직 포함)
                var updateResult = await ExecuteWithRetryAsync(
                    () => priceService.UpdateAllPricesAsync(),
                    _maxRetryAttempts,
                    _retryDelay,
                    cancellationToken);

                // 3. 업데이트 결과 로깅
                var cycleDuration = DateTime.UtcNow - cycleStart;

                if (updateResult > 0)
                {
                    _logger.LogInformation("가격 업데이트 사이클 완료: {CycleId} - {UpdatedCount}개 아이템, {Duration}ms",
                        cycleId, updateResult, cycleDuration.TotalMilliseconds);

                    // 4. 성공 통계 기록
                    await RecordUpdateStatisticsAsync(redisService, updateResult, cycleDuration, true);
                }
                else
                {
                    _logger.LogWarning("가격 업데이트 사이클 완료 (업데이트 없음): {CycleId} - {Duration}ms",
                        cycleId, cycleDuration.TotalMilliseconds);

                    await RecordUpdateStatisticsAsync(redisService, 0, cycleDuration, false);
                }

                // 5. 선택적 정리 작업
                await PerformMaintenanceTasksAsync(redisService, cancellationToken);
            }
            catch (Exception ex)
            {
                var cycleDuration = DateTime.UtcNow - cycleStart;
                _logger.LogError(ex, "가격 업데이트 사이클 실패: {CycleId} - {Duration}ms",
                    cycleId, cycleDuration.TotalMilliseconds);

                // 실패 통계 기록
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();
                    await RecordUpdateStatisticsAsync(redisService, 0, cycleDuration, false);
                }
                catch (Exception statEx)
                {
                    _logger.LogError(statEx, "실패 통계 기록 중 오류");
                }
            }
        }

        /// <summary>
        /// 시스템 건강성을 검증합니다.
        /// </summary>
        private async Task ValidateSystemHealthAsync(IRedisService redisService, CancellationToken cancellationToken)
        {
            // Redis 연결 상태 확인
            var isRedisConnected = await redisService.IsConnectedAsync();
            if (!isRedisConnected)
            {
                throw new InvalidOperationException("Redis 연결이 끊어져 있어 가격 업데이트를 수행할 수 없습니다.");
            }

            // 온라인 플레이어 수 확인 (선택적)
            var onlinePlayerCount = await redisService.GetOnlinePlayerCountAsync();
            _logger.LogDebug("현재 온라인 플레이어: {Count}명", onlinePlayerCount);

            cancellationToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// 재시도 로직과 함께 작업을 실행합니다.
        /// </summary>
        private async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxRetries,
            TimeSpan retryDelay,
            CancellationToken cancellationToken)
        {
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await operation();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "가격 업데이트 시도 {Attempt}/{MaxRetries} 실패",
                        attempt, maxRetries);

                    if (attempt == maxRetries)
                    {
                        break;
                    }

                    await Task.Delay(retryDelay, cancellationToken);
                }
            }

            throw new InvalidOperationException(
                $"가격 업데이트가 {maxRetries}회 시도 후 실패했습니다.", lastException);
        }

        /// <summary>
        /// 업데이트 통계를 기록합니다.
        /// </summary>
        private async Task RecordUpdateStatisticsAsync(
            IRedisService redisService,
            int updatedCount,
            TimeSpan duration,
            bool success)
        {
            try
            {
                var statsKey = $"update_stats:{DateTime.UtcNow:yyyyMMdd}";
                var statsData = new
                {
                    timestamp = DateTime.UtcNow.ToString("O"),
                    updated_count = updatedCount,
                    duration_ms = duration.TotalMilliseconds,
                    success = success,
                    cycle_number = await GetTodaysCycleNumberAsync(redisService)
                };

                await redisService.SetSystemStatsAsync(statsKey, statsData);

                // 최근 업데이트 정보도 별도 저장 (대시보드용)
                await redisService.SetSystemStatsAsync("last_price_update", new
                {
                    timestamp = DateTime.UtcNow.ToString("O"),
                    updated_items = updatedCount,
                    duration_seconds = duration.TotalSeconds,
                    success = success
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "업데이트 통계 기록 중 오류");
            }
        }

        /// <summary>
        /// 오늘의 사이클 번호를 가져옵니다.
        /// </summary>
        private async Task<int> GetTodaysCycleNumberAsync(IRedisService redisService)
        {
            try
            {
                var today = DateTime.UtcNow.ToString("yyyyMMdd");
                var cycleKey = $"cycle_count:{today}";

                var cycleCountStr = await redisService.GetStringAsync(cycleKey);

                if (int.TryParse(cycleCountStr, out int currentCount))
                {
                    await redisService.SetStringAsync(cycleKey, (currentCount + 1).ToString(), TimeSpan.FromDays(2));
                    return currentCount + 1;
                }
                else
                {
                    await redisService.SetStringAsync(cycleKey, "1", TimeSpan.FromDays(2));
                    return 1;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "사이클 번호 조회/업데이트 중 오류");
                return 0;
            }
        }

        /// <summary>
        /// 주기적 유지보수 작업을 수행합니다.
        /// </summary>
        private async Task PerformMaintenanceTasksAsync(IRedisService redisService, CancellationToken cancellationToken)
        {
            try
            {
                // 매 시간의 첫 번째 사이클에서만 정리 작업 수행
                var currentTime = DateTime.UtcNow;
                if (currentTime.Minute < 10) // 0-9분 사이
                {
                    _logger.LogDebug("시간당 유지보수 작업 시작");

                    // 오래된 거래 데이터 정리
                    var deletedKeys = await redisService.CleanupOldTradeDataAsync();

                    if (deletedKeys > 0)
                    {
                        _logger.LogInformation("유지보수 완료: {DeletedKeys}개 오래된 키 삭제", deletedKeys);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "유지보수 작업 중 오류 (무시하고 계속)");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("가격 업데이트 서비스 정지 중...");

            try
            {
                // 현재 진행 중인 사이클 완료 대기 (최대 30초)
                await base.StopAsync(cancellationToken);

                _logger.LogInformation("가격 업데이트 서비스 정상 종료");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "가격 업데이트 서비스 종료 중 오류");
            }
        }
    }

    /// <summary>
    /// 가격 업데이트 결과 정보
    /// </summary>
    public class PriceUpdateResult
    {
        public int UpdatedItemCount { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }
}