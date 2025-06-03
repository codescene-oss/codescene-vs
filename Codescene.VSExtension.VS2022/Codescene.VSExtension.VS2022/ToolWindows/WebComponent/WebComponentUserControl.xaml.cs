using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
/// <summary>
/// Interaction logic for WebComponentUserControl.xaml
/// </summary>
public partial class WebComponentUserControl : UserControl
{
    private ILogger _logger;
    public Func<Task> CloseRequested;
    private const string FOLDER_LOCATION = @"ToolWindows\WebComponent";
    private const string HOST = "myapp.local";
    private static readonly string[] AllowedDomains =
    {
        "https://refactoring.com",
        "https://en.wikipedia.org",
        "https://codescene.io",
        "https://codescene.com",
        "https://blog.ploeh.dk/2018/08/27/on-constructor-over-injection/"
    };

    public WebComponentUserControl(WebComponentPayload payload, ILogger logger)
    {
        _logger = logger;
        InitializeComponent();
        Initialize(payload, payload.View);
    }

    public WebComponentUserControl(ShowDocsPayload payload, ILogger logger)
    {
        _logger = logger;
        InitializeComponent();
        Initialize(payload, payload.View);
    }

    private void Initialize<T>(T payload, string view)
    {
        SetWebViewBackground(null);
        _ = InitializeWebView2Async(payload, view);
        VSColorTheme.ThemeChanged += SetWebViewBackground;
    }

    private void SetWebViewBackground(ThemeChangedEventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var vsColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
        webView.DefaultBackgroundColor = Color.FromArgb(vsColor.A, vsColor.R, vsColor.G, vsColor.B);
    }

    private string GenerateInitialScript<T>(T payload)
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


    private async Task InitializeWebView2Async<T>(T payload, string view)
    {
        var env = await CreatePerWindowEnvAsync(view);

        await webView.EnsureCoreWebView2Async(env);

        webView.CoreWebView2.NavigationStarting += HandleNavigationStarting;
        webView.NavigationCompleted += (_, __) => loadingOverlay.Visibility = System.Windows.Visibility.Collapsed;

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

    /// <summary>
    /// This method enforces navigation rules for URLs:
    /// <list type="bullet">
    ///   <item>
    ///     <description>Allows navigation within the embedded WebView for the local app domain (e.g., https://myapp.local/index.html).</description>
    ///   </item>
    ///   <item>
    ///     <description>Opens allowed external domains in the user's default external browser.</description>
    ///   </item>
    ///   <item>
    ///     <description>Blocks and cancels any navigation attempts to disallowed external domains to prevent unwanted or potentially unsafe content.</description>
    ///   </item>
    /// </list>
    /// </summary>
    private void HandleNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs args)
    {
        var uri = args.Uri;
        if (uri.Equals($"https://{HOST}/index.html")) return;

        bool isExternalNavigationAllowed = AllowedDomains.Any(domain => uri.StartsWith(domain, StringComparison.OrdinalIgnoreCase));
        if (isExternalNavigationAllowed)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri,
                    UseShellExecute = true
                });

                args.Cancel = true;
                _logger.Info($"Opened link '{uri}' in external browser.");
            }
            catch (Exception ex)
            {
                _logger.Error($"Could not open external link: {uri}", ex);
            }
        }
        else
        {
            args.Cancel = true;
            _logger.Info($"Blocked navigation to disallowed link '{uri}'.");
        }
    }

    private async Task OnWebMessageReceivedAsync(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var handler = new WebComponentMessageHandler(this);
        await handler.HandleAsync(e.WebMessageAsJson);
    }

    public void UpdateView<T>(T message)
    {
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            Formatting = Formatting.None
        };
        var messageString = JsonConvert.SerializeObject(message, settings);

        webView.CoreWebView2.PostWebMessageAsJson(messageString);
    }
}
