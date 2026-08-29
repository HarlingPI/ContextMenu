// 本文件由 Codex 新增

#region 由 Codex 添加
using PIToolKit.Public.Utils;
using System;
using System.Diagnostics;
using System.IO;

namespace CustomTools.Tools
{
    /// <summary>
    /// 打开统一配置文件工具：在系统默认编辑器中打开 Configs/config.cfg。
    /// </summary>
    /// <remarks>配置</remarks>
    [MenuItem("打开配置文件", 0, Catgray.Manage)]
    public class Settings : ITool
    {
        public void Process(string path)
        {
            var configPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ToolsConfig.ConfigPath));
            Console.WriteLine($"打开配置文件:{configPath}");

            if (!File.Exists(configPath))
            {
                Console.WriteLine($"配置文件不存在:{configPath}");
                return;
            }

            OpenConfig(configPath);
        }

        // 由 Codex 修改：.cfg 没有默认关联时改用记事本打开
        private static void OpenConfig(string configPath)
        {
            try
            {
                FileUtils.OpenPath(configPath);
            }
            catch
            {
                var startInfo = new ProcessStartInfo("notepad.exe", $"\"{configPath}\"")
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(startInfo);
            }
        }
    }
}
#endregion
