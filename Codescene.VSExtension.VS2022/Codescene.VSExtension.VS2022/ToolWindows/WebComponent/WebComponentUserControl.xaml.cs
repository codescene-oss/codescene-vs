using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
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
    // Use process ID and view type to make host unique per VS instance and view type
    // This prevents conflicts when multiple instances are open
    private static string GetHost(string view) => $"myapp-{System.Diagnostics.Process.GetCurrentProcess().Id}-{view}.local";
    private string _host;
    private const string STYLE_ELEMENT_ID = "cs-theme-vars";
    private bool _initialized = false;
    private string _pendingMessage = null;
    private static readonly string[] AllowedDomains =
    {
        "https://refactoring.com",
        "https://en.wikipedia.org",
        "https://codescene.io",
        "https://codescene.com",
        "https://blog.ploeh.dk/2018/08/27/on-constructor-over-injection/",
        "https://supporthub.codescene.com"
    };

	public WebComponentUserControl(WebComponentPayload<AceComponentData> payload, ILogger logger)
	{
		_logger = logger;
		InitializeComponent();
		Initialize(payload, payload.View);
	}

	public WebComponentUserControl(WebComponentPayload<CodeSmellDocumentationComponentData> payload, ILogger logger)
    {
        _logger = logger;
        InitializeComponent();
        Initialize(payload, payload.View);
    }

    public WebComponentUserControl(WebComponentPayload<CodeHealthMonitorComponentData> payload, ILogger logger)
    {
        _logger = logger;
        InitializeComponent();
        Initialize(payload, payload.View);
    }

    private void Initialize<T>(T payload, string view)
    {
        OnThemeChanged(null);
        _ = InitializeWebView2Async(payload, view);
        VSColorTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(ThemeChangedEventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var vsColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
        webView.DefaultBackgroundColor = Color.FromArgb(vsColor.A, vsColor.R, vsColor.G, vsColor.B);

        _ = ApplyThemeToWebViewAsync();
    }

    /// <summary>
    /// Applies the current Visual Studio theme as CSS variables into the WebView DOM.
    /// Replaces any previously injected style element with the same ID.
    /// </summary>
    private async Task ApplyThemeToWebViewAsync()
    {
        if (webView.CoreWebView2 == null) return;

        var css = GenerateCssVariablesFromTheme().Replace("`", "\\`");

        string script = $@"
        (function() {{
            const existing = document.getElementById('{STYLE_ELEMENT_ID}');
            if (existing) {{
                existing.remove();
            }}
            const style = document.createElement('style');
            style.id = '{STYLE_ELEMENT_ID}';
            style.textContent = `{css}`;
            document.head.appendChild(style);
        }})();
        ";

        await webView.CoreWebView2.ExecuteScriptAsync(script);
    }

    /// <summary>
    /// Generates an initialization script for setting context and injecting theme CSS into the WebView DOM.
    /// </summary>
    private async Task<string> GenerateInitialScriptAsync<T>(T payload)
    {
        const string template = $@"
        function setContext() {{
            window.ideContext = %ideContext%;
            const css = `%cssVars%`;
            const style = document.createElement('style');
            style.id = '{STYLE_ELEMENT_ID}';
            style.textContent = css;
            document.head.appendChild(style);
        }}
        setContext();
        ";

        var settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            Formatting = Formatting.None
        };

        var ideContext = JsonConvert.SerializeObject(payload, settings);

        if (!ThreadHelper.CheckAccess()) await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var cssVars = GenerateCssVariablesFromTheme();

        var script = template
           .Replace("%ideContext%", ideContext)
           .Replace("%cssVars%", cssVars.Replace("`", "\\`"));

        return script;
    }

    /// <summary>
    /// Gets a CSS string defining theme variables based on the current Visual Studio color theme.
    /// These variables are used for styling elements inside the WebView to match the IDE appearance.
    /// </summary>
    private static string GenerateCssVariablesFromTheme() => StyleHelper.GenerateCssVariablesFromTheme();


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
        
        webView.NavigationCompleted += (_, args) =>
        {
            loadingOverlay.Visibility = System.Windows.Visibility.Collapsed;
        };

        webView.CoreWebView2.WebMessageReceived += (object sender, CoreWebView2WebMessageReceivedEventArgs e) =>
        {
            _ = OnWebMessageReceivedAsync(sender, e);
        };
        
        var exePath = Assembly.GetExecutingAssembly().Location;
        var exeFolder = Path.GetDirectoryName(exePath);
        string localFolder = Path.Combine(exeFolder, FOLDER_LOCATION);

        _host = GetHost(view);
        webView.CoreWebView2.SetVirtualHostNameToFolderMapping(_host, localFolder, CoreWebView2HostResourceAccessKind.Allow);

        // Generate and inject the initialization script BEFORE navigation
        // This ensures window.ideContext is set before React components initialize and check for it
        // AddScriptToExecuteOnDocumentCreatedAsync runs the script before any page scripts execute
        var initialScript = await GenerateInitialScriptAsync(payload);
        await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initialScript);

        webView.Source = new Uri($"https://{_host}/index.html");
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
        // Allow navigation to our local app host
        if (!string.IsNullOrEmpty(_host) && uri.Equals($"https://{_host}/index.html", StringComparison.OrdinalIgnoreCase)) 
            return;

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
                SendTelemetry(uri);
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

    public async Task UpdateViewAsync<T>(T message)
    {
        try
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
                Formatting = Formatting.None
            };
            var messageString = JsonConvert.SerializeObject(message, settings);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            
            if (!_initialized)
            {
                // Save the last message if webview is not yet initialized
                _pendingMessage = messageString;
                _logger.Debug("Webview not initialized, queuing message.");
                return;
            }

            webView.CoreWebView2?.PostWebMessageAsJson(messageString);
        }
        catch (Exception e)
        {
            _logger.Error("Could not update webview.", e);
        }
    }

    /// <summary>
    /// Marks the webview as initialized and processes any pending message.
    /// Called when CWF sends an 'init' message.
    /// </summary>
    public async Task MarkAsInitializedAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        
        _initialized = true;
        
        if (_pendingMessage != null)
        {
            _logger.Debug("Webview initialized, sending pending message.");
            try
            {
                webView.CoreWebView2?.PostWebMessageAsJson(_pendingMessage);
                _pendingMessage = null;
            }
            catch (Exception e)
            {
                _logger.Error("Could not send pending message after initialization.", e);
            }
        }
    }

    private void SendTelemetry(string uri)
    {
        Task.Run(async () =>
        {
            var additionalData = new Dictionary<string, object>
            {
                { "url", uri }
            };

            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            telemetryManager.SendTelemetry(Constants.Telemetry.OPEN_LINK, additionalData);
        }).FireAndForget();
    }
}
