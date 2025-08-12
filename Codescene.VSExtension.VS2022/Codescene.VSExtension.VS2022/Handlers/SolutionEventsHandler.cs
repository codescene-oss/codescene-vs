using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Handlers;

public class SolutionEventsHandler : IVsSolutionEvents, IDisposable
{
    private readonly AsyncPackage _package;
    private readonly IServiceProvider _serviceProvider;
    private uint _cookie;
    private IVsSolution _solution;

    private const string SettingsCollection = "CodeSceneExtension";
    private const string AcceptedTermsProperty = "AcceptedTerms";

    public SolutionEventsHandler(AsyncPackage package, IServiceProvider serviceProvider)
    {
        _package = package;
        _serviceProvider = serviceProvider;
    }

    public async Task InitializeAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        _solution = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));
        _solution.AdviseSolutionEvents(this, out _cookie);
    }

    public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
    {
        //_package.JoinableTaskFactory.RunAsync(ShowTermsIfNeededAsync).FireAndForget();
        Task.Run(() =>
        {
            ShowTermsIfNeededAsync();
        }).FireAndForget();

        return VSConstants.S_OK;
    }

    private bool GetAcceptedTerms()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var settingsManager = new ShellSettingsManager(_serviceProvider);
        var store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

        if (!store.CollectionExists(SettingsCollection))
            store.CreateCollection(SettingsCollection);

        return store.PropertyExists(SettingsCollection, AcceptedTermsProperty) &&
               store.GetBoolean(SettingsCollection, AcceptedTermsProperty);
    }

    private void SetAcceptedTerms(bool value)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var settingsManager = new ShellSettingsManager(_serviceProvider);
        var store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

        if (!store.CollectionExists(SettingsCollection))
            store.CreateCollection(SettingsCollection);

        store.SetBoolean(SettingsCollection, AcceptedTermsProperty, value);
    }

    private async Task ShowTermsIfNeededAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        if (GetAcceptedTerms())
            return;

        if (_serviceProvider.GetService(typeof(SVsInfoBarUIFactory)) is not IVsInfoBarUIFactory factory)
            return;

        IVsInfoBarActionItem[] actionItems =
        {
            new InfoBarButton("Accept"),
            new InfoBarButton("Decline"),
            new InfoBarHyperlink("View Terms && Policies")
        };

        var model = new InfoBarModel(
            [new InfoBarTextSpan("By using this extension you agree to CodeScene's Terms and Privacy Policy")],
            actionItems,
            KnownMonikers.StatusInformation,
            isCloseButtonVisible: false
        );

        var uiElement = factory.CreateInfoBar(model);
        if (uiElement == null)
            return;

        uiElement.Advise(new InfoBarEvents(SetAcceptedTerms), out _);

        if (_serviceProvider.GetService(typeof(SVsShell)) is IVsShell vsShell &&
            vsShell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var obj) == VSConstants.S_OK &&
            obj is IVsInfoBarHost mainHost)
        {
            mainHost.AddInfoBar(uiElement);
        }
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_solution != null && _cookie != 0)
        {
            _solution.UnadviseSolutionEvents(_cookie);
            _cookie = 0;
        }
    }

    private class InfoBarEvents : IVsInfoBarUIEvents
    {
        private readonly Action<bool> _setAcceptedTerms;

        public InfoBarEvents(Action<bool> setAcceptedTerms) => _setAcceptedTerms = setAcceptedTerms;

        public void OnActionItemClicked(IVsInfoBarUIElement element, IVsInfoBarActionItem actionItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            switch (actionItem.Text)
            {
                case "Accept":
                    _setAcceptedTerms(true);
                    element.Close();
                    break;
                case "Decline":
                    _setAcceptedTerms(false);
                    System.Diagnostics.Debug.WriteLine("User declined Terms & Conditions.");
                    element.Close();
                    break;
                case "View Terms & Policies":
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://codescene.com/policies",
                        UseShellExecute = true
                    });
                    break;
            }
        }

        public void OnClosed(IVsInfoBarUIElement element) { }
    }

    // The remaining event methods are currently unused, but required by the IVsSolutionEvents interface.
    public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) => VSConstants.S_OK;

    public int OnAfterCloseSolution(object pUnkReserved) => VSConstants.S_OK;

    public int OnBeforeCloseSolution(object pUnkReserved) => VSConstants.S_OK;

    public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;

    public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.S_OK;

    public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => VSConstants.S_OK;

    public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;

    public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.S_OK;

    public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;
}