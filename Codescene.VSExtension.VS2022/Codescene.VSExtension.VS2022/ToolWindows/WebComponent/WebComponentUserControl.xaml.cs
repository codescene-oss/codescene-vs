using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
/// <summary>
/// Interaction logic for WebComponentUserControl.xaml
/// </summary>
public partial class WebComponentUserControl : UserControl
{
    private const string FOLDER_LOCATION = @"ToolWindows\WebComponent";

    public WebComponentUserControl(string view, string data = "{}")
    {
        InitializeComponent();
        _ = InitializeWebView2Async(view: view, data: data);
    }

    private string GenerateInitialScript(string view, string data)
    {
        const string expression = "function setContext() { window.ideContext = { ideType:'Visual Studio', view:'viewType', data:dataObject } }; setContext();";
        return expression.Replace("viewType", view).Replace("dataObject", data);
    }

    private async Task InitializeWebView2Async(string view, string data)
    {
        string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyExtensionName", "WebView2Cache");

        Directory.CreateDirectory(localAppDataPath);

        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: localAppDataPath);

        await webView.EnsureCoreWebView2Async(env);

        webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        var exePath = Assembly.GetExecutingAssembly().Location;
        var exeFolder = Path.GetDirectoryName(exePath);
        string localFolder = Path.Combine(exeFolder, FOLDER_LOCATION);

        webView.CoreWebView2.SetVirtualHostNameToFolderMapping("myapp.local", localFolder, CoreWebView2HostResourceAccessKind.Allow);

        webView.Source = new Uri("https://myapp.local/index.html");

        await webView.CoreWebView2.ExecuteScriptAsync(GenerateInitialScript(view: view, data: data));
    }

    private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var data = JsonConvert.DeserializeObject<MessageObj<string>>(e.WebMessageAsJson).Data;
        System.Diagnostics.Debug.WriteLine($"Message sent from web view:{e.WebMessageAsJson}");
    }
}

class MessageObj<T>
{
    public string Command { get; set; }
    public T Data { get; set; }
}
