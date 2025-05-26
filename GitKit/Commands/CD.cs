using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/20 22:39:40
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class CD : Command
    {
        private static Regex idxexp = new Regex(@"^\d+$", RegexOptions.Compiled);
        public CD(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            var folder = "";
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                var march = idxexp.Match(arg);

                if (march.Success)
                {
                    var index = int.Parse(march.Value);
                    folder = projects[index];
                    args = args.Where(a => !idxexp.IsMatch(a)).ToArray();
                    break;
                }
            }

            if (string.IsNullOrEmpty(folder) && args != null && args.Length > 0)
            {
                folder = args[0];
                folder = folder.Replace("\"", "");
            }
            Program.InitProgram(folder);
        }
    }
}