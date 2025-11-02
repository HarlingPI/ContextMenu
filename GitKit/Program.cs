using ConsoleKit;
using GitKit.Commands;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace GitKit
{
    internal class Program
    {
        private static Dictionary<string, Command> allcmds = new Dictionary<string, Command>();
        private static string working = "";
        private static string[] projects;
        static void Main(string[] args)
        {
            Console.BufferHeight = 30000;
            //注册非Unicode编码
            Console.OutputEncoding = Encoding.UTF8;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            VirtualTerminal.Enable();
            //检查和注册
            Unins.RegisterContextMenu();
            //执行
            ExcuteArgs(args);
        }

        private static void ExcuteArgs(string[] args)
        {
            InitProgram(args.Length > 0 ? args[0] : null);
            while (true)
            {
                string typein = string.Empty;
                //输出工作路径
                using (var scope = new ConsoleScope(foreground: ConsoleColor.Blue))
                {
                    Console.WriteLine($"{working}");
                    Console.Write(">");
                    typein = Console.ReadLine();
                }
                Console.WriteLine();

                if (!string.IsNullOrEmpty(typein))
                {
                    var analyzer = new Analyzer(typein, projects);

                    var cmdname = analyzer.Words[0];
                    if (allcmds.TryGetValue(cmdname.ToLower(), out var command))
                    {
                        command.Excute(analyzer.FilteredProjects, analyzer.Retry, analyzer.Words[1..]);
                    }
                    else
                    {
                        //默认命令执行
                        for (int i = 0; i < analyzer.FilteredProjects.Length; i++)
                        {
                            GitLib.ExcuteCommand(analyzer.FilteredProjects[i], typein, analyzer.Retry);
                        }
                    }
                    //执行附加命令
                    switch (analyzer.PostCmd)
                    {
                        case 'E':
                        case 'e':
                            Environment.Exit(0);
                            break;
                        default:
                            break;
                    }
                }
                else Environment.Exit(0);
            }
        }

        /// <summary>
        /// 初始化程序工作目录与查找项目
        /// </summary>
        /// <param name="folder"></param>
        public static void InitProgram(string folder = null)
        {
            Effects.ShowSpinner2Char("Searching", Task.Run(() =>
            {
                //获取工作路径
                working = SetAndGetWorkingFolder(folder);
                //初始化所有指令
                InitCommands(working);
                //查找当前目录下的所有Git项目
                projects = GitLib.FindProjects(working).ToArray();
            }));

            //计算最长的路径长度
            var dirl = projects.Length > 0 ? projects.Select(p => p.Length).Max() : 0;
            //输出所有找到的Git项目
            for (int i = 0; i < projects.Length; i++)
            {
                var project = projects[i];
                //读取分支名
                var content = File.ReadAllText(project + "/.git/HEAD");
                var branch = content[(content.LastIndexOf('/') + 1)..^1];
                var outstr = project.PadRight(dirl, ' ');
                //项目索引
                var length = (int)MathF.Ceiling(MathF.Log10(projects.Length));
                var idxstr = i.ToString().PadLeft(length, '0');
                //输出项目
                Console.WriteLine($"{outstr}\t[{idxstr}]({branch})");
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
                folder = folder.Trim();
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
        private static void InitCommands(string folder)
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
        }
        /// <summary>
        /// 获取所有指令名称
        /// </summary>
        /// <returns></returns>
        public static Command[] GetCommands()
        {
            return allcmds
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToArray();
        }
    }
}
