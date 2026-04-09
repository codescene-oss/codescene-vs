// Copyright (c) CodeScene. All rights reserved.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.VS2022.E2ETests;

[TestClass]
[DoNotParallelize]
[TestCategory("E2E")]
public sealed class SmokeTests
{
    private static VisualStudioTestHost? _host;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        _ = testContext;
        _host = VisualStudioTestHost.Start();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _host?.Dispose();
        _host = null;
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (_host == null)
        {
            return;
        }

        if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed)
        {
            _host.CaptureScreenshot(TestContext.TestName);
        }

        _host.DismissTransientUi();
    }

    [TestMethod]
    public void PackageLoadsWithoutActivityLogErrors()
    {
        var host = RequireHost();
        Assert.IsTrue(File.Exists(host.OpenedSolutionPath), "E2E workspace solution should exist.");
        host.AssertNoCodeSceneErrorsInActivityLog();
    }

    [TestMethod]
    public void CanOpenCodesceneOptionsPage()
    {
        var host = RequireHost();
        host.InvokeCommand(E2ETestEnvironment.PackageGuid, E2ETestEnvironment.OpenSettingsCommandId);
        host.WaitForElementByName("Show Debug Logs");
        host.WaitForElementByName("Auth Token");
    }

    [TestMethod]
    public void CanOpenCodeHealthMonitorToolWindow()
    {
        var host = RequireHost();
        var initialCount = host.CountElementsByName("CodeScene");
        host.InvokeCommand(E2ETestEnvironment.PackageGuid, E2ETestEnvironment.OpenCodeHealthMonitorCommandId);
        host.WaitForElementCountToIncrease("CodeScene", initialCount);
    }

    private static VisualStudioTestHost RequireHost()
    {
        Assert.IsNotNull(_host);
        return _host;
    }
}
