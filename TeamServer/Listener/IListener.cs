﻿﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;
using DataList.Client.Models;

namespace TeamServer.Listener
{
    /// <summary>
    /// 监听器接口，支持异步启动和停止监听，包含基础属性与事件，适用于C2 TeamServer中肉鸡连接管理
    /// </summary>
    internal interface IListener
    {
        /// <summary>
        /// 启动监听器
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// 停止监听器
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 当前监听器是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 协议类型（如TCP/HTTP/HTTPS/SMB等）
        /// </summary>
        string Protocol { get; }

        /// <summary>
        /// 监听绑定的地址（如0.0.0.0或指定网卡IP）
        /// </summary>
        string BindAddress { get; }

        /// <summary>
        /// 监听端口
        /// </summary>
        int Port { get; }

        /// <summary>
        /// 新客户端（肉鸡）连接事件
        /// </summary>
        event Action<IBeaconSession> OnBeaconConnected;

        /// <summary>
        /// 客户端（肉鸡）断开连接事件
        /// </summary>
        event Action<IBeaconSession> OnBeaconDisconnected;

        /// <summary>
        /// 新Client（用户界面）连接事件
        /// </summary>
        event Action<ClientSession> OnClientConnected;

        /// <summary>
        /// Client（用户界面）断开连接事件
        /// </summary>
        event Action<ClientSession> OnClientDisconnected;

        /// <summary>
        /// 监听器出现异常时触发
        /// </summary>
        event Action<Exception> OnError;
    }
}