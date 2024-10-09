namespace Clone
{
    internal class Program
    {
        static void Main(string[] args)
        {
            List<string> modules = GitLib.SearchModules();

            for (int i = 0; i < modules.Count; i++)
            {
                string? url = null;
                while (url == null)
                {
                    Console.WriteLine("请输入目标项目的地址:");

                    url = Console.ReadLine();
                }
                string folder = GetFolderFromUrl(url);
                string path = modules[i] + $"/{folder}";
                string cmd = $"clone --recursive {url} {path}";

                Console.WriteLine($"项目：{url}克隆中...");
                GitLib.ExcuteCommand(path, new[] { cmd }, false);
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("'clone'已完成,请按任意键退出!");
            Console.Read();
        }

        private static string GetFolderFromUrl(string? url)
        {
            string folder;
            int lst = url.LastIndexOf('/');
            folder = url[(lst + 1)..url.Length];
            lst = folder.LastIndexOf('.');
            folder = folder[..lst];
            return folder;
        }
    }
}
