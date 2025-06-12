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
                if (_baseUrl != value)
                {
                    _baseUrl = value;
                    _httpClient.BaseAddress = new Uri(value);
                    _logger.LogInformation("API 기본 URL 변경: {BaseUrl}", value);
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
                    _apiKey = value;
                    UpdateApiKeyHeader();
                    _logger.LogInformation("API 키 업데이트됨");
                }
            }
        }

        public event EventHandler<PriceChangedEventArgs>? PriceChanged;
        public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
        public event EventHandler<ApiErrorEventArgs>? ApiError;

        // ============================================================================
        // 연결 관리
        // ============================================================================

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("API 연결 테스트 시작");

                var response = await _httpClient.GetAsync("/api/status", cancellationToken);
                var isConnected = response.IsSuccessStatusCode;

                await SetConnectionStatusAsync(isConnected);

                _logger.LogInformation("API 연결 테스트 결과: {IsConnected}", isConnected);
                return isConnected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API 연결 테스트 실패");
                await SetConnectionStatusAsync(false, ex.Message);
                return false;
            }
        }

        public async Task<ApiStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            return await SendGetRequestAsync<ApiStatusResponse>("/api/status", cancellationToken);
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
        // 내부 HTTP 통신 메서드들
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

        private async Task<T> SendRequestAsync<T>(HttpMethod method, string endpoint, object? content, CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = new HttpRequestMessage(method, endpoint);

                // POST/PUT 요청인 경우 JSON 본문 추가
                if (content != null && (method == HttpMethod.Post || method == HttpMethod.Put))
                {
                    var json = JsonSerializer.Serialize(content, _jsonOptions);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                _logger.LogDebug("API 요청: {Method} {Endpoint}", method, endpoint);

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("API 응답 성공: {StatusCode}", response.StatusCode);

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

        // ============================================================================
        // 유틸리티 메서드들
        // ============================================================================

        private void UpdateApiKeyHeader()
        {
            _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
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