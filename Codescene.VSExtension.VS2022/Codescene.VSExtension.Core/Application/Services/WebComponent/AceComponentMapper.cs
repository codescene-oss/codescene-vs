using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.Application.Services.WebComponent
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

            return CreateAceComponentData(
                loading: false,
                error: null,
                fileData: fileData,
                aceResultData: model.Refactored);
        }

        public AceComponentData Map(string path, FnToRefactorModel model)
        {
            var fileData = CreateFileData(
                path,
                model.Name,
                model.Range);

            return CreateAceComponentData(
                loading: true,
                error: null,
                fileData: fileData,
                aceResultData: null);
        }

        public AceComponentData Map(string path, FnToRefactorModel model, string error)
        {
            var fileData = CreateFileData(
                path,
                model.Name,
                model.Range);

            return CreateAceComponentData(
                loading: false,
                error: error,
                fileData: fileData,
                aceResultData: null);
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

        private static AceComponentData CreateAceComponentData(
        bool loading,
        string error,
        WebComponentFileData fileData,
        RefactorResponseModel aceResultData)
        {
            return new AceComponentData
            {
                Loading = loading,
                Error = error,
                FileData = fileData,
                AceResultData = aceResultData
            };
        }

    }
}
