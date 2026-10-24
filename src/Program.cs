using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace FeiyuZip
{
    internal static class Program
    {
        private static string _logPath;

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal))
                {
                    _logPath = Path.Combine(Environment.CurrentDirectory, "cli_error.log");
                    Environment.ExitCode = RunCli(args);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(_logPath))
                {
                    AppendLog("FATAL: " + ex);
                    Environment.ExitCode = 1;
                }
                else
                {
                    MessageBox.Show("程序出错：\r\n" + ex.Message, "肥鱼压缩器",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.ExitCode = 1;
                }
            }
        }

        // ---------------- 命令行模式（用于自动化测试） ----------------

        private static int RunCli(string[] rawArgs)
        {
            List<string> args = new List<string>(rawArgs);
            for (int i = 0; i < args.Count; i++)
            {
                if (args[i] == "--log" && i + 1 < args.Count)
                {
                    _logPath = Path.GetFullPath(args[i + 1]);
                    args.RemoveAt(i + 1);
                    args.RemoveAt(i);
                    break;
                }
            }
            if (args.Count == 0) return 2;

            string cmd = args[0];
            try
            {
                switch (cmd)
                {
                    case "--selftest":
                        return SelfTest(args.Count > 1 ? Path.GetFullPath(args[1]) : Environment.CurrentDirectory);

                    case "--compress":
                        {
                            if (args.Count < 3) return 2;
                            string password = args[1];
                            List<string> inputs = new List<string>();
                            for (int i = 2; i < args.Count; i++) inputs.Add(args[i]);
                            AppendLog("开始压缩，密码长度=" + password.Length + "，输入=" + inputs.Count + " 个");
                            List<CompressionResult> rs = CompressionFlow.Run(inputs, password,
                                delegate (string s) { AppendLog("阶段：" + s); },
                                delegate (int p) { },
                                delegate (string s) { });
                            foreach (CompressionResult r in rs) AppendLog("完成：" + r.FinalArchivePath);
                            return 0;
                        }

                    case "--decompress":
                        {
                            if (args.Count < 3) return 2;
                            string password = args[1];
                            List<string> files = new List<string>();
                            for (int i = 2; i < args.Count; i++) files.Add(args[i]);
                            List<ExtractionResult> rs = ExtractionFlow.Run(files, password,
                                delegate (string s) { AppendLog("阶段：" + s); },
                                delegate (int p) { },
                                delegate (string s) { });
                            foreach (ExtractionResult r in rs) AppendLog("还原到：" + r.OutputDir);
                            return 0;
                        }

                    case "--list":
                        {
                            if (args.Count < 3) return 2;
                            AppendLog(SevenZip.List(args[1], args[2]));
                            return 0;
                        }

                    case "--screenshot":
                        {
                            string path = Path.Combine(Environment.CurrentDirectory, "screenshot.png");
                            string shotMode = "compress";
                            string shotSelect = null;
                            string shotPassword = null;
                            for (int i = 1; i < args.Count; i++)
                            {
                                if (args[i] == "--out" && i + 1 < args.Count) path = Path.GetFullPath(args[++i]);
                                else if (args[i] == "--mode" && i + 1 < args.Count) shotMode = args[++i];
                                else if (args[i] == "--select" && i + 1 < args.Count) shotSelect = args[++i];
                                else if (args[i] == "--password" && i + 1 < args.Count) shotPassword = args[++i];
                            }
                            Application.EnableVisualStyles();
                            Application.SetCompatibleTextRenderingDefault(false);
                            MainForm form = new MainForm();
                            if (shotMode == "extract") form.TestSetMode(WorkMode.Extract);
                            if (!string.IsNullOrEmpty(shotPassword)) form.TestSetPassword(shotPassword);
                            if (!string.IsNullOrEmpty(shotSelect)) form.TestSetSelection(new string[] { shotSelect });

                            DateTime deadline = DateTime.Now.AddSeconds(120);
                            Timer timer = new Timer();
                            timer.Interval = 400;
                            timer.Tick += delegate
                            {
                                if (form.IsBusy && DateTime.Now < deadline) return;
                                timer.Stop();
                                Timer shotTimer = new Timer();
                                shotTimer.Interval = 350;
                                shotTimer.Tick += delegate
                                {
                                    shotTimer.Stop();
                                    try
                                    {
                                        using (Bitmap bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                                        {
                                            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.ClientSize.Width, form.ClientSize.Height));
                                            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                                        }
                                        AppendLog("截图已保存：" + path + "（忙碌中=" + form.IsBusy + "）");
                                    }
                                    catch (Exception ex)
                                    {
                                        AppendLog("截图失败：" + ex);
                                    }
                                    form.Close();
                                };
                                shotTimer.Start();
                            };
                            timer.Start();
                            Application.Run(form);
                            return 0;
                        }

                    default:
                        AppendLog("未知命令：" + cmd);
                        return 2;
                }
            }
            catch (InvalidPasswordException ex)
            {
                AppendLog("密码错误：" + ex.Message);
                return 3;
            }
            catch (Exception ex)
            {
                AppendLog("出错：" + ex);
                return 1;
            }
        }

        // ---------------- 自检 ----------------

        private static int SelfTest(string workspace)
        {
            _logPath = Path.Combine(workspace, "selftest.log");
            int failures = 0;
            DateTime start = DateTime.Now;
            try
            {
                Directory.CreateDirectory(workspace);
                string root = Path.Combine(workspace, "selftest_work");
                AppendLog("===== 自检开始 =====");
                if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(root);

                string dataDir = Path.Combine(root, "srcdata");
                CreateTestData(dataDir);
                Dictionary<string, string> hashes = HashDirectory(dataDir);
                HashSet<string> dirs = ListDirectories(dataDir);
                AppendLog("测试数据：文件 " + hashes.Count + " 个，目录 " + dirs.Count + " 个");

                string password = "TestPass123!";
                List<CompressionResult> compressed = CompressionFlow.Run(new string[] { dataDir }, password,
                    delegate (string s) { AppendLog("阶段：" + s); },
                    delegate (int p) { },
                    delegate (string s) { });
                if (compressed.Count != 1) { AppendLog("FAIL: 压缩结果数量异常"); failures++; }
                CompressionResult result = compressed[0];
                string finalName = Path.GetFileName(result.FinalArchivePath);
                if (!File.Exists(result.FinalArchivePath)) { AppendLog("FAIL: 最终文件不存在"); failures++; }
                else AppendLog("最终文件：" + finalName);

                if (finalName != "srcdata.mp4.7z")
                {
                    AppendLog("FAIL: 最终文件名不是 srcdata.mp4.7z，而是 " + finalName);
                    failures++;
                }
                if (Directory.GetFileSystemEntries(root, "~feiyu_*").Length > 0)
                {
                    AppendLog("FAIL: 临时文件未清理");
                    failures++;
                }

                // 第二层里面应该只有一个 srcdata.mp4
                string listing = SevenZip.List(result.FinalArchivePath, password);
                int fileCount = 0;
                foreach (string line in listing.Replace("\r", "\n").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Trim().EndsWith("srcdata.mp4", StringComparison.OrdinalIgnoreCase)) fileCount++;
                }
                if (fileCount == 1 && listing.IndexOf("srcdata.mp4", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AppendLog("PASS: 第二层只含加密的 srcdata.mp4");
                }
                else
                {
                    AppendLog("FAIL: 第二层结构异常（找到 " + fileCount + " 处 srcdata.mp4）");
                    failures++;
                }

                // 解压还原并比对
                string backup = Path.Combine(root, "backup");
                Directory.Move(dataDir, backup);
                List<ExtractionResult> extracted = ExtractionFlow.Run(new string[] { result.FinalArchivePath }, password,
                    delegate (string s) { AppendLog("阶段：" + s); },
                    delegate (int p) { },
                    delegate (string s) { });
                if (extracted.Count == 1) AppendLog("PASS: 解压流程返回正常");
                else { AppendLog("FAIL: 解压结果数量异常"); failures++; }

                Dictionary<string, string> restoredHashes = HashDirectory(dataDir);
                failures += CompareHashes(hashes, restoredHashes, "文件内容");
                HashSet<string> restoredDirs = ListDirectories(dataDir);
                failures += CompareSets(dirs, restoredDirs, "目录结构");

                // 错误密码必须打不开
                try
                {
                    ExtractionFlow.Run(new string[] { result.FinalArchivePath }, "WrongPassword!", null, null, null);
                    AppendLog("FAIL: 错误密码居然解压成功了");
                    failures++;
                }
                catch (InvalidPasswordException)
                {
                    AppendLog("PASS: 错误密码被正确拒绝");
                }

                // 密码为空必须被拒绝
                try
                {
                    CompressionFlow.Run(new string[] { backup }, "", null, null, null);
                    AppendLog("FAIL: 空密码居然能压缩");
                    failures++;
                }
                catch (ArgumentException)
                {
                    AppendLog("PASS: 空密码被正确拒绝");
                }

                AppendLog(failures == 0
                    ? "===== 自检全部通过（耗时 " + (DateTime.Now - start).TotalSeconds.ToString("0.0") + " 秒） ====="
                    : "===== 自检失败 " + failures + " 项 =====");
                return failures == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                AppendLog("FAIL: 自检异常 " + ex);
                return 1;
            }
        }

        private static void CreateTestData(string dir)
        {
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(Path.Combine(dir, "文件夹"));
            Directory.CreateDirectory(Path.Combine(dir, "文件夹", "子文件夹"));
            Directory.CreateDirectory(Path.Combine(dir, "文件夹", "空文件夹"));
            Directory.CreateDirectory(Path.Combine(dir, "a", "b", "c", "d", "e"));

            File.WriteAllText(Path.Combine(dir, "hello.txt"), "你好，肥鱼！这是一段测试文本。\r\n" + new string('A', 5000), Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "中文名 文件.txt"), "中文文件名测试", Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "empty.txt"), "", Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "文件夹", "子文件夹", "嵌套.txt"), "嵌套内容", Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "a", "b", "c", "d", "e", "f.txt"), "深层目录", Encoding.UTF8);
            File.WriteAllText(Path.Combine(dir, "带 空格 的名字.txt"), "空格", Encoding.UTF8);

            byte[] big = new byte[4 * 1024 * 1024];
            Random rnd = new Random(20261024);
            rnd.NextBytes(big);
            File.WriteAllBytes(Path.Combine(dir, "大文件.bin"), big);

            byte[] video = new byte[2 * 1024 * 1024];
            rnd.NextBytes(video);
            File.WriteAllBytes(Path.Combine(dir, "视频.mp4"), video);
        }

        private static Dictionary<string, string> HashDirectory(string root)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/');
                using (SHA256 sha = SHA256.Create())
                using (FileStream fs = File.OpenRead(file))
                {
                    result[rel] = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "");
                }
            }
            return result;
        }

        private static HashSet<string> ListDirectories(string root)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string d in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                string rel = d.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/');
                result.Add(rel);
            }
            return result;
        }

        private static int CompareHashes(Dictionary<string, string> expected, Dictionary<string, string> actual, string label)
        {
            int bad = 0;
            foreach (KeyValuePair<string, string> pair in expected)
            {
                string value;
                if (!actual.TryGetValue(pair.Key, out value))
                {
                    AppendLog("FAIL: " + label + " 缺少 " + pair.Key);
                    bad++;
                }
                else if (value != pair.Value)
                {
                    AppendLog("FAIL: " + label + " 不一致 " + pair.Key);
                    bad++;
                }
            }
            foreach (string key in actual.Keys)
            {
                if (!expected.ContainsKey(key))
                {
                    AppendLog("FAIL: " + label + " 多出 " + key);
                    bad++;
                }
            }
            if (bad == 0) AppendLog("PASS: " + label + " 完全一致（" + expected.Count + " 个文件）");
            return bad;
        }

        private static int CompareSets(HashSet<string> expected, HashSet<string> actual, string label)
        {
            int bad = 0;
            foreach (string s in expected)
            {
                if (!actual.Contains(s))
                {
                    AppendLog("FAIL: " + label + " 缺少 " + s);
                    bad++;
                }
            }
            foreach (string s in actual)
            {
                if (!expected.Contains(s))
                {
                    AppendLog("FAIL: " + label + " 多出 " + s);
                    bad++;
                }
            }
            if (bad == 0) AppendLog("PASS: " + label + " 完全一致（" + expected.Count + " 个目录）");
            return bad;
        }

        // ---------------- 日志 ----------------

        private static void AppendLog(string line)
        {
            Console.WriteLine(line);
            if (!string.IsNullOrEmpty(_logPath))
            {
                try
                {
                    File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch { }
            }
        }
    }
}
