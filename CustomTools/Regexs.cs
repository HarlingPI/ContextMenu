using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CustomTools
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/10/24 21:00:21
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public static class Regexs
    {
        public static readonly Regex Fixexp = new Regex(@"[〖【({\[][\u0391-\u03A9\u03B1-\u03C9\u4E00-\u9FA5ぁ-ゔ\u30A1-\u30FFA-Za-z0-9ａ-ｚＡ-Ｚ０-９_.· -@🔞_Δ～〜♂♀=*●★（）。]+[\]})】〗]", RegexOptions.Compiled);
        public static Regex bilibiliexp = new Regex(@"\(Av\d{9},P\d+\)", RegexOptions.Compiled);
        public static Regex expisod = new Regex(@"\[\d{2}\]", RegexOptions.Compiled);

        public static Regex Numexp = new Regex("^[零一二三四五六七八九十百千万亿\\d]+$", RegexOptions.Compiled);
        /// <summary>
        /// IDM重复文件标记
        /// </summary>
        public static Regex IDMmark = new Regex("_[1-9]+$", RegexOptions.Compiled);

        private static readonly Dictionary<string, string> chinese = new Dictionary<string, string>
        {
            {"负", "-"},
            {"零", "0"},
            {"一", "1"},
            {"二", "2"},
            {"三", "3"},
            {"四", "4"},
            {"五", "5"},
            {"六", "6"},
            {"七", "7"},
            {"八", "8"},
            {"九", "9"},
            {"十", "10"},
            {"百", "100"},
            {"千", "1000"},
            {"万", "10000"},
            {"亿", "100000000"},
        };
    }
}