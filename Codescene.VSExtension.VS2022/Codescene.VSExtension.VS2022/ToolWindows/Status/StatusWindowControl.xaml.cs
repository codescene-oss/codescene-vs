using Codescene.VSExtension.Core.Application.Services.Authentication;
using Codescene.VSExtension.Core.Application.Services.MDFileHandler;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.VS2022.ToolWindows.Markdown;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Codescene.VSExtension.VS2022.ToolWindows.Status
{
    public partial class StatusWindowControl : UserControl
    {
        [Import]
        private IAuthenticationService _authenticationService;

        [Import]
        private IMDFileHandler _mDFileHandler;
        public StatusWindowModel ViewModel { get; set; }

        public StatusWindowControl()
        {
            InitializeComponent();
            //fire event on settings change
            General.Saved += OnSettingsSaved;
            InitializeComponent();

            //initalize view model of control
            var i = _authenticationService.IsLoggedIn();
            ViewModel = new StatusWindowModel
            {
                CodeHealthActivated = General.Instance.PreviewCodeHealthGate,
                IsLoggedIn = _authenticationService.IsLoggedIn(),
            };

            _authenticationService.OnSignedIn += (LoginResponse response) => { ViewModel.IsLoggedIn = true; };
            _authenticationService.OnSignedOut += () => { ViewModel.IsLoggedIn = false; };
            DataContext = ViewModel;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true; // Prevents the default handling of the event
        }

        //change the property based on fired event
        private void OnSettingsSaved(General obj)
        {
            ViewModel.CodeHealthActivated = obj.PreviewCodeHealthGate;
        }

        private async void OpenMarkdownButton_Click(object sender, RoutedEventArgs e)
        {
            _mDFileHandler.SetFileName("bumpy-road-ahead");
            await MarkdownWindow.ShowAsync();
        }
    }
}