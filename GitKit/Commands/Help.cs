using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/20 21:54:04
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Help : Command
    {
        public Help(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, params string[] args)
        {
            var orgcolor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("All Commands:");
            var names = Program.GetCommandsNames();
            for (int i = 0; i < names.Length; i++)
            {
                Console.WriteLine($"\t{names[i]}");
            }
            Console.ForegroundColor = orgcolor;
        }
    }
}