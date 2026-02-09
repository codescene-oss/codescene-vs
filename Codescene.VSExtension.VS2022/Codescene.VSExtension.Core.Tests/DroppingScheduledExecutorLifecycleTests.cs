// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Util;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class DroppingScheduledExecutorLifecycleTests : DroppingScheduledExecutorTestBase
{
    [TestMethod]
    public void Constructor_NullAction_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DroppingScheduledExecutor(null, TimeSpan.FromMilliseconds(100), _mockLogger.Object));
    }

    [TestMethod]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DroppingScheduledExecutor(() => Task.CompletedTask, TimeSpan.FromMilliseconds(100), null));
    }

    [TestMethod]
    public void Start_WhenDisposed_ThrowsObjectDisposedException()
    {
        _executor = new DroppingScheduledExecutor(() => Task.CompletedTask, TimeSpan.FromMilliseconds(100), _mockLogger.Object);

        _executor.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _executor.Start());
    }

    [TestMethod]
    public void Start_WhenAlreadyStarted_LogsWarningAndReturns()
    {
        _executor = new DroppingScheduledExecutor(() => Task.CompletedTask, TimeSpan.FromMilliseconds(100), _mockLogger.Object);

        _executor.Start();
        _executor.Start();

        _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("already started"))), Times.Once);
    }

    [TestMethod]
    public void Stop_WhenNotStarted_DoesNotThrow()
    {
        _executor = new DroppingScheduledExecutor(() => Task.CompletedTask, TimeSpan.FromMilliseconds(100), _mockLogger.Object);

        _executor.Stop();
    }

    [TestMethod]
    public void Dispose_StopsTimer()
    {
        _executor = new DroppingScheduledExecutor(() => Task.CompletedTask, TimeSpan.FromMilliseconds(100), _mockLogger.Object);

        _executor.Start();
        _executor.Dispose();

        _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("disposed"))), Times.Once);
    }

    [TestMethod]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        _executor = new DroppingScheduledExecutor(() => Task.CompletedTask, TimeSpan.FromMilliseconds(100), _mockLogger.Object);

        _executor.Dispose();
        _executor.Dispose();
    }
}
