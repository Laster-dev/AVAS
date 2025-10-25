using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TeamServer.Config
{
    /// <summary>
    /// INI配置文件解析器
    /// </summary>
    public class IniConfig
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections = new();
        private readonly string _filePath;

        public IniConfig(string filePath)
        {
            _filePath = filePath;
            LoadFromFile();
        }

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        private void LoadFromFile()
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            try
            {
                var lines = File.ReadAllLines(_filePath, Encoding.UTF8);
                string currentSection = "";

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // 跳过空行和注释
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#") || trimmedLine.StartsWith(";"))
                    {
                        continue;
                    }

                    // 处理节名 [SectionName]
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                        if (!_sections.ContainsKey(currentSection))
                        {
                            _sections[currentSection] = new Dictionary<string, string>();
                        }
                    }
                    // 处理键值对 key=value
                    else if (trimmedLine.Contains("="))
                    {
                        var parts = trimmedLine.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();
                            
                            if (!_sections.ContainsKey(currentSection))
                            {
                                _sections[currentSection] = new Dictionary<string, string>();
                            }
                            
                            _sections[currentSection][key] = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 加载INI配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取字符串值
        /// </summary>
        public string GetString(string section, string key, string defaultValue = "")
        {
            if (_sections.ContainsKey(section) && _sections[section].ContainsKey(key))
            {
                return _sections[section][key];
            }
            return defaultValue;
        }

        /// <summary>
        /// 获取整数值
        /// </summary>
        public int GetInt(string section, string key, int defaultValue = 0)
        {
            var value = GetString(section, key, defaultValue.ToString());
            if (int.TryParse(value, out int result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// 获取布尔值
        /// </summary>
        public bool GetBool(string section, string key, bool defaultValue = false)
        {
            var value = GetString(section, key, defaultValue.ToString().ToLower());
            return value.ToLower() switch
            {
                "true" => true,
                "1" => true,
                "yes" => true,
                "on" => true,
                _ => false
            };
        }

        /// <summary>
        /// 获取字符串列表
        /// </summary>
        public List<string> GetStringList(string section, string key, List<string> defaultValue = null)
        {
            var value = GetString(section, key, "");
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue ?? new List<string>();
            }

            var items = value.Split(',');
            var result = new List<string>();
            foreach (var item in items)
            {
                var trimmed = item.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    result.Add(trimmed);
                }
            }
            return result;
        }

        /// <summary>
        /// 获取整数列表
        /// </summary>
        public List<int> GetIntList(string section, string key, List<int> defaultValue = null)
        {
            var stringList = GetStringList(section, key);
            var result = new List<int>();
            
            foreach (var item in stringList)
            {
                if (int.TryParse(item, out int value))
                {
                    result.Add(value);
                }
            }
            
            return result.Count > 0 ? result : (defaultValue ?? new List<int>());
        }

        /// <summary>
        /// 设置字符串值
        /// </summary>
        public void SetString(string section, string key, string value)
        {
            if (!_sections.ContainsKey(section))
            {
                _sections[section] = new Dictionary<string, string>();
            }
            _sections[section][key] = value;
        }

        /// <summary>
        /// 设置整数值
        /// </summary>
        public void SetInt(string section, string key, int value)
        {
            SetString(section, key, value.ToString());
        }

        /// <summary>
        /// 设置布尔值
        /// </summary>
        public void SetBool(string section, string key, bool value)
        {
            SetString(section, key, value.ToString().ToLower());
        }

        /// <summary>
        /// 设置字符串列表
        /// </summary>
        public void SetStringList(string section, string key, List<string> value)
        {
            SetString(section, key, string.Join(",", value));
        }

        /// <summary>
        /// 设置整数列表
        /// </summary>
        public void SetIntList(string section, string key, List<int> value)
        {
            SetString(section, key, string.Join(",", value));
        }

        /// <summary>
        /// 保存到文件
        /// </summary>
        public void SaveToFile()
        {
            try
            {
                var sb = new StringBuilder();
                
                foreach (var section in _sections)
                {
                    sb.AppendLine($"[{section.Key}]");
                    foreach (var kvp in section.Value)
                    {
                        sb.AppendLine($"{kvp.Key}={kvp.Value}");
                    }
                    sb.AppendLine();
                }

                File.WriteAllText(_filePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 保存INI配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查节是否存在
        /// </summary>
        public bool HasSection(string section)
        {
            return _sections.ContainsKey(section);
        }

        /// <summary>
        /// 检查键是否存在
        /// </summary>
        public bool HasKey(string section, string key)
        {
            return _sections.ContainsKey(section) && _sections[section].ContainsKey(key);
        }

        /// <summary>
        /// 获取指定节的所有键
        /// </summary>
        public List<string> GetAllKeys(string section)
        {
            if (_sections.TryGetValue(section, out var keys))
            {
                return keys.Keys.ToList();
            }
            return new List<string>();
        }
    }
}
