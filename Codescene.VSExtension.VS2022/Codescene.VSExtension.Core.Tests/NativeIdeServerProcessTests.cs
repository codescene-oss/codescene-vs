// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class NativeIdeServerProcessTests
    {
        [TestMethod]
        public void Kill_WhenAlreadyExited_DoesNotThrow()
        {
            var factory = new NativeIdeServerProcessFactory();
            using var process = (NativeIdeServerProcess)factory.Start("cmd.exe", "/d /c exit");
            process.Kill();
            process.Kill();
        }

        [TestMethod]
        public async Task BeginErrorReadLine_CapturesStderrSnapshot()
        {
            var factory = new NativeIdeServerProcessFactory();
            var received = new TaskCompletionSource<bool>();
            using var process = (NativeIdeServerProcess)factory.Start("cmd.exe", "/d /c \"echo server-error 1>&2\"");
            process.ErrorDataReceived += (_, __) => received.TrySetResult(true);
            process.BeginErrorReadLine();
            await received.Task;
            Assert.Contains("server-error", process.StderrSnapshot);
        }
    }
}
