using Codescene.VSExtension.Core.Models.WebComponent;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
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

    public WebComponentUserControl(WebComponentPayload payload)
    {
        InitializeComponent();
        _ = InitializeWebView2Async(payload);
    }

    private string GenerateInitialScript(WebComponentPayload payload)
    {
        const string template = "function setContext() { window.ideContext = %ideContext% }; setContext();";

        var settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            Formatting = Formatting.None
        };
        var ideContext = JsonConvert.SerializeObject(payload, settings);

        var script = template.Replace("%ideContext%", ideContext);

        return script;
    }

    private async Task<CoreWebView2Environment> CreatePerWindowEnvAsync(string view)
    {
        string cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyExtensionName", $"WebView2Cache_{view}");
        return await CoreWebView2Environment.CreateAsync(userDataFolder: cachePath);
    }


    private const string HOST = "myapp.local";
    private async Task InitializeWebView2Async(WebComponentPayload payload)
    {
        var env = await CreatePerWindowEnvAsync(payload.View);

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

        await webView.CoreWebView2.ExecuteScriptAsync(GenerateInitialScript(payload));
    }

    private async Task OnWebMessageReceivedAsync(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var handler = new WebComponentMessageHandler(this);
        await handler.HandleAsync(e.WebMessageAsJson);
    }

    //webView.CoreWebView2.PostWebMessageAsJson(jsonString);
}
