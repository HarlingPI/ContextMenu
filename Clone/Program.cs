namespace Clone
{
    internal class Program
    {
        static void Main(string[] args)
        {
            List<string> modules = GitLib.SearchModules();

            for (int i = 0; i < modules.Count; i++)
            {
                Console.WriteLine(modules[i]);
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("'clone'已完成,请按任意键退出!");
            Console.Read();
        }
    }
}
