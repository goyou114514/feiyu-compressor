using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FeiyuZip
{
    internal sealed class CompressionResult
    {
        public string FinalArchivePath;
        public string BaseName;
        public string SourcePath;
        public long InputBytes;
    }

    /// <summary>
    /// 压缩流程（两层，7z 格式）：
    /// 文件/文件夹 → 第一层 7z 密码加密压缩 → 后缀改成 .mp4 → 第二层 7z 密码加密压缩 → 文件名.mp4.7z
    /// </summary>
    internal static class CompressionFlow
    {
        public static List<CompressionResult> Run(
            IList<string> inputs,
            string password,
            Action<string> stage,
            Action<int> progress,
            Action<string> status)
        {
            if (inputs == null || inputs.Count == 0) throw new ArgumentException("没有选择要压缩的文件或文件夹。");
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("请先输入密码。");

            List<string> normalized = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string input in inputs)
            {
                if (string.IsNullOrEmpty(input)) continue;
                string full = Path.GetFullPath(input.Trim().Trim('"'));
                if (!File.Exists(full) && !Directory.Exists(full))
                {
                    throw new FileNotFoundException("找不到文件或文件夹：" + full);
                }
                if (seen.Add(full)) normalized.Add(full);
            }
            if (normalized.Count == 0) throw new ArgumentException("没有选择要压缩的文件或文件夹。");

            List<CompressionResult> results = new List<CompressionResult>();
            for (int i = 0; i < normalized.Count; i++)
            {
                string label = normalized.Count > 1 ? "（" + (i + 1) + "/" + normalized.Count + "）" : "";
                results.Add(CompressOne(normalized[i], password, stage, progress, status, label));
            }
            if (progress != null) progress(100);
            return results;
        }

        private static CompressionResult CompressOne(
            string input,
            string password,
            Action<string> stage,
            Action<int> progress,
            Action<string> status,
            string label)
        {
            bool isDir = Directory.Exists(input);
            string workDir = isDir
                ? Path.GetDirectoryName(input.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : Path.GetDirectoryName(input);
            if (string.IsNullOrEmpty(workDir)) workDir = Path.GetPathRoot(input);

            string baseName = BuildBaseName(input, isDir);
            long inputBytes = 0;
            if (isDir) inputBytes = DirectorySize(input);
            else inputBytes = new FileInfo(input).Length;

            string guid = Guid.NewGuid().ToString("N").Substring(0, 8);
            string tempDir = Path.Combine(workDir, "~feiyu_" + guid + "_tmp");
            string layer1 = Path.Combine(tempDir, "layer1.7z");
            string mp4 = Path.Combine(tempDir, baseName + ".mp4");
            string layer2 = Path.Combine(workDir, "~feiyu_" + guid + "_layer2.7z");
            string finalPath = UniquePath(workDir, baseName + ".mp4.7z");

            try
            {
                Directory.CreateDirectory(tempDir);

                // ---------- 第一层：加密压缩 ----------
                if (stage != null) stage("第一层加密压缩" + label);
                if (progress != null) progress(0);
                SevenZip.Compress(input, layer1, password, true,
                    delegate (int p) { if (progress != null) progress(p * 50 / 100); },
                    status);

                // ---------- 把后缀改成 .mp4 ----------
                if (stage != null) stage("把后缀改成 .mp4" + label);
                if (progress != null) progress(50);
                File.Move(layer1, mp4);

                // ---------- 第二层：对 mp4 再加密压缩 ----------
                if (stage != null) stage("第二层加密压缩" + label);
                SevenZip.Compress(mp4, layer2, password, true,
                    delegate (int p) { if (progress != null) progress(50 + p / 2); },
                    status);

                File.Move(layer2, finalPath);
                if (progress != null) progress(100);
                if (status != null) status("完成：" + Path.GetFileName(finalPath));

                return new CompressionResult
                {
                    FinalArchivePath = finalPath,
                    BaseName = baseName,
                    SourcePath = input,
                    InputBytes = inputBytes
                };
            }
            finally
            {
                TryDelete(layer1);
                TryDelete(layer2);
                TryDeleteDirectory(tempDir);
            }
        }

        private static string BuildBaseName(string path, bool isDirectory)
        {
            string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (isDirectory) return name;
            if (name.EndsWith(".mp4.7z", StringComparison.OrdinalIgnoreCase)) return name.Substring(0, name.Length - 7);
            if (name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)) return name.Substring(0, name.Length - 3);
            if (name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) return name.Substring(0, name.Length - 4);
            string noExt = Path.GetFileNameWithoutExtension(name);
            if (!string.IsNullOrEmpty(noExt)) return noExt;
            return name;
        }

        private static long DirectorySize(string dir)
        {
            long total = 0;
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(file).Length; } catch { }
                }
            }
            catch { }
            return total;
        }

        private static string UniquePath(string dir, string fileName)
        {
            string path = Path.Combine(dir, fileName);
            if (!File.Exists(path)) return path;
            string withoutExt = Path.GetFileNameWithoutExtension(fileName); // 形如 xxx.mp4
            string ext = Path.GetExtension(fileName);                      // .7z
            for (int i = 1; i < 10000; i++)
            {
                path = Path.Combine(dir, withoutExt + " (" + i + ")" + ext);
                if (!File.Exists(path)) return path;
            }
            throw new IOException("无法生成不重复的文件名。");
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void TryDeleteDirectory(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }
}
