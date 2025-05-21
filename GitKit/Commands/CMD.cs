using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/21 20:56:35
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class CMD : Command
    {
        public CMD(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, params string[] args)
        {
            var index = Array.IndexOf(args, "runas");
            var isas = index != -1;
            if (!isas)
            {
                Console.WriteLine(ExecuteNormal(string.Join(' ', args)));
            }
            else
            {
                args = args.Where(a => a != "runas").ToArray();
                Console.WriteLine(ExecuteAdmin(string.Join(' ', args)));
            }
        }
        private string ExecuteNormal(string command)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    WorkingDirectory = WorkingFolder,
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process())
                {
                    process.StartInfo = processInfo;
                    process.Start();

                    var result = "";
                    // 异步读取输出和错误流，避免死锁
                    result += process.StandardOutput.ReadToEnd();
                    result += process.StandardError.ReadToEnd();

                    process.WaitForExit(); // 等待命令执行完成

                    return result;
                }
            }
            catch (Exception ex)
            {
                return $"Exception: {ex.Message}";
            }
        }

        private string ExecuteAdmin(string command)
        {
            //生成临时文件路径
            string tempFile = Path.GetTempFileName(); 
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    WorkingDirectory = WorkingFolder,
                    FileName = "cmd.exe",
                    //输出重定向到临时文件
                    Arguments = $"/c cd /d \"{WorkingFolder}\" && {command} > \"{tempFile}\" 2>&1", 
                    Verb = "runas",
                    UseShellExecute = true
                };

                var process = Process.Start(processInfo);
                //等待命令执行完成
                process.WaitForExit(); 
                //读取临时文件信息
                return File.ReadAllText(tempFile, Encoding.GetEncoding("GBK"));
            }
            catch (Exception ex)
            {
                return $"Exception: {ex.Message}";
            }
            finally
            {
                //清理临时文件
                File.Delete(tempFile); 
            }
        }
    }
}