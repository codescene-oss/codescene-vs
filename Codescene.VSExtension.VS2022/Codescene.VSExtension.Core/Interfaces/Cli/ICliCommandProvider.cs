// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface ICliCommandProvider
    {
        string VersionCommand { get; }

        string DeviceIdCommand { get; }

        string RefactorCommand { get; }

        string ReviewFileContentCommand { get; }

        string SendTelemetryCommand(string jsonEvent);

        string GetReviewFileContentPayload(string filePath, string fileContent, string cachePath);

        string GetReviewDeltaCommand(string oldScore, string newScore);

        string GetRefactorWithDeltaResultPayload(string fileName, string fileContent, string cachePath, DeltaResponseModel deltaResult, PreFlightResponseModel preflight = null);

        string GetRefactorWithCodeSmellsPayload(string fileName, string fileContent, string cachePath, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight = null);

        string GetPreflightSupportInformationCommand(bool force);

        string GetRefactorPostCommand(FnToRefactorModel fnToRefactor, bool skipCache, string token = null);
    }
}
