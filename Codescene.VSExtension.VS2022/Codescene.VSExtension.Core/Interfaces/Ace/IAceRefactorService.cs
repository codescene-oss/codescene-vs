// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Interfaces.Ace
{
    public interface IAceRefactorService
    {
        /// <summary>
        /// Checks if a file contains refactorable functions based on its code smells.
        /// </summary>
        IList<FnToRefactorModel> CheckContainsRefactorableFunctions(FileReviewModel result, string code);
    }
}
