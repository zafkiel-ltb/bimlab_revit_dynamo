using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace DynLock.Installer
{
    public class MainForm : Form
    {
        private const string Base = @"C:\ProgramData\BIMLab\DynLock";
        private static readonly string ScriptsDir = Path.Combine(Base, "Scripts");
        private const string RevitAddinsRoot = @"C:\ProgramData\Autodesk\Revit\Addins";
        private static readonly string UserRevitAddinsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Autodesk\Revit\Addins");
        private const string AddInId = "7FBF4548-DA2C-42AD-8F99-7FCF8CF45E85";

        private static readonly Color HeaderColor = Color.FromArgb(18, 90, 175);
        private static readonly Color ToolbarColor = Color.FromArgb(244, 246, 251);
        private static readonly Color BorderColor = Color.FromArgb(210, 218, 236);
        private static readonly Color GreenButton = Color.FromArgb(34, 126, 68);
        private static readonly Color GrayButton = Color.FromArgb(218, 226, 242);
        private static readonly Color GrayText = Color.FromArgb(52, 72, 108);

        private readonly TextBox _log = new TextBox();
        private readonly Button _install = new Button();
        private readonly Button _uninstall = new Button();
        private readonly CheckBox _chk2024 = new CheckBox();
        private readonly CheckBox _chk2025 = new CheckBox();
        private readonly CheckBox _chk2026 = new CheckBox();
        private readonly TextBox _authServerUrl = new TextBox();
        private readonly HashSet<string> _extracted = new HashSet<string>();

        public MainForm()
        {
            Text = "BIMLab Player";
            Size = new Size(680, 600);
            MinimumSize = new Size(680, 600);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.75f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.White;

            var header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = HeaderColor };

            var iconBmp = LoadAppIcon(40);
            var iconWrap = new Panel { Bounds = new Rectangle(18, 14, 48, 48), BackColor = Color.White };
            var iconBox = new PictureBox
            {
                Image = iconBmp != null ? (Image)iconBmp : null,
                SizeMode = PictureBoxSizeMode.Zoom,
                Bounds = new Rectangle(4, 4, 40, 40),
                BackColor = Color.White,
            };
            iconWrap.Controls.Add(iconBox);

            var lblTitle = new Label
            {
                Text = "BIMLab Player",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(78, 17),
            };
            var lblSub = new Label
            {
                Text = "Đã đăng nhập: " + (string.IsNullOrWhiteSpace(AuthSession.Email) ? "" : AuthSession.Email),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(160, 200, 255),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(80, 46),
            };
            header.Controls.AddRange(new Control[] { iconWrap, lblTitle, lblSub });

            var versionPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 118,
                BackColor = ToolbarColor,
            };

            var lblVersion = new Label
            {
                Text = "Chọn phiên bản Revit để cài:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = GrayText,
                AutoSize = true,
                Location = new Point(16, 12),
            };

            SetupCheckbox(_chk2024, "Revit 2024", 16, true);
            SetupCheckbox(_chk2025, "Revit 2025", 140, true);
            SetupCheckbox(_chk2026, "Revit 2026", 264, true);

            var lblAuthServer = new Label
            {
                Text = "Auth server URL:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = GrayText,
                AutoSize = true,
                Location = new Point(16, 76),
            };
            _authServerUrl.Text = string.IsNullOrWhiteSpace(AuthSession.AuthServerUrl)
                ? LoadExistingAuthServerUrl()
                : AuthSession.AuthServerUrl;
            _authServerUrl.Font = new Font("Segoe UI", 9.5f);
            _authServerUrl.BorderStyle = BorderStyle.FixedSingle;
            _authServerUrl.Location = new Point(140, 72);
            _authServerUrl.Width = 490;

            versionPanel.Controls.AddRange(new Control[] { lblVersion, _chk2024, _chk2025, _chk2026, lblAuthServer, _authServerUrl });

            _log.Dock = DockStyle.Fill;
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BackColor = Color.White;
            _log.Font = new Font("Consolas", 9f);
            _log.BorderStyle = BorderStyle.None;

            var logPad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 8, 14, 8) };
            logPad.Controls.Add(_log);

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 58,
                Padding = new Padding(14, 10, 14, 10),
                BackColor = ToolbarColor,
            };

            Btn(_install, "Cài add-in", GreenButton, Color.White, 160, 38);
            Btn(_uninstall, "Gỡ cài đặt", GrayButton, GrayText, 120, 38);
            _install.Click += (_, __) => DoInstall();
            _uninstall.Click += (_, __) => DoUninstall();
            bottom.Controls.AddRange(new Control[] { _install, _uninstall });

            Controls.Add(logPad);
            Controls.Add(versionPanel);
            Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = BorderColor });
            Controls.Add(header);
            Controls.Add(bottom);
            Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = BorderColor });

            Log("Sẵn sàng. Bấm \"Cài add-in\" để bắt đầu.");
            Log("BIMLab Player sẽ dùng package Dynamo đã cài sẵn trong Dynamo Revit.");
            Log("Nếu muốn copy package tự động, có thể đặt thư mục Packages cạnh file BIMLab Player.exe.");
        }

        private static void SetupCheckbox(CheckBox cb, string text, int x, bool isChecked)
        {
            cb.Text = text;
            cb.Font = new Font("Segoe UI", 9.5f);
            cb.ForeColor = GrayText;
            cb.Checked = isChecked;
            cb.AutoSize = true;
            cb.Location = new Point(x, 40);
        }

        private static void Btn(Button b, string text, Color bg, Color fg, int w, int h)
        {
            b.Text = text;
            b.Width = w;
            b.Height = h;
            b.BackColor = bg;
            b.ForeColor = fg;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.BorderColor = bg;
            b.Font = new Font("Segoe UI", 9.5f);
            b.Cursor = Cursors.Hand;
        }

        private void DoInstall()
        {
            try
            {
                _install.Enabled = false;
                Log("");
                Log("=== Bắt đầu cài đặt ===");

                var years = new List<int>();
                if (_chk2024.Checked) years.Add(2024);
                if (_chk2025.Checked) years.Add(2025);
                if (_chk2026.Checked) years.Add(2026);

                if (years.Count == 0)
                {
                    MessageBox.Show(this, "Vui lòng chọn ít nhất một phiên bản Revit.",
                        "BIMLab Player", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string authServerUrl = NormalizeAuthServerUrl(_authServerUrl.Text);
                if (string.IsNullOrWhiteSpace(authServerUrl))
                {
                    MessageBox.Show(this, "Vui lòng nhập Auth server URL, ví dụ http://192.168.1.50:5050.",
                        "BIMLab Player", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Directory.CreateDirectory(Base);
                Directory.CreateDirectory(ScriptsDir);
                File.WriteAllText(Path.Combine(Base, "config.json"),
                    "{ \"ScriptFolders\": [ \"" + ScriptsDir.Replace("\\", "\\\\") + "\" ] }");
                Log("[OK] Tạo thư mục cài đặt và cấu hình Scripts.");

                foreach (int year in years)
                {
                    string fw = (year <= 2024) ? "net48" : "net8";
                    EnsureFramework(fw);

                    string dllPath = Path.Combine(Base, fw, "DynLock.Addin.dll");
                    if (!File.Exists(dllPath))
                        throw new FileNotFoundException("Không tìm thấy DLL add-in sau khi cài.", dllPath);

                    WriteAddinRegistration(year, dllPath);
                }

                InstallDynamoPackages();
                File.WriteAllText(
                    Path.Combine(Base, "authserver.json"),
                    "{\r\n  \"AuthServerUrl\": \"" + authServerUrl.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"\r\n}\r\n");
                Log("[OK] Tạo authserver.json.");
                TryCreateShortcut();

                Log(">>> CÀI ĐẶT XONG <<<");
                Log("Mở Revit -> tab BIMLab -> Login, sau đó dùng Load để nạp file .dynx.");
                MessageBox.Show(this,
                    "Cài đặt xong!\n\nMở Revit -> tab BIMLab -> Login, sau đó dùng Load để nạp file .dynx.",
                    "BIMLab Player", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("[LỖI] " + ex.Message);
                MessageBox.Show(this, "Cài đặt thất bại:\n" + ex.Message,
                    "BIMLab Player", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _install.Enabled = true;
            }
        }

        private void DoUninstall()
        {
            if (MessageBox.Show(this, "Gỡ BIMLab Player khỏi máy này?",
                    "BIMLab Player", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            Log("");
            Log("=== Gỡ cài đặt ===");
            try
            {
                foreach (var dir in DetectRevitYears())
                {
                    string addin = Path.Combine(dir, "DynLock.addin");
                    if (File.Exists(addin))
                    {
                        File.Delete(addin);
                        Log("[OK] Đã gỡ add-in Revit " + ParseYear(dir) + ".");
                    }
                }

                if (Directory.Exists(Base))
                {
                    Directory.Delete(Base, true);
                    Log("[OK] Đã xóa dữ liệu cài đặt.");
                }

                Log(">>> ĐÃ GỠ XONG <<<");
                MessageBox.Show(this, "Đã gỡ xong.", "BIMLab Player",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("[LỖI] " + ex.Message);
            }
        }

        private void WriteAddinRegistration(int year, string dllPath)
        {
            string machineDir = Path.Combine(RevitAddinsRoot, year.ToString());
            string userDir = Path.Combine(UserRevitAddinsRoot, year.ToString());
            DeleteAddinManifest(machineDir);
            DeleteAddinManifest(userDir);

            try
            {
                WriteAddinManifest(machineDir, dllPath);
                Log("[OK] Revit " + year + " -> đã đăng ký add-in (máy).");
            }
            catch (Exception ex)
            {
                Log("[WARN] Không ghi được add-in scope máy cho Revit " + year + ": " + ex.Message);
                WriteAddinManifest(userDir, dllPath);
                Log("[OK] Revit " + year + " -> đã đăng ký add-in (user).");
            }
        }

        private static void DeleteAddinManifest(string yearDir)
        {
            string addinPath = Path.Combine(yearDir, "DynLock.addin");
            if (File.Exists(addinPath))
                File.Delete(addinPath);
        }

        private static void WriteAddinManifest(string yearDir, string dllPath)
        {
            Directory.CreateDirectory(yearDir);
            string addinPath = Path.Combine(yearDir, "DynLock.addin");

            File.WriteAllText(addinPath, AddinXml(dllPath));
            if (!File.Exists(addinPath))
                throw new FileNotFoundException("Không ghi được file add-in.", addinPath);
        }

        private void EnsureFramework(string fw)
        {
            string dir = Path.Combine(Base, fw);
            Directory.CreateDirectory(dir);

            string addin = Path.Combine(dir, "DynLock.Addin.dll");
            string core = Path.Combine(dir, "DynLock.Core.dll");
            string json = Path.Combine(dir, "Newtonsoft.Json.dll");

            // Always overwrite the embedded runtime payload so a reinstall/update can never
            // leave Revit loading a stale DynLock.Addin.dll from a previous version.
            ExtractTo(fw + ".DynLock.Addin.dll", addin);
            ExtractTo(fw + ".DynLock.Core.dll", core);
            ExtractTo(fw + ".Newtonsoft.Json.dll", json);

            if (fw == "net8")
            {
                string deps = Path.Combine(dir, "DynLock.Addin.deps.json");
                ExtractTo("net8.DynLock.Addin.deps.json", deps);
            }

            _extracted.Add(fw);

            Log("[OK] Cài thư viện nền tảng " + fw + ".");
        }

        private void InstallDynamoPackages()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string sourceRoot = Path.Combine(exeDir, "Packages");
            if (!Directory.Exists(sourceRoot))
            {
                Log("[INFO] Không có thư mục Packages đi kèm, bỏ qua bước cài package Dynamo.");
                return;
            }

            var packageDirs = Directory.GetDirectories(sourceRoot)
                .Where(d => !string.Equals(Path.GetFileName(d), "_readme", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (packageDirs.Count == 0)
            {
                Log("[INFO] Thư mục Packages đang trống, bỏ qua bước cài package Dynamo.");
                return;
            }

            string dynamoRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Dynamo", "Dynamo Revit");

            if (!Directory.Exists(dynamoRoot))
            {
                Log("[WARN] Chưa thấy thư mục Dynamo Revit trong AppData.");
                Log("       Hãy mở Dynamo trong Revit một lần, sau đó chạy lại BIMLab Player để cài package.");
                return;
            }

            var versionDirs = Directory.GetDirectories(dynamoRoot).ToList();
            if (versionDirs.Count == 0)
            {
                Log("[WARN] Không tìm thấy phiên bản Dynamo Revit nào để copy package.");
                return;
            }

            foreach (string versionDir in versionDirs)
            {
                string packagesTarget = Path.Combine(versionDir, "packages");
                Directory.CreateDirectory(packagesTarget);

                foreach (string packageDir in packageDirs)
                {
                    string target = Path.Combine(packagesTarget, Path.GetFileName(packageDir));
                    CopyDirectory(packageDir, target);
                    Log("[OK] Cài package " + Path.GetFileName(packageDir) + " -> Dynamo " + Path.GetFileName(versionDir) + ".");
                }
            }
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);

            foreach (string file in Directory.GetFiles(source))
            {
                string dest = Path.Combine(target, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string dest = Path.Combine(target, Path.GetFileName(dir));
                CopyDirectory(dir, dest);
            }
        }

        private static string AddinXml(string dllPath) =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
            "<RevitAddIns>\r\n" +
            "  <AddIn Type=\"Application\">\r\n" +
            "    <Name>DynLock</Name>\r\n" +
            "    <Assembly>" + dllPath + "</Assembly>\r\n" +
            "    <AddInId>" + AddInId + "</AddInId>\r\n" +
            "    <FullClassName>DynLock.Addin.App</FullClassName>\r\n" +
            "    <VendorId>BIMLAB</VendorId>\r\n" +
            "    <VendorDescription>BIMLab, thegioiphanmem.com.vn</VendorDescription>\r\n" +
            "  </AddIn>\r\n" +
            "</RevitAddIns>\r\n";

        private static int ParseYear(string yearDir) =>
            int.TryParse(Path.GetFileName(yearDir), out int y) ? y : 2024;

        private static List<string> DetectRevitYears()
        {
            var result = new List<string>();
            if (Directory.Exists(RevitAddinsRoot))
            {
                foreach (var d in Directory.GetDirectories(RevitAddinsRoot))
                    if (int.TryParse(Path.GetFileName(d), out int y) && y >= 2024)
                        result.Add(d);
            }
            if (Directory.Exists(UserRevitAddinsRoot))
            {
                foreach (var d in Directory.GetDirectories(UserRevitAddinsRoot))
                    if (int.TryParse(Path.GetFileName(d), out int y) && y >= 2024)
                        result.Add(d);
            }
            return result;
        }

        private void ExtractTo(string resourceName, string destPath)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (Stream src = asm.GetManifestResourceStream(resourceName))
            {
                if (src == null) throw new InvalidOperationException("Thiếu payload: " + resourceName);
                using (var dst = File.Create(destPath))
                    src.CopyTo(dst);
            }
        }

        private void TryCreateShortcut()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string lnk = Path.Combine(desktop, "BIMLab Scripts.lnk");
                string ps = "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('" + lnk +
                            "'); $s.TargetPath='" + ScriptsDir + "'; $s.Save()";
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe",
                    "-NoProfile -WindowStyle Hidden -Command \"" + ps + "\"")
                { CreateNoWindow = true, UseShellExecute = false };
                System.Diagnostics.Process.Start(psi)?.WaitForExit(4000);
                Log("[OK] Tạo shortcut BIMLab Scripts trên Desktop.");
            }
            catch
            {
                Log("[INFO] Không tạo được shortcut Desktop, có thể mở thư mục Scripts trong ProgramData.");
            }
        }

        private void Log(string line) => _log.AppendText(line + Environment.NewLine);

        private static void WriteTemplateIfMissing(string path, string content)
        {
            if (File.Exists(path))
                return;

            File.WriteAllText(path, content);
        }

        private static string NormalizeAuthServerUrl(string value)
        {
            value = (value ?? "").Trim().TrimEnd('/');
            return value;
        }

        private static string LoadExistingAuthServerUrl()
        {
            try
            {
                string path = Path.Combine(Base, "authserver.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    const string key = "\"AuthServerUrl\"";
                    int k = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                    if (k >= 0)
                    {
                        int colon = json.IndexOf(':', k);
                        int q1 = colon >= 0 ? json.IndexOf('"', colon + 1) : -1;
                        int q2 = q1 >= 0 ? json.IndexOf('"', q1 + 1) : -1;
                        if (q1 >= 0 && q2 > q1)
                            return json.Substring(q1 + 1, q2 - q1 - 1);
                    }
                }
            }
            catch
            {
            }

            return "http://localhost:5050";
        }

        private static Bitmap LoadAppIcon(int size)
        {
            try
            {
                var icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                if (icon == null) return null;

                var bmp = new Bitmap(size, size);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(icon.ToBitmap(), 0, 0, size, size);
                }
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }
}
