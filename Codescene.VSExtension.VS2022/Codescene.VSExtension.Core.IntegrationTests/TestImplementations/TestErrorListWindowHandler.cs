using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models;
using Moq;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(IErrorListWindowHandler))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestErrorListWindowHandler : IErrorListWindowHandler
    {
        internal Mock<IErrorListWindowHandler> Mock = new();

        public void Handle(FileReviewModel review) => Mock.Object.Handle(review);
    }
}
