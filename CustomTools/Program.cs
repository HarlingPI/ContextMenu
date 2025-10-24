using PIToolKit.Public.Utils;
using System.Reflection;
using System.Resources;

namespace CustomTools
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var path = "D:/FFOutput/Temp/3D/";
            //Console.WriteLine(FileUtils.GetFullPath("Config.ico"));
            //FileUtils.BytesToFile(Resource.Config, "Config.ico");
            //FileUtils.BytesToFile((byte[])Resource.ResourceManager.GetObject("Config"), "Config.ico");

            foreach(var file in FileUtils.SearchFiles(path))
            {
                Console.WriteLine(file);
            }

            Console.Read();
        }
    }
}
