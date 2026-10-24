// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Interfaces;

namespace Codescene.VSExtension.Core.Tests
{
    public sealed class FakeLogger : ILogger
    {
        public List<string> Errors { get; } = new List<string>();

        public List<string> Warnings { get; } = new List<string>();

        public List<string> Infos { get; } = new List<string>();

        public List<string> Debugs { get; } = new List<string>();

        public List<string> SnapshotErrorMessages()
        {
            return Errors.ToList();
        }

        public List<string> SnapshotInfoMessages()
        {
            return Infos.ToList();
        }

        public void Error(string message, Exception ex)
        {
            Errors.Add(message);
        }

        public void Warn(string message, bool statusBar = false)
        {
            Warnings.Add(message);
        }

        public void Info(string message, bool statusBar = false)
        {
            Infos.Add(message);
        }

        public void Debug(string message)
        {
            Debugs.Add(message);
        }
    }
}
