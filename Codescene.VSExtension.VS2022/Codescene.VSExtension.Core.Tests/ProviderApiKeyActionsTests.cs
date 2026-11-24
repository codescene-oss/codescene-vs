// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Models.Agent;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class ProviderApiKeyActionsTests
{
    [TestMethod]
    public void ConnectivityReport_Success()
    {
        var result = new ConnectivityResult
        {
            Success = true,
        };

        var report = ProviderApiKeyActions.ConnectivityReport(result);

        Assert.AreEqual("Provider connectivity test successful.", report);
    }

    [TestMethod]
    public void ConnectivityReport_Failure()
    {
        var result = new ConnectivityResult
        {
            Error = "Authentication failed",
        };

        var report = ProviderApiKeyActions.ConnectivityReport(result);

        Assert.AreEqual("Provider connectivity test failed: Authentication failed", report);
    }

    [TestMethod]
    public void ConnectivityReport_Cancelled_IsNull()
    {
        var result = new ConnectivityResult
        {
            Cancelled = true,
        };

        var report = ProviderApiKeyActions.ConnectivityReport(result);

        Assert.IsNull(report);
    }

    [TestMethod]
    public void ConnectivityReport_NullResult_IsNull()
    {
        var report = ProviderApiKeyActions.ConnectivityReport(null);

        Assert.IsNull(report);
    }
}
