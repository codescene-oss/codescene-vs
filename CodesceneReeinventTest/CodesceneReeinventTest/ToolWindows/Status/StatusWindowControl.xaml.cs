using CodesceneReeinventTest.Application.MDFileHandler;
using CodesceneReeinventTest.ToolWindows.Markdown;
using Core.Application.Services.Authentication;
using Core.Application.Services.FileDownloader;
using Core.Models;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace CodesceneReeinventTest.ToolWindows.Status
{
    public partial class StatusWindowControl : UserControl
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly IMDFileHandler _mDFileHandler;
        private readonly IFileDownloader _fileDownloader;
        public StatusWindowModel ViewModel { get; set; }

        public StatusWindowControl()
        {
            _authenticationService = CodesceneReeinventTestPackage.GetService<IAuthenticationService>();
            _mDFileHandler = CodesceneReeinventTestPackage.GetService<IMDFileHandler>();
            _fileDownloader = CodesceneReeinventTestPackage.GetService<IFileDownloader>();

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

        private async void OpenFileDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            await _fileDownloader.HandleAsync();
        }
    }
}