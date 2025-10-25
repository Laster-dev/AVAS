using Avalonia.Threading;
using AVASClient.MessagePackLib;
using AVASClient.Services;
using System;
using System.Text;
using System.Threading.Tasks;

namespace AVASClient.HandlePacket
{
    internal class HandleShell
    {
        public static async Task HandleAsync(ClientMsgPack msg)
        {
            await Task.Run(() => {
                Handle(msg);
            });
        }
        private static void Handle(ClientMsgPack unpack_msgpack)
        {
            try
            {
                string bid = unpack_msgpack.ForcePathObject("BID").GetAsString();
                string action = unpack_msgpack.ForcePathObject("Action").GetAsString();
                if (!string.Equals(action, "Output", StringComparison.OrdinalIgnoreCase)) return;

                string shellType = string.Empty;
                try
                {
                    shellType = unpack_msgpack.ForcePathObject("ShellType").GetAsString();
                }
                catch { shellType = string.Empty; }
                int isError = 0;
                try
                {
                    isError = (int)unpack_msgpack.ForcePathObject("IsError").AsInteger;
                }
                catch { isError = 0; }
                int completed = 0;
                try
                {
                    completed = (int)unpack_msgpack.ForcePathObject("Completed").AsInteger;
                }
                catch { completed = 0; }
                string text = unpack_msgpack.ForcePathObject("Text").GetAsString();

                string windowName = "Shell:" + bid;
                Dispatcher.UIThread.Post(async () =>
                {
                    var winObj = await Helper.FindForm.FindWindowByNameSafe(windowName);
                    var win = winObj as Form_Shell;
                    if (win != null)
                    {
                        // 仅在错误时加 [ERR]，正常输出不加多余前缀，减少混乱
                        string toAppend = text ?? string.Empty;
                        if (isError == 1) toAppend = "[ERR] " + toAppend;
                        win.GetType().GetMethod("AppendOutput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                            ?.Invoke(win, new object[] { toAppend });

                        if (completed == 1)
                        {
                            // 可选：结束标记
                            win.GetType().GetMethod("AppendOutput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                ?.Invoke(win, new object[] { "\r\n[Shell completed]\r\n" });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"处理 Shell 数据包异常: {ex.Message}", "HandleShell");
            }
        }
    }
}
