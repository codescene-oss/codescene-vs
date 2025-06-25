using Codescene.VSExtension.Core.Application.Services.AceManager;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Codescene.VSExtension.VS2022.UnderlineTagger
{
    public class AceRefactorTooltipModel
    {
        public string FunctionName { get; set; }
        public string Path { get; set; }
        public ICommand RefactorCommand { get; }

        private readonly IAceManager _aceManager;
        private readonly Func<string> _getFileContent;

        public AceRefactorTooltipModel(
            string functionName,
            string path,
            IAceManager aceManager,
            Func<string> getFileContent)
        {
            FunctionName = functionName;
            Path = path;
            _aceManager = aceManager ?? throw new ArgumentNullException(nameof(aceManager));
            _getFileContent = getFileContent ?? throw new ArgumentNullException(nameof(getFileContent));
            RefactorCommand = new AsyncRelayCommand(ExecuteRefactorAsync);
        }

        private async Task ExecuteRefactorAsync(object parameter)
        {
            try
            {
                string content = _getFileContent();
                if (string.IsNullOrWhiteSpace(content))
                    return;

                // Call ACE refactor service
                await _aceManager.Refactor(Path, content);
                // Optionally, handle the result (e.g., show a diff, notification, etc.)
            }
            catch (Exception ex)
            {
                // Handle/log error as appropriate
                System.Diagnostics.Debug.WriteLine($"ACE Refactor failed: {ex.Message}");
            }
        }

        // Simple async command implementation
        private class AsyncRelayCommand : ICommand
        {
            private readonly Func<object, Task> _execute;
            private bool _isExecuting;

            public AsyncRelayCommand(Func<object, Task> execute)
            {
                _execute = execute;
            }

            public bool CanExecute(object parameter) => !_isExecuting;

            public async void Execute(object parameter)
            {
                if (_isExecuting) return;
                _isExecuting = true;
                try
                {
                    await _execute(parameter);
                }
                finally
                {
                    _isExecuting = false;
                }
            }

            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }
        }
    }
}