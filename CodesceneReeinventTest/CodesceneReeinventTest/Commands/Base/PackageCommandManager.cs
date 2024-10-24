using Core.Application.Services.Authentication;
using Core.Application.Services.ErrorHandling;
using System.ComponentModel.Design;

namespace CodesceneReeinventTest.Commands
{
    internal class PackageCommandManager(IMenuCommandService menuService, IAuthenticationService authService, IErrorsHandler errorsHandler)
    {
        internal delegate void ShowOptionsPage(Type optionsPageToOpen);
        private readonly IMenuCommandService menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
        private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
        private readonly IErrorsHandler errorsHandler = errorsHandler ?? throw new ArgumentNullException(nameof(errorsHandler));

        public void Initialize(ShowOptionsPage showOptionsPage)
        {
            RegisterCommand(PackageGuids.CodeSceneCmdSetString, OpenStatusWindowCommand.Id, new OpenStatusWindowCommand());
            RegisterCommand(PackageGuids.CodeSceneCmdSetString, OptionsCommand.Id, new OptionsCommand(showOptionsPage));
            RegisterCommand(PackageGuids.CodeSceneCmdSetString, SignOutCommand.Id, new SignOutCommand(authService, errorsHandler));
            RegisterCommand(PackageGuids.CodeSceneCmdSetString, SignInCommand.Id, new SignInCommand(authService, errorsHandler));
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
            return RegisterCommand(PackageGuids.CodeSceneCmdSetString, commandId, command);
        }
        internal OleMenuCommand RegisterCommand(string commandSetGuid, int commandId, VsCommandBase command)
        {
            return AddCommand(new Guid(commandSetGuid), commandId, command.Invoke, command.QueryStatus);
        }
    }
}
