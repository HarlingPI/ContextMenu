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
        public override string Description => "列出程序支持的所有命令";

        public override string Formate => "help";

        public override string[] Parametes => null;
        public Help(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            var orgcolor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            var cmds = Program.GetCommands();
            for (int i = 0; i < cmds.Length; i++)
            {
                var cmd = cmds[i];
                Console.WriteLine($"{cmd.GetType().Name.ToLower()}\t{cmd.Description}");
                Console.WriteLine($"{cmd.Formate}");
                var paramsinfo = cmd.Parametes;
                if(paramsinfo != null && paramsinfo.Length > 0)
                {
                    for (int j = 0; j < paramsinfo.Length; j++)
                    {
                        Console.WriteLine($"\t{paramsinfo[j]}");
                    }
                }
                Console.WriteLine();
            }
            Console.ForegroundColor = orgcolor;
        }
    }
}