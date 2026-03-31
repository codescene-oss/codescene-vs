// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces;
using Moq;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(ILogger))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestLogger : ILogger
    {
        internal Mock<ILogger> Mock = new Mock<ILogger>();

        public void Debug(string message) => Mock.Object.Debug(message);

        public void Error(string message, Exception ex) => Mock.Object.Error(message, ex);

        public void Info(string message, bool statusBar = false) => Mock.Object.Info(message, statusBar);

        public void Warn(string message, bool statusBar = false) => Mock.Object.Warn(message, statusBar);
    }
}
