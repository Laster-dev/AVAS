using TeamServer.Listener;
using TeamServer.Config;
using TeamServer.Services;

namespace TeamServer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // 启动日志服务
            LoggerService.Instance.Start();
            
            // 应用日志配置
            AppConfig.Instance.ApplyLogConfig();
            
            
            try
            {
                // 解析命令行参数（仅用于Client端口）
                int clientPort = ParseCommandLineArgs(args);
                
                // 更新配置
                if (clientPort != 50050) // 如果提供了自定义端口
                {
                    await AppConfig.Instance.UpdateClientPortAsync(clientPort);
                }

                // 显示当前配置
                AppConfig.Instance.DisplayConfig();

                // 初始化监听器管理器
                await ListenerManager.Instance.InitializeAsync();

                LoggerService.Instance.Success("所有监听器已启动，等待连接...", "Program"); 
                
                while (true)
                {
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Critical("程序启动失败", "Program", ex);
                throw;
            }
        }

        /// <summary>
        /// 解析命令行参数
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <returns>Client端口号</returns>
        private static int ParseCommandLineArgs(string[] args)
        {
            int clientPort = 50050; // 默认端口

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-p":
                    case "--port":
                    case "-client-port":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int port))
                        {
                            if (port > 0 && port <= 65535)
                            {
                                clientPort = port;
                                LoggerService.Instance.Success($"使用命令行参数指定的Client端口: {clientPort}", "Program");
                            }
                            else
                            {
                                LoggerService.Instance.Warning($"无效的端口号: {port}，使用默认端口: {clientPort}", "Program");
                            }
                        }
                        else
                        {
                            LoggerService.Instance.Warning("端口参数缺少值，使用默认端口: 50050", "Program");
                        }
                        i++; // 跳过下一个参数（端口值）
                        break;
                    case "-h":
                    case "--help":
                        ShowHelp();
                        Environment.Exit(0);
                        break;
                    default:
                        // 如果参数不是以-开头，可能是直接的端口号
                        if (!args[i].StartsWith("-") && int.TryParse(args[i], out int directPort))
                        {
                            if (directPort > 0 && directPort <= 65535)
                            {
                                clientPort = directPort;
                                LoggerService.Instance.Success($"使用直接指定的Client端口: {clientPort}", "Program");
                            }
                        }
                        break;
                }
            }

            return clientPort;
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine("AVAS TeamServer - 远程管理服务器");
            Console.WriteLine();
            Console.WriteLine("用法:");
            Console.WriteLine("  TeamServer.exe [选项] [端口号]");
            Console.WriteLine();
            Console.WriteLine("选项:");
            Console.WriteLine("  -p, --port, -client-port <端口>  指定Client监听端口 (默认: 50050)");
            Console.WriteLine("  -h, --help                       显示此帮助信息");
            Console.WriteLine();
            Console.WriteLine("说明:");
            Console.WriteLine("  默认只启动Client监听器，Beacon和其他监听器通过Client消息动态添加");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  TeamServer.exe                   使用默认配置");
            Console.WriteLine("  TeamServer.exe 8080              使用Client端口 8080");
            Console.WriteLine("  TeamServer.exe -p 9090           使用Client端口 9090");
        }
    }
}