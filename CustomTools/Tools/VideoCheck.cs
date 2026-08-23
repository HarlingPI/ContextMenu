// 本文件由 Codex 新增

namespace CustomTools.Tools
{
    /// <summary>
    /// 视频查重工具，当前仅保留菜单入口，功能待实现。
    /// </summary>
    [MenuItem("视频查重", 5, Catgray.File)]
    public class VideoCheck : ITool
    {
        public void Process(string path)
        {
            // 视频查重功能尚未实现，先输出占位提示。
            Console.WriteLine("视频查重功能尚未实现");
        }
    }
}
