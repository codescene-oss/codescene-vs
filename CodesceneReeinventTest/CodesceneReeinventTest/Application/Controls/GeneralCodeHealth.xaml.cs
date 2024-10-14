﻿using System.Diagnostics;
using System.Windows.Controls;

namespace CodesceneReeinventTest.Application.Controls
{
    /// <summary>
    /// Interaction logic for GeneralCodeHealth.xaml
    /// </summary>
    public partial class GeneralCodeHealth : UserControl
    {
        public GeneralCodeHealth()
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
