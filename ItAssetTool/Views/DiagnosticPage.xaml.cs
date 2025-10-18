using Microsoft.UI.Xaml.Controls;
using ItAssetTool.ViewModels;
using ItAssetTool.Logic;
using Microsoft.Extensions.DependencyInjection; // [新增引用]

namespace ItAssetTool.Views;

public sealed partial class DiagnosticPage : Page
{
    public DiagnosticPage()
    {
        // vvvv 核心修改：从 DI 容器中获取 ViewModel 实例 vvvv
        this.DataContext = App.Services.GetRequiredService<DiagnosticViewModel>();
        // ^^^^ 核心修改结束 ^^^^

        // 确保 DiagnosticPage.xaml 存在，并且其 Build Action 设置为 Page
        this.InitializeComponent();
    }
}