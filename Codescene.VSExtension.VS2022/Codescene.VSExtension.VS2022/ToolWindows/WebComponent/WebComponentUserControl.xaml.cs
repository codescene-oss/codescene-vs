using Microsoft.Web.WebView2.Core;
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
    public Func<Task> CloseRequested;
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

    private async Task<CoreWebView2Environment> CreatePerWindowEnvAsync(string view)
    {
        string cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyExtensionName", $"WebView2Cache_{view}");
        return await CoreWebView2Environment.CreateAsync(userDataFolder: cachePath);
    }


    private const string HOST = "myapp.local";
    private async Task InitializeWebView2Async(string view, string data)
    {
        var env = await CreatePerWindowEnvAsync(view);

        await webView.EnsureCoreWebView2Async(env);

        webView.CoreWebView2.WebMessageReceived += (object sender, CoreWebView2WebMessageReceivedEventArgs e) =>
        {
            _ = OnWebMessageReceivedAsync(sender, e);
        };
        var exePath = Assembly.GetExecutingAssembly().Location;
        var exeFolder = Path.GetDirectoryName(exePath);
        string localFolder = Path.Combine(exeFolder, FOLDER_LOCATION);

        webView.CoreWebView2.SetVirtualHostNameToFolderMapping(HOST, localFolder, CoreWebView2HostResourceAccessKind.Allow);

        webView.Source = new Uri($"https://{HOST}/index.html");

        await webView.CoreWebView2.ExecuteScriptAsync(GenerateInitialScript(view: view, data: data));
    }

    private async Task OnWebMessageReceivedAsync(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var handler = new WebComponentMessageHandler(this);
        await handler.HandleAsync(e.WebMessageAsJson);
    }
}
