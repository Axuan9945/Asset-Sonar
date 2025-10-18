using ItAssetTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace ItAssetTool.Views;

public sealed partial class AboutPage : Page
{
    // 方便地从后台代码访问 ViewModel
    public AboutViewModel ViewModel => (AboutViewModel)this.DataContext;

    public AboutPage()
    {
        this.InitializeComponent();
        // 从 DI 容器中获取 ViewModel 实例并设置为页面的数据上下文
        this.DataContext = App.Services.GetRequiredService<AboutViewModel>();
    }
}