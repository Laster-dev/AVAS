using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DataList.Beacon.Models
{
    public class BeaconInfo : INotifyPropertyChanged
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

        #region 这些是由Server自动生成和维护的字段

        private string _id = string.Empty;
        /// <summary>
        /// ID
        /// </summary>
        public required string Id 
        { 
            get => _id; 
            set => SetProperty(ref _id, value); 
        }

        private string _listenerId = string.Empty;
        /// <summary>
        /// 监听器ID或类型
        /// </summary>
        public required string ListenerId 
        { 
            get => _listenerId; 
            set => SetProperty(ref _listenerId, value); 
        }

        private string _ipProt = string.Empty;
        /// <summary>
        /// IP和端口的组合，格式如127.0.0.1:8888
        /// </summary>
        public required string IPProt 
        { 
            get => _ipProt; 
            set => SetProperty(ref _ipProt, value); 
        }

        private string _ipAddress = string.Empty;
        /// <summary>
        /// IP对应的地址
        /// </summary>
        public required string IPAddress 
        { 
            get => _ipAddress; 
            set => SetProperty(ref _ipAddress, value); 
        }

        private DateTime _connectedTime;
        /// <summary>
        /// Beacon上线的时间
        /// </summary>
        public required DateTime ConnectedTime 
        { 
            get => _connectedTime; 
            set => SetProperty(ref _connectedTime, value); 
        }

        private DateTime _lastSeenTime;
        /// <summary>
        /// 最后心跳时间
        /// </summary>
        public required DateTime LastSeenTime 
        { 
            get => _lastSeenTime; 
            set => SetProperty(ref _lastSeenTime, value); 
        }
        #endregion

        /// <summary>
        /// 其他扩展属性(由Beacon自动携带,本质是Json，这里直接发送给Client，CLient会做解析)
        /// </summary>
        private string? _hwid;
        public string? HWID 
        { 
            get => _hwid; 
            set => SetProperty(ref _hwid, value); 
        }

        private string? _user;
        public string? User 
        { 
            get => _user; 
            set => SetProperty(ref _user, value); 
        }

        private string? _os;
        public string? OS 
        { 
            get => _os; 
            set => SetProperty(ref _os, value); 
        }

        private string? _path;
        public string? Path 
        { 
            get => _path; 
            set => SetProperty(ref _path, value); 
        }

        private string? _admin;
        public string? Admin 
        { 
            get => _admin; 
            set => SetProperty(ref _admin, value); 
        }

        private string? _perfor_mance;
        public string? Perfor_mance 
        { 
            get => _perfor_mance; 
            set => SetProperty(ref _perfor_mance, value); 
        }

        private string? _anti_virus;
        public string? Anti_virus 
        { 
            get => _anti_virus; 
            set => SetProperty(ref _anti_virus, value); 
        }

        private string? _install_ed;
        public string? Install_ed 
        { 
            get => _install_ed; 
            set => SetProperty(ref _install_ed, value); 
        }

        private string? _group;
        public string? Group 
        { 
            get => _group; 
            set => SetProperty(ref _group, value); 
        }

        private string? _notes;
        public string? Notes 
        { 
            get => _notes; 
            set => SetProperty(ref _notes, value); 
        }

        private string? _tg;
        public string? tg 
        { 
            get => _tg; 
            set => SetProperty(ref _tg, value); 
        }

        private string? _wx;
        public string? wx 
        { 
            get => _wx; 
            set => SetProperty(ref _wx, value); 
        }
    }
}
