// 本文件由 Codex 新增

using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Xml;

namespace CustomTools.Tools
{
    /// <summary>
    /// 读取所有工具统一配置文件
    /// </summary>
    public sealed class ToolsConfig
    {
        public const string ConfigPath = "Configs/config.xml";

        public List<string> Fixes = new List<string>();
        public List<string> Ignores = new List<string>();
        public Dictionary<string, string> Mapper = new Dictionary<string, string>();

        public static ToolsConfig Load()
        {
            var config = new ToolsConfig();
            if (!FileUtils.FileIsExist(ConfigPath))
            {
                return config;
            }

            var doc = new XmlDocument();
            doc.Load(ConfigPath);
            var root = doc.DocumentElement;
            if (root == null)
            {
                return config;
            }

            var fixes = root.SelectSingleNode("./Fixes");
            if (fixes != null)
            {
                foreach (XmlNode item in fixes.ChildNodes)
                {
                    var value = item.Attributes?["V"]?.Value;
                    if (value != null)
                    {
                        config.Fixes.Add(value);
                    }
                }
            }

            var classify = root.SelectSingleNode("./Classify");
            if (classify != null)
            {
                var ignores = classify.SelectSingleNode("./Ignores");
                if (ignores != null)
                {
                    foreach (XmlNode item in ignores.ChildNodes)
                    {
                        var value = item.Attributes?["V"]?.Value;
                        if (value != null)
                        {
                            config.Ignores.Add(value);
                        }
                    }
                }

                var mapper = classify.SelectSingleNode("./Mapper");
                if (mapper != null)
                {
                    foreach (XmlNode item in mapper.ChildNodes)
                    {
                        var key = item.Attributes?["K"]?.Value;
                        var value = item.Attributes?["V"]?.Value;
                        if (key != null && value != null)
                        {
                            config.Mapper.TryAdd(key, value);
                        }
                    }
                }
            }

            return config;
        }
    }
}
