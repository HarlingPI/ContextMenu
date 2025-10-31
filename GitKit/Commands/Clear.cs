using ConsoleKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/10/31 14:47:28
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Clear : Command
    {
        public override string Description => "清空内容";

        public override string Formate => "clear a|s";

        public override string[] Parametes => new[]
        {
            "a:清空缓冲区",
            "s:清空当前屏幕内容",
        };
        public Clear(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            switch (args[0])
            {
                case "a":
                    Console.Clear();
                    break;
                case "s":
                    Ansi.ClearScreen();
                    break;
                default:
                    throw new ArgumentException("参数错误");
            }
        }
    }
}