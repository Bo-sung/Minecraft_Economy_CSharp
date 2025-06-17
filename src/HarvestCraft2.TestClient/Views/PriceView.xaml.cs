using System.Windows.Controls;
using HarvestCraft2.TestClient.ViewModels;

namespace HarvestCraft2.TestClient.Views
{
    /// <summary>
    /// PriceView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PriceView : UserControl
    {
        public PriceView()
        {
            InitializeComponent();
        }

        public PriceView(PriceViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}