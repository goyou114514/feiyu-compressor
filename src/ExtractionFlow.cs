using System;
using System.Collections.Generic;
using System.IO;

namespace FeiyuZip
{
    internal sealed class ExtractionResult
    {
        public string ArchivePath;
        public string OutputDir;
        public string InnerArchiveName;
    }

    /// <summary>
    /// 解压流程（两层，7z 格式）：
    /// 文件.mp4.7z → 第一层解压 → 得到 文件.mp4（内容其实是 7z 包）→ 后缀改回 .7z → 第二层解压 → 还原原始内容
    /// </summary>
    internal static class ExtractionFlow
    {
        public static List<ExtractionResult> Run(
            IList<string> archives,
            string password,
            Action<string> stage,
            Action<int> progress,
            Action<string> status)
        {
            if (archives == null || archives.Count == 0) throw new ArgumentException("没有选择要解压的文件。");
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("请先输入密码。");

            List<ExtractionResult> results = new List<ExtractionResult>();
            for (int i = 0; i < archives.Count; i++)
            {
                string archive = Path.GetFullPath(archives[i].Trim().Trim('"'));
                if (!File.Exists(archive)) throw new FileNotFoundException("找不到压缩文件：" + archive);
                string label = archives.Count > 1 ? "（" + (i + 1) + "/" + archives.Count + "）" : "";
                results.Add(ExtractOne(archive, password, stage, progress, status, label));
                if (progress != null) progress(100);
            }
            return results;
        }

        private static ExtractionResult ExtractOne(
            string archive,
            string password,
            Action<string> stage,
            Action<int> progress,
            Action<string> status,
            string label)
        {
            string outDir = Path.GetDirectoryName(archive);
            if (string.IsNullOrEmpty(outDir)) outDir = Environment.CurrentDirectory;

            string tempDir = Path.Combine(Path.GetTempPath(), "feiyu_unzip_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);

                // ---------- 第一层解压 ----------
                if (stage != null) stage("第一层解压" + label);
                if (progress != null) progress(0);
                SevenZip.Extract(archive, tempDir, password,
                    delegate (int p) { if (progress != null) progress(p * 45 / 100); },
                    status);

                string inner = FindSingleFile(tempDir);
                if (inner == null) throw new IOException("第一层解压后没有找到文件，这个压缩包可能不是本程序生成的。");
                if (!Is7zArchive(inner))
                {
                    throw new IOException("这不是本程序生成的压缩包（内部不是两层加密结构），无法自动解压。");
                }

                // ---------- 后缀改回 .7z ----------
                if (stage != null) stage("把后缀改回 .7z" + label);
                if (!inner.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                {
                    string renamed = Path.ChangeExtension(inner, ".7z");
                    if (string.IsNullOrEmpty(Path.GetExtension(inner))) renamed = inner + ".7z";
                    if (File.Exists(renamed) && !string.Equals(renamed, inner, StringComparison.OrdinalIgnoreCase))
                    {
                        renamed = UniquePath(Path.GetDirectoryName(renamed),
                            Path.GetFileNameWithoutExtension(renamed) + "_inner.7z");
                    }
                    File.Move(inner, renamed);
                    inner = renamed;
                }

                // ---------- 第二层解压 ----------
                if (stage != null) stage("第二层解压" + label);
                if (progress != null) progress(45);
                SevenZip.Extract(inner, outDir, password,
                    delegate (int p) { if (progress != null) progress(47 + p * 51 / 100); },
                    status);

                if (progress != null) progress(100);
                return new ExtractionResult { ArchivePath = archive, OutputDir = outDir, InnerArchiveName = inner };
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }
        }

        private static bool Is7zArchive(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] sig = new byte[6];
                    if (fs.Read(sig, 0, 6) != 6) return false;
                    return sig[0] == 0x37 && sig[1] == 0x7A && sig[2] == 0xBC
                        && sig[3] == 0xAF && sig[4] == 0x27 && sig[5] == 0x1C;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string FindSingleFile(string dir)
        {            List<string> files = new List<string>();
            foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(f);
                if (name.StartsWith("__MACOSX", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase)) continue;
                files.Add(f);
            }
            if (files.Count == 0) return null;
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files[0];
        }

        private static string UniquePath(string dir, string fileName)
        {
            string path = Path.Combine(dir, fileName);
            if (!File.Exists(path) && !Directory.Exists(path)) return path;
            string withoutExt = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            for (int i = 1; i < 10000; i++)
            {
                path = Path.Combine(dir, withoutExt + " (" + i + ")" + ext);
                if (!File.Exists(path) && !Directory.Exists(path)) return path;
            }
            throw new IOException("无法生成不重复的文件名。");
        }

        private static void TryDeleteDirectory(string dir)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch { }
        }
    }
}
