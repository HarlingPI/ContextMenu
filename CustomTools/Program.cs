using PIToolKit.Public.Utils;
using System.Reflection;
using System.Resources;

namespace CustomTools
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(FileUtils.GetFullPath("Config.ico"));
            FileUtils.BytesToFile(Resource.Config, "Config.ico");
            Console.WriteLine("Hello, World!");
            Console.Read();
        }
    }
}
