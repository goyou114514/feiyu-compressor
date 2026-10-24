using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace FeiyuZip
{
    internal sealed class InvalidPasswordException : Exception
    {
        public InvalidPasswordException(string message) : base(message) { }
    }

    /// <summary>负责把内置的 7zr.exe（7-Zip 官方命令行引擎）释放到本机缓存并定位。</summary>
    internal static class SevenZipTool
    {
        private static string _toolPath;

        public static string EnsureTool()
        {
            if (!string.IsNullOrEmpty(_toolPath) && File.Exists(_toolPath)) return _toolPath;

            string[] candidates = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FeiyuZip", "bin"),
                Path.Combine(Path.GetTempPath(), "FeiyuZip_bin")
            };

            Exception lastError = null;
            foreach (string dir in candidates)
            {
                try
                {
                    string path = Path.Combine(dir, "7zr.exe");
                    if (!File.Exists(path))
                    {
                        Directory.CreateDirectory(dir);
                        string temp = path + ".tmp";
                        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("7zr.exe"))
                        {
                            if (s == null) throw new FileNotFoundException("程序内置的压缩引擎丢失，请重新下载程序。");
                            using (FileStream fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                s.CopyTo(fs);
                            }
                        }
                        if (File.Exists(path)) File.Delete(path);
                        File.Move(temp, path);
                    }
                    _toolPath = path;
                    return path;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }
            throw new IOException("无法释放压缩引擎：" + (lastError == null ? "未知错误" : lastError.Message));
        }

        public static void WarmUp()
        {
            try
            {
                string path = EnsureTool();
                if (!string.IsNullOrEmpty(path)) return;
            }
            catch { }
        }
    }

    internal static class SevenZip
    {
        private static readonly Regex PercentRegex = new Regex(@"(\d+)\s*%", RegexOptions.Compiled);

        /// <summary>用 7zr 压缩一个文件或文件夹（AES-256 加密，可选加密文件名）。</summary>
        public static void Compress(string inputPath, string archivePath, string password, bool encryptFileNames,
            Action<int> progress, Action<string> status)
        {
            string fullInput = Path.GetFullPath(inputPath);
            bool isDir = Directory.Exists(fullInput);
            if (!isDir && !File.Exists(fullInput)) throw new FileNotFoundException("找不到要压缩的内容：" + fullInput);

            string workingDir = Path.GetDirectoryName(fullInput.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(workingDir)) workingDir = Path.GetPathRoot(fullInput);
            string inputName = Path.GetFileName(fullInput.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (status != null) status(isDir ? "正在打包文件夹：" + inputName : "正在压缩：" + inputName);
            StringBuilder sb = new StringBuilder();
            sb.Append("a -t7z -mx=5 -y -bsp1 -bso0 -bse1 ");
            if (encryptFileNames) sb.Append("-mhe=on ");
            sb.Append(BuildPasswordArg(password));
            sb.Append(' ');
            sb.Append(Quote(Path.GetFullPath(archivePath)));
            sb.Append(' ');
            sb.Append(Quote(inputName));

            Run(sb.ToString(), workingDir, progress, status);
        }

        /// <summary>用 7zr 解压（支持 AES-256 与加密文件名）。</summary>
        public static void Extract(string archivePath, string destDir, string password,
            Action<int> progress, Action<string> status)
        {
            string fullArchive = Path.GetFullPath(archivePath);
            if (!File.Exists(fullArchive)) throw new FileNotFoundException("找不到压缩文件：" + fullArchive);
            if (status != null) status("正在解压：" + Path.GetFileName(fullArchive));

            string workingDir = Path.GetDirectoryName(fullArchive);
            if (string.IsNullOrEmpty(workingDir)) workingDir = Environment.CurrentDirectory;
            StringBuilder sb = new StringBuilder();
            sb.Append("x ");
            sb.Append(Quote(fullArchive));
            sb.Append(" -o");
            sb.Append(Quote(Path.GetFullPath(destDir)));
            sb.Append(" -y -bsp1 -bso0 -bse1 ");
            sb.Append(BuildPasswordArg(password));

            Run(sb.ToString(), workingDir, progress, status);
        }

        /// <summary>列出压缩包内容（用于读取第一层里包含的文件名）。</summary>
        public static string List(string archivePath, string password)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("l ");
            sb.Append(Quote(Path.GetFullPath(archivePath)));
            sb.Append(" -y -bso1 -bse1 ");
            sb.Append(BuildPasswordArg(password));
            return Run(sb.ToString(), Path.GetDirectoryName(Path.GetFullPath(archivePath)), null, null);
        }

        public static string BuildPasswordArg(string password)
        {
            if (password == null) password = string.Empty;
            if (password.IndexOf('"') >= 0 || password.IndexOf('\r') >= 0 || password.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("密码不能包含英文双引号或换行符。");
            }
            return "-p" + Quote(password);
        }

        private static string Run(string arguments, string workingDir, Action<int> progress, Action<string> status)
        {
            string exe;
            try
            {
                exe = SevenZipTool.EnsureTool();
            }
            catch (Exception ex)
            {
                throw new IOException("压缩引擎初始化失败：" + ex.Message);
            }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exe;
            psi.Arguments = arguments;
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "feiyu_7zr_cmd.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " | " + workingDir + " | " + arguments + Environment.NewLine);
            }
            catch { }
            psi.WorkingDirectory = workingDir;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.Default;
            psi.StandardErrorEncoding = Encoding.Default;

            StringBuilder output = new StringBuilder();
            object sync = new object();
            int lastPercent = -1;

            DataReceivedEventHandler handler = delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (sync) output.AppendLine(e.Data);
                if (progress != null && e.Data.IndexOf('%') >= 0)
                {
                    foreach (Match m in PercentRegex.Matches(e.Data))
                    {
                        int v;
                        if (int.TryParse(m.Groups[1].Value, out v) && v >= 0 && v <= 100 && v > lastPercent)
                        {
                            lastPercent = v;
                            try { progress(v); } catch { }
                        }
                    }
                }
            };

            int exitCode;
            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += handler;
                p.ErrorDataReceived += handler;
                try
                {
                    p.Start();
                }
                catch (Exception ex)
                {
                    throw new IOException("无法启动压缩引擎：" + ex.Message);
                }
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                exitCode = p.ExitCode;
            }

            string text;
            lock (sync) text = output.ToString();
            if (exitCode == 0) return text;

            string lower = text.ToLowerInvariant();
            if (exitCode == 2 && (lower.Contains("wrong password") || lower.Contains("cannot open encrypted archive")
                || lower.Contains("data error in encrypted file") || lower.Contains("crc failed")
                || lower.Contains("encrypted archive")))
            {
                throw new InvalidPasswordException("密码错误，或压缩包已损坏。");
            }
            throw new IOException("压缩引擎出错（代码 " + exitCode + "）：" + LastLines(text, 6));
        }

        private static string LastLines(string text, int count)
        {
            if (string.IsNullOrEmpty(text)) return "无详细信息";
            string[] lines = text.Replace("\r", "\n").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int start = Math.Max(0, lines.Length - count);
            return string.Join(" / ", lines, start, lines.Length - start);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty) + "\"";
        }
    }
}
