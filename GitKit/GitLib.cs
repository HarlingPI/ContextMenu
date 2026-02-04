using ConsoleKit;
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
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
        private static Regex errorexp = new Regex("fatal:|error:", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex guidexp = new Regex(@"[a-z0-9]{40}", RegexOptions.Compiled);
        /// <summary>
        /// 查找指定路径下的所有Git项目
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static IEnumerable<ProjectInfo> FindProjects(string folder)
        {
            var projects = SearchGitProjects(folder);
            //如果向下没有找到任何Git项目，则向上查找Git项目
            if (projects.IsNullOrEmpty())
            {
                var root = ExcuteGitCommand(folder, "rev-parse --show-toplevel", false);
                if (!root.StartsWith("fatal: not a git repository"))
                {
                    var info = new ProjectInfo()
                    {
                        Path = root.Trim().Replace('/', '\\'),
                        Branch = ExcuteGitCommand(folder, "rev-parse --abbrev-ref HEAD", false).Trim()
                    };
                    yield return GetGitInfo(root.Trim().Replace('/', '\\'));
                }
            }
            foreach (var item in projects)
            {
                yield return item;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ProjectInfo[] SearchGitProjects(string path)
        {
            return SearchGitProjectsInternal(path)
                .Where(p => !guidexp.IsMatch(p.Branch))
                .Distinct()
                .ToArray();
        }
        private static IEnumerable<ProjectInfo> SearchGitProjectsInternal(string path)
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
                    yield return GetGitInfo(Path.GetDirectoryName(folder));
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
                        yield return GetGitInfo(folder);
                        var lines = File.ReadAllLines(file);
                        var modules = lines.Select((l, i) => new { Line = l.Trim(), Index = i })
                            .Where(x => x.Line.StartsWith("[submodule"))
                            .Select(x => x.Index)
                            .ToArray();
                        for (int i = 0; i < modules.Length; i++)
                        {
                            var info = new ProjectInfo();
                            int start = modules[i] + 1;
                            while (info.IsEmpty)
                            {
                                var line = lines[start].Trim();

                                if (line.StartsWith("path"))
                                {
                                    var subproject = Path.Combine(folder, line[7..]);
                                    //获取绝对子模块路径
                                    info.Path = Path.GetFullPath(subproject);
                                }
                                else if (line.StartsWith("branch"))
                                {
                                    info.Branch = line[9..];
                                }
                                start++;
                            }
                            yield return info;
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
        private static ProjectInfo GetGitInfo(string folder)
        {
            var content = File.ReadAllText(folder + "/.git/HEAD");
            var pb = content[(content.LastIndexOf('/') + 1)..^1];
            return new ProjectInfo() { Path = folder, Branch = pb };
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
            Console.WriteLine($":{project}");

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