using CodesceneReeinventTest.Application.Services.Authentication;
using CodesceneReeinventTest.ToolWindows.Status;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

namespace CodesceneReeinventTest
{
    public partial class StatusWindowControl : UserControl
    {
        private static IAuthenticationService _authenticationService;
        public StatusWindowModel ViewModel { get; set; }

        public static void SetAuthenticationService(IAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }
        public StatusWindowControl()
        {
            //fire event on settings change
            General.Saved += OnSettingsSaved;
            this.InitializeComponent();
            //initačize view model of control
            ViewModel = new StatusWindowModel
            {
                CodeHealthActivated = General.Instance.PreviewCodeHealthGate,
                IsLoggedIn = true// _authenticationService.IsLoggedIn()
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
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true; // Prevents the default handling of the event
        }
        //change the property based on fired event
        private void OnSettingsSaved(General obj)
        {
            ViewModel.CodeHealthActivated = obj.PreviewCodeHealthGate;
        }
    }
}