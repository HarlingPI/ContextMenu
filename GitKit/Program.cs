using ConsoleKit;
using GitKit.Commands;
using PIToolKit.Public.Utils;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace GitKit
{
    internal static class Program
    {
        private static Dictionary<string, Command> allcmds = new Dictionary<string, Command>();
        private static string working = "";
        private static string[] projects;
        public readonly static string Version;
        static Program()
        {
            //获取当前程序集
            var assembly = Assembly.GetExecutingAssembly();
            //获取文件版本
            var fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            Version = fileVersionAttr?.Version ?? "0.0.0";
        }
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
            var infos = projects.Select(p =>
                {
                    var wide = p.EnumerateRunes().Count(r => r.IsWide());
                    var rune = p.EnumerateRunes().Count();
                    return (wide, rune, display: wide + rune);
                })
                .ToArray();
            var maxl = infos.Max(i => i.display);
            //输出所有找到的Git项目
            for (int i = 0; i < projects.Length; i++)
            {
                var project = projects[i];
                //读取分支名
                var content = File.ReadAllText(project + "/.git/HEAD");
                var branch = content[(content.LastIndexOf('/') + 1)..^1];
                var outstr = project.PadRight(maxl - infos[i].wide, ' ');
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
        public static bool IsWide(this Rune r)
        {
            int code = r.Value;

            // Fast-path: ASCII 永远是半角
            if (code <= 0x007F) return false;

            // ---- East Asian Wide (W) ----
            if (
                (code >= 0x1100 && code <= 0x115F) ||   // Hangul Jamo
                (code >= 0x2329 && code <= 0x232A) ||
                (code >= 0x2E80 && code <= 0x2FFF) ||   // CJK Radicals + ID symbols
                (code >= 0x3000 && code <= 0x303E) ||
                (code >= 0x3040 && code <= 0x309F) ||   // Hiragana
                (code >= 0x30A0 && code <= 0x30FF) ||   // Katakana
                (code >= 0x3100 && code <= 0x312F) ||   // Bopomofo
                (code >= 0x3130 && code <= 0x318F) ||   // Hangul Compatibility Jamo
                (code >= 0x31A0 && code <= 0x31BF) ||
                (code >= 0x31C0 && code <= 0x31EF) ||
                (code >= 0x3200 && code <= 0x32FF) ||
                (code >= 0x3300 && code <= 0x33FF) ||
                (code >= 0x3400 && code <= 0x4DBF) ||   // CJK Ext A
                (code >= 0x4E00 && code <= 0x9FFF) ||   // CJK Unified Ideographs
                (code >= 0xA960 && code <= 0xA97F) ||
                (code >= 0xAC00 && code <= 0xD7A3) ||   // Hangul
                (code >= 0xF900 && code <= 0xFAFF) ||   // CJK Compatibility Ideographs
                (code >= 0xFE10 && code <= 0xFE19) ||
                (code >= 0xFE30 && code <= 0xFE6F) ||
                (code >= 0xFF01 && code <= 0xFF60) ||   // Full-width ASCII variants
                (code >= 0xFFE0 && code <= 0xFFE6)
            )
                return true;

            // ---- Emoji（全部）----
            // Unicode Emoji 范围极广，全部 Surrogate Pair（U+1xxxx）
            // 全都统一按宽字符处理
            if (code >= 0x1F000 && code <= 0x1FAFF) return true;  // Emoji blocks
            if (code >= 0x1FC00 && code <= 0x1FFFF) return true;  // 新增扩展区（未来兼容）

            // 其他高位平面（大概率是 emoji 或 pictograph）
            if (code > 0x10000) return true;

            return false;
        }
        /// <summary>
        /// 获取显示宽度
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static int GetDisplayWidth(this string str)
        {
            int width = 0;
            foreach (var rune in str.EnumerateRunes())
            {
                width += IsWide(rune) ? 2 : 1;
            }
            return width;
        }
    }
}
