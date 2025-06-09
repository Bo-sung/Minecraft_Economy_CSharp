using HarvestCraft2.Economy.API.Services.Interfaces;
using HarvestCraft2.Economy.API.Data;
using Microsoft.EntityFrameworkCore;

namespace HarvestCraft2.Economy.API.Services
{
    /// <summary>
    /// 오래된 데이터 정리 및 시스템 최적화를 담당하는 백그라운드 서비스
    /// </summary>
    public class DataCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DataCleanupBackgroundService> _logger;
        private readonly IConfiguration _configuration;

        // 설정값들
        private readonly TimeSpan _cleanupInterval;
        private readonly int _transactionRetentionDays;
        private readonly int _priceHistoryRetentionDays;
        private readonly int _sessionRetentionDays;

        public DataCleanupBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<DataCleanupBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;

            // 설정값 로드
            var cleanupHours = _configuration.GetValue<int>("DataCleanup:IntervalHours", 6);
            _cleanupInterval = TimeSpan.FromHours(cleanupHours);

            _transactionRetentionDays = _configuration.GetValue<int>("DataCleanup:TransactionRetentionDays", 90);
            _priceHistoryRetentionDays = _configuration.GetValue<int>("DataCleanup:PriceHistoryRetentionDays", 30);
            _sessionRetentionDays = _configuration.GetValue<int>("DataCleanup:SessionRetentionDays", 7);

            _logger.LogInformation("데이터 정리 서비스 초기화: 간격 {Interval}시간, " +
                "거래데이터 {TransactionDays}일, 가격히스토리 {PriceDays}일, 세션 {SessionDays}일 보관",
                cleanupHours, _transactionRetentionDays, _priceHistoryRetentionDays, _sessionRetentionDays);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("데이터 정리 백그라운드 서비스 시작");

            // 서비스 시작 시 5분 대기 (시스템 안정화)
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformCleanupCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("데이터 정리 서비스 종료 요청됨");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "데이터 정리 사이클 중 치명적 오류 발생");

                    // 오류 시 1시간 대기 후 재시도
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }

                // 다음 정리까지 대기
                await Task.Delay(_cleanupInterval, stoppingToken);
            }

            _logger.LogInformation("데이터 정리 백그라운드 서비스 종료");
        }

        /// <summary>
        /// 하나의 데이터 정리 사이클을 수행합니다.
        /// </summary>
        private async Task PerformCleanupCycleAsync(CancellationToken cancellationToken)
        {
            var cycleStart = DateTime.UtcNow;
            var cycleId = Guid.NewGuid().ToString("N")[..8];

            _logger.LogInformation("데이터 정리 사이클 시작: {CycleId}", cycleId);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();

                var cleanupResults = new CleanupResults();

                // 1. MySQL 데이터 정리
                await CleanupDatabaseAsync(dbContext, cleanupResults, cancellationToken);

                // 2. Redis 캐시 정리
                await CleanupRedisAsync(redisService, cleanupResults, cancellationToken);

                // 3. 시스템 최적화
                await OptimizeSystemAsync(dbContext, redisService, cleanupResults, cancellationToken);

                // 4. 정리 결과 로깅 및 통계 기록
                var cycleDuration = DateTime.UtcNow - cycleStart;

                _logger.LogInformation("데이터 정리 사이클 완료: {CycleId} - {Duration}ms\n" +
                    "거래데이터: {TransactionCount}개 삭제\n" +
                    "가격히스토리: {PriceHistoryCount}개 삭제\n" +
                    "세션데이터: {SessionCount}개 삭제\n" +
                    "Redis 키: {RedisKeyCount}개 삭제",
                    cycleId, cycleDuration.TotalMilliseconds,
                    cleanupResults.DeletedTransactions,
                    cleanupResults.DeletedPriceHistories,
                    cleanupResults.DeletedSessions,
                    cleanupResults.DeletedRedisKeys);

                await RecordCleanupStatisticsAsync(redisService, cleanupResults, cycleDuration);
            }
            catch (Exception ex)
            {
                var cycleDuration = DateTime.UtcNow - cycleStart;
                _logger.LogError(ex, "데이터 정리 사이클 실패: {CycleId} - {Duration}ms",
                    cycleId, cycleDuration.TotalMilliseconds);
            }
        }

        /// <summary>
        /// 데이터베이스 정리를 수행합니다.
        /// </summary>
        private async Task CleanupDatabaseAsync(
            ApplicationDbContext dbContext,
            CleanupResults results,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("데이터베이스 정리 시작");

            try
            {
                // 1. 오래된 거래 데이터 삭제
                var transactionCutoff = DateTime.UtcNow.AddDays(-_transactionRetentionDays);
                var oldTransactions = await dbContext.ShopTransactions
                    .Where(t => t.CreatedAt < transactionCutoff)
                    .CountAsync(cancellationToken);

                if (oldTransactions > 0)
                {
                    // 배치 단위로 삭제 (성능 최적화)
                    const int batchSize = 1000;
                    int deletedCount = 0;

                    while (deletedCount < oldTransactions && !cancellationToken.IsCancellationRequested)
                    {
                        var transactionsToDelete = await dbContext.ShopTransactions
                            .Where(t => t.CreatedAt < transactionCutoff)
                            .Take(batchSize)
                            .ToListAsync(cancellationToken);

                        if (!transactionsToDelete.Any()) break;

                        dbContext.ShopTransactions.RemoveRange(transactionsToDelete);
                        await dbContext.SaveChangesAsync(cancellationToken);

                        deletedCount += transactionsToDelete.Count;

                        _logger.LogDebug("거래 데이터 배치 삭제: {Deleted}/{Total}",
                            deletedCount, oldTransactions);

                        // CPU 부하 방지를 위한 짧은 대기
                        await Task.Delay(100, cancellationToken);
                    }

                    results.DeletedTransactions = deletedCount;
                    _logger.LogInformation("오래된 거래 데이터 {Count}개 삭제 완료", deletedCount);
                }

                // 2. 오래된 가격 히스토리 삭제
                var priceHistoryCutoff = DateTime.UtcNow.AddDays(-_priceHistoryRetentionDays);
                var oldPriceHistories = await dbContext.PriceHistories
                    .Where(p => p.PriceTimestamp < priceHistoryCutoff)
                    .CountAsync(cancellationToken);

                if (oldPriceHistories > 0)
                {
                    // 중요한 데이터는 샘플링해서 보관 (매일 자정 데이터만)
                    var samplesToKeep = await dbContext.PriceHistories
                        .Where(p => p.PriceTimestamp < priceHistoryCutoff)
                        .Where(p => p.PriceTimestamp.Hour == 0 && p.PriceTimestamp.Minute == 0)
                        .Select(p => p.Id)
                        .ToListAsync(cancellationToken);

                    var historiesToDelete = await dbContext.PriceHistories
                        .Where(p => p.PriceTimestamp < priceHistoryCutoff)
                        .Where(p => !samplesToKeep.Contains(p.Id))
                        .Take(1000)
                        .ToListAsync(cancellationToken);

                    if (historiesToDelete.Any())
                    {
                        dbContext.PriceHistories.RemoveRange(historiesToDelete);
                        await dbContext.SaveChangesAsync(cancellationToken);

                        results.DeletedPriceHistories = historiesToDelete.Count;
                        _logger.LogInformation("오래된 가격 히스토리 {Count}개 삭제 완료 (샘플 {Samples}개 보관)",
                            historiesToDelete.Count, samplesToKeep.Count);
                    }
                }

                // 3. 오래된 플레이어 세션 삭제
                var sessionCutoff = DateTime.UtcNow.AddDays(-_sessionRetentionDays);
                var oldSessions = await dbContext.PlayerSessions
                    .Where(s => s.LastActivity < sessionCutoff)
                    .ToListAsync(cancellationToken);

                if (oldSessions.Any())
                {
                    dbContext.PlayerSessions.RemoveRange(oldSessions);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    results.DeletedSessions = oldSessions.Count;
                    _logger.LogInformation("오래된 플레이어 세션 {Count}개 삭제 완료", oldSessions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "데이터베이스 정리 중 오류 발생");
                throw;
            }
        }

        /// <summary>
        /// Redis 캐시 정리를 수행합니다.
        /// </summary>
        private async Task CleanupRedisAsync(
            IRedisService redisService,
            CleanupResults results,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Redis 캐시 정리 시작");

            try
            {
                // 1. 오래된 거래량 데이터 정리
                var deletedTradeKeys = await redisService.CleanupOldTradeDataAsync();
                results.DeletedRedisKeys += deletedTradeKeys;

                // 2. 만료된 세션 정리
                var onlinePlayers = await redisService.GetOnlinePlayersAsync();
                var sessionCleanupCount = 0;

                foreach (var playerId in onlinePlayers)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var sessionStart = await redisService.GetPlayerSessionStartAsync(playerId);
                        if (sessionStart.HasValue)
                        {
                            var sessionAge = DateTime.UtcNow - sessionStart.Value;

                            // 24시간 이상 된 세션은 정리
                            if (sessionAge.TotalHours > 24)
                            {
                                await redisService.SetPlayerOfflineAsync(playerId);
                                sessionCleanupCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "플레이어 세션 정리 중 오류: {PlayerId}", playerId);
                    }
                }

                if (sessionCleanupCount > 0)
                {
                    _logger.LogInformation("만료된 Redis 세션 {Count}개 정리 완료", sessionCleanupCount);
                }

                // 3. 통계 데이터 정리 (30일 이상 된 데이터)
                // 실제 구현에서는 패턴 기반으로 오래된 통계 키들을 찾아 삭제

                _logger.LogInformation("Redis 캐시 정리 완료: {TotalKeys}개 키 삭제",
                    results.DeletedRedisKeys);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis 캐시 정리 중 오류 발생");
                throw;
            }
        }

        /// <summary>
        /// 시스템 최적화 작업을 수행합니다.
        /// </summary>
        private async Task OptimizeSystemAsync(
            ApplicationDbContext dbContext,
            IRedisService redisService,
            CleanupResults results,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("시스템 최적화 시작");

            try
            {
                // 1. 데이터베이스 통계 업데이트 (MySQL의 경우)
                // 주의: 실제 프로덕션에서는 ANALYZE TABLE이 성능에 영향을 줄 수 있음

                // 2. 메모리 정리
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var memoryBefore = GC.GetTotalMemory(false);
                var memoryAfter = GC.GetTotalMemory(true);
                var freedMemory = memoryBefore - memoryAfter;

                if (freedMemory > 1024 * 1024) // 1MB 이상 정리된 경우만 로깅
                {
                    _logger.LogDebug("메모리 정리: {FreedMB:F2}MB 해제",
                        freedMemory / (1024.0 * 1024.0));
                }

                // 3. 시스템 상태 검증
                var redisConnected = await redisService.IsConnectedAsync();
                var dbConnected = await dbContext.Database.CanConnectAsync(cancellationToken);

                if (!redisConnected || !dbConnected)
                {
                    _logger.LogWarning("시스템 연결 상태 이상: Redis={RedisStatus}, DB={DbStatus}",
                        redisConnected, dbConnected);
                }

                _logger.LogDebug("시스템 최적화 완료");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "시스템 최적화 중 오류 발생 (무시하고 계속)");
            }
        }

        /// <summary>
        /// 정리 작업 통계를 기록합니다.
        /// </summary>
        private async Task RecordCleanupStatisticsAsync(
            IRedisService redisService,
            CleanupResults results,
            TimeSpan duration)
        {
            try
            {
                var statsKey = $"cleanup_stats:{DateTime.UtcNow:yyyyMMdd}";
                var statsData = new
                {
                    timestamp = DateTime.UtcNow.ToString("O"),
                    deleted_transactions = results.DeletedTransactions,
                    deleted_price_histories = results.DeletedPriceHistories,
                    deleted_sessions = results.DeletedSessions,
                    deleted_redis_keys = results.DeletedRedisKeys,
                    duration_ms = duration.TotalMilliseconds,
                    success = true
                };

                await redisService.SetSystemStatsAsync(statsKey, statsData);

                // 최근 정리 정보도 별도 저장
                await redisService.SetSystemStatsAsync("last_cleanup", statsData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "정리 통계 기록 중 오류");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("데이터 정리 서비스 정지 중...");

            try
            {
                await base.StopAsync(cancellationToken);
                _logger.LogInformation("데이터 정리 서비스 정상 종료");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "데이터 정리 서비스 종료 중 오류");
            }
        }
    }

    /// <summary>
    /// 데이터 정리 결과를 담는 클래스
    /// </summary>
    public class CleanupResults
    {
        public int DeletedTransactions { get; set; }
        public int DeletedPriceHistories { get; set; }
        public int DeletedSessions { get; set; }
        public long DeletedRedisKeys { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public bool Success { get; set; } = true;
        public List<string> Errors { get; set; } = new();

        public int TotalDeletedItems => DeletedTransactions + DeletedPriceHistories + DeletedSessions;
    }
}