using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Ludens
{
    public static class JsonHelper
    {
        public static string Serialize<T>(T value, bool pretty)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                string json = Encoding.UTF8.GetString(stream.ToArray());
                return pretty ? Pretty(json) : json;
            }
        }

        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new InvalidDataException("文件内容为空。");
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                object value = serializer.ReadObject(stream);
                if (value == null) throw new InvalidDataException("无法读取 JSON 数据。");
                return (T)value;
            }
        }

        public static string Pretty(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;
            StringBuilder sb = new StringBuilder(json.Length + 512);
            int indent = 0;
            bool inString = false;
            bool escaping = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (inString)
                {
                    sb.Append(c);
                    if (escaping) escaping = false;
                    else if (c == '\\') escaping = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    sb.Append(c);
                }
                else if (c == '{' || c == '[')
                {
                    sb.Append(c);
                    indent++;
                    sb.Append('\n');
                    AppendIndent(sb, indent);
                }
                else if (c == '}' || c == ']')
                {
                    indent = Math.Max(0, indent - 1);
                    sb.Append('\n');
                    AppendIndent(sb, indent);
                    sb.Append(c);
                }
                else if (c == ',')
                {
                    sb.Append(c);
                    sb.Append('\n');
                    AppendIndent(sb, indent);
                }
                else if (c == ':')
                {
                    sb.Append(": ");
                }
                else if (!char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static void AppendIndent(StringBuilder sb, int count)
        {
            for (int i = 0; i < count; i++) sb.Append("  ");
        }
    }
}
