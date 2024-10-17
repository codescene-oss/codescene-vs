using Core.Application.Services.Authentication;
using Core.Application.Services.ErrorHandling;
using Core.Application.Services.FileReviewer;
using Core.Application.Services.IssueHandler;
using System.ComponentModel.Design;

namespace CodesceneReeinventTest.Commands
{
    internal class PackageCommandManager
    {
        internal delegate void ShowOptionsPage(Type optionsPageToOpen);
        private readonly IMenuCommandService menuService;
        private readonly IFileReviewer fileReviewer;
        private readonly IIssuesHandler issuesHandler;
        private readonly IAuthenticationService authService;
        private readonly IErrorsHandler errorsHandler;

        public PackageCommandManager(IMenuCommandService menuService, IFileReviewer fileReviewer, IIssuesHandler issuesHandler, IAuthenticationService authService, IErrorsHandler errorsHandler)
        {
            this.menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
            this.fileReviewer = fileReviewer ?? throw new ArgumentNullException(nameof(fileReviewer));
            this.issuesHandler = issuesHandler ?? throw new ArgumentNullException(nameof(issuesHandler));
            this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
            this.errorsHandler = errorsHandler ?? throw new ArgumentNullException(nameof(errorsHandler));
        }

        public void Initialize(ShowOptionsPage showOptionsPage)
        {
            RegisterCommand(PackageGuids.CodeSceneMenuCommandSet, OpenErrorListWindowCommand.Id, new OpenErrorListWindowCommand(issuesHandler));
            RegisterCommand(PackageGuids.CodeSceneMenuCommandSet, ShowReviewResultInErrorListCommand.Id, new ShowReviewResultInErrorListCommand(fileReviewer, issuesHandler, errorsHandler));
            RegisterCommand(PackageGuids.CodeSceneMenuCommandSet, ReviewActiveFileCommand.Id, new ReviewActiveFileCommand(fileReviewer, errorsHandler));
            RegisterCommand(PackageGuids.CodeSceneMenuCommandSet, OpenCodesceneSiteCommand.Id, new OpenCodesceneSiteCommand(authService, errorsHandler));
            RegisterCommand(PackageGuids.CodeSceneMenuCommandSet, OpenStatusWindowCommand.Id, new OpenStatusWindowCommand());
            RegisterCommand(PackageGuids.CodeSceneMenuCommandSet, OpenProblemsWindowCommand.Id, new OpenProblemsWindowCommand());
            RegisterCommand(PackageGuids.CodeSceneMenuCommandSet, OptionsCommand.Id, new OptionsCommand(showOptionsPage));

        }

        private OleMenuCommand AddCommand(Guid commandGroupGuid, int commandId, EventHandler invokeHandler, EventHandler beforeQueryStatus)
        {
            var idObject = new CommandID(commandGroupGuid, commandId);
            var command = new OleMenuCommand(invokeHandler, delegate { }, beforeQueryStatus, idObject);

            menuService.AddCommand(command);

            return command;
        }
        internal OleMenuCommand RegisterCommand(int commandId, VsCommandBase command)
        {
            return RegisterCommand(PackageGuids.CodeSceneMenuCommandSet, commandId, command);
        }
        internal OleMenuCommand RegisterCommand(string commandSetGuid, int commandId, VsCommandBase command)
        {
            return AddCommand(new Guid(commandSetGuid), commandId, command.Invoke, command.QueryStatus);
        }
    }
}
