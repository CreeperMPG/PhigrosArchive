using System;
using System.Collections.Generic;

namespace PhigrosArchive.Utils
{
    public static class PathUtils
    {
        public static long GetTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalMilliseconds);
        }
        public static string ResolvePath(string current, string input)
        {
            // 空输入返回当前路径
            if (string.IsNullOrWhiteSpace(input)) return current;

            // 绝对路径直接返回
            if (input.StartsWith("/")) return NormalizePath(input);

            // 拆分当前路径和输入路径
            var currentParts = current.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var inputParts = input.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries);

            var resultParts = new List<string>(current == "/" ? Array.Empty<string>() : currentParts);

            foreach (var part in inputParts)
            {
                if (part == ".")
                {
                    // 当前目录，跳过
                    continue;
                }
                else if (part == "..")
                {
                    // 上级目录，移除最后一个
                    if (resultParts.Count > 0)
                        resultParts.RemoveAt(resultParts.Count - 1);
                }
                else
                {
                    resultParts.Add(part);
                }
            }

            return "/" + string.Join('/', resultParts);
        }

        private static string NormalizePath(string path)
        {
            // 处理绝对路径中的 . 和 ..
            var parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var resultParts = new List<string>();
            foreach (var part in parts)
            {
                if (part == ".")
                    continue;
                else if (part == "..")
                {
                    if (resultParts.Count > 0)
                        resultParts.RemoveAt(resultParts.Count - 1);
                }
                else
                    resultParts.Add(part);
            }
            return "/" + string.Join('/', resultParts);
        }
    }
}