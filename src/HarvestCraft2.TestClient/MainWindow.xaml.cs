using System.Windows;
using HarvestCraft2.TestClient.ViewModels;

namespace HarvestCraft2.TestClient
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();

            // ViewModel을 DataContext로 설정 (핵심!)
            DataContext = viewModel;
        }
    }
}