// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.VS2022.E2ETests;

internal sealed class VisualStudioTestHost : IDisposable
{
    private readonly Process _process;
    private readonly Application _application;
    private readonly UIA3Automation _automation;
    private readonly object _dteObject;
    private readonly E2EWorkspace _workspace;
    private bool _disposed;

    private VisualStudioTestHost(
        Process process,
        Application application,
        UIA3Automation automation,
        Window mainWindow,
        object dteObject,
        E2EWorkspace workspace,
        string artifactsDirectory,
        string activityLogPath)
    {
        _process = process;
        _application = application;
        _automation = automation;
        _dteObject = dteObject;
        _workspace = workspace;
        MainWindow = mainWindow;
        ArtifactsDirectory = artifactsDirectory;
        ActivityLogPath = activityLogPath;
    }

    public string ArtifactsDirectory { get; }

    public string ActivityLogPath { get; }

    public string WorkspaceRoot => _workspace.RootDirectory;

    public string OpenedSolutionPath => _workspace.SolutionPath;

    public Window MainWindow { get; }

    public static VisualStudioTestHost Start(string scenarioName = "MinimalScenario")
    {
        /*
        if (!E2ETestEnvironment.IsEnabled())
        {
            Assert.Inconclusive($"Set {E2ETestEnvironment.EnableVariableName}=true to run the e2e suite.");
        }
        */

        E2EWorkspace? workspace = null;
        try
        {
            var devenvPath = E2ETestEnvironment.FindDevenvPath();
            var vsixPath = E2ETestEnvironment.FindVsixPath();
            var artifactsDirectory = E2ETestEnvironment.CreateArtifactsDirectory();
            var activityLogPath = Path.Combine(artifactsDirectory, "ActivityLog.xml");

            ExperimentalInstanceDeploy.EnsureExperimentalInstanceClosed();
            ExperimentalInstanceDeploy.ResetExperimentalHive();
            ExperimentalInstanceDeploy.DeployVsixToExperimentalInstance(vsixPath, devenvPath);
            ExperimentalInstanceDeploy.SuppressCopilotFreePromoNotification();

            workspace = E2EWorkspace.Create(scenarioName);

            var (process, application, automation, mainWindow, dteObject) = LaunchAndAttachExperimentalVisualStudio(
                devenvPath,
                workspace.SolutionPath,
                activityLogPath);

            var host = new VisualStudioTestHost(
                process,
                application,
                automation,
                mainWindow,
                dteObject,
                workspace,
                artifactsDirectory,
                activityLogPath);
            workspace = null;

            host.WaitForShellStabilization();
            return host;
        }
        catch (FileNotFoundException ex)
        {
            workspace?.Dispose();
            Assert.Inconclusive(ex.Message);
            throw;
        }
        catch
        {
            workspace?.Dispose();
            throw;
        }
    }

    public int CountElementsByName(string name)
    {
        return _automation.GetDesktop().FindAllDescendants(cf => cf.ByName(name)).Length;
    }

    public void InvokeCommand(string commandSetGuid, int commandId)
    {
        WaitForSuccess(
            () =>
            {
                dynamic dte = _dteObject;
                dte.Commands.Raise(commandSetGuid, commandId, null, null);
            },
            E2ETestEnvironment.UiTimeout,
            $"Could not execute command {commandSetGuid}/{commandId}.");
    }

    public void WaitForElementByName(string name)
    {
        var element = WaitForElement(name, E2ETestEnvironment.UiTimeout);
        Assert.IsNotNull(element, $"Could not find a UI element named '{name}'.");
    }

    public void WaitForElementCountToIncrease(string name, int initialCount)
    {
        var success = WaitUntil(
            () => CountElementsByName(name) > initialCount,
            E2ETestEnvironment.UiTimeout);
        Assert.IsTrue(success, $"Timed out waiting for the number of '{name}' elements to increase from {initialCount}.");
    }

    public void DismissTransientUi()
    {
        Keyboard.Press(VirtualKeyShort.ESCAPE);
        Keyboard.Release(VirtualKeyShort.ESCAPE);
        Thread.Sleep(TimeSpan.FromMilliseconds(500));
    }

    public void AssertNoCodeSceneErrorsInActivityLog()
    {
        var ready = WaitUntil(() => File.Exists(ActivityLogPath), E2ETestEnvironment.UiTimeout);
        Assert.IsTrue(ready, $"Activity log was not created at '{ActivityLogPath}'.");

        var document = XDocument.Load(ActivityLogPath);
        var failures = document
            .Descendants("entry")
            .Where(entry => string.Equals((string?)entry.Element("type"), "Error", StringComparison.OrdinalIgnoreCase))
            .Where(EntryContainsCodeScene)
            .Select(entry => string.Join(" | ", entry.Elements().Select(element => element.Value.Trim())))
            .ToArray();

        if (failures.Length > 0)
        {
            Assert.Fail("Found CodeScene-related errors in the Visual Studio activity log:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
        }
    }

    public void CaptureScreenshot(string fileName)
    {
        var sanitized = string.Concat(fileName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        using var image = Capture.Element(_automation.GetDesktop());
        image.ToFile(Path.Combine(ArtifactsDirectory, sanitized + ".png"));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            try
            {
                dynamic dte = _dteObject;
                dte.Quit();
            }
            catch
            {
            }

            if (!_process.WaitForExit((int)TimeSpan.FromSeconds(20).TotalMilliseconds))
            {
                _process.CloseMainWindow();
            }

            if (!_process.WaitForExit((int)TimeSpan.FromSeconds(10).TotalMilliseconds) && !_process.HasExited)
            {
                _process.Kill();
            }
        }
        finally
        {
            _automation.Dispose();
            _application.Dispose();
            _process.Dispose();
            _workspace.Dispose();
            _disposed = true;
        }
    }

    private static (Process Process, Application Application, UIA3Automation Automation, Window MainWindow, object Dte)
        LaunchAndAttachExperimentalVisualStudio(string devenvPath, string solutionPath, string activityLogPath)
    {
        var devenvArguments =
            $"\"{solutionPath}\" /RootSuffix {E2ETestEnvironment.RootSuffix} /Log \"{activityLogPath}\"";
        var starterProcess = TestProcessRunner.StartProcess(devenvPath, devenvArguments);
        var starterApplication = Application.Attach(starterProcess);
        var automation = new UIA3Automation();
        var mainWindow = WaitForMainWindow(starterApplication, automation);
        var visualStudioProcessId = ReadProcessIdFromWindow(mainWindow);
        Process process;
        try
        {
            process = Process.GetProcessById(visualStudioProcessId);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                $"Could not open the Visual Studio process (pid {visualStudioProcessId} from UI Automation).", ex);
        }

        try
        {
            starterApplication.Dispose();
        }
        catch
        {
        }

        try
        {
            starterProcess.Dispose();
        }
        catch
        {
        }

        var application = Application.Attach(process);
        var dteObject = RunningObjectTableHelper.WaitForDte(visualStudioProcessId, E2ETestEnvironment.StartupTimeout);
        return (process, application, automation, mainWindow, dteObject);
    }

    private static bool EntryContainsCodeScene(XElement entry)
    {
        var value = string.Join(" ", entry.Elements().Select(element => element.Value));
        return value.IndexOf("CodeScene", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("Codescene.VSExtension", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Window WaitForMainWindow(Application application, UIA3Automation automation)
    {
        var deadline = DateTime.UtcNow + E2ETestEnvironment.StartupTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var window = application.GetMainWindow(automation, TimeSpan.FromSeconds(1));
            if (window != null)
            {
                return window;
            }

            var fallback = TryFindExperimentalVisualStudioWindow(automation);
            if (fallback != null)
            {
                return fallback;
            }
        }

        throw new TimeoutException("Timed out waiting for the Visual Studio main window.");
    }

    private static Window? TryFindExperimentalVisualStudioWindow(UIA3Automation automation)
    {
        foreach (var element in automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window)))
        {
            var name = element.Name ?? string.Empty;
            if (name.IndexOf("Microsoft Visual Studio", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (name.IndexOf("experimental", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            return element.AsWindow();
        }

        return null;
    }

    private static int ReadProcessIdFromWindow(Window mainWindow)
    {
        if (!mainWindow.Properties.ProcessId.IsSupported)
        {
            throw new InvalidOperationException(
                "The Visual Studio main window does not expose ProcessId. Cannot resolve the real devenv process (the launcher process may have already exited).");
        }

        return mainWindow.Properties.ProcessId.Value;
    }

    private static AutomationElement? FindCloseButtonForWindow(AutomationElement window)
    {
        var close = window.FindFirstDescendant(
            cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Close")));
        if (close == null || !close.IsAvailable)
        {
            close = window.FindFirstDescendant(cf => cf.ByName("Close"));
        }

        if (close == null || !close.IsAvailable)
        {
            return null;
        }

        return close;
    }

    private static bool TryInvokeCloseButton(AutomationElement close)
    {
        try
        {
            close.AsButton()?.Invoke();
            return true;
        }
        catch
        {
            try
            {
                close.Click();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static void WaitForSuccess(Action action, TimeSpan timeout, string failureMessage)
    {
        Exception? lastException = null;
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
            }
        }

        Assert.Fail($"{failureMessage} Last error: {lastException?.Message}");
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(500));
        }

        return false;
    }

    private AutomationElement? WaitForElement(string name, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var element = _automation.GetDesktop().FindFirstDescendant(cf => cf.ByName(name));
            if (element != null)
            {
                return element;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(500));
        }

        return null;
    }

    private void WaitForShellStabilization()
    {
        WaitUntil(() => !_process.HasExited, TimeSpan.FromSeconds(5));
        Thread.Sleep(TimeSpan.FromSeconds(3));
        DismissKnownBlockingDialogs();
        Thread.Sleep(TimeSpan.FromSeconds(7));
    }

    private void DismissKnownBlockingDialogs()
    {
        if (E2ETestEnvironment.ShouldSkipCopilotOnboardingDismiss())
        {
            return;
        }

        var idleIterations = 0;
        const int maxIdleIterations = 12;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);

        while (DateTime.UtcNow < deadline && idleIterations < maxIdleIterations)
        {
            var dismissed =
                TryDismissCopilotPopupUnderMainWindow() ||
                TryDismissCopilotFreeOnboarding() ||
                TryCloseCopilotWindowChrome();

            if (dismissed)
            {
                idleIterations = 0;
                Thread.Sleep(TimeSpan.FromMilliseconds(800));
                continue;
            }

            idleIterations++;
            Thread.Sleep(TimeSpan.FromMilliseconds(500));
        }
    }

    private bool TryDismissCopilotPopupUnderMainWindow()
    {
        var window = _application.GetMainWindow(_automation, TimeSpan.FromSeconds(10));
        if (window == null || !window.IsAvailable)
        {
            return false;
        }

        var maybeLater = window.FindFirstDescendant(cf => cf.ByName("Maybe later"));
        if (maybeLater != null && maybeLater.IsAvailable)
        {
            try
            {
                maybeLater.Focus();
                var button = maybeLater.AsButton();
                if (button != null)
                {
                    button.Invoke();
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                if (maybeLater.Patterns.Invoke.IsSupported)
                {
                    maybeLater.Patterns.Invoke.Pattern.Invoke();
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                maybeLater.Click();
                return true;
            }
            catch
            {
            }
        }

        foreach (var element in window.FindAllDescendants(cf => cf.ByControlType(ControlType.Window)))
        {
            var name = element.Name ?? string.Empty;
            if (name.IndexOf("Copilot", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var close = FindCloseButtonForWindow(element);
            if (close != null && TryInvokeCloseButton(close))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryDismissCopilotFreeOnboarding()
    {
        var root = _automation.GetDesktop();
        var maybeLater = root.FindFirstDescendant(cf => cf.ByName("Maybe later"));
        if (maybeLater == null || !maybeLater.IsAvailable)
        {
            return false;
        }

        try
        {
            maybeLater.Focus();
            if (maybeLater.Patterns.Invoke.IsSupported)
            {
                maybeLater.Patterns.Invoke.Pattern.Invoke();
                return true;
            }
        }
        catch
        {
        }

        try
        {
            maybeLater.Click();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryCloseCopilotWindowChrome()
    {
        foreach (var window in _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window)))
        {
            var name = window.Name ?? string.Empty;
            if (name.IndexOf("Copilot", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var close = FindCloseButtonForWindow(window);
            if (close != null && TryInvokeCloseButton(close))
            {
                return true;
            }
        }

        return false;
    }
}
