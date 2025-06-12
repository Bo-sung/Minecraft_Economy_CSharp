using Microsoft.Extensions.DependencyInjection;
using HarvestCraft2.TestClient.ViewModels;

namespace HarvestCraft2.TestClient.Extensions
{
    /// <summary>
    /// 의존성 주입 컨테이너 확장 메서드
    /// 기존 App.xaml.cs에서 사용할 ViewModel 등록
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// ViewModels을 DI 컨테이너에 등록
        /// 기존 App.xaml.cs의 Host 설정에서 호출
        /// </summary>
        public static IServiceCollection AddViewModels(this IServiceCollection services)
        {
            // 메인 ViewModel 등록
            services.AddSingleton<MainWindowViewModel>();

            // 향후 Phase 3에서 추가될 탭 ViewModels
            // services.AddTransient<ShopViewModel>();
            // services.AddTransient<PriceViewModel>();
            // services.AddTransient<MarketViewModel>();
            // services.AddTransient<PlayerViewModel>();
            // services.AddTransient<AdminViewModel>();
            // services.AddTransient<SettingsViewModel>();

            return services;
        }

        /// <summary>
        /// Views를 DI 컨테이너에 등록
        /// MainWindow는 이미 등록되어 있을 것이므로 확장용
        /// </summary>
        public static IServiceCollection AddViews(this IServiceCollection services)
        {
            // MainWindow는 기존 App.xaml.cs에서 등록됨
            // services.AddSingleton<MainWindow>();

            // 향후 Phase 4에서 추가될 Views
            // services.AddTransient<Views.ShopView>();
            // services.AddTransient<Views.PriceView>();
            // services.AddTransient<Views.MarketView>();

            return services;
        }

        /// <summary>
        /// 향후 Phase 2에서 구현될 서비스들 등록
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Phase 2에서 구현될 서비스들
            // services.AddHttpClient<IApiService, ApiService>();
            // services.AddSingleton<IPlayerService, PlayerService>();
            // services.AddSingleton<IChartService, ChartService>();
            // services.AddSingleton<IConfigurationService, ConfigurationService>();

            return services;
        }
    }
}

// 기존 App.xaml.cs에서 사용할 확장 방법:
// 
// protected override async void OnStartup(StartupEventArgs e)
// {
//     // 기존 Host 빌더 설정에 추가:
//     _host = Host.CreateDefaultBuilder()
//         .ConfigureServices((context, services) =>
//         {
//             // 기존 서비스들...
//             
//             // ViewModels 등록 추가
//             services.AddViewModels();
//         })
//         .Build();
//     
//     // 기존 코드 계속...
// }