// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Exceptions;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliExceptionWarnLoggingTests
    {
        [TestMethod]
        public void ShouldLogAsWarning_NoPredicatesMatch()
        {
            Assert.IsFalse(CliExceptionWarnLogging.ShouldLogAsWarning(null));
            Assert.IsFalse(CliExceptionWarnLogging.ShouldLogAsWarning(new DevtoolsException("You ran out of refactoring credits", 402, "trace-1")));
        }

        [TestMethod]
        public void FormatWarningMessage_IncludesStatusAndTraceForDevtoolsErrors()
        {
            var message = CliExceptionWarnLogging.FormatWarningMessage(new DevtoolsException("Denied", 403, null), "Reviewing file. ");

            Assert.AreEqual("Reviewing file: Denied (status 403, trace n/a)", message);
        }

        [TestMethod]
        public void FormatWarningMessage_PlainException_UsesMessage()
        {
            var message = CliExceptionWarnLogging.FormatWarningMessage(new InvalidOperationException("boom"), null);

            Assert.AreEqual(": boom", message);
        }
    }
}
