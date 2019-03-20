using System.ComponentModel;

namespace AgroAdd.Models.Mvvm
{
    public delegate void IsBusyChangedHandler(bool state);

    public class BaseViewModel : INotifyPropertyChanged
    {
        public event IsBusyChangedHandler IsBusyChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isBusy;

        protected void OnBusyChanged(bool state)
        {
            IsBusyChanged?.Invoke(state);
        }

        public bool IsBusy
        {
            get
            {
                return _isBusy;
            }
            protected set
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                OnBusyChanged(_isBusy);
            }
        }
        protected virtual void OnPropertyChanged(string propertyName, params DelegateCommand[] commands)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            foreach (DelegateCommand delegateCommand in commands)
            {
                delegateCommand.RaiseCanExecute();
            }
        }
    }
}
