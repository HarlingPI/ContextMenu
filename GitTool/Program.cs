
using System.IO;
using System.Reflection;

namespace GitTool
{
    internal class Program
    {
        private static Dictionary<string, Command> cmds = new Dictionary<string, Command>();
        //private static string[] cmds = { "clone", "pull", "push" };
        static void Main(string[] args)
        {
            var working = InitCommands();
            Console.WriteLine(working);

            var cmd = "";
            while (string.IsNullOrEmpty(cmd))
            {
                Console.WriteLine("请输入操作指令");
                cmd = Console.ReadLine();
                if (!string.IsNullOrEmpty(cmd) && cmds.Contains(cmd))
                {
                    break;
                }
            }

            //switch (cmd)
            //{
            //    case "clone":
            //        Clone();
            //        break;
            //    case "pull":
            //        Pull();
            //        break;
            //    case "push":
            //        Push();
            //        break;
            //}

            //Console.ForegroundColor = ConsoleColor.White;
            //Console.WriteLine($"'{cmd}'已完成,请按任意键退出!");
            Console.Read();
        }

        private static string InitCommands()
        {
            var cmdtype = typeof(Command);

            var types = cmdtype
                .Assembly
                .GetTypes()
                .Where(t => t.IsSubclassOf(cmdtype))
                .Where(t => !t.IsAbstract)
                .ToArray();

            var working = AppDomain.CurrentDomain.BaseDirectory;
            working = working[0..(working.Length - 1)];

            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                var command = (Command)Activator.CreateInstance(type, working);
                cmds.Add(type.Name.ToLower(), command);
            }
            return working;
        }

        private static void Clone()
        {
            string? url = null;
            while (url == null)
            {
                Console.WriteLine("请输入目标项目的地址:");

                url = Console.ReadLine();
            }
            string folder = GitLib.GetFolderFromUrl(url);
            string path = GitLib.SearchModules()[0] + $"/{folder}";
            string cmd = $"clone --recursive {url} {path}";

            Console.WriteLine($"项目：{url}克隆中...");
            GitLib.ExcuteCommand(path, new[] { cmd }, false);
        }
        private static void Pull()
        {
            List<string> modules = GitLib.SearchModules();

            Console.WriteLine($"已找到项目:");
            for (int i = 0; i < modules.Count; i++)
            {
                Console.WriteLine(modules[i]);
            }

            string[] commands = new[] { "pull" };

            for (int i = 0; i < modules.Count; i++)
            {
                GitLib.ExcuteCommand(modules[i], commands);
            }
        }
        private static void Push()
        {
            List<string> modules = GitLib.SearchModules();

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
            Console.WriteLine($"即将开始执行操作'{optinename}'");
            commitcmd = string.Format(commitcmd, optinename);
            string[] commands = new[] { addcmd, commitcmd, pushcmd };

            for (int i = 0; i < modules.Count; i++)
            {
                GitLib.ExcuteCommand(modules[i], commands);
            }
        }
    }
}
