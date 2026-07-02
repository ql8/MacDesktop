// 虚拟桌面 COM 桥接子进程。
//
// 协议：stdin/stdout 上的「JSON Lines」（每行一个 JSON 对象）。
//   请求：{"id":1,"method":"switch","params":{"index":2}}
//   响应：{"id":1,"ok":true,"result":...}  或  {"id":1,"ok":false,"error":"..."}
//   启动就绪后先主动发一行：{"event":"ready"}
//
// 放在 VirtualDesktop 命名空间内，以便访问 VirtualDesktopApi.cs 中的 internal 成员。
using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;

namespace VirtualDesktop
{
    internal static class Bridge
    {
        [STAThread]
        private static void Main()
        {
            var stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
            var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };

            Send(stdout, new JsonObject { ["event"] = "ready" });

            string line;
            while ((line = stdin.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                JsonNode id = null;
                try
                {
                    var req = JsonNode.Parse(line);
                    id = req?["id"]?.DeepClone();
                    var method = (string)req?["method"];
                    var p = req?["params"] as JsonObject ?? new JsonObject();

                    JsonNode result = Dispatch(method, p);
                    Send(stdout, new JsonObject { ["id"] = id, ["ok"] = true, ["result"] = result });
                }
                catch (Exception ex)
                {
                    Send(stdout, new JsonObject { ["id"] = id, ["ok"] = false, ["error"] = ex.Message });
                }
            }
        }

        private static JsonNode Dispatch(string method, JsonObject p)
        {
            switch (method)
            {
                case "ping":
                    return "pong";

                case "count":
                    return Desktop.Count;

                case "currentIndex":
                    return Desktop.FromDesktop(Desktop.Current);

                case "list":
                {
                    var arr = new JsonArray();
                    int c = Desktop.Count;
                    for (int i = 0; i < c; i++)
                    {
                        var ivd = DesktopManager.GetDesktop(i);
                        arr.Add(new JsonObject
                        {
                            ["index"] = i,
                            ["name"] = TryGetName(ivd), // 该 build 上 HString 不可用时为 null
                            ["id"] = ivd.GetId().ToString(),
                        });
                    }
                    return arr;
                }

                case "create":
                    // 返回新桌面索引
                    return Desktop.FromDesktop(Desktop.Create());

                case "remove":
                {
                    int index = GetInt(p, "index");
                    Desktop fallback = null;
                    if (p["fallbackIndex"] != null)
                        fallback = Desktop.FromIndex(GetInt(p, "fallbackIndex"));
                    Desktop.FromIndex(index).Remove(fallback);
                    return true;
                }

                case "switch":
                {
                    int index = GetInt(p, "index");
                    Desktop.FromIndex(index).MakeVisible();
                    return true;
                }

                case "getName":
                    return TryGetName(DesktopManager.GetDesktop(GetInt(p, "index")));

                case "setName":
                {
                    int index = GetInt(p, "index");
                    string name = (string)p["name"] ?? "";
                    try
                    {
                        DesktopManager.VirtualDesktopManagerInternal.SetDesktopName(DesktopManager.GetDesktop(index), name);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("当前 Windows build 下不支持重命名桌面（HString 编组不可用）：" + ex.Message);
                    }
                    return true;
                }

                case "setAnimation":
                    Desktop.SetAnimation(GetBool(p, "enabled"));
                    return true;

                case "moveWindow":
                {
                    IntPtr hwnd = GetHwnd(p);
                    int index = GetInt(p, "index");
                    Desktop.FromIndex(index).MoveWindow(hwnd);
                    return true;
                }

                case "windowDesktopIndex":
                    return Desktop.FromDesktop(Desktop.FromWindow(GetHwnd(p)));

                case "isWindowOnCurrent":
                    return DesktopManager.VirtualDesktopManager.IsWindowOnCurrentVirtualDesktop(GetHwnd(p));

                default:
                    throw new Exception("unknown method: " + method);
            }
        }

        private static void Send(TextWriter w, JsonNode node)
        {
            w.Write(node.ToJsonString());
            w.Write('\n');
        }

        // 该 build 的内置 COM 互操作对 HString 返回值编组不支持时会抛异常，此处降级为 null。
        private static string TryGetName(IVirtualDesktop ivd)
        {
            try { return ivd.GetName(); }
            catch { return null; }
        }

        private static int GetInt(JsonObject p, string key)
        {
            var n = p[key] ?? throw new Exception("missing param: " + key);
            return int.Parse(n.ToString());
        }

        private static bool GetBool(JsonObject p, string key)
        {
            var n = p[key] ?? throw new Exception("missing param: " + key);
            return n.GetValue<bool>();
        }

        private static IntPtr GetHwnd(JsonObject p)
        {
            var n = p["hwnd"] ?? throw new Exception("missing param: hwnd");
            // 句柄以十进制字符串/数字传入，避免 JS 侧大整数精度问题
            long v = long.Parse(n.ToString());
            return new IntPtr(v);
        }
    }
}
