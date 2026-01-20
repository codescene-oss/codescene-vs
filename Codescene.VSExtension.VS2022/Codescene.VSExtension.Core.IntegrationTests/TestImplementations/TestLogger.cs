using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Moq;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(ILogger))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestLogger : ILogger
    {
        internal Mock<ILogger> Mock = new();

        public void Debug(string message) => Mock.Object.Debug(message);
        public void Error(string message, Exception ex) => Mock.Object.Error(message, ex);
        public void Info(string message) => Mock.Object.Info(message);
        public void Warn(string message) => Mock.Object.Warn(message);
    }
}
