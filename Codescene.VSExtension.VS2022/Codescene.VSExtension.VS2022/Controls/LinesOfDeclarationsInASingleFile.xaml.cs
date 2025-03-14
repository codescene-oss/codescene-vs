using System.Diagnostics;
using System.Windows.Controls;

namespace Codescene.VSExtension.VS2022.Controls
{
    /// <summary>
    /// Interaction logic for LinesOfDeclarationsInASingleFile.xaml
    /// </summary>
    public partial class LinesOfDeclarationsInASingleFile : UserControl
    {
        public LinesOfDeclarationsInASingleFile()
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
