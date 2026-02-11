// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Model;

namespace Codescene.VSExtension.Core.Application.Mappers
{
    [Export(typeof(AceComponentMapper))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AceComponentMapper
    {
        public static CodeRangeModel MapRange(CliRangeModel range)
        {
            if (range == null)
            {
                return null;
            }

            return new CodeRangeModel(range.StartLine, range.EndLine, range.StartColumn, range.EndColumn);
        }

        public static CliRangeModel MapRange(CodeRangeModel range)
        {
            if (range == null)
            {
                return null;
            }

            return new CliRangeModel
            {
                StartLine = range.StartLine,
                EndLine = range.EndLine,
                StartColumn = range.StartColumn,
                EndColumn = range.EndColumn,
            };
        }

        public AceComponentData Map(CachedRefactoringActionModel model)
        {
            return MapCachedModel(model, isStale: false);
        }

        public AceComponentData Map(string path, FnToRefactorModel model)
        {
            return MapFromPathAndFunction(path, model, loading: true, error: null);
        }

        public AceComponentData Map(string path, FnToRefactorModel model, string error)
        {
            return MapFromPathAndFunction(path, model, loading: false, error: error);
        }

        /// <summary>
        /// Maps a cached refactoring model to component data with IsStale set to true.
        /// Used when the function has been modified and the refactoring is no longer valid.
        /// </summary>
        public AceComponentData MapAsStale(CachedRefactoringActionModel model)
        {
            return MapCachedModel(model, isStale: true);
        }

        private static WebComponentFileData CreateFileData(
        string fileName,
        string fnName,
        CliRangeModel range)
        {
            return new WebComponentFileData
            {
                FileName = fileName,
                Fn = new WebComponentFileDataBaseFn
                {
                    Name = fnName,
                    Range = MapRange(range),
                },
            };
        }

        private static AceComponentData CreateAceComponentData(CreateAceComponentDataParams aceParams)
        {
            return new AceComponentData
            {
                Error = aceParams.Error,
                Loading = aceParams.Loading,
                IsStale = aceParams.IsStale,
                FileData = aceParams.FileData,
                FnToRefactor = aceParams.FnToRefactor,
                AceResultData = aceParams.AceResultData,
            };
        }

        private AceComponentData MapFromPathAndFunction(string path, FnToRefactorModel model, bool loading, string error)
        {
            var fileData = CreateFileData(path, model.Name, model.Range);
            var aceParams = new CreateAceComponentDataParams
            {
                Loading = loading,
                Error = error,
                FileData = fileData,
                AceResultData = null,
                FnToRefactor = model,
            };

            return CreateAceComponentData(aceParams);
        }

        private AceComponentData MapCachedModel(CachedRefactoringActionModel model, bool isStale)
        {
            var fileData = CreateFileData(
                model.Path,
                model.RefactorableCandidate.Name,
                model.RefactorableCandidate.Range);
            var aceParams = new CreateAceComponentDataParams
            {
                Loading = false,
                Error = null,
                IsStale = isStale,
                FileData = fileData,
                AceResultData = model.Refactored,
                FnToRefactor = model.RefactorableCandidate,
            };

            return CreateAceComponentData(aceParams);
        }
    }
}
