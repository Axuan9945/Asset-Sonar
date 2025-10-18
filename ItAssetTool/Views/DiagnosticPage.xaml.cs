using Microsoft.UI.Xaml.Controls;
using ItAssetTool.ViewModels;
using ItAssetTool.Logic;
using Microsoft.Extensions.DependencyInjection; // [��������]

namespace ItAssetTool.Views;

public sealed partial class DiagnosticPage : Page
{
    public DiagnosticPage()
    {
        // vvvv �����޸ģ��� DI �����л�ȡ ViewModel ʵ�� vvvv
        this.DataContext = App.Services.GetRequiredService<DiagnosticViewModel>();
        // ^^^^ �����޸Ľ��� ^^^^

        // ȷ�� DiagnosticPage.xaml ���ڣ������� Build Action ����Ϊ Page
        this.InitializeComponent();
    }
}