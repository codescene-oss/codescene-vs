using Codescene.VSExtension.Core.Application.Services.Authentication;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using System.ComponentModel.Design;

namespace Codescene.VSExtension.VS2022.Commands
{
    internal class PackageCommandManager(IMenuCommandService menuService, IAuthenticationService authService, ILogger errorsHandler)
    {
        internal delegate void ShowOptionsPage(Type optionsPageToOpen);
        private readonly IMenuCommandService menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
        private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
        private readonly ILogger errorsHandler = errorsHandler ?? throw new ArgumentNullException(nameof(errorsHandler));

        public void Initialize(ShowOptionsPage showOptionsPage)
        {
            RegisterCommand(PackageGuids.CodeSceneCmdSetString, OpenStatusWindowCommand.Id, new OpenStatusWindowCommand());
            RegisterCommand(PackageGuids.CodeSceneCmdSetString, OptionsCommand.Id, new OptionsCommand(showOptionsPage));

            var signInCommand = RegisterCommand(PackageGuids.CodeSceneCmdSetString, SignInCommand.Id, new SignInCommand(authService, errorsHandler));
            signInCommand.BeforeQueryStatus += SignInCommand_BeforeQueryStatus;

            var signOutCommand = RegisterCommand(PackageGuids.CodeSceneCmdSetString, SignOutCommand.Id, new SignOutCommand(authService, errorsHandler));
            signOutCommand.BeforeQueryStatus += SignOutCommand_BeforeQueryStatus;
        }

        private void SignOutCommand_BeforeQueryStatus(object sender, EventArgs e)
        {
            var command = (OleMenuCommand)sender;
            var loggedIn = authService.IsLoggedIn();
            command.Visible = loggedIn;
            command.Enabled = loggedIn;
            if (loggedIn)
            {
                var data = authService.GetData();
                command.Text = $"{data.Name} - Sign Out";
            }
            else
            {
                command.Text = "Sign Out";
            }
        }

        private void SignInCommand_BeforeQueryStatus(object sender, EventArgs e)
        {
            var command = (OleMenuCommand)sender;
            var loggedIn = authService.IsLoggedIn();
            command.Visible = !loggedIn;
            command.Enabled = !loggedIn;
        }

        private OleMenuCommand AddCommand(Guid commandGroupGuid, int commandId, EventHandler invokeHandler, EventHandler beforeQueryStatus)
        {
            var idObject = new CommandID(commandGroupGuid, commandId);
            var command = new OleMenuCommand(invokeHandler, delegate { }, beforeQueryStatus, idObject);

            menuService.AddCommand(command);

            return command;
        }

        internal OleMenuCommand RegisterCommand(string commandSetGuid, int commandId, VsCommandBase command)
        {
            return AddCommand(new Guid(commandSetGuid), commandId, command.Invoke, command.QueryStatus);
        }
    }
}
