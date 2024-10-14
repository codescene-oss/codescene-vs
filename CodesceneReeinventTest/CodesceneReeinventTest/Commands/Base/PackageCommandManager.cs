using CodesceneReeinventTest.Application;
using CodesceneReeinventTest.Application.Services.FileReviewer;
using System.ComponentModel.Design;

namespace CodesceneReeinventTest.Commands
{
    internal class PackageCommandManager
    {
        internal delegate void ShowOptionsPage(Type optionsPageToOpen);
        private readonly IMenuCommandService menuService;
        private readonly IFileReviewer fileReviewer;
        private readonly IIssuesHandler issuesHandler;

        public PackageCommandManager(IMenuCommandService menuService, IFileReviewer fileReviewer, IIssuesHandler issuesHandler)
        {
            this.menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
            this.fileReviewer = fileReviewer ?? throw new ArgumentNullException(nameof(fileReviewer));
            this.issuesHandler = issuesHandler ?? throw new ArgumentNullException(nameof(issuesHandler));
        }

        public void Initialize(ShowOptionsPage showOptionsPage)
        {
            RegisterCommand(PackageGuids.CodeSceneMenuCommandSet, OpenErrorListWindowCommand.Id, new OpenErrorListWindowCommand(issuesHandler));
            RegisterCommand(PackageGuids.CodeSceneMenuCommandSet, ShowReviewResultInErrorListCommand.Id, new ShowReviewResultInErrorListCommand(fileReviewer, issuesHandler));
            RegisterCommand(PackageGuids.CodeSceneMenuCommandSet, ReviewActiveFileCommand.Id, new ReviewActiveFileCommand(fileReviewer));
            RegisterCommand(PackageGuids.CodeSceneMenuCommandSet, OpenCodesceneSiteCommand.Id, new OpenCodesceneSiteCommand());
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
