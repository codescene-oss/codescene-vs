using System.Windows;
using System.Windows.Controls;

namespace CodesceneReeinventTest;
public partial class ProblemsWindowControl : UserControl
{
    public ProblemsWindowControl()
    {
        InitializeComponent();
    }

    private void button1_Click(object sender, RoutedEventArgs e)
    {
        VS.MessageBox.Show("ProblemsWindowControl", "Button clicked");
    }
}
