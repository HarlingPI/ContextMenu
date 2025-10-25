using CustomTools.Tools;
using PIToolKit.Public.Utils;
using System.Reflection;
using System.Resources;

namespace CustomTools
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine(FileUtils.GetFullPath("Config.ico"));
            //FileUtils.BytesToFile(Resource.Config, "Config.ico");
            //FileUtils.BytesToFile((byte[])Resource.ResourceManager.GetObject("Config"), "Config.ico");

            new Archive().Process("D:/FFOutput/Temp/3D/");

            Console.Read();
        }
    }
}
