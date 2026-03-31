// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IDocumentSaveEventSource : IDisposable
    {
        event EventHandler<string> DocumentSaved;

        void Start();
    }
}
