using Microsoft.Win32;
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
    /// 时间:   2025/10/14 20:18:17
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Unins : Command
    {
        public override string Description => "卸载软件";

        public override string Formate => "unins";

        public override string[] Parametes => null;
        public Unins(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            DegisterContextMenu();
        }

        private static RegistryKey RootKey => Registry.CurrentUser;
        // 右键菜单项名称
        private const string MenuItemName = "打开Gitkit";
        // 注册表项名称
        private const string RegistryKeyName = "OpenGitkit";
        private const string DirectoryKey = @$"Software\Classes\Directory\shell\{RegistryKeyName}";
        private const string BackgroundKey = @$"Software\Classes\Directory\Background\shell\{RegistryKeyName}";

        /// <summary>
        /// 查找并注册系统右键菜单
        /// </summary>
        public static void RegisterContextMenu()
        {
            var root = RootKey.OpenSubKey(DirectoryKey);
            if (root == null)
            {
                try
                {
                    Console.WriteLine("初次运行,开始注册右键菜单……");
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    var cmd = $"\"{exePath}\" \"%V \"";

                    // 注册到目录右键菜单
                    using (var key = RootKey.CreateSubKey(DirectoryKey))
                    {
                        SetKey(key, exePath, cmd);
                    }

                    // 注册到目录背景右键菜单
                    using (var key = RootKey.CreateSubKey(BackgroundKey))
                    {
                        SetKey(key, exePath, cmd);
                    }
                    Console.WriteLine("右键菜单注册成功！");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"注册失败: {ex.Message}");
                }
            }

            static void SetKey(RegistryKey key, string? exePath, string cmd)
            {
                key.SetValue("", MenuItemName);
                key.SetValue("Icon", exePath);
                key.SetValue("Position", "Middle");

                using (var commandKey = key.CreateSubKey("command"))
                {
                    commandKey.SetValue("", cmd);
                }
            }
        }

        /// <summary>
        /// 注销右键菜单
        /// </summary>
        private static void DegisterContextMenu()
        {
            try
            {
                RootKey.DeleteSubKeyTree(DirectoryKey, false);
                RootKey.DeleteSubKeyTree(BackgroundKey, false);

                Console.WriteLine("右键菜单已注销！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"注销失败: {ex.Message}");
            }
        }
    }
}