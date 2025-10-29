using ConsoleKit;
using CustomTools.Tools;
using PIToolKit.Public.Utils;
using System.Reflection;
using System.Resources;
using System.Text;

namespace CustomTools
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.BufferHeight = 30000;
            //注册非Unicode编码
            Console.OutputEncoding = Encoding.UTF8;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            VirtualTerminal.Enable();
            //Console.WriteLine(FileUtils.GetFullPath("Config.ico"));
            //FileUtils.BytesToFile(Resource.Config, "Config.ico");
            //FileUtils.BytesToFile((byte[])Resource.ResourceManager.GetObject("Config"), "Config.ico");

            new Archive().Process("D:/InstallFolder/迅雷下载/新建文件夹/");

            Console.Read();
        }
    }
}
