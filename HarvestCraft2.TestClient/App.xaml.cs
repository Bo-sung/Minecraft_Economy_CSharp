using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace HarvestCraft2.TestClient
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 설정 구성
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Serilog 설정
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            // 호스트 빌드
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services, configuration);
                })
                .Build();

            base.OnStartup(e);
        }

        private void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // 설정 등록
            services.AddSingleton(configuration);

            // HTTP 클라이언트 등록
            services.AddHttpClient("ApiClient", client =>
            {
                var baseUrl = configuration["ApiSettings:BaseUrl"];
                var apiKey = configuration["ApiSettings:ApiKey"];

                client.BaseAddress = new Uri(baseUrl!);
                client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
                client.Timeout = TimeSpan.FromMilliseconds(
                    configuration.GetValue<int>("ApiSettings:Timeout", 30000));
            });

            // 서비스들 등록 (나중에 추가 예정)
            // services.AddSingleton<IApiService, ApiService>();
            // services.AddSingleton<IPlayerService, PlayerService>();

            // 뷰모델들 등록 (나중에 추가 예정)
            // services.AddTransient<MainWindowViewModel>();

            // 메인 윈도우 등록
            services.AddSingleton<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        public static T GetService<T>() where T : class
        {
            var app = (App)Current;
            return app._host?.Services.GetRequiredService<T>()!;
        }
    }
}