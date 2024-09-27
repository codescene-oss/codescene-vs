using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodesceneReeinventTest.ToolWindows.Status
{
    public class StatusWindowModel : INotifyPropertyChanged
    {
        private bool _codeHealthActivated;
        private bool _aceActive;
        private bool _isLoggedIn;

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set
            {
                if (_isLoggedIn != value)
                {
                    _isLoggedIn = value;
                    OnPropertyChanged(nameof(IsLoggedIn));
                }
            }
        }

        public bool CodeHealthActivated
        {
            get => _codeHealthActivated;
            set
            {
                if (_codeHealthActivated != value)
                {
                    _codeHealthActivated = value;
                    OnPropertyChanged(nameof(CodeHealthActivated));
                }
            }
        }

        public bool ACEActive
        {
            get => _aceActive;
            set
            {
                if (_aceActive != value)
                {
                    _aceActive = value;
                    OnPropertyChanged(nameof(ACEActive));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
