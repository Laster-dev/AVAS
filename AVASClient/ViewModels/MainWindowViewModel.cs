using AVASClient.Models;
using DataList.Beacon.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AVASClient.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion

        private ObservableCollection<BeaconInfo> _beacons = new();
        public ObservableCollection<BeaconInfo> Beacons
        {
            get => _beacons;
            set => SetProperty(ref _beacons, value);
        }

        private LogViewModel? _logViewModel;
        public LogViewModel? LogViewModel
        {
            get => _logViewModel;
            set => SetProperty(ref _logViewModel, value);
        }
    }
}
