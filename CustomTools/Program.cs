using ConsoleKit;
using CustomTools.Tools;
using Microsoft.Win32;
using PIToolKit.Public.Reflection;
using PIToolKit.Public.Utils;
using System.Diagnostics;
using System.Reflection;
using System.Resources;
using System.Text;

namespace CustomTools
{
    internal class Program
    {
        private readonly static Dictionary<string, ITool> tools;
        public readonly static string Version;
        static Program()
        {
            var interfacetype = typeof(ITool);
            tools = typeof(ITool).Assembly.GetTypes()
                .Where(t => interfacetype.IsAssignableFrom(t))
                .Where(t => !t.IsInterface)
                .Where(t => !t.IsAbstract)
                .Select(t => ReflectUtils.CreateInstance(t))
                .ToDictionary(t => t.GetType().Name, t => (ITool)t);

            //获取当前程序集
            var assembly = Assembly.GetExecutingAssembly();
            //获取文件版本
            var fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            Version = fileVersionAttr?.Version ?? "0.0.0";
        }

        static void Main(string[] args)
        {
            Console.BufferHeight = 30000;
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            //注册非Unicode编码
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            VirtualTerminal.Enable();
            //Console.WriteLine(FileUtils.GetFullPath("Config.ico"));
            //FileUtils.BytesToFile(Resource.Config, "Config.ico");
            //FileUtils.BytesToFile((byte[])Resource.ResourceManager.GetObject("Config"), "Config.ico");
#if DEBUG
            //args = new[] { "D:/InstallFolder/迅雷下载/新建文件夹/", "Classify" };
            args = new[] { "D:/InstallFolder/迅雷下载/新建文件夹/", "Flatten" };
#endif
            if (args.IsNullOrEmpty())
            {
                CheckAndInstall();
            }
            else
            {
                using (var scope = new ConsoleScope(foreground: ConsoleColor.Blue))
                {
                    Console.WriteLine($"{args[0]}");
                    Console.WriteLine($">{args[1]}");
                }
                tools[args[1]].Process(args[0]);
            }
            Console.Read();
        }

        private static void CheckAndInstall()
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            var RootKey = Registry.CurrentUser;
            // 右键菜单项名称
            var displayName = "自定义工具";
            // 注册表项名称
            var mainName = "CustomTools";
            var foreKey = @$"Software\Classes\Directory\shell\{mainName}";
            var backKey = @$"Software\Classes\Directory\Background\shell\{mainName}";

            var root = RootKey.OpenSubKey(foreKey);

            var needUpdate = root == null;

            if (!needUpdate)
            {
                var version = (string)root.GetValue("Version");
                var iconpath = (string)root.GetValue("Icon");
                needUpdate = version != Version || exePath != iconpath;

                if (needUpdate)
                {
                    Console.WriteLine("旧版本卸载开始");

                    RootKey.DeleteSubKeyTree(foreKey, false);
                    RootKey.DeleteSubKeyTree(backKey, false);

                    Console.WriteLine("旧版本卸载结束");
                }
            }
            if (needUpdate)
            {
                Console.WriteLine("右键菜单注册开始");
                // 注册到目录右键菜单
                using (var key = RootKey.CreateSubKey(foreKey))
                {
                    SetKey(key, displayName, exePath);
                }

                using (var key = RootKey.CreateSubKey(backKey))
                {
                    SetKey(key, displayName, exePath);
                }
                Console.WriteLine("右键菜单注册成功");
            }

            static void SetKey(RegistryKey key, string displayName, string? exePath)
            {
                key.SetValue("MUIVerb", displayName);
                key.SetValue("Icon", exePath);
                key.SetValue("SubCommands", "");
                key.SetValue("Version", Version);
                key.SetValue("Position", "Bottom");
            }
        }
    }
}
