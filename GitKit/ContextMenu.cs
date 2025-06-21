using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/6/20 22:58:56
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public static class ContextMenu
    {
        // 右键菜单项名称
        private const string MenuItemName = "打开Gitkit";
        // 注册表项名称
        private const string RegistryKeyName = "ShowDirPath";
        private const string DirectoryKey = $"Directory\\shell\\{RegistryKeyName}";
        private const string BackgroundKey = $"Directory\\Background\\shell\\{RegistryKeyName}";

        public static void EditContextMenu()
        {
            Console.WriteLine("请选择操作:");
            Console.WriteLine("1. 注册右键菜单");
            Console.WriteLine("2. 取消注册右键菜单");
            var breaked = false;
            while (!breaked)
            {
                string choice = Console.ReadLine();
                switch (choice)
                {
                    case "1":
                        RegisterContextMenu();
                        breaked = true;
                        break;
                    case "2":
                        DegisterContextMenu();
                        breaked = true;
                        break;
                    default:
                        Console.WriteLine("无效选择,请重新输入!");
                        break;
                }
            }
            Console.WriteLine("操作已完成,按任意键退出…");
            Console.ReadKey();
        }
        private static void RegisterContextMenu()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                var cmd = $"\"{exePath}\" \"%V\"";

                // 注册到目录右键菜单
                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(DirectoryKey))
                {
                    key.SetValue("", MenuItemName);
                    key.SetValue("Icon", exePath);

                    using (RegistryKey commandKey = key.CreateSubKey("command"))
                    {
                        commandKey.SetValue("", cmd);
                    }
                }

                // 注册到目录背景右键菜单
                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(BackgroundKey))
                {
                    key.SetValue("", MenuItemName);
                    key.SetValue("Icon", exePath);

                    using (RegistryKey commandKey = key.CreateSubKey("command"))
                    {
                        commandKey.SetValue("", cmd);
                    }
                }

                Console.WriteLine("右键菜单注册成功！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"注册失败: {ex.Message}");
            }
        }

        // 取消注册右键菜单
        private static void DegisterContextMenu()
        {
            try
            {
                Registry.ClassesRoot.DeleteSubKeyTree(DirectoryKey, false);
                Registry.ClassesRoot.DeleteSubKeyTree(BackgroundKey, false);

                Console.WriteLine("右键菜单已取消注册！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"取消注册失败: {ex.Message}");
            }
        }
    }
}