using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace CodesceneReeinventTest.ToolWindows.Spike
{
    /// <summary>
    /// Interaction logic for SpikeWIndowControl.xaml
    /// </summary>
    public partial class SpikeWIndowControl : UserControl
    {
        public SpikeWIndowControl()
        {
            InitializeComponent();
            InitializeWebView2Async();
        }

        private async void InitializeWebView2Async()
        {
            string localAppDataPath = Path.Combine(
                System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyExtensionName", // ili neko ime vašeg projekta
                "WebView2Cache"
            );

            Directory.CreateDirectory(localAppDataPath);

            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: localAppDataPath
            );

            await webView.EnsureCoreWebView2Async(env);

            webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            var exePath = Assembly.GetExecutingAssembly().Location;
            var exeFolder = Path.GetDirectoryName(exePath);
            string localFolder = Path.Combine(exeFolder, "toolwindows/spike");

            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "myapp.local",
                localFolder,
                CoreWebView2HostResourceAccessKind.Allow
            );

            webView.Source = new System.Uri("https://myapp.local/index.html");
        }


        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            txtBlockMessage.Text = JsonConvert.DeserializeObject<MessageObj<string>>(e.WebMessageAsJson).Data;
            System.Diagnostics.Debug.WriteLine($"Message sent from web view:{e.WebMessageAsJson}");
        }

        private void BtnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            var obj = new MessageObj<string[]>();
            obj.Command = "renderNames";
            obj.Data = ["name1", "name2", "name3"];
            string jsonString = JsonConvert.SerializeObject(obj);
            webView.CoreWebView2.PostWebMessageAsJson(jsonString);
        }
    }

    class MessageObj<T>
    {
        public string Command { get; set; }
        public T Data { get; set; }
    }
}