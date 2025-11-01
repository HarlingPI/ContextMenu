using ConsoleKit;
using CustomTools.Tools;
using PIToolKit.Public.Reflection;
using PIToolKit.Public.Utils;
using System.Reflection;
using System.Resources;
using System.Text;

namespace CustomTools
{
    internal class Program
    {
        private readonly static Dictionary<string, ITool> tools;
        static Program()
        {
            var interfacetype = typeof(ITool);
            tools = typeof(ITool).Assembly.GetTypes()
                .Where(t => interfacetype.IsAssignableFrom(t))
                .Where(t => !t.IsInterface)
                .Where(t => !t.IsAbstract)
                .Select(t => ReflectUtils.CreateInstance(t))
                .ToDictionary(t => t.GetType().Name, t => (ITool)t);
        }

        static void Main(string[] args)
        {
            args = new[] { "D:/InstallFolder/迅雷下载/新建文件夹/", "Archive" };
            Console.BufferHeight = 30000;
            //注册非Unicode编码
            Console.OutputEncoding = Encoding.UTF8;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            VirtualTerminal.Enable();
            //Console.WriteLine(FileUtils.GetFullPath("Config.ico"));
            //FileUtils.BytesToFile(Resource.Config, "Config.ico");
            //FileUtils.BytesToFile((byte[])Resource.ResourceManager.GetObject("Config"), "Config.ico");

            using (var scope = new ConsoleScope(foreground: ConsoleColor.Blue))
            {
                Console.WriteLine($"{args[0]}");
                Console.WriteLine($">{args[1]}");
            }
            Console.Read();
        }


    }
}
