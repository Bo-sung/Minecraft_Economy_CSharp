using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows;
//using HarvestCraft2.TestClient.Services.Interfaces;
//using HarvestCraft2.TestClient.Services;
//using HarvestCraft2.TestClient.ViewModels;
//using HarvestCraft2.TestClient.Views;

namespace HarvestCraft2.TestClient
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 호스트 빌더 설정
            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    // appsettings.json 설정 파일 로드
                    config.SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                                     optional: true, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // Configuration 등록
                    services.AddSingleton(context.Configuration);

                    // HttpClient 등록
                    //services.AddHttpClient<IApiService, ApiService>(client =>
                    //{
                    //    var baseUrl = context.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7001";
                    //    client.BaseAddress = new Uri(baseUrl);
                    //    client.DefaultRequestHeaders.Add("X-API-Key",
                    //        context.Configuration["ApiSettings:ApiKey"] ?? "test-api-key");
                    //});

                    // Core Services 등록
                    //services.AddSingleton<IApiService, ApiService>();
                    //services.AddSingleton<IPlayerService, PlayerService>();
                    //services.AddSingleton<IChartService, ChartService>();
                    //services.AddSingleton<INotificationService, NotificationService>();

                    // ViewModels 등록
                    //services.AddTransient<MainWindowViewModel>();
                    //services.AddTransient<ShopViewModel>();
                    //services.AddTransient<PriceViewModel>();
                    //services.AddTransient<MarketViewModel>();
                    //services.AddTransient<PlayerViewModel>();
                    //services.AddTransient<AdminViewModel>();
                    //services.AddTransient<SettingsViewModel>();

                    // Views 등록
                    //services.AddTransient<MainWindow>();
                    //services.AddTransient<ShopView>();
                    //services.AddTransient<PriceView>();
                    //services.AddTransient<MarketView>();
                    //services.AddTransient<PlayerView>();
                    //services.AddTransient<AdminView>();
                    //services.AddTransient<SettingsView>();

                    // Logging 설정
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.AddDebug();
                        builder.SetMinimumLevel(LogLevel.Information);
                    });
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                    logging.AddDebug();
                })
                .Build();

            // 서비스 시작
            _host.Start();

            // 메인 윈도우 생성 및 표시
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 애플리케이션 종료시 호스트 정리
            _host?.Dispose();
            base.OnExit(e);
        }

        /// <summary>
        /// 전역 예외 처리
        /// </summary>
        private void Application_DispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = _host?.Services.GetService<ILogger<App>>();
            logger?.LogError(e.Exception, "Unhandled exception occurred");

            MessageBox.Show($"예상치 못한 오류가 발생했습니다:\n{e.Exception.Message}",
                          "오류", MessageBoxButton.OK, MessageBoxImage.Error);

            // 애플리케이션 계속 실행
            e.Handled = true;
        }

        /// <summary>
        /// 현재 호스트의 서비스 프로바이더 반환
        /// </summary>
        public static IServiceProvider? ServiceProvider => ((App)Current)._host?.Services;

        /// <summary>
        /// 서비스 인스턴스 가져오기 헬퍼 메소드
        /// </summary>
        public static T? GetService<T>() where T : class
        {
            return ServiceProvider?.GetService<T>();
        }

        /// <summary>
        /// 필수 서비스 인스턴스 가져오기 헬퍼 메소드
        /// </summary>
        public static T GetRequiredService<T>() where T : class
        {
            return ServiceProvider?.GetRequiredService<T>()
                ?? throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
        }
    }
}