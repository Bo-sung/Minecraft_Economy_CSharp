using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace HarvestCraft2.Economy.API.Configuration
{
    /// <summary>
    /// API Key 기반 인증 핸들러
    /// X-API-Key 헤더를 통한 간단한 인증을 제공합니다.
    /// </summary>
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private const string API_KEY_HEADER_NAME = "X-API-Key";
        private readonly ILogger<ApiKeyAuthenticationHandler> _logger;
        private readonly IConfiguration _configuration;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IConfiguration configuration)
            : base(options, logger, encoder, clock)
        {
            _logger = logger.CreateLogger<ApiKeyAuthenticationHandler>();
            _configuration = configuration;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            try
            {
                // API Key 헤더 확인
                if (!Request.Headers.ContainsKey(API_KEY_HEADER_NAME))
                {
                    _logger.LogWarning("API 요청에 {HeaderName} 헤더가 없음: {Path}",
                        API_KEY_HEADER_NAME, Request.Path);
                    return AuthenticateResult.NoResult();
                }

                var providedApiKey = Request.Headers[API_KEY_HEADER_NAME].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(providedApiKey))
                {
                    _logger.LogWarning("빈 API Key 제공됨: {Path}", Request.Path);
                    return AuthenticateResult.Fail("API Key가 비어있습니다.");
                }

                // 설정에서 유효한 API Key들 조회
                var validApiKeys = GetValidApiKeys();
                var apiKeyInfo = ValidateApiKey(providedApiKey, validApiKeys);

                if (apiKeyInfo == null)
                {
                    _logger.LogWarning("유효하지 않은 API Key: {ApiKey} from {IP}",
                        MaskApiKey(providedApiKey), GetClientIpAddress());
                    return AuthenticateResult.Fail("유효하지 않은 API Key입니다.");
                }

                // 인증 성공 - Claims 생성
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, apiKeyInfo.Name),
                    new Claim(ClaimTypes.NameIdentifier, apiKeyInfo.Id),
                    new Claim("ApiKeyName", apiKeyInfo.Name),
                    new Claim("ApiKeyPermissions", string.Join(",", apiKeyInfo.Permissions))
                };

                // 권한별 Claims 추가
                foreach (var permission in apiKeyInfo.Permissions)
                {
                    claims.Add(new Claim("permission", permission));
                }

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                _logger.LogInformation("API Key 인증 성공: {Name} ({Id}) from {IP}",
                    apiKeyInfo.Name, apiKeyInfo.Id, GetClientIpAddress());

                // 사용량 통계 업데이트 (비동기)
                _ = Task.Run(() => UpdateApiKeyUsageAsync(apiKeyInfo.Id));

                return AuthenticateResult.Success(ticket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API Key 인증 중 오류 발생");
                return AuthenticateResult.Fail("인증 처리 중 오류가 발생했습니다.");
            }
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 401;
            Response.ContentType = "application/json";

            var errorResponse = new
            {
                Success = false,
                Message = "인증이 필요합니다. X-API-Key 헤더를 포함해주세요.",
                Timestamp = DateTime.Now
            };

            await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
        }

        protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 403;
            Response.ContentType = "application/json";

            var errorResponse = new
            {
                Success = false,
                Message = "접근 권한이 없습니다.",
                Timestamp = DateTime.Now
            };

            await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
        }

        /// <summary>
        /// 설정에서 유효한 API Key 목록을 가져옵니다.
        /// </summary>
        private List<ApiKeyInfo> GetValidApiKeys()
        {
            var apiKeys = new List<ApiKeyInfo>();

            try
            {
                // appsettings.json에서 API Key 설정 읽기
                var apiKeySection = _configuration.GetSection("ApiKeys");

                if (apiKeySection.Exists())
                {
                    foreach (var keySection in apiKeySection.GetChildren())
                    {
                        var apiKeyInfo = new ApiKeyInfo
                        {
                            Id = keySection.Key,
                            Name = keySection["Name"] ?? keySection.Key,
                            Key = keySection["Key"] ?? string.Empty,
                            Permissions = keySection.GetSection("Permissions").Get<string[]>() ?? new[] { "read" },
                            IsActive = keySection.GetValue<bool>("IsActive", true),
                            CreatedAt = keySection.GetValue<DateTime>("CreatedAt", DateTime.Now),
                            LastUsed = keySection.GetValue<DateTime?>("LastUsed")
                        };

                        if (apiKeyInfo.IsActive && !string.IsNullOrEmpty(apiKeyInfo.Key))
                        {
                            apiKeys.Add(apiKeyInfo);
                        }
                    }
                }

                // 기본 API Key (개발용)
                if (!apiKeys.Any())
                {
                    apiKeys.Add(new ApiKeyInfo
                    {
                        Id = "default",
                        Name = "Default Development Key",
                        Key = _configuration["DefaultApiKey"] ?? "HarvestCraft2-Dev-Key-2025",
                        Permissions = new[] { "read", "write", "admin" },
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API Key 설정 로드 중 오류");

                // 오류 시 기본 키만 제공
                apiKeys.Clear();
                apiKeys.Add(new ApiKeyInfo
                {
                    Id = "emergency",
                    Name = "Emergency Key",
                    Key = "Emergency-Key-12345",
                    Permissions = new[] { "read" },
                    IsActive = true,
                    CreatedAt = DateTime.Now
                });
            }

            return apiKeys;
        }

        /// <summary>
        /// 제공된 API Key가 유효한지 확인합니다.
        /// </summary>
        private ApiKeyInfo? ValidateApiKey(string providedKey, List<ApiKeyInfo> validKeys)
        {
            try
            {
                // 정확한 일치 확인
                var exactMatch = validKeys.FirstOrDefault(k =>
                    k.IsActive &&
                    string.Equals(k.Key, providedKey, StringComparison.Ordinal));

                if (exactMatch != null)
                {
                    return exactMatch;
                }

                // 해시 기반 검증 (향후 보안 강화 시)
                // 현재는 단순 문자열 비교만 수행

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API Key 검증 중 오류");
                return null;
            }
        }

        /// <summary>
        /// API Key 사용량을 업데이트합니다.
        /// </summary>
        private async Task UpdateApiKeyUsageAsync(string apiKeyId)
        {
            try
            {
                // 실제로는 Redis나 데이터베이스에 사용량 기록
                // 현재는 로그만 기록
                _logger.LogDebug("API Key 사용 기록: {ApiKeyId} at {Timestamp}",
                    apiKeyId, DateTime.Now);

                await Task.Delay(1); // 비동기 작업 시뮬레이션
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API Key 사용량 업데이트 중 오류: {ApiKeyId}", apiKeyId);
            }
        }

        /// <summary>
        /// API Key를 마스킹합니다 (로그 보안).
        /// </summary>
        private string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey) || apiKey.Length <= 8)
            {
                return "***";
            }

            var start = apiKey.Substring(0, 4);
            var end = apiKey.Substring(apiKey.Length - 4);
            var middle = new string('*', Math.Max(4, apiKey.Length - 8));

            return $"{start}{middle}{end}";
        }

        /// <summary>
        /// 클라이언트 IP 주소를 가져옵니다.
        /// </summary>
        private string GetClientIpAddress()
        {
            try
            {
                // X-Forwarded-For 헤더 확인 (프록시/로드밸런서 뒤에 있을 때)
                var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedFor))
                {
                    return forwardedFor.Split(',')[0].Trim();
                }

                // X-Real-IP 헤더 확인
                var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
                if (!string.IsNullOrEmpty(realIp))
                {
                    return realIp;
                }

                // 직접 연결된 IP
                return Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }

    /// <summary>
    /// API Key 정보 클래스
    /// </summary>
    public class ApiKeyInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string[] Permissions { get; set; } = Array.Empty<string>();
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsed { get; set; }
        public int UsageCount { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// API Key 관련 확장 메소드
    /// </summary>
    public static class ApiKeyExtensions
    {
        /// <summary>
        /// 현재 사용자가 특정 권한을 가지고 있는지 확인합니다.
        /// </summary>
        public static bool HasPermission(this ClaimsPrincipal principal, string permission)
        {
            return principal.HasClaim("permission", permission);
        }

        /// <summary>
        /// 현재 사용자의 API Key 이름을 가져옵니다.
        /// </summary>
        public static string GetApiKeyName(this ClaimsPrincipal principal)
        {
            return principal.FindFirst("ApiKeyName")?.Value ?? "Unknown";
        }

        /// <summary>
        /// 현재 사용자의 모든 권한을 가져옵니다.
        /// </summary>
        public static IEnumerable<string> GetPermissions(this ClaimsPrincipal principal)
        {
            return principal.FindAll("permission").Select(c => c.Value);
        }
    }
}