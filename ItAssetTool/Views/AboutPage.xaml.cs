using ItAssetTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace ItAssetTool.Views;

public sealed partial class AboutPage : Page
{
    // ����شӺ�̨������� ViewModel
    public AboutViewModel ViewModel => (AboutViewModel)this.DataContext;

    public AboutPage()
    {
        this.InitializeComponent();
        // �� DI �����л�ȡ ViewModel ʵ��������Ϊҳ�������������
        this.DataContext = App.Services.GetRequiredService<AboutViewModel>();
    }
}