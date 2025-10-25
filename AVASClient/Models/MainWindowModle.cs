using DataList.Beacon.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AVASClient.Models
{
    public static class MainWindowModle
    {
        public static ObservableCollection<BeaconInfo> Beacons = new ObservableCollection<BeaconInfo>();
    }
}
