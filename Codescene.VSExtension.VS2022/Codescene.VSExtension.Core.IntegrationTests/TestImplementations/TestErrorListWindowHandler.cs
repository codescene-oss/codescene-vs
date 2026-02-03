// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models;
using Moq;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(IErrorListWindowHandler))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestErrorListWindowHandler : IErrorListWindowHandler
    {
        internal Mock<IErrorListWindowHandler> Mock = new Mock<IErrorListWindowHandler>();

        public void Handle(FileReviewModel review) => Mock.Object.Handle(review);
    }
}
