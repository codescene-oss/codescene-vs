using System.Diagnostics;
using System.Windows.Controls;

namespace CodesceneReeinventTest.Controls
{
    /// <summary>
    /// Interaction logic for LinesOfCodeInASingleFile.xaml
    /// </summary>
    public partial class LinesOfCodeInASingleFile : UserControl
    {
        public LinesOfCodeInASingleFile()
        {
            InitializeComponent();
        }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true; // Prevents the default handling of the event
        }
    }
}
