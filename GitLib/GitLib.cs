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
    private static Regex fatal = new Regex("fatal|error", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string[] FindProjects(string folder)
    {
        var projects = new List<string>();
        SearchSubProjects(folder, projects);
        return projects.ToArray();
    }
    private static void SearchSubProjects(string folder, List<string> result)
    {
        //检查是否有子模块
        var mapper = Path.Combine(folder, ".gitmodules");
        if (File.Exists(mapper))
        {
            var content = File.ReadAllText(mapper);
            foreach (Match match in moduleexp.Matches(content))
            {
                var subproject = Path.Combine(folder, match.Value[7..]);
                //检查子文件夹
                SearchSubProjects(subproject, result);
                result.Add(subproject);
            }
            result.Add(folder);
        }
        else
        {
            var project = Path.Combine(folder, ".git");
            if (Directory.Exists(project))
            {
                result.Add(folder);
            }
            //遍历子文件夹
            var subs = Directory.GetDirectories(folder);
            for (int i = 0; i < subs.Length; i++)
            {
                SearchSubProjects(subs[i], result);
            }
        }
    }
    public static void ExcuteCommand(string directory, string[] commands, bool setworkdir = true)
    {
        var orgcolor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.White;
        for (int i = 0; i < commands.Length; i++)
        {
            int count = 1;
            var cmd = commands[i];

            Console.WriteLine(cmd);

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
                string output = StartProcess(startinfo);
                if (fatal.IsMatch(output))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(output);
                    Console.WriteLine($"第{count++}次处理失败,将开始尝试第{count}次处理!");
                    needretry = true;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(output);
                    needretry = false;
                }
            }
        }
        Console.ForegroundColor = orgcolor;
    }

    public static string StartProcess(ProcessStartInfo startinfo)
    {
        string output = null;
        using (Process process = new Process { StartInfo = startinfo })
        {
            process.Start();

            // 读取标准输出
            output += process.StandardOutput.ReadToEnd();
            // 读取标准错误
            output += process.StandardError.ReadToEnd();
            // 等待进程完成
            process.WaitForExit();
        }

        return output;
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
