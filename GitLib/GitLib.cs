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
    private static Regex fatal = new Regex("fatal|error", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    public static void ExcuteCommand(string directory, string[] commands)
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
                    WorkingDirectory=directory,
                    // 重定向标准输出
                    RedirectStandardOutput = true,
                    // 重定向标准错误
                    RedirectStandardError = true,
                    // 不使用系统shell
                    UseShellExecute = false,
                    // 不创建窗口
                    CreateNoWindow = true
                };
                // 启动进程
                using (Process process = new Process { StartInfo = startinfo })
                {
                    process.Start();

                    string output = null;
                    // 读取标准输出
                    output += process.StandardOutput.ReadToEnd();
                    // 读取标准错误
                    output += process.StandardError.ReadToEnd();
                    // 等待进程完成
                    process.WaitForExit();

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
    }
}
