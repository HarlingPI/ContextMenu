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
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// 图标资源名称
        /// </summary>
        public string Icon { get; private set; }
        /// <summary>
        /// 绘制顺序
        /// </summary>
        public int Order { get; private set; }
        /// <summary>
        /// 绘制类别
        /// </summary>
        public int Catgray { get; private set; }
        public MenuItemAttribute(string name, string icon, int order, int catgray)
        {
            Name = name;
            Icon = icon;
            Order = order;
            Catgray = catgray;
        }
    }
}