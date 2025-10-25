using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.ComponentModel;

namespace AVASClient
{
    public partial class InputDialog : Window, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler? PropertyChanged;

        private string _dialogTitle = "输入对话框";
        public string DialogTitle
        {
            get => _dialogTitle;
            set
            {
                _dialogTitle = value;
                OnPropertyChanged();
            }
        }

        private string _message = "";
        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        private string _inputText = "";
        public string InputText
        {
            get => _inputText;
            set
            {
                _inputText = value;
                OnPropertyChanged();
            }
        }

        public bool? DialogResult { get; private set; }

        public InputDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void OK_Click(object? sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close(true);
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close(false);
        }

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
