using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomTools
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/10/20 19:42:54
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks>菜单标记</remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class MenuItemAttribute : Attribute
    {
        public string DeclaringType { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// 绘制顺序
        /// </summary>
        public int Order { get; private set; }
        /// <summary>
        /// 绘制类别
        /// </summary>
        public Catgray Catgray { get; private set; }
        public MenuItemAttribute(string name, int order, Catgray catgray)
        {
            Name = name;
            Order = order;
            Catgray = catgray;
        }
    }
}