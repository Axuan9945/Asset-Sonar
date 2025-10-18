// File: ItAssetTool/Views/SettingsPage.xaml.cs

using ItAssetTool.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetTool.Views;

public sealed partial class SettingsPage : Page
{
    // 方便在后台代码中访问 ViewModel
    public SettingsViewModel ViewModel => (SettingsViewModel)this.DataContext;

    public SettingsPage()
    {
        // CS1061 错误发生在这里。我们必须确保 XAML 编译器成功运行。
        this.InitializeComponent();

        // 从 DI 容器中获取 ViewModel 实例
        this.DataContext = App.Services.GetRequiredService<SettingsViewModel>();
    }

    // “新建”按钮的点击事件处理器
    private async void NewProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var inputDialog = new TextBox
        {
            AcceptsReturn = false,
            Height = 32
        };
        var dialog = new ContentDialog
        {
            Title = "新建配置方案",
            Content = inputDialog,
            PrimaryButtonText = "创建",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.AddNewProfileCommand.Execute(inputDialog.Text);
        }
    }
}