using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.Application.Mappers
{
    [Export(typeof(AceComponentMapper))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AceComponentMapper
    {
        public AceComponentData Map(CachedRefactoringActionModel model)
        {
            var fileData = CreateFileData(
                model.Path,
                model.RefactorableCandidate.Name,
                model.RefactorableCandidate.Range);
            var aceParams = new CreateAceComponentDataParams
            {
                Loading = false,
                Error = null,
                FileData = fileData,
                AceResultData = model.Refactored,
                FnToRefactor = model.RefactorableCandidate
            };

            return CreateAceComponentData(aceParams);
        }

        public AceComponentData Map(string path, FnToRefactorModel model)
        {
            var fileData = CreateFileData(
                path,
                model.Name,
                model.Range);
            var aceParams = new CreateAceComponentDataParams
            {
                Loading = true,
                Error = null,
                FileData = fileData,
                AceResultData = null,
                FnToRefactor = model
            };

            return CreateAceComponentData(aceParams);
        }

        public AceComponentData Map(string path, FnToRefactorModel model, string error)
        {
            var fileData = CreateFileData(
                path,
                model.Name,
                model.Range);
            var aceParams = new CreateAceComponentDataParams
            {
                Loading = false,
                Error = error,
                FileData = fileData,
                AceResultData = null,
                FnToRefactor = model
            };

            return CreateAceComponentData(aceParams);
        }

        private static CliRangeModel MapRange(CliRangeModel range)
        {
            return new CliRangeModel
            {
                Startline = range.Startline,
                StartColumn = range.StartColumn,
                EndLine = range.EndLine,
                EndColumn = range.EndColumn
            };
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
                    Range = MapRange(range)
                }
            };
        }

        sealed class CreateAceComponentDataParams
        {
            public bool Loading { get; set; }
            public string Error { get; set; }
            public WebComponentFileData FileData { get; set; }
            public FnToRefactorModel FnToRefactor { get; set; }
            public RefactorResponseModel AceResultData { get; set; }
        }

        private static AceComponentData CreateAceComponentData(CreateAceComponentDataParams aceParams)
        {
            return new AceComponentData
            {
                Error = aceParams.Error,
                Loading = aceParams.Loading,
                FileData = aceParams.FileData,
                FnToRefactor = aceParams.FnToRefactor,
                AceResultData = aceParams.AceResultData,
            };
        }

    }
}
