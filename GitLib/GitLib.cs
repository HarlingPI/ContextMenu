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

    public static List<string> SearchModules()
    {
        string path = System.Environment.CurrentDirectory;
        //先查找gitmodules文件
        string mapper = path + "/.gitmodules";
        List<string> modules = new List<string>();
        if (File.Exists(mapper))
        {
            string content = File.ReadAllText(mapper);
            foreach (Match item in moduleexp.Matches(content))
            {
                modules.Add($"{path}/{item.Value[7..]}");
            }
        }
        modules.Add(path);
        return modules;
    }
    public static void ExcuteCommand(string directory, string[] commands, bool setworkdir = true)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"即将开始处理项目'{directory}'");
        for (int i = 0; i < commands.Length; i++)
        {
            int count = 1;
            var cmd = commands[i];

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
                    Console.WriteLine($"第{count++}次处理失败,失败命令{cmd},将开始尝试{count}次处理!");
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
}
