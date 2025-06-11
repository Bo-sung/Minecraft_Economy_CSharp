using Microsoft.Extensions.Diagnostics.HealthChecks;
using HarvestCraft2.Economy.API.Services.Interfaces;

namespace HarvestCraft2.Economy.API.Configuration
{
    /// <summary>
    /// Redis 연결 상태를 확인하는 헬스체크
    /// </summary>
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IRedisService _redisService;
        private readonly ILogger<RedisHealthCheck> _logger;

        public RedisHealthCheck(IRedisService redisService, ILogger<RedisHealthCheck> logger)
        {
            _redisService = redisService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Redis 연결 상태 확인
                var isConnected = await _redisService.IsConnectedAsync();

                if (!isConnected)
                {
                    _logger.LogWarning("Redis 헬스체크 실패: 연결되지 않음");
                    return HealthCheckResult.Unhealthy("Redis에 연결할 수 없습니다.");
                }

                // 추가 검증: 간단한 작업 수행
                var serverInfo = await _redisService.GetServerInfoAsync();
                var keyCount = await _redisService.GetKeyCountAsync();

                var healthData = new Dictionary<string, object>
                {
                    ["connected"] = isConnected,
                    ["server_info"] = serverInfo,
                    ["key_count"] = keyCount,
                    ["check_time"] = DateTime.UtcNow
                };

                _logger.LogDebug("Redis 헬스체크 성공: {KeyCount}개 키 존재", keyCount);

                return HealthCheckResult.Healthy("Redis가 정상적으로 작동 중입니다.", healthData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis 헬스체크 중 오류 발생");

                return HealthCheckResult.Unhealthy(
                    "Redis 헬스체크 중 오류가 발생했습니다.",
                    ex);
            }
        }
    }
}