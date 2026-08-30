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

        // 由 Codex 修改：优先用 VSCode 打开，找不到再退回记事本
        private static void OpenConfig(string configPath)
        {
            if (!TryOpenWithVSCode(configPath))
            {
                var startInfo = new ProcessStartInfo("notepad.exe", $"\"{configPath}\"")
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(startInfo);
            }
        }

        private static bool TryOpenWithVSCode(string path)
        {
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo("code.exe", $"\"{path}\"")
                {
                    UseShellExecute = true
                });
                return true;
            }
            catch
            {
                // 尝试常见安装目录
            }

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "Code.exe"),
                Path.Combine(programFilesX86, "Microsoft VS Code", "Code.exe")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    System.Diagnostics.Process.Start(new ProcessStartInfo(candidate, $"\"{path}\"")
                    {
                        UseShellExecute = true
                    });
                    return true;
                }
            }
            return false;
        }
    }
}
#endregion
