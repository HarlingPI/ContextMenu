using ConsoleKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GitKit
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/23 14:20:31
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public static class GitLib
    {
        private static Regex moduleexp = new Regex("path = [\\w]+", RegexOptions.Compiled);
        private static Regex errorexp = new Regex("fatal:|error:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 查找指定路径下的所有Git项目
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static IEnumerable<string> FindProjects(string folder)
        {
            var projects = SearchGitProjects(folder);
            //如果向下没有找到任何Git项目，则向上查找Git项目
            if (projects.IsNullOrEmpty())
            {
                var root = ExcuteGitCommand(folder, "rev-parse --show-toplevel", false);
                if (!root.StartsWith("fatal: not a git repository"))
                {
                    yield return root.Trim().Replace('/', '\\');
                }
            }
            foreach (var item in projects)
            {
                yield return item.Trim();
            }
        }
        public static IEnumerable<string> SearchGitProjects(string path)
        {
            if (!Directory.Exists(path)) yield break;
            var queue = new Queue<string>();
            queue.Enqueue(path);
            while (queue.Count > 0)
            {
                var folder = queue.Dequeue();
                //查找是否是.git目录
                if (folder.EndsWith("\\.git", StringComparison.OrdinalIgnoreCase))
                {
                    yield return Path.GetDirectoryName(folder)!;
                    continue;
                }
                IEnumerator<string>? enumerator = null;
                try
                {
                    // 惰性枚举当前目录的文件（不会创建完整数组）
                    enumerator = Directory.EnumerateFiles(folder).GetEnumerator();
                }
                catch (Exception e)
                {
                    if (e is UnauthorizedAccessException ||
                        e is PathTooLongException ||
                        e is IOException)
                    {
                        continue;
                    }
                    else throw;
                }

                while (enumerator.MoveNext())
                {
                    var file = enumerator.Current;
                    if (file.EndsWith(".gitmodules", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return folder;
                        var content = File.ReadAllText(file);
                        foreach (Match match in GitLib.moduleexp.Matches(content))
                        {
                            var subproject = Path.Combine(folder, match.Value[7..]);
                            //获取绝对子模块路径
                            yield return Path.GetFullPath(subproject);
                        }
                        //如果有.gitmodules直接跳过
                        continue;
                    }
                }
                enumerator?.Dispose();

                //深度查找子目录
                try
                {
                    foreach (var subfolder in Directory.EnumerateDirectories(folder))
                    {
                        queue.Enqueue(subfolder);
                    }
                }
                catch { }
            }
        }
        /// <summary>
        /// 执行指令
        /// </summary>
        /// <param name="project"></param>
        /// <param name="command"></param>
        /// <param name="retry"></param>
        /// <param name="setworkdir"></param>
        /// <returns></returns>
        public static string ExcuteCommand(string project, string command, uint retry = uint.MaxValue, bool setworkdir = true)
        {
            command = command.Trim();

            using (var scope = new ConsoleScope(foreground: ConsoleColor.Blue))
            {
                Console.Write($"\"{command}\"");
            }
            Console.Write($":{project}");

            var output = "";
            for (int i = 0; i < retry; i++)
            {
                output = ExcuteGitCommand(project, command, setworkdir);

                if (/*exitcode != 0 || */errorexp.IsMatch(output))
                {
                    using (var scope = new ConsoleScope(foreground: ConsoleColor.Red))
                    {
                        Console.WriteLine(output);
                        var count = i + 1;
                        if (count < retry)
                        {
                            Console.WriteLine($"第{count}次处理失败,将开始尝试第{count + 1}次处理!");
                        }
                    }
                }
                else
                {
                    using (var scope = new ConsoleScope(foreground: ConsoleColor.Green))
                    {
                        Console.WriteLine(output);
                    }
                    break;
                }
            }
            return output;
        }

        public static string ExcuteGitCommand(string directory, string command, bool setworkdir = true)
        {
            string? output;
            var startinfo = new ProcessStartInfo()
            {
                FileName = "cmd.exe",
                Arguments = $"/c chcp 65001 >nul && git {command}",
                // 重定向标准输出
                RedirectStandardOutput = true,
                // 重定向标准错误
                RedirectStandardError = true,
                // 不使用系统shell
                UseShellExecute = false,
                // 不创建窗口
                CreateNoWindow = true,
                //设置输入输出编码为UTF8，防止乱码
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            if (setworkdir)
            {
                startinfo.WorkingDirectory = directory;
            }
            // 启动进程
            StartProcess(startinfo, out var exitcode, out output);
            return output;
        }

        /// <summary>
        /// 开启一个进程并等待其完成
        /// </summary>
        /// <param name="startinfo"></param>
        /// <param name="exitcode"></param>
        /// <param name="output"></param>
        public static void StartProcess(ProcessStartInfo startinfo, out int exitcode, out string output)
        {
            output = "";
            using (Process process = new Process { StartInfo = startinfo })
            {
                process.Start();

                // 读取标准输出
                output += process.StandardOutput.ReadToEnd();
                // 读取标准错误
                output += process.StandardError.ReadToEnd();
                // 等待进程完成
                process.WaitForExit();

                exitcode = process.ExitCode;
            }
        }
        /// <summary>
        /// 从Git仓库的URL中获取项目名称
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string GetProjectNameFromUrl(string? url)
        {
            string folder;
            int lst = url.LastIndexOf('/');
            folder = url[(lst + 1)..url.Length];
            lst = folder.LastIndexOf('.');
            folder = folder[..lst];
            return folder;
        }
    }

}