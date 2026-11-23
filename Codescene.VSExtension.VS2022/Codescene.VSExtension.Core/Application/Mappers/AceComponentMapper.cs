// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Refactoring;
using Codescene.VSExtension.Core.Models.WebComponent.Data;

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

        public AceComponentData MapSession(RefactoringSession session)
        {
            if (session == null)
            {
                return null;
            }

            var fileData = CreateFileData(session.Path, session.Function);

            return new AceComponentData
            {
                Loading = session.State == RefactoringSessionState.Running && string.IsNullOrEmpty(session.ProgressMessage),
                Error = session.Error,
                IsStale = session.State == RefactoringSessionState.Stale,
                FileData = fileData,
                AceResultData = session.Response,
                ProgressMessage = session.ProgressMessage,
            };
        }

        public AceComponentData MapLoading(string path, FnToRefactorModel fn)
        {
            var fileData = CreateFileData(path, fn);

            return new AceComponentData
            {
                Loading = true,
                Error = null,
                IsStale = false,
                FileData = fileData,
                AceResultData = null,
                ProgressMessage = null,
            };
        }

        public AceComponentData MapError(string path, FnToRefactorModel fn, string error)
        {
            var fileData = CreateFileData(path, fn);

            return new AceComponentData
            {
                Loading = false,
                Error = error,
                IsStale = false,
                FileData = fileData,
                AceResultData = null,
                ProgressMessage = null,
            };
        }

        private static WebComponentFileData CreateFileData(string fileName, FnToRefactorModel fn)
        {
            return new WebComponentFileData
            {
                FileName = fileName,
                Fn = new WebComponentFileDataBaseFn
                {
                    Name = fn.Name,
                    Range = MapRange(fn.Range),
                },
                FnToRefactor = fn,
            };
        }
    }
}
