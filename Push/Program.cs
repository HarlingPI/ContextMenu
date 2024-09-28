using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Push
{
    internal class Program
    {
        private static Regex moduleexp = new Regex("path = [\\w]+", RegexOptions.Compiled);
        static void Main(string[] args)
        {
            string path = System.Environment.CurrentDirectory;
            //先查找gitmodules文件
            string mapper = path + "/.gitmodules";
            List<string> modules = new List<string>();
            if (File.Exists(mapper))
            {
                string content = File.ReadAllText(mapper);
                foreach (Match item in moduleexp.Matches(content))
                {
                    modules.Add($"{path}/{item.Value[7..]}");
                }
            }
            modules.Add(path);

            Console.WriteLine($"已找到项目:");
            for (int i = 0; i < modules.Count; i++)
            {
                Console.WriteLine(modules[i]);
            }

            string addcmd = "add .";
            string commitcmd = "commit -m \"{0}\"";
            string pushcmd = "push";

            Console.WriteLine("请输入本次操作名称:");
            string? optinename = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(optinename))
            {
                optinename = "Commit by scripts";
            }
            Console.WriteLine($"即将开始执行操作L{optinename}");
            commitcmd = string.Format(commitcmd, optinename);
            string[] commands = new[] { addcmd, commitcmd, pushcmd };

            for (int i = 0; i < modules.Count; i++)
            {
                GitLib.ExcuteCommand(modules[i], commands);
            }


            Console.WriteLine("'push'已完成,请按任意键退出!");
            Console.Read();
        }
    }
}
