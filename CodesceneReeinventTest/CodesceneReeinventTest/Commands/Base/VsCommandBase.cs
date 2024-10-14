namespace CodesceneReeinventTest.Commands
{
    public abstract class VsCommandBase
    {
        protected virtual void QueryStatusInternal(OleMenuCommand command)
        {
        }

        protected abstract void InvokeInternal();

        public void Invoke(object sender, EventArgs args)
        {
            var command = sender as OleMenuCommand;
            if (command.Enabled)
            {
                this.InvokeInternal();
            }
        }

        public void QueryStatus(object sender, EventArgs args)
        {
            var command = sender as OleMenuCommand;
            this.QueryStatusInternal(command);
        }
    }
}
