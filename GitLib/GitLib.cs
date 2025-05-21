using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

public static class GitLib
{
    private static Regex moduleexp = new Regex("path = [\\w]+", RegexOptions.Compiled);
    private static Regex errorexp = new Regex("fatal:|error:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string[] FindProjects(string folder)
    {
        var projects = new List<string>();
        SearchSubProjects(folder, projects);
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
        var subs = Directory.GetDirectories(folder);
        for (int i = 0; i < subs.Length; i++)
        {
            DeepFirstSearch(subs[i], result);
        }
        result.Add(folder);
    }
    public static void ExcuteCommand(string directory, string[] commands, bool setworkdir = true)
    {
        var orgcolor = Console.ForegroundColor;
        for (int i = 0; i < commands.Length; i++)
        {
            int count = 1;
            var cmd = commands[i];

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"\"{cmd}\":{directory}");

            bool needretry = true;
            while (needretry)
            {
                var startinfo = new ProcessStartInfo()
                {
                    FileName = "git",
                    Arguments = cmd,
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
                StartProcess(startinfo, out var exitcode, out string output);
                needretry = exitcode != 0 || errorexp.IsMatch(output);

                if (needretry)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(output);
                    Console.WriteLine($"第{count++}次处理失败,将开始尝试第{count}次处理!");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(output);
                }
            }
        }
        Console.ForegroundColor = orgcolor;
    }

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
