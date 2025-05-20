using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitTool.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/20 21:54:04
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Help : Command
    {
        private static string[] list;
        static Help()
        {
            var cmdtype = typeof(Command);

            list = cmdtype
                .Assembly
                .GetTypes()
                .Where(t => t.IsSubclassOf(cmdtype))
                .Where(t => !t.IsAbstract)
                .Select(t => t.Name)
                .ToArray();
        }
        public Help(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, params string[] args)
        {
            var orgcolor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("All Commands:");
            for (int i = 0; i < list.Length; i++)
            {
                Console.WriteLine($"\t{list[i]}");
            }
            Console.ForegroundColor = orgcolor;
        }
    }
}