using CodesceneReeinventTest.Application.Helpers;
using CodesceneReeinventTest.Application.Services.Authentication;
using CodesceneReeinventTest.ToolWindows.Status;
using EnvDTE;
using Markdig;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CodesceneReeinventTest.ToolWindows.Markdown;
using Community.VisualStudio.Toolkit;
using System.ComponentModel.Design;
using Community.VisualStudio.Toolkit.DependencyInjection;
using Microsoft.VisualStudio.TextManager.Interop;
using EnvDTE80;

namespace CodesceneReeinventTest
{
    public partial class StatusWindowControl : UserControl
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly DIToolkitPackage _serviceProvider;
        public StatusWindowModel ViewModel { get; set; }
       
        public StatusWindowControl(IAuthenticationService authenticationService, DIToolkitPackage serviceProvider)
        {
            _authenticationService = authenticationService;
            _serviceProvider = serviceProvider;
            //fire event on settings change
            General.Saved += OnSettingsSaved;
            this.InitializeComponent();
            //initalize view model of control
            var i = _authenticationService.IsLoggedIn();
            ViewModel = new StatusWindowModel
            { 
                CodeHealthActivated = General.Instance.PreviewCodeHealthGate,
                IsLoggedIn = _authenticationService.IsLoggedIn()
            };
            this.DataContext = ViewModel;
        }
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            VS.MessageBox.Show("StatusWindowControl", "Button clicked");
        }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true; // Prevents the default handling of the event
        }
        //change the property based on fired event
        private void OnSettingsSaved(General obj)
        {
            ViewModel.CodeHealthActivated = obj.PreviewCodeHealthGate;
        }

        private async void OpenMarkdownButton_Click(object sender, RoutedEventArgs e)
        {
            await OpenMarkdownFileAsync("bumpy-road-ahead");
        }
        private async Task OpenMarkdownFileAsync(string fileName)
        {
            var markdownCommand = new OpenMarkdownWindowCommand(_serviceProvider, fileName);
            await markdownCommand.OpenAsync(null);
        }


    }
}