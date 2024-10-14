namespace CodesceneReeinventTest.Commands
{
    internal class OptionsCommand : VsCommandBase
    {
        internal const int Id = PackageIds.OptionsCommand;

        private readonly PackageCommandManager.ShowOptionsPage showOptionsPage;

        public OptionsCommand(PackageCommandManager.ShowOptionsPage showOptionsPage)
        {
            this.showOptionsPage = showOptionsPage;
        }

        protected override void InvokeInternal()
        {
            showOptionsPage(typeof(OptionsProvider.GeneralOptions));
        }
    }
}
