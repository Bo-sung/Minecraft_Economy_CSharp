using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HarvestCraft2.TestClient.Models;
using System.Net;

namespace HarvestCraft2.TestClient.Services
{
    /// <summary>
    /// HarvestCraft 2 Economy API와의 HTTP 통신을 담당하는 서비스 구현체
    /// </summary>
    public class ApiService : IApiService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        private string _baseUrl = "http://localhost:5000";
        private string _apiKey = string.Empty;
        private bool _isConnected = false;
        private readonly Timer _connectionCheckTimer;

        // ============================================================================
        // 생성자 및 초기화
        // ============================================================================

        public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // BaseAddress가 null인 경우 기본값 설정
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri("http://localhost:5000");
                _logger.LogWarning("HttpClient BaseAddress가 null입니다. 기본값으로 설정: http://localhost:5000");
            }

            _baseUrl = _httpClient.BaseAddress.ToString().TrimEnd('/');
            _logger.LogInformation("ApiService 초기화 완료. BaseURL: {BaseUrl}", _baseUrl);

            // JSON 직렬화 옵션 설정
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

            // 5분마다 연결 상태 확인
            _connectionCheckTimer = new Timer(CheckConnectionAsync, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

            _logger.LogInformation("ApiService 초기화 완료");
        }

        // ============================================================================
        // 속성 및 이벤트
        // ============================================================================

        public bool IsConnected => _isConnected;

        public string BaseUrl
        {
            get => _baseUrl;
            set
            {
                if (!string.IsNullOrEmpty(value) && _baseUrl != value)
                {
                    if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
                    {
                        var oldUrl = _baseUrl;
                        _baseUrl = value.TrimEnd('/');

                        // HttpClient BaseAddress도 업데이트
                        _httpClient.BaseAddress = uri;

                        _logger.LogInformation("API 기본 URL 변경: {OldUrl} → {NewUrl}", oldUrl, _baseUrl);

                        // 연결 상태 재확인 (비동기)
                        _ = Task.Run(async () => await TestConnectionAsync());
                    }
                    else
                    {
                        _logger.LogWarning("잘못된 BaseURL 형식: {BaseUrl}", value);
                        throw new ArgumentException($"잘못된 URL 형식: {value}");
                    }
                }
            }
        }

        public string ApiKey
        {
            get => _apiKey;
            set
            {
                if (_apiKey != value)
                {
                    _apiKey = value ?? string.Empty;
                    UpdateApiKeyHeader();
                    _logger.LogInformation("API 키 업데이트됨");
                }
            }
        }

        public event EventHandler<PriceChangedEventArgs>? PriceChanged;
        public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
        public event EventHandler<ApiErrorEventArgs>? ApiError;
        public event EventHandler<SettingsUpdatedEventArgs>? SettingsUpdated;

        // ============================================================================
        // 설정 관리 메서드들 (새로 추가)
        // ============================================================================

        /// <summary>
        /// 런타임에 API 설정을 업데이트합니다.
        /// </summary>
        public async Task UpdateSettingsAsync(ApiSettings settings)
        {
            try
            {
                _logger.LogInformation("API 설정을 업데이트합니다...");

                var oldSettings = GetCurrentSettings();

                // BaseURL 업데이트
                if (!string.IsNullOrEmpty(settings.BaseUrl) && settings.BaseUrl != _baseUrl)
                {
                    if (Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var newUri))
                    {
                        var oldBaseUrl = _baseUrl;
                        _baseUrl = settings.BaseUrl.TrimEnd('/');

                        // HttpClient BaseAddress도 업데이트
                        _httpClient.BaseAddress = newUri;

                        _logger.LogInformation("API BaseURL 변경: {OldUrl} → {NewUrl}", oldBaseUrl, _baseUrl);
                    }
                    else
                    {
                        _logger.LogWarning("잘못된 BaseURL 형식: {BaseUrl}. 기존 설정 유지", settings.BaseUrl);
                        throw new ArgumentException($"잘못된 URL 형식: {settings.BaseUrl}");
                    }
                }

                // API Key 업데이트
                if (settings.ApiKey != _apiKey)
                {
                    _apiKey = settings.ApiKey ?? string.Empty;
                    UpdateApiKeyHeader();
                    _logger.LogInformation("API Key 업데이트됨");
                }

                // Timeout 업데이트
                if (settings.TimeoutSeconds > 0)
                {
                    _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                    _logger.LogInformation("HTTP Timeout 변경: {TimeoutSeconds}초", settings.TimeoutSeconds);
                }

                // 설정 변경 이벤트 발생
                var newSettings = GetCurrentSettings();
                OnSettingsUpdated(oldSettings, newSettings);

                // 연결 상태 재확인
                _logger.LogInformation("새 설정으로 연결 테스트를 실행합니다...");
                await TestConnectionAsync();

                _logger.LogInformation("API 설정 업데이트 완료");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API 설정 업데이트 중 오류 발생");
                throw;
            }
        }

        /// <summary>
        /// API 설정을 object로 받아서 업데이트합니다.
        /// </summary>
        public async Task UpdateSettingsAsync(object settingsObject)
        {
            try
            {
                // dynamic으로 변환해서 처리
                dynamic settings = settingsObject;

                var apiSettings = new ApiSettings
                {
                    BaseUrl = settings.BaseUrl?.ToString() ?? _baseUrl,
                    ApiKey = settings.ApiKey?.ToString() ?? _apiKey,
                    TimeoutSeconds = Convert.ToInt32(settings.TimeoutSeconds ?? 30),
                    RetryCount = Convert.ToInt32(settings.RetryCount ?? 3),
                    UseHttps = Convert.ToBoolean(settings.UseHttps ?? false)
                };

                await UpdateSettingsAsync(apiSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API 설정 업데이트 중 오류 발생 (object 타입)");

                // fallback: 기본 설정 정보만 업데이트
                if (settingsObject is ApiSettings apiSettings)
                {
                    await UpdateSettingsAsync(apiSettings);
                }
                else
                {
                    throw new ArgumentException("올바른 설정 객체가 아닙니다.", nameof(settingsObject));
                }
            }
        }

        /// <summary>
        /// 현재 설정 정보를 반환합니다.
        /// </summary>
        public ApiSettings GetCurrentSettings()
        {
            return new ApiSettings
            {
                BaseUrl = _baseUrl,
                ApiKey = _apiKey,
                TimeoutSeconds = (int)_httpClient.Timeout.TotalSeconds,
                RetryCount = 3, // 기본값
                UseHttps = _baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            };
        }

        /// <summary>
        /// 설정 변경 이벤트 발생
        /// </summary>
        private void OnSettingsUpdated(ApiSettings oldSettings, ApiSettings newSettings)
        {
            SettingsUpdated?.Invoke(this, new SettingsUpdatedEventArgs
            {
                OldSettings = oldSettings,
                NewSettings = newSettings,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // ============================================================================
        // 연결 관리
        // ============================================================================

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("API 서버 연결 테스트 시작: {BaseUrl}", _baseUrl);

                // /api/status 엔드포인트로 테스트 (절대 URI 사용)
                var response = await SendRequestAsync<string>(HttpMethod.Get, "/health", null, cancellationToken);

                await SetConnectionStatusAsync(true);
                _logger.LogInformation("API 서버 연결 성공: {BaseUrl}", _baseUrl);

                return true;
            }
            catch (Exception ex)
            {
                await SetConnectionStatusAsync(false, ex.Message);
                _logger.LogWarning(ex, "API 서버 연결 실패: {BaseUrl}", _baseUrl);

                return false;
            }
        }

        public async Task<ApiStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<ApiStatusResponse>("/health", cancellationToken);
        }

        // ============================================================================
        // 상점 관련 API
        // ============================================================================

        public async Task<PurchaseResponse> PurchaseItemAsync(string playerId, string itemId, int quantity, CancellationToken cancellationToken = default)
        {
            var request = new { playerId, itemId, quantity };
            return await SendPostRequestAsync<PurchaseResponse>("/api/shop/purchase", request, cancellationToken);
        }

        public async Task<SellResponse> SellItemAsync(string playerId, string itemId, int quantity, CancellationToken cancellationToken = default)
        {
            var request = new { playerId, itemId, quantity };
            return await SendPostRequestAsync<SellResponse>("/api/shop/sell", request, cancellationToken);
        }

        public async Task<BatchTradeResponse> BatchTradeAsync(string playerId, List<TradeRequest> trades, CancellationToken cancellationToken = default)
        {
            var request = new { playerId, trades };
            return await SendPostRequestAsync<BatchTradeResponse>("/api/shop/batch-trade", request, cancellationToken);
        }

        public async Task<List<TransactionResponse>> GetPlayerTransactionsAsync(string playerId, int page = 1, int size = 50, CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<List<TransactionResponse>>($"/api/shop/transactions/{playerId}?page={page}&size={size}", cancellationToken);
        }

        // ============================================================================
        // 가격 관련 API
        // ============================================================================

        public async Task<PriceResponse> GetItemPriceAsync(string itemId, CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<PriceResponse>($"/api/price/{itemId}", cancellationToken);
        }

        public async Task<List<PriceResponse>> GetItemPricesAsync(List<string> itemIds, CancellationToken cancellationToken = default)
        {
            var request = new { itemIds };
            return await SendPostRequestAsync<List<PriceResponse>>("/api/price/batch", request, cancellationToken);
        }

        public async Task<List<PriceHistoryResponse>> GetPriceHistoryAsync(string itemId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            var query = BuildQueryString(new Dictionary<string, object?>
            {
                ["startDate"] = startDate?.ToString("yyyy-MM-dd"),
                ["endDate"] = endDate?.ToString("yyyy-MM-dd")
            });

            return await SendGetRequestAsync<List<PriceHistoryResponse>>($"/api/price/{itemId}/history{query}", cancellationToken);
        }

        public async Task<PricePredictionResponse> GetPricePredictionAsync(string itemId, int days = 7, CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<PricePredictionResponse>($"/api/price/{itemId}/prediction?days={days}", cancellationToken);
        }

        // ============================================================================
        // 시장 분석 API
        // ============================================================================

        public async Task<MarketDashboardResponse> GetMarketDashboardAsync(CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<MarketDashboardResponse>("/api/market/dashboard", cancellationToken);
        }

        public async Task<List<PopularItemResponse>> GetPopularItemsAsync(int limit = 10, CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<List<PopularItemResponse>>($"/api/market/popular?limit={limit}", cancellationToken);
        }

        public async Task<List<VolatileItemResponse>> GetVolatileItemsAsync(int limit = 10, CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<List<VolatileItemResponse>>($"/api/market/volatile?limit={limit}", cancellationToken);
        }

        public async Task<List<CategoryStatsResponse>> GetCategoryStatsAsync(CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<List<CategoryStatsResponse>>("/api/market/categories", cancellationToken);
        }

        // ============================================================================
        // 플레이어 관리 API
        // ============================================================================

        public async Task<PlayerResponse> GetPlayerAsync(string playerId, CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<PlayerResponse>($"/api/player/{playerId}", cancellationToken);
        }

        public async Task<PlayerResponse> CreatePlayerAsync(string playerName, decimal initialBalance = 1000m, CancellationToken cancellationToken = default)
        {
            var request = new { playerName, initialBalance };
            return await SendPostRequestAsync<PlayerResponse>("/api/player/create", request, cancellationToken);
        }

        public async Task<BalanceResponse> GetPlayerBalanceAsync(string playerId, CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<BalanceResponse>($"/api/player/{playerId}/balance", cancellationToken);
        }

        public async Task<BalanceResponse> SetPlayerBalanceAsync(string playerId, decimal amount, CancellationToken cancellationToken = default)
        {
            var request = new { amount };
            return await SendPostRequestAsync<BalanceResponse>($"/api/player/{playerId}/balance", request, cancellationToken);
        }

        public async Task<List<PlayerResponse>> GetOnlinePlayersAsync(CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<List<PlayerResponse>>("/api/player/online", cancellationToken);
        }

        // ============================================================================
        // 관리자 API
        // ============================================================================

        public async Task<SystemMetricsResponse> GetSystemMetricsAsync(CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<SystemMetricsResponse>("/api/admin/metrics", cancellationToken);
        }

        public async Task<List<ItemResponse>> GetItemsAsync(CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<List<ItemResponse>>("/api/admin/items", cancellationToken);
        }

        public async Task<ItemResponse> UpdateItemAsync(string itemId, UpdateItemRequest request, CancellationToken cancellationToken = default)
        {
            return await SendPutRequestAsync<ItemResponse>($"/api/admin/items/{itemId}", request, cancellationToken);
        }

        public async Task<PriceResponse> AdjustPriceAsync(string itemId, decimal newPrice, string reason, CancellationToken cancellationToken = default)
        {
            var request = new { newPrice, reason };
            return await SendPostRequestAsync<PriceResponse>($"/api/admin/price/{itemId}/adjust", request, cancellationToken);
        }

        public async Task<CleanupResponse> CleanupDataAsync(DateTime beforeDate, CancellationToken cancellationToken = default)
        {
            var request = new { beforeDate };
            return await SendPostRequestAsync<CleanupResponse>("/api/admin/cleanup", request, cancellationToken);
        }

        // ============================================================================
        // 내부 HTTP 통신 메서드들 (수정됨)
        // ============================================================================

        private async Task<T> SendGetRequestAsync<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            return await SendRequestAsync<T>(HttpMethod.Get, endpoint, null, cancellationToken);
        }

        private async Task<T> SendPostRequestAsync<T>(string endpoint, object? content, CancellationToken cancellationToken = default)
        {
            return await SendRequestAsync<T>(HttpMethod.Post, endpoint, content, cancellationToken);
        }

        private async Task<T> SendPutRequestAsync<T>(string endpoint, object? content, CancellationToken cancellationToken = default)
        {
            return await SendRequestAsync<T>(HttpMethod.Put, endpoint, content, cancellationToken);
        }

        /// <summary>
        /// HTTP 요청을 보내고 응답을 받습니다. (핵심 수정 부분)
        /// </summary>
        private async Task<T> SendRequestAsync<T>(HttpMethod method, string endpoint, object? content, CancellationToken cancellationToken = default)
        {
            try
            {
                // 🔥 핵심 수정: 항상 절대 URI 생성하여 사용
                var requestUri = CreateAbsoluteUri(endpoint);
                _logger.LogDebug("API 요청: {Method} {Uri}", method, requestUri);

                using var request = new HttpRequestMessage(method, requestUri);

                // POST/PUT 요청인 경우 JSON 본문 추가
                if (content != null && (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch))
                {
                    var json = JsonSerializer.Serialize(content, _jsonOptions);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                // API Key 헤더 추가 (런타임에 설정된 값 사용)
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    request.Headers.Add("X-API-Key", _apiKey);
                }

                // User-Agent 헤더 추가
                request.Headers.Add("User-Agent", "HarvestCraft2-TestClient/1.0");

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("API 응답 성공: {StatusCode}", response.StatusCode);

                    if (typeof(T) == typeof(string))
                    {
                        return (T)(object)responseContent;
                    }

                    if (typeof(T) == typeof(bool))
                    {
                        return (T)(object)true;
                    }

                    if (string.IsNullOrEmpty(responseContent))
                    {
                        return default(T)!;
                    }

                    return JsonSerializer.Deserialize<T>(responseContent, _jsonOptions)!;
                }
                else
                {
                    var errorMessage = $"API 요청 실패: {response.StatusCode} - {responseContent}";
                    _logger.LogError(errorMessage);

                    // API 오류 이벤트 발생
                    ApiError?.Invoke(this, new ApiErrorEventArgs
                    {
                        Method = method.Method,
                        Endpoint = endpoint,
                        StatusCode = (int)response.StatusCode,
                        ErrorMessage = responseContent,
                        OccurredAt = DateTime.UtcNow
                    });

                    throw new HttpRequestException(errorMessage);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("API 요청 취소됨: {Method} {Endpoint}", method, endpoint);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API 요청 중 오류 발생: {Method} {Endpoint}", method, endpoint);

                // API 오류 이벤트 발생
                ApiError?.Invoke(this, new ApiErrorEventArgs
                {
                    Method = method.Method,
                    Endpoint = endpoint,
                    StatusCode = 0,
                    ErrorMessage = ex.Message,
                    Exception = ex,
                    OccurredAt = DateTime.UtcNow
                });

                throw;
            }
        }

        /// <summary>
        /// 절대 URI를 생성합니다. (설정 변경 반영)
        /// </summary>
        private Uri CreateAbsoluteUri(string endpoint)
        {
            try
            {
                // endpoint가 이미 절대 URI인 경우
                if (Uri.TryCreate(endpoint, UriKind.Absolute, out var absoluteUri))
                {
                    return absoluteUri;
                }

                // 상대 URI인 경우 현재 BaseUrl과 결합
                var baseUri = new Uri(_baseUrl);

                // endpoint가 '/'로 시작하지 않으면 추가
                if (!endpoint.StartsWith("/"))
                {
                    endpoint = "/" + endpoint;
                }

                var combinedUri = new Uri(baseUri, endpoint);
                _logger.LogTrace("절대 URI 생성: {BaseUrl} + {Endpoint} = {FinalUri}", _baseUrl, endpoint, combinedUri);

                return combinedUri;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "URI 생성 실패: BaseUrl={BaseUrl}, Endpoint={Endpoint}", _baseUrl, endpoint);

                // 폴백: 기본 조합 시도
                var fallbackUrl = $"{_baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
                return new Uri(fallbackUrl);
            }
        }

        // ============================================================================
        // 유틸리티 메서드들
        // ============================================================================

        /// <summary>
        /// API Key 헤더를 업데이트합니다.
        /// </summary>
        private void UpdateApiKeyHeader()
        {
            // 기존 API Key 헤더 제거
            _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            _httpClient.DefaultRequestHeaders.Remove("Authorization");

            // 새 API Key 헤더 추가
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
                _logger.LogDebug("API Key 헤더 업데이트됨");
            }
            else
            {
                _logger.LogDebug("API Key가 제거됨");
            }
        }

        private async Task SetConnectionStatusAsync(bool isConnected, string? errorMessage = null)
        {
            if (_isConnected != isConnected)
            {
                _isConnected = isConnected;

                ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
                {
                    IsConnected = isConnected,
                    ErrorMessage = errorMessage,
                    ChangedAt = DateTime.UtcNow
                });

                _logger.LogInformation("연결 상태 변경: {IsConnected}", isConnected);
            }
        }

        private async void CheckConnectionAsync(object? state)
        {
            try
            {
                await TestConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "정기 연결 확인 중 오류 발생");
            }
        }

        private static string BuildQueryString(Dictionary<string, object?> parameters)
        {
            var queryParams = new List<string>();

            foreach (var (key, value) in parameters)
            {
                if (value != null)
                {
                    queryParams.Add($"{key}={Uri.EscapeDataString(value.ToString()!)}");
                }
            }

            return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        }

        // ============================================================================
        // IDisposable 구현
        // ============================================================================

        public void Dispose()
        {
            _connectionCheckTimer?.Dispose();
            _httpClient?.Dispose();
            _logger.LogInformation("ApiService 리소스 정리 완료");
        }
    }
}