using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.IO;
using System.Windows;
//using HarvestCraft2.TestClient.Services;
//using HarvestCraft2.TestClient.ViewModels;
using Serilog.Events;

namespace HarvestCraft2.TestClient
{
    /// <summary>
    /// HarvestCraft 2 Economy 테스트 클라이언트 애플리케이션
    /// </summary>
    public partial class App : Application
    {
        private static IHost? _host;

        /// <summary>
        /// 애플리케이션 시작 시 의존성 주입 및 설정 초기화
        /// </summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Serilog 설정
                ConfigureLogging();

                // Host 빌더 설정
                _host = CreateHostBuilder(e.Args).Build();

                // 서비스 시작
                await _host.StartAsync();

                // 메인 윈도우 표시
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();

                Log.Information("HarvestCraft 2 테스트 클라이언트가 시작되었습니다.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "애플리케이션 시작 중 치명적인 오류가 발생했습니다.");
                MessageBox.Show($"애플리케이션 시작 실패:\n{ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }

            base.OnStartup(e);
        }

        /// <summary>
        /// 애플리케이션 종료 시 리소스 정리
        /// </summary>
        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                if (_host != null)
                {
                    await _host.StopAsync();
                    _host.Dispose();
                    _host = null; // static이므로 명시적으로 null 설정
                }

                Log.Information("HarvestCraft 2 테스트 클라이언트가 종료되었습니다.");
                Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                // 종료 중 오류는 로깅만 하고 계속 진행
                Log.Error(ex, "애플리케이션 종료 중 오류가 발생했습니다.");
            }

            base.OnExit(e);
        }

        /// <summary>
        /// 처리되지 않은 예외 처리
        /// </summary>
        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            Log.Information("시스템 세션이 종료됩니다. 이유: {Reason}", e.ReasonSessionEnding);
            base.OnSessionEnding(e);
        }

        /// <summary>
        /// Host 빌더 생성 및 의존성 주입 설정
        /// </summary>
        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    // appsettings.json 설정
                    config.SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                                     optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables()
                          .AddCommandLine(args);
                })
                .ConfigureServices((context, services) =>
                {
                    // 설정 바인딩
                    var configuration = context.Configuration;
                    services.Configure<ApiSettings>(configuration.GetSection("ApiSettings"));
                    services.Configure<UiSettings>(configuration.GetSection("UiSettings"));

                    // HTTP 클라이언트 (임시로 주석처리 - 서비스 미구현)
                    /*
                    services.AddHttpClient<IApiService, ApiService>(client =>
                    {
                        var apiSettings = configuration.GetSection("ApiSettings").Get<ApiSettings>();
                        client.BaseAddress = new Uri(apiSettings?.BaseUrl ?? "http://localhost:5000");
                        client.Timeout = TimeSpan.FromSeconds(apiSettings?.TimeoutSeconds ?? 30);
                        
                        // API Key 헤더 추가
                        if (!string.IsNullOrEmpty(apiSettings?.ApiKey))
                        {
                            client.DefaultRequestHeaders.Add("X-API-Key", apiSettings.ApiKey);
                        }
                    });
                    */

                    // 서비스 등록 (미구현 서비스들은 주석처리)
                    // RegisterServices(services);

                    // 뷰모델 등록 (미구현 뷰모델들은 주석처리)
                    // RegisterViewModels(services);

                    // 윈도우 등록
                    RegisterWindows(services);
                })
                .UseSerilog();

        /// <summary>
        /// 서비스 의존성 주입 등록 (현재 미구현으로 주석처리)
        /// </summary>
        private static void RegisterServices(IServiceCollection services)
        {
            // TODO: Phase 2에서 구현 예정
            /*
            // 핵심 서비스
            services.AddSingleton<IApiService, ApiService>();
            services.AddSingleton<IPlayerService, PlayerService>();
            services.AddSingleton<IChartService, ChartService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<INotificationService, NotificationService>();

            // 유틸리티 서비스
            services.AddSingleton<IDialogService, DialogService>();
            services.AddTransient<IExportService, ExportService>();
            */
        }

        /// <summary>
        /// 뷰모델 의존성 주입 등록 (현재 미구현으로 주석처리)
        /// </summary>
        private static void RegisterViewModels(IServiceCollection services)
        {
            // TODO: Phase 3에서 구현 예정
            /*
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<ShopViewModel>();
            services.AddTransient<PriceViewModel>();
            services.AddTransient<MarketViewModel>();
            services.AddTransient<PlayerViewModel>();
            services.AddTransient<AdminViewModel>();
            services.AddTransient<SettingsViewModel>();
            */
        }

        /// <summary>
        /// 윈도우 및 뷰 의존성 주입 등록
        /// </summary>
        private static void RegisterWindows(IServiceCollection services)
        {
            services.AddSingleton<MainWindow>();
        }

        /// <summary>
        /// Serilog 로깅 설정
        /// </summary>
        private static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}")
                .WriteTo.File(
                    path: Path.Combine("logs", "harvestcraft2-testclient-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {NewLine}{Exception}")
                .CreateLogger();
        }

        /// <summary>
        /// 전역 예외 처리기 등록
        /// </summary>
        private void RegisterGlobalExceptionHandlers()
        {
            // WPF UI 스레드 예외
            DispatcherUnhandledException += (sender, e) =>
            {
                Log.Error(e.Exception, "UI 스레드에서 처리되지 않은 예외가 발생했습니다.");
                MessageBox.Show($"예상치 못한 오류가 발생했습니다:\n{e.Exception.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            // 애플리케이션 도메인 예외
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                Log.Fatal(exception, "애플리케이션 도메인에서 처리되지 않은 예외가 발생했습니다.");
            };

            // Task 예외
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Log.Error(e.Exception, "관찰되지 않은 Task 예외가 발생했습니다.");
                e.SetObserved();
            };
        }

        /// <summary>
        /// 서비스 제공자 인스턴스 (전역 접근용)
        /// </summary>
        public static IServiceProvider? ServiceProvider => _host?.Services;
    }

    // ============================================================================
    // 설정 클래스들
    // ============================================================================

    /// <summary>
    /// API 연결 설정
    /// </summary>
    public class ApiSettings
    {
        public string BaseUrl { get; set; } = "http://localhost:5000";
        public string ApiKey { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
        public int RetryCount { get; set; } = 3;
        public bool UseHttps { get; set; } = false;
    }

    /// <summary>
    /// UI 관련 설정
    /// </summary>
    public class UiSettings
    {
        public string Theme { get; set; } = "Light";
        public string Language { get; set; } = "ko-KR";
        public bool ShowNotifications { get; set; } = true;
        public bool AutoRefresh { get; set; } = true;
        public int RefreshIntervalSeconds { get; set; } = 30;
        public bool ShowAdvancedFeatures { get; set; } = false;
    }
}