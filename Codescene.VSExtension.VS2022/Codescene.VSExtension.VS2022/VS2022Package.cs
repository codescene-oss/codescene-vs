using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.VS2022.Application.ErrorHandling;
using Codescene.VSExtension.VS2022.DocumentEventsHandler;
using Codescene.VSExtension.VS2022.ToolWindows.Markdown;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using CodeSceneConstants = Codescene.VSExtension.Core.Application.Services.Util.Constants;
using Task = System.Threading.Tasks.Task;

namespace Codescene.VSExtension.VS2022;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(PackageGuids.CodesceneExtensionString)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
//[ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]

[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Codescene", "General", 0, 0, true, SupportsProfiles = true)]

[ProvideToolWindow(typeof(MarkdownWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer)]
[ProvideToolWindow(typeof(CodeSmellDocumentationWindow.Pane), Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer, Transient = true)]
public sealed class VS2022Package : ToolkitPackage
{
    public static VS2022Package Instance { get; private set; }

    private const string SettingsCollection = "CodeSceneExtension";
    private const string AcceptedTermsProperty = "AcceptedTerms";

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        Instance = this;

        try
        {
            // Logging
            await InitializeLoggerPaneAsync();

            // Tool windows
            this.RegisterToolWindows();

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // ToC pop-up
            //await ShowTermsIfNeededAsync();

            // Commands
            await this.RegisterCommandsAsync();

            // Cli file
            await CheckCliFileAsync();

            // Subscribe on active document change event
            await SubscribeOnActiveWindowChangeAsync();

            // Subscribe to solution events handler
            await InitializeSolutionEventsHandlerAsync();

            SendTelemetry(CodeSceneConstants.Telemetry.ON_ACTIVATE_EXTENSION);
        }
        catch (Exception e)
        {
            // Note: we may not be able to report every failure via telemetry
            // (e.g. if the extension hasn't fully loaded or the CLI hasn't been downloaded yet).

            System.Diagnostics.Debug.Fail($"VS2022Package.InitializeAsync failed for CodeScene Extension: {e}");
            SendTelemetry(CodeSceneConstants.Telemetry.ON_ACTIVATE_EXTENSION_ERROR);
        }
    }

    async Task InitializeSolutionEventsHandlerAsync()
    {
        await new SolutionEventsHandler(this, this).InitializeAsync();
    }

    #region Terms and Conditions
    private bool GetAcceptedTerms()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var settingsManager = new ShellSettingsManager(this);
        var store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

        if (!store.CollectionExists(SettingsCollection))
            store.CreateCollection(SettingsCollection);

        return store.PropertyExists(SettingsCollection, AcceptedTermsProperty) &&
               store.GetBoolean(SettingsCollection, AcceptedTermsProperty);
    }

    private void SetAcceptedTerms(bool value)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var settingsManager = new ShellSettingsManager(this);
        var store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

        if (!store.CollectionExists(SettingsCollection))
            store.CreateCollection(SettingsCollection);

        store.SetBoolean(SettingsCollection, AcceptedTermsProperty, value);
    }

    private async Task ShowTermsIfNeededAsync()
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);

        if (GetAcceptedTerms())
            return;

        if (GetService(typeof(SVsInfoBarUIFactory)) is not IVsInfoBarUIFactory factory)
            return;

        IVsInfoBarActionItem[] actionItems =
        [
          new InfoBarButton("Accept"),
          new InfoBarButton("Decline"),
          new InfoBarHyperlink("View Terms & Policies")
        ];

        var model = new InfoBarModel(
            [new InfoBarTextSpan("By using this extension you agree to CodeScene's Terms and Privacy Policy")],
            actionItems,
            KnownMonikers.StatusInformation,
            isCloseButtonVisible: false
        );

        var uiElement = factory.CreateInfoBar(model);
        if (uiElement == null)
            return;

        var events = new InfoBarEvents(this);
        uiElement.Advise(events, out _);

        // Show it in the main VS shell window's InfoBar host
        if (GetService(typeof(SVsShell)) is IVsShell shell)
        {
            if (shell is IVsInfoBarHost host)
            {
                host.AddInfoBar(uiElement);
            }
            else
            {
                // Alternatively, attach to the main window frame
                if (GetService(typeof(SVsShell)) is IVsShell vsShell &&
                    vsShell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var obj) == VSConstants.S_OK &&
                    obj is IVsInfoBarHost mainHost)
                {
                    mainHost.AddInfoBar(uiElement);
                }
            }
        }


        Task.Run(async () =>
        {
            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            telemetryManager.SendTelemetry(CodeSceneConstants.Telemetry.TERMS_AND_POLICIES_SHOWN);
        }).FireAndForget();
    }

    private class InfoBarEvents : IVsInfoBarUIEvents
    {
        private readonly VS2022Package _package;

        public InfoBarEvents(VS2022Package package) => _package = package;

        public void OnActionItemClicked(IVsInfoBarUIElement element, IVsInfoBarActionItem actionItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Task.Run(async () =>
            {
                var additionalData = new Dictionary<string, object>
                {
                    { "selection", actionItem.Text }
                };
                var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
                telemetryManager.SendTelemetry(CodeSceneConstants.Telemetry.TERMS_AND_POLICIES_RESPONSE, additionalData);
            }).FireAndForget();

            switch (actionItem.Text)
            {
                case "Accept":
                    _package.SetAcceptedTerms(true);
                    element.Close();
                    break;
                case "Decline":
                    _package.SetAcceptedTerms(false);
                    System.Diagnostics.Debug.WriteLine("User declined Terms & Conditions.");
                    element.Close();
                    break;
                case "View Terms & Policies":
                    System.Diagnostics.Process.Start("https://codescene.com/policies");
                    break;
            }
        }

        public void OnClosed(IVsInfoBarUIElement element) { }
    }
    #endregion

    async Task<T> GetServiceAsync<T>()
    {
        if (await GetServiceAsync(typeof(SComponentModel)) is IComponentModel componentModel)
        {
            return componentModel.DefaultExportProvider.GetExportedValue<T>();
        }

        throw new Exception($"Can not find component {nameof(T)}");
    }

    private void SendTelemetry(string eventName)
    {
        Task.Run(async () =>
        {
            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            telemetryManager.SendTelemetry(eventName);
        }).FireAndForget();
    }

    async Task InitializeLoggerPaneAsync()
    {
        var logPane = await GetServiceAsync<OutputPaneManager>();
        await logPane.InitializeAsync();
    }

    async Task CheckCliFileAsync()
    {
        var cliFileChecker = await GetServiceAsync<ICliFileChecker>();
        await cliFileChecker.Check();
    }

    async Task SubscribeOnActiveWindowChangeAsync()
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);

        if (await GetServiceAsync(typeof(SDTE)) is DTE dte)
        {
            var handler = await GetServiceAsync<OnActiveWindowChangeHandler>();
            dte.Events.WindowEvents.WindowActivated += handler.Handle;
        }
    }
}
