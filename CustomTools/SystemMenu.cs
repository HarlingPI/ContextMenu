using CustomTools.Tools;
using Microsoft.Win32;
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CustomTools
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/11/2 9:18:28
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public static class SystemMenu
    {
        private readonly static string exePath = Process.GetCurrentProcess().MainModule?.FileName;
        private static RegistryKey RootKey => Registry.CurrentUser;
        // 右键菜单项名称
        private readonly static string displayName = "自定义工具";
        // 注册表项名称
        private readonly static string mainName = "CustomTools";
        private readonly static string foreKey = @$"Software\Classes\Directory\shell\{mainName}";
        private readonly static string backKey = @$"Software\Classes\Directory\Background\shell\{mainName}";

        /// <summary>
        /// 检查并安装右键菜单
        /// </summary>
        public static void CheckInstall()
        {
            var root = RootKey.OpenSubKey(foreKey);

            var needUpdate = root == null;

            if (!needUpdate)
            {
                var version = (string)root.GetValue("Version");
                var iconpath = (string)root.GetValue("Icon");
                needUpdate = version != Program.Version || exePath != exePath;

                if (needUpdate)
                {
                    Console.WriteLine("旧版本卸载开始");
                    UnInstall();
                    Console.WriteLine("旧版本卸载结束");
                }
            }
            if (needUpdate)
            {
                Console.WriteLine("安装开始");
                //获取工具分组信息
                var groups = Program.Tools.Values
                    .Select(t => t.GetType())
                    .Select(t =>
                    {
                        var attr = t.GetCustomAttribute<MenuItemAttribute>();
                        if (attr != null)
                        {
                            attr.DeclaringType = t.Name;
                        }
                        return attr;
                    })
                    .Where(t => t != null)
                    .GroupBy(a => a.Catgray)
                    .Select(g => (g.Key, g.OrderBy(a => a.Order).ToArray()))
                    .OrderBy(kvp => kvp.Key)
                    .ToArray();
                // 注册到目录右键菜单
                using (var key = RootKey.CreateSubKey(foreKey))
                {
                    SetMainKey(key, displayName, exePath);
                    CreateSubKeys(groups, key);
                }

                using (var key = RootKey.CreateSubKey(backKey))
                {
                    SetMainKey(key, displayName, exePath);
                    CreateSubKeys(groups, key);
                }
                Console.WriteLine("安装结束");
            }
        }
        private static void SetMainKey(RegistryKey key, string displayName, string? exePath)
        {
            key.SetValue("MUIVerb", displayName);
            key.SetValue("Icon", exePath);
            key.SetValue("SubCommands", "");
            key.SetValue("Version", Program.Version);
            key.SetValue("Position", "Bottom");
        }
        private static void CreateSubKeys((Catgray, MenuItemAttribute?[])[] groups, RegistryKey mainkey)
        {
            using (var subCommandsKey = mainkey.CreateSubKey("shell"))
            {
                var counter = 0;
                for (int i = 0; i < groups.Length; i++)
                {
                    var group = groups[i];
                    var items = group.Item2;

                    for (int j = 0; j < items.Length; j++)
                    {
                        var item = items[j];

                        var itemKeyName = $"{counter.ToString().PadLeft(2, '0')}_{item.DeclaringType}";
                        using (var itemKey = subCommandsKey.CreateSubKey(itemKeyName))
                        {
                            itemKey.SetValue("MUIVerb", item.Name, RegistryValueKind.String);
                            itemKey.SetValue("Icon", ExtractIcon(item.DeclaringType), RegistryValueKind.String);

                            using (var commandKey = itemKey.CreateSubKey("command"))
                            {
                                commandKey.SetValue("", $"\"{exePath}\" %V \"{item.DeclaringType}\"", RegistryValueKind.String);
                            }

                            if (i < groups.Length - 1 && j == items.Length - 1)
                            {
                                // 如果不是最后一组，在当前组的最后一个子项后添加分隔线
                                itemKey.SetValue("CommandFlags", 0x40, RegistryValueKind.DWord);
                            }
                        }
                        counter++;
                    }
                }
            }
        }

        /// <summary>
        /// 卸载
        /// </summary>
        public static void UnInstall()
        {
            try
            {
                RootKey.DeleteSubKeyTree(foreKey);
                RootKey.DeleteSubKeyTree(backKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"卸载失败:{ex.Message}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ExtractIcon(string name)
        {
            var bytes = (byte[])Resource.ResourceManager.GetObject(name);
            var path = FileUtils.GetFullPath($"Icons/{name}.ico");
            FileUtils.BytesToFile(bytes, path);
            return path;
        }
    }
}