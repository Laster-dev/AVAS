﻿﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataList.Client.Models
{
    /// <summary>
    /// Client基础信息类，仅包含连接必要信息
    /// </summary>
    public class ClientInfo
    {
        /// <summary>
        /// 连接ID
        /// </summary>
        public required string Id { get; set; }
        
        /// <summary>
        /// IP和端口的组合，格式如127.0.0.1:8888
        /// </summary>
        public required string IPProt { get; set; }

        /// <summary>
        /// IP地址
        /// </summary>
        public required string IPAddress { get; set; }

        /// <summary>
        /// 连接时间
        /// </summary>
        public required DateTime ConnectedTime { get; set; }

        /// <summary>
        /// 最后活动时间
        /// </summary>
        public required DateTime LastSeenTime { get; set; }
    }
}
