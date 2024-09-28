using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace Config
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (!IsRunAsAdmin())
            {
                Console.WriteLine("当前没有管理员权限，正在尝试以管理员权限重新启动...");
                RunAsAdmin();
                return;
            }
            Console.WriteLine("程序以管理员权限运行。");
            //安装Package
            InstallPythonPackage();
            //文件类型注册
            RegisterFile();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("配置已完成,请按任意键退出!");
            Console.Read();
        }

        private static Regex alreadyexp = new Regex("already", RegexOptions.Compiled);
        private static void InstallPythonPackage()
        {
            var cmds = new[]
                        {
                ("pip","install GitPython"),
                ("pip","install pyinstaller"),
                ("pip","install pywin32"),
                ("git","config --global core.autocrlf false"),
            };

            for (int i = 0; i < cmds.Length; i++)
            {
                var cmd = cmds[i];
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"即将开始执行命令:{cmd.Item1} {cmd.Item2}");
                var startinfo = new ProcessStartInfo()
                {
                    FileName = cmd.Item1,
                    Arguments = cmd.Item2,
                    // 重定向标准输出
                    RedirectStandardOutput = true,
                    // 重定向标准错误
                    RedirectStandardError = true,
                    // 不使用系统shell
                    UseShellExecute = false,
                    // 不创建窗口
                    CreateNoWindow = true
                };
                string output = GitLib.StartProcess(startinfo);
                if (alreadyexp.IsMatch(output))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(output);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(output);
                }
            }
        }

        private static bool IsRunAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        private static void RunAsAdmin()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath, // 当前程序路径
                UseShellExecute = true, // 使用外壳执行
                Verb = "runas" // 以管理员身份运行
            };

            try
            {
                Process.Start(processInfo);
            }
            catch (Exception)
            {
                Console.WriteLine("无法以管理员身份启动程序。");
            }

            Environment.Exit(0); // 关闭当前进程
        }

        private static void RegisterFile()
        {
            var localMachine = Registry.LocalMachine;
            var python = localMachine.OpenSubKey(@"SOFTWARE\Python");
            bool yes = false;
            if (python != null)
            {
                Console.WriteLine("已找到Python的安装目录,即将开始.py文件的注册!");
                yes = true;
            }
            else
            {
                Console.WriteLine("当前系统中未安装Python,仍然开始.py文件的注册？[Y/N]");
                string? input = Console.ReadLine();
                yes = input == "Y";
            }
            python?.Close();
            localMachine?.Close();

            var root = Registry.ClassesRoot;
            try
            {
                python = root?.OpenSubKey(".py", true);
                Console.WriteLine("开始注册右键新建文件项");
                var shell = python.CreateSubKey("ShellNew");
                shell?.SetValue("FileName", "", RegistryValueKind.String);
                var prog = python.CreateSubKey("OpenWithProgids");
                prog.SetValue("VSCode.py", "", RegistryValueKind.String);

                python?.Close();
                shell?.Close();
                prog?.Close();

                string path = null;
                if (SearchVSCode(ref path))
                {
                    Console.WriteLine("开始注册右键打开文件项");
                    python = root.OpenSubKey("*", true);
                    shell = python.CreateSubKey("shell");
                    var open = shell.CreateSubKey("VSCode");
                    var command = open.CreateSubKey("command");

                    command.SetValue("", $"{path} %1", RegistryValueKind.String);
                    open.SetValue("", $"{path},0", RegistryValueKind.String);
                    command?.Close();
                    open?.Close();
                    python?.Close();
                    Console.WriteLine("注册完成");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"注册过程中出现错误'{e.Message}'");
            }
            root?.Close();
        }

        private static bool SearchVSCode(ref string path)
        {
            bool found;
            try
            {
                var cu = Registry.CurrentUser;
                var vscode = cu.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{771FD6B0-FA20-440A-A002-3B3BAC16DC50}_is1");
                path = (string)vscode.GetValue("InstallLocation");
                path = path + "/Code.exe";
                vscode?.Close();
                cu?.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
