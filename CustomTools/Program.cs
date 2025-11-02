using ConsoleKit;
using CustomTools.Tools;
using Microsoft.Win32;
using PIToolKit.Public.Reflection;
using PIToolKit.Public.Utils;
using System.Diagnostics;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text;

namespace CustomTools
{
    internal class Program
    {
        public readonly static Dictionary<string, ITool> Tools;
        public readonly static string Version;
        static Program()
        {
            var interfacetype = typeof(ITool);
            Tools = typeof(ITool).Assembly.GetTypes()
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
#if DEBUG
            //args = new[] { "E:\\视频\\动漫\\3D", "Classify" };
            //args = new[] { "D:/InstallFolder/迅雷下载/新建文件夹/", "Flatten" };
#endif
            if (args.IsNullOrEmpty())
            {
                SystemMenu.CheckInstall();
            }
            else
            {
                using (var scope = new ConsoleScope(foreground: ConsoleColor.Blue))
                {
                    Console.WriteLine($"{args[0]}");
                    Console.WriteLine($">{args[1]}");
                }
                Console.WriteLine("意键开始任务");
                Console.ReadKey();
                Ansi.ClearLastLine();

                Tools[args[1]].Process(args[0].Trim());
            }
            Console.ReadKey();
        }
    }
}
