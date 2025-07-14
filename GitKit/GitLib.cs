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
        public static string[] FindProjects(string folder)
        {
            var projects = new List<string>();
            SearchSubProjects(folder, projects);
            //如果向下没有找到任何Git项目，则向上查找Git项目
            if (projects.IsNullOrEmpty())
            {
                var root = ExcuteGitCommand(folder, "rev-parse --show-toplevel", false);
                projects.Add(root.Trim().Replace('/', '\\'));
            }
            return projects.ToArray();
        }
        private static void SearchSubProjects(string folder, List<string> result)
        {
            var subfolders = new List<string>();
            DeepFirstSearch(folder, subfolders);
            //遍历子文件夹，判断是否为git项目
            for (int i = 0; i < subfolders.Count; i++)
            {
                var subfolder = subfolders[i];
                //判断是否为独立git项目
                var project = Path.Combine(subfolder, ".git");
                if (Directory.Exists(project))
                {
                    result.Add(subfolder);
                }
                else
                {
                    //检查是否有子模块
                    var mapper = Path.Combine(subfolder, ".gitmodules");
                    if (File.Exists(mapper))
                    {
                        var content = File.ReadAllText(mapper);
                        foreach (Match match in moduleexp.Matches(content))
                        {
                            var subproject = Path.Combine(subfolder, match.Value[7..]);
                            //获取绝对子模块路径
                            subproject = Path.GetFullPath(subproject);
                            result.Add(subproject);
                        }
                        result.Add(subfolder);
                    }
                }
            }
        }
        private static void DeepFirstSearch(string folder, List<string> result)
        {
            try
            {
                var subs = Directory.GetDirectories(folder);
                for (int i = 0; i < subs.Length; i++)
                {
                    DeepFirstSearch(subs[i], result);
                }
                result.Add(folder);
            }
            catch (UnauthorizedAccessException)
            {

            }
            catch (IOException)
            {

            }
            catch (Exception ex)
            {
                Console.WriteLine($"未知错误: {ex.Message}");
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
            var orgcolor = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"\"{command}\":{project}");
            var output = "";

            for (int i = 0; i < retry; i++)
            {
                output = ExcuteGitCommand(project, command, setworkdir);

                if (/*exitcode != 0 || */errorexp.IsMatch(output))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(output);
                    var count = i + 1;
                    if (count < retry)
                    {
                        Console.WriteLine($"第{count}次处理失败,将开始尝试第{count + 1}次处理!");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(output);
                    break;
                }
            }
            Console.ForegroundColor = orgcolor;
            return output;
        }

        private static string ExcuteGitCommand(string directory, string command, bool setworkdir = true)
        {
            string? output;
            var startinfo = new ProcessStartInfo()
            {
                FileName = "cmd.exe",
                Arguments = $"/c git {command}",
                // 重定向标准输出
                RedirectStandardOutput = true,
                // 重定向标准错误
                RedirectStandardError = true,
                // 不使用系统shell
                UseShellExecute = false,
                // 不创建窗口
                CreateNoWindow = true
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