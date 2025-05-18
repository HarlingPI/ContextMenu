
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace GitTool
{
    internal class Program
    {
        private static Dictionary<string, Command> allcmds = new Dictionary<string, Command>();
        static void Main(string[] args)
        {
            //获取工作路径与初始化命令字典
            var working = InitCommands();
            //查找当前目录下的所有Git项目
            var projects = GitLib.FindProjects(working);
            //输出所有找到的Git项目
            for (int i = 0; i < projects.Length; i++)
            {
                Console.WriteLine(projects[i]);
            }
            //
            while (true)
            {
                //输出工作路径
                var orgcolor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"{working}");
                Console.Write(">");
                Console.ForegroundColor = orgcolor;

                var typein = Console.ReadLine();

                if (!string.IsNullOrEmpty(typein))
                {
                    var words = SplitCommand(typein.ToLower().Trim());
                    var cmdname = words[0];
                    if (cmdname == "exit") allcmds["exit"].Excute(null);
                    else if (allcmds.TryGetValue(cmdname, out var command))
                    {
                        command.Excute(projects, words[1..]);
                    }
                    else continue;
                }
                else Environment.Exit(0);
            }
        }
        private static Regex splitexp = new Regex(@"(\""[^\""]*\"")|(\S+)", RegexOptions.Compiled);
        private static string[] SplitCommand(string command)
        {
            //return command.Split(' ');
            return splitexp
                .Split(command)
                .Select(w => w.Trim())
                .Select(w => w.Replace("\"", ""))
                .Where(w => !string.IsNullOrEmpty(w))
                .ToArray();
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

            //获取工作路径
            var working = AppDomain.CurrentDomain.BaseDirectory;

            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                var command = (Command)Activator.CreateInstance(type, working);
                allcmds.Add(type.Name.ToLower(), command);
            }
            return working;
        }
    }
}
