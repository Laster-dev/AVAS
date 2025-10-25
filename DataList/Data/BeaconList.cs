using DataList.Beacon.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TeamServer.Beacon.Interface;
using TeamServer.Beacon.Models;

namespace TeamServer.Data
{
    public class BeaconList
    {
        public static List<IBeaconSession> Beacons = new List<IBeaconSession>();

        /// <summary>
        /// 序列化客户端列表到JSON字符串，带类型信息
        /// </summary>
        public static string Serialize()
        {
            List<BeaconInfo> info = new List<BeaconInfo>();
            foreach (var beacon in Beacons)
            {
                info.Add(beacon.Info);
            }

            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                Formatting = Formatting.Indented
            };
            return JsonConvert.SerializeObject(info, settings);
        }

        /// <summary>
        /// 从JSON字符串反序列化客户端列表，自动识别类型
        /// </summary>
        public static List<BeaconInfo> Deserialize(string json)
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            };
            return JsonConvert.DeserializeObject<List<BeaconInfo>>(json, settings);
        }
    }
}