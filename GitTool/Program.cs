
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace GitTool
{
    internal class Program
    {
        private static Dictionary<string, Command> allcmds = new Dictionary<string, Command>();
        private static string working = "";
        private static string[] projects;
        static void Main(string[] args)
        {
            InitProgram();
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
                    var words = SplitCommand(typein.Trim());
                    var cmdname = words[0];
                    if (allcmds.TryGetValue(cmdname.ToLower(), out var command))
                    {
                        command.Excute(projects, words[1..]);
                    }
                    else continue;
                }
                else Environment.Exit(0);
            }
        }



        /// <summary>
        /// 命令分割正则表达式，空格分割，""中的空格不分割
        /// </summary>
        private static Regex splitexp = new Regex(@"(\""[^\""]*\"")|(\S+)", RegexOptions.Compiled);
        private static string[] SplitCommand(string command)
        {
            return splitexp
                .Split(command)
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrEmpty(w))
                .ToArray();
        }
        /// <summary>
        /// 初始化程序工作目录与查找项目
        /// </summary>
        /// <param name="folder"></param>
        public static void InitProgram(string folder = null)
        {
            //获取工作路径
            working = SetAndGetWorkingFolder(folder);
            //初始化所有指令
            InitCommands(working);
            //查找当前目录下的所有Git项目
            projects = GitLib.FindProjects(working);
            //输出所有找到的Git项目
            for (int i = 0; i < projects.Length; i++)
            {
                Console.WriteLine(projects[i]);
            }
        }

        private static string SetAndGetWorkingFolder(string folder = null)
        {
            var working = "";
            if (string.IsNullOrWhiteSpace(folder))
            {
                working = AppDomain.CurrentDomain.BaseDirectory;
                Directory.SetCurrentDirectory(working);
            }
            else
            {
                //target = Environment.ExpandEnvironmentVariables(folder);
                // 解析相对路径
                Directory.SetCurrentDirectory(Path.GetFullPath(folder));
                working = Directory.GetCurrentDirectory();
            }
            return working;
        }

        /// <summary>
        /// 初始化程序工作目录与查找项目
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        private static string InitCommands(string folder)
        {
            var cmdtype = typeof(Command);
            var types = cmdtype
                .Assembly
                .GetTypes()
                .Where(t => t.IsSubclassOf(cmdtype))
                .Where(t => !t.IsAbstract)
                .ToArray();

            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                var command = (Command)Activator.CreateInstance(type, folder);
                allcmds[type.Name.ToLower()] = command;
            }
            return working;
        }
        /// <summary>
        /// 获取所有指令名称
        /// </summary>
        /// <returns></returns>
        public static string[] GetCommandsNames()
        {
            return allcmds.Keys.OrderBy(x => x).ToArray();
        }
    }
}
