using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomTools.Tools
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/10/20 19:52:49
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    [MenuItem("卸载", 0, Catgray.Manage)]
    public class Unins : ITool
    {
        public void Process(string path)
        {
            Console.WriteLine("任意键开始卸载");
            Console.ReadKey();
            SystemMenu.UnInstall();
            Console.WriteLine("卸载成功");
        }
    }
}