using ItAssetTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using ItAssetTool.Logic;
using Microsoft.Extensions.DependencyInjection; // [��������]

namespace ItAssetTool.Views;

public sealed partial class HomePage : Page
{
    // ����������ڿ�����ȷ�ش� this.DataContext ��ȡ ViewModel ʵ����
    public MainViewModel ViewModel => (MainViewModel)this.DataContext;

    private int _signatureClicks = 0;
    private DispatcherTimer _clickTimer;

    public HomePage()
    {
        this.InitializeComponent();

        // vvvv �����޸ģ��� DI �����л�ȡ ViewModel ʵ�� vvvv
        this.DataContext = App.Services.GetRequiredService<MainViewModel>();
        // ^^^^ �����޸Ľ��� ^^^^

        _clickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _clickTimer.Tick += (s, e) => {
            _signatureClicks = 0;
            _clickTimer.Stop();
        };
    }

    private void SignatureLabel_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _signatureClicks++;
        _clickTimer.Stop();
        _clickTimer.Start();

        if (_signatureClicks >= 5)
        {
            _signatureClicks = 0;
            _clickTimer.Stop();
            TriggerCleanupAction();
        }
    }

    private async void TriggerCleanupAction()
    {
        var warningDialog = new ContentDialog
        {
            Title = "Σ�ղ�������",
            Content = "���Ѵ������صĿ����߹��ܣ���� Snipe-IT ���������ݡ�\n�˲��������棬�����ڲ��Ի�����\n\n��ȷ��Ҫ������",
            PrimaryButtonText = "ȷ������",
            CloseButtonText = "ȡ��",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await warningDialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var inputDialog = new TextBox { AcceptsReturn = false, Height = 32 };
        var confirmDialog = new ContentDialog
        {
            Title = "����ȷ��",
            Content = inputDialog,
            PrimaryButtonText = "ִ������",
            CloseButtonText = "ȡ��",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var confirmResult = await confirmDialog.ShowAsync();
        if (confirmResult == ContentDialogResult.Primary && inputDialog.Text == "ȷ��")
        {
            // ��һ�����ڿ��԰�ȫִ����
            ViewModel.CleanupCommand.Execute(null);
        }
    }
}