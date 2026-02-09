// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Interfaces;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

public abstract class DroppingScheduledExecutorTestBase
{
    protected Mock<ILogger> _mockLogger;
    protected DroppingScheduledExecutor _executor;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _executor?.Dispose();
    }
}
