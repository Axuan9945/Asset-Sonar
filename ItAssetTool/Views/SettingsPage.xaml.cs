// File: ItAssetTool/Views/SettingsPage.xaml.cs

using ItAssetTool.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace ItAssetTool.Views;

public sealed partial class SettingsPage : Page
{
    // �����ں�̨�����з��� ViewModel
    public SettingsViewModel ViewModel => (SettingsViewModel)this.DataContext;

    public SettingsPage()
    {
        // CS1061 ��������������Ǳ���ȷ�� XAML �������ɹ����С�
        this.InitializeComponent();

        // �� DI �����л�ȡ ViewModel ʵ��
        this.DataContext = App.Services.GetRequiredService<SettingsViewModel>();
    }

    // ���½�����ť�ĵ���¼�������
    private async void NewProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var inputDialog = new TextBox
        {
            AcceptsReturn = false,
            Height = 32
        };
        var dialog = new ContentDialog
        {
            Title = "�½����÷���",
            Content = inputDialog,
            PrimaryButtonText = "����",
            CloseButtonText = "ȡ��",
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