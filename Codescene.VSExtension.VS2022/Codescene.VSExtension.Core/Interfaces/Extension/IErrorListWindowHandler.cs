// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.Core.Interfaces.Extension
{
    public interface IErrorListWindowHandler
    {
        void Handle(FileReviewModel review);
    }
}
