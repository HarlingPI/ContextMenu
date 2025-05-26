using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/26 19:30:51
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Url : Command
    {
        public Url(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            var cmd = "";
            if (args == null || args.Length == 0)
            {
                cmd = "remote get-url --push --all origin";
            }

            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "ad":
                        cmd = $"remote set-url --add origin {string.Join(' ', args[1..])}";
                        break;
                    case "rm":
                        cmd = $"remote set-url --delete origin {string.Join(' ', args[1..])}";
                        break;
                    default:
                        break;
                }
            }

            for (int i = 0; i < projects.Length; i++)
            {
                GitLib.ExcuteCommand(projects[i], cmd, retry);
            }
        }
    }
}