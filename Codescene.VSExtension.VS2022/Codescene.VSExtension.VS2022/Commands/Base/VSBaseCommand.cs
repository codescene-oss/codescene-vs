using System;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Commands
{
    public abstract class VSBaseCommand
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
