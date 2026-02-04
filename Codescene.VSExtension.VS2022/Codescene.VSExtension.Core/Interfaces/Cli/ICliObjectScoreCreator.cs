// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface ICliObjectScoreCreator
    {
        /// <summary>
        /// <param name="oldScore">Raw base64 encoded old score.</param>
        /// <param name="newScore">Raw base64 encoded new score.</param>
        /// <returns>score as json</returns>
        /// </summary>
        string Create(string oldScore, string newScore);
    }
}
