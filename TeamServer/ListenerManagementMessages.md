# 监听器管理消息格式

## 启动行为

程序启动时默认只启动 Client 监听器（端口 50050），不会自动启动任何 Beacon 或其他类型的监听器。所有其他监听器都需要通过 Client 发送管理消息来动态添加。

## 消息类型

### 1. 获取支持的监听器类型

**请求:**
```json
{
  "Pac_ket": "ListenerManagement",
  "Action": "GET_SUPPORTED"
}
```

**响应:**
```json
{
  "Type": "ListenerManagement",
  "Action": "GET_SUPPORTED_RESPONSE",
  "Success": true,
  "Data": "TCP,UDP,WebSocket,ICMP,DHCP,HTTP,SMB,Client"
}
```

### 2. 获取活跃的监听器

**请求:**
```json
{
  "Pac_ket": "ListenerManagement",
  "Action": "GET_ACTIVE"
}
```

**响应:**
```json
{
  "Type": "ListenerManagement",
  "Action": "GET_ACTIVE_RESPONSE",
  "Success": true,
  "Data": "base64编码的监听器列表数据"
}
```

### 3. 添加监听器

**请求:**
```json
{
  "Pac_ket": "ListenerManagement",
  "Action": "ADD",
  "Type": "TCP",
  "BindAddress": "0.0.0.0",
  "Port": 8888
}
```

**响应:**
```json
{
  "Type": "ListenerManagement",
  "Action": "ADD_RESPONSE",
  "Success": true,
  "Message": "监听器添加成功"
}
```

### 4. 删除监听器

**请求:**
```json
{
  "Pac_ket": "ListenerManagement",
  "Action": "REMOVE",
  "Type": "TCP",
  "BindAddress": "0.0.0.0",
  "Port": 8888
}
```

**响应:**
```json
{
  "Type": "ListenerManagement",
  "Action": "REMOVE_RESPONSE",
  "Success": true,
  "Message": "监听器删除成功"
}
```

### 5. 停止监听器

**请求:**
```json
{
  "Pac_ket": "ListenerManagement",
  "Action": "STOP",
  "Type": "TCP",
  "BindAddress": "0.0.0.0",
  "Port": 8888
}
```

**响应:**
```json
{
  "Type": "ListenerManagement",
  "Action": "STOP_RESPONSE",
  "Success": true,
  "Message": "监听器停止成功"
}
```

### 6. 启动监听器

**请求:**
```json
{
  "Pac_ket": "ListenerManagement",
  "Action": "START",
  "Type": "TCP",
  "BindAddress": "0.0.0.0",
  "Port": 8888
}
```

**响应:**
```json
{
  "Type": "ListenerManagement",
  "Action": "START_RESPONSE",
  "Success": true,
  "Message": "监听器启动成功"
}
```

## 支持的监听器类型

- **TCP**: TCP监听器（用于Beacon连接）
- **UDP**: UDP监听器
- **WebSocket**: WebSocket监听器
- **ICMP**: ICMP监听器
- **DHCP**: DHCP监听器
- **HTTP**: HTTP监听器
- **SMB**: SMB监听器
- **Client**: Client监听器（用于客户端连接）

## 注意事项

1. 所有消息都使用MessagePack格式
2. 监听器状态变化会自动保存到配置文件
3. 程序重启后会根据配置文件自动恢复监听器状态
4. 添加监听器后会自动启动
5. 删除监听器前会自动停止
