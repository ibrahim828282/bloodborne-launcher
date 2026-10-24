// Bloodborne Launcher - front window for BBport (normal play) and BBSC (Seamless Co-op).
// Written for the C# 5 compiler that ships with Windows (.NET Framework 4.x csc.exe).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// File details Windows and antivirus programs show for the exe.
[assembly: AssemblyTitle("Bloodborne Launcher")]
[assembly: AssemblyDescription("Launcher for Bloodborne on PC: BBport and Seamless Co-op")]
[assembly: AssemblyProduct("Bloodborne Launcher")]
[assembly: AssemblyCompany("ibrahim828282")]
[assembly: AssemblyCopyright("ibrahim828282 - github.com/ibrahim828282/bloodborne-launcher")]
[assembly: AssemblyVersion("1.1.1.0")]
[assembly: AssemblyFileVersion("1.1.1.0")]

namespace BBLauncher
{
    static class Theme
    {
        public static readonly Color Gold = Color.FromArgb(214, 180, 112);
        public static readonly Color GoldLight = Color.FromArgb(240, 214, 160);
        public static readonly Color GoldDim = Color.FromArgb(150, 125, 80);
        public static readonly Color Blood = Color.FromArgb(128, 18, 48);     // burgundy
        public static readonly Color BloodDark = Color.FromArgb(78, 8, 30);   // dark burgundy
        public static readonly Color Text = Color.FromArgb(232, 225, 210);
        public static readonly Color TextDim = Color.FromArgb(170, 160, 145);
        public static readonly Color Green = Color.FromArgb(130, 210, 130);
    }

    static class Res
    {
        public static Image Img(string name)
        {
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (s == null) return null;
            return Image.FromStream(s);
        }

        static PrivateFontCollection fonts = new PrivateFontCollection();
        static FontFamily cinzel;

        public static FontFamily Cinzel()
        {
            if (cinzel != null) return cinzel;
            try
            {
                Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Cinzel-Bold.ttf");
                byte[] data = new byte[s.Length];
                s.Read(data, 0, data.Length);
                IntPtr p = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, p, data.Length);
                fonts.AddMemoryFont(p, data.Length);
                cinzel = fonts.Families[0];
            }
            catch { cinzel = new FontFamily("Georgia"); }
            return cinzel;
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    class Settings
    {
        // A fresh copy keeps everything next to itself; launcher.ini holds the real paths once set.
        public string BBportExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"BBport\Bloodborne.exe");
        public string SeamlessCmd = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"BBSC\PLAY BBSC.cmd");
        public string BackupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "save-backups");
        public string SeamlessEmu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"BBSC\emulator\shadPS4.exe");
        public string GameEboot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"game\eboot.bin");
        public string Discord = "https://discord.gg/Df7BCmfWdt";
        public string BBportDiscord = "https://discord.gg/yTMG8c4Bqm";
        public string MyDiscord = "https://discord.gg/F2VbkNMNPz";
        public string BBportVer = ""; // release tag of BBport this launcher installed
        public string LauncherRepo = "ibrahim828282/bloodborne-launcher"; // GitHub "owner/repo" where new versions of this launcher are published
        public string Lang = I18n.Code;
        public bool AutoGfx; // Seamless graphics already set from the hardware (or by hand)
        public bool MinimizeOnPlay = true;
        public bool CheckUpdates = true;
        public string Mode = "seamless";
        public string FilePath;

        public Settings()
        {
            FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.ini");
            if (File.Exists(FilePath))
            {
                AutoGfx = true; // an existing setup keeps its graphics unless the file says otherwise
                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (line.StartsWith("#") || eq < 1) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    if (k == "BBportExe") BBportExe = v;
                    else if (k == "SeamlessCmd") SeamlessCmd = v;
                    else if (k == "BackupDir") BackupDir = v;
                    else if (k == "SeamlessEmu") SeamlessEmu = v;
                    else if (k == "GameEboot") GameEboot = v;
                    else if (k == "Discord") Discord = v;
                    else if (k == "MyDiscord") MyDiscord = v;
                    else if (k == "BBportDiscord") BBportDiscord = v;
                    else if (k == "LauncherRepo") LauncherRepo = v;
                    else if (k == "BBportVer") BBportVer = v;
                    else if (k == "AutoGfx") AutoGfx = IsTrue(v);
                    else if (k == "Lang") Lang = v;
                    else if (k == "MinimizeOnPlay") MinimizeOnPlay = IsTrue(v);
                    else if (k == "CheckUpdates") CheckUpdates = IsTrue(v);
                    else if (k == "Mode") Mode = v == "normal" ? "normal" : "seamless";
                }
            }
            Relocate();
            Save();
        }

        // A copy of the launcher on another PC: anything missing goes next to the launcher
        // (BBport\, BBSC\, save-backups\), and the game folder comes from BBport if it knows it.
        public static string BaseDir { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        void Relocate()
        {
            if (!File.Exists(BBportExe))
            {
                string c = FindFile(Path.Combine(BaseDir, "BBport"), "Bloodborne.exe");
                BBportExe = c ?? Path.Combine(BaseDir, @"BBport\Bloodborne.exe");
            }
            if (!File.Exists(SeamlessEmu))
            {
                string c = FindFile(Path.Combine(BaseDir, "BBSC"), "PLAY BBSC.cmd");
                string dir = c != null ? Path.GetDirectoryName(c) : Path.Combine(BaseDir, "BBSC");
                SeamlessCmd = Path.Combine(dir, "PLAY BBSC.cmd");
                SeamlessEmu = Path.Combine(dir, @"emulator\shadPS4.exe");
            }
            if (!File.Exists(GameEboot))
            {
                string gd = ReadJsonString(Path.Combine(AppData, @"bbport-launcher\settings.json"), "game_dir");
                if (!string.IsNullOrEmpty(gd) && File.Exists(Path.Combine(gd, "eboot.bin"))) GameEboot = Path.Combine(gd, "eboot.bin");
                else { string e = FindFile(BaseDir, "eboot.bin"); if (e != null) GameEboot = e; } // the game copied next to the launcher
            }
            try { if (!Directory.Exists(Path.GetPathRoot(BackupDir)) || !Directory.Exists(Path.GetDirectoryName(BackupDir))) BackupDir = Path.Combine(BaseDir, "save-backups"); }
            catch { BackupDir = Path.Combine(BaseDir, "save-backups"); }
        }

        // The file in dir or one or two folders below it (zips often have a top folder).
        public static string FindFile(string dir, string name)
        {
            try
            {
                if (!Directory.Exists(dir)) return null;
                string f = Path.Combine(dir, name);
                if (File.Exists(f)) return f;
                foreach (string d in Directory.GetDirectories(dir))
                {
                    f = Path.Combine(d, name);
                    if (File.Exists(f)) return f;
                    foreach (string d2 in Directory.GetDirectories(d))
                    {
                        f = Path.Combine(d2, name);
                        if (File.Exists(f)) return f;
                    }
                }
            }
            catch { }
            return null;
        }

        static bool IsTrue(string v) { return v.ToLower() != "false" && v != "0"; }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath,
                    "# Bloodborne Launcher settings\r\n" +
                    "BBportExe=" + BBportExe + "\r\n" +
                    "SeamlessCmd=" + SeamlessCmd + "\r\n" +
                    "BackupDir=" + BackupDir + "\r\n" +
                    "SeamlessEmu=" + SeamlessEmu + "\r\n" +
                    "GameEboot=" + GameEboot + "\r\n" +
                    "Discord=" + Discord + "\r\n" +
                    "MyDiscord=" + MyDiscord + "\r\n" +
                    "BBportDiscord=" + BBportDiscord + "\r\n" +
                    "LauncherRepo=" + LauncherRepo + "\r\n" +
                    "BBportVer=" + BBportVer + "\r\n" +
                    "AutoGfx=" + (AutoGfx ? "true" : "false") + "\r\n" +
                    "Lang=" + Lang + "\r\n" +
                    "MinimizeOnPlay=" + (MinimizeOnPlay ? "true" : "false") + "\r\n" +
                    "CheckUpdates=" + (CheckUpdates ? "true" : "false") + "\r\n" +
                    "Mode=" + Mode + "\r\n", new UTF8Encoding(false));
            }
            catch { }
        }

        public static string ReadJsonString(string file, string key)
        {
            try
            {
                if (!File.Exists(file)) return null;
                string txt = File.ReadAllText(file);
                Match m = Regex.Match(txt, "\"" + key + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                if (!m.Success) return null;
                return Regex.Unescape(m.Groups[1].Value).Trim();
            }
            catch { return null; }
        }

        static string AppData { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } }

        public string BBSCVersion()
        {
            string mod = Path.Combine(Path.GetDirectoryName(SeamlessCmd), @"mods\CUSA03173\bbsc\mod.json");
            string v = ReadJsonString(mod, "version");
            return string.IsNullOrEmpty(v) ? "?" : v;
        }

        public string PlayerName()
        {
            string n;
            if (Mode == "normal")
            {
                string js = Path.Combine(AppData, @"bbport-launcher\settings.json");
                n = ReadJsonString(js, "thd_online_id");
                if (string.IsNullOrEmpty(n)) n = ReadJsonString(js, "online_npid");
                if (string.IsNullOrEmpty(n)) n = ReadJsonString(js, "player_name");
            }
            else n = ReadJsonString(Path.Combine(AppData, @"shadPS4\users.json"), "shadnet_npid");
            return string.IsNullOrEmpty(n) ? "" : n;
        }

        public string NormalSaveDir()
        {
            string user = ReadJsonString(Path.Combine(AppData, @"bbport-launcher\settings.json"), "user_dir");
            if (string.IsNullOrEmpty(user)) user = Path.Combine(Path.GetDirectoryName(BBportExe), "user");
            return Path.Combine(user, @"savedata\1\CUSA00207\SPRJ0005");
        }

        public string SeamlessSaveDir()
        {
            string home = ReadJsonString(Path.Combine(AppData, @"shadPS4\config.json"), "home_dir");
            if (string.IsNullOrEmpty(home)) home = Path.Combine(AppData, @"shadPS4\home");
            return Path.Combine(home.Replace('/', '\\'), @"1000\savedata\CUSA00207\SPRJ0005");
        }
    }

    class UpdateInfo
    {
        public bool Available;
        public string Latest = "";
        public string Url = "";
        public string Zip;
    }

    static class Updates
    {
        static string Get(string url)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "BloodborneLauncher/2.0";
                wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                wc.Encoding = Encoding.UTF8;
                return wc.DownloadString(url);
            }
        }

        static Version Ver(string tag)
        {
            Match m = Regex.Match(tag ?? "", @"(\d+)\.(\d+)\.(\d+)");
            if (!m.Success) return null;
            return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
        }

        // BBport: newest release vs. the date of the installed Bloodborne.exe (the updater rewrites it).
        public static UpdateInfo BBport(string exe, string installedTag)
        {
            UpdateInfo u = new UpdateInfo();
            string json = Get("https://api.github.com/repos/Supermedo/bloodborne_pc/releases/latest");
            Match t = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            Match d = Regex.Match(json, "\"published_at\"\\s*:\\s*\"([^\"]+)\"");
            if (!t.Success) return null;
            Version v = Ver(t.Groups[1].Value);
            u.Latest = v != null ? v.ToString() : t.Groups[1].Value;
            u.Url = "https://github.com/Supermedo/bloodborne_pc/releases/tag/" + t.Groups[1].Value;
            if (d.Success && File.Exists(exe))
            {
                DateTime pub = DateTime.Parse(d.Groups[1].Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                // a fresh install from the zip keeps old file dates, so the file's creation time counts too
                DateTime limit = pub.AddHours(-1);
                u.Available = File.GetLastWriteTimeUtc(exe) < limit && File.GetCreationTimeUtc(exe) < limit;
                Version inst = Ver(installedTag), latest = Ver(t.Groups[1].Value);
                if (inst != null && latest != null && inst >= latest) u.Available = false;
            }
            return u;
        }

        // This launcher: the newest release of the repo it is published in.
        public static UpdateInfo Launcher(string repo, string installed)
        {
            UpdateInfo u = new UpdateInfo();
            string json = Get("https://api.github.com/repos/" + repo + "/releases/latest");
            Match t = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            if (!t.Success) return null;
            Version v = Ver(t.Groups[1].Value), cur = Ver(installed);
            u.Latest = v != null ? v.ToString() : t.Groups[1].Value;
            u.Url = "https://github.com/" + repo + "/releases/tag/" + t.Groups[1].Value;
            foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
                if (m.Groups[1].Value.ToLowerInvariant().EndsWith(".zip")) { u.Zip = m.Groups[1].Value; break; }
            u.Available = v != null && cur != null && v > cur && u.Zip != null;
            return u;
        }

        // BBSC: every release is a pre-release, so look at the list and take the highest x.y.z.
        public static UpdateInfo BBSC(string installed)
        {
            UpdateInfo u = new UpdateInfo();
            string json = Get("https://api.github.com/repos/CLSAMG22/BBSC/releases?per_page=15");
            Version best = null; string bestTag = null;
            foreach (Match m in Regex.Matches(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\""))
            {
                Version v = Ver(m.Groups[1].Value);
                if (v != null && (best == null || v > best)) { best = v; bestTag = m.Groups[1].Value; }
            }
            if (best == null) return null;
            u.Latest = best.ToString();
            u.Url = "https://github.com/CLSAMG22/BBSC/releases/tag/" + bestTag;
            Version cur = Ver(installed);
            u.Available = cur != null && best > cur;
            return u;
        }
    }

    // Finds the zip to download for a mod from its GitHub releases.
    static class Releases
    {
        static string Get(string url)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "BloodborneLauncher/2.0";
                wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                wc.Encoding = Encoding.UTF8;
                return wc.DownloadString(url);
            }
        }

        // Returns { zip url, version, release page } or null.
        public static string[] Find(bool seamless)
        {
            string json, tag;
            if (seamless)
            {
                json = Get("https://api.github.com/repos/CLSAMG22/BBSC/releases?per_page=15");
                Version best = null; tag = null;
                foreach (Match m in Regex.Matches(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\""))
                {
                    Match v = Regex.Match(m.Groups[1].Value, @"(\d+)\.(\d+)\.(\d+)");
                    if (!v.Success) continue;
                    Version ver = new Version(int.Parse(v.Groups[1].Value), int.Parse(v.Groups[2].Value), int.Parse(v.Groups[3].Value));
                    if (best == null || ver > best) { best = ver; tag = m.Groups[1].Value; }
                }
                if (tag == null) return null;
            }
            else
            {
                json = Get("https://api.github.com/repos/Supermedo/bloodborne_pc/releases/latest");
                Match t = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                if (!t.Success) return null;
                tag = t.Groups[1].Value;
            }
            string page = "https://github.com/" + (seamless ? "CLSAMG22/BBSC" : "Supermedo/bloodborne_pc") + "/releases/tag/" + tag;
            List<string> zips = new List<string>();
            foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
            {
                string u = m.Groups[1].Value;
                if (u.Contains("/download/" + tag + "/") && u.ToLowerInvariant().EndsWith(".zip")) zips.Add(u);
            }
            string pick = null;
            foreach (string u in zips)
            {
                string n = Path.GetFileName(u).ToLowerInvariant();
                if (n.Contains("source") || n.Contains("linux") || n.Contains("mac")) continue;
                if (seamless ? n.Contains("play") : (n.Contains("win") || pick == null)) { pick = u; if (seamless || n.Contains("win")) break; }
            }
            if (pick == null && !seamless && zips.Count > 0) pick = zips[0];
            return new string[] { pick, tag, page };
        }
    }

    // Interface language: the Arabic text in the code is the key, lang.txt (embedded) has the other 12 languages.
    static class I18n
    {
        public static readonly string[] Codes = { "ar", "en", "ru", "es", "pt", "fr", "de", "it", "pl", "tr", "zh", "ja", "ko" };
        public static readonly string[] Names = { "\u0627\u0644\u0639\u0631\u0628\u064a\u0629", "English", "Русский", "Español", "Português (Brasil)", "Français", "Deutsch", "Italiano", "Polski", "Türkçe", "简体中文", "日本語", "한국어" };
        static string code;
        static Dictionary<string, string> map;

        public static string Code { get { Init(); return code; } }
        public static bool Rtl { get { return Code == "ar"; } }
        public static MessageBoxOptions MsgOpt { get { return Rtl ? MessageBoxOptions.RtlReading : (MessageBoxOptions)0; } }

        static void Init()
        {
            if (code != null) return;
            string ini = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.ini");
            string c = null;
            try
            {
                if (File.Exists(ini))
                {
                    c = "ar"; // a setup from before the language option was Arabic
                    foreach (string raw in File.ReadAllLines(ini, Encoding.UTF8))
                    {
                        string l = raw.Trim();
                        if (l.StartsWith("Lang=")) c = l.Substring(5).Trim();
                    }
                }
            }
            catch { }
            if (c == null)
            {
                string os = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                c = Array.IndexOf(Codes, os) >= 0 ? os : "en";
            }
            if (Array.IndexOf(Codes, c) < 0) c = "en";
            code = c;
            map = new Dictionary<string, string>();
            if (code == "ar") return;
            try
            {
                Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("lang.txt");
                if (st == null) return;
                using (StreamReader rd = new StreamReader(st, Encoding.UTF8))
                {
                    string line;
                    string pre = code + "\t";
                    while ((line = rd.ReadLine()) != null)
                    {
                        if (!line.StartsWith(pre)) continue;
                        string[] parts = line.Split('\t');
                        if (parts.Length < 3) continue;
                        map[Regex.Unescape(parts[1])] = Regex.Unescape(parts[2]);
                    }
                }
            }
            catch { }
        }

        public static string T(string ar)
        {
            Init();
            string v;
            return map.TryGetValue(ar, out v) ? v : ar;
        }
    }

    // A clickable area of the window, drawn by the form itself.
    class Hot
    {
        public Rectangle R;
        public MethodInvoker Click;
        public string Tip;
        public string Page; // null = every page
        public Hot(Rectangle r, MethodInvoker click, string tip) { R = r; Click = click; Tip = tip; }
    }

    class MainForm : Form
    {
        const int W = 1280, H = 720;
        Settings cfg = new Settings();
        Image bg, icoNormal, icoSeamless;
        string bbsc, player, status = "";
        UpdateInfo upN, upS;
        bool checking;
        List<Hot> hots = new List<Hot>();
        Hot hover, pressed;
        Hot flash; // briefly lit after a click, so every button visibly reacts
        System.Windows.Forms.Timer flashTimer;

        static bool Same(Hot a, Hot b) { return a != null && b != null && a.R == b.R && a.Page == b.Page; }

        bool IsDown(Hot h) { return Same(pressed, h) || Same(flash, h); }

        void Flash(Hot h)
        {
            flash = h;
            if (flashTimer == null)
            {
                flashTimer = new System.Windows.Forms.Timer();
                flashTimer.Interval = 160;
                flashTimer.Tick += delegate { flashTimer.Stop(); flash = null; Invalidate(); };
            }
            flashTimer.Stop();
            flashTimer.Start();
            Invalidate();
        }
        ToolTip tip = new ToolTip();
        ContextMenuStrip savesMenu;

        string page = "home";
        Hot[] setBtns = new Hot[6];
        Hot hLang; Hot[] langHots; bool langOpen;
        Hot hGraphics, hControls, hOnline, hFovMinus, hFovPlus, hFovReset;
        Hot hClose, hMin, hMax, hHome, hFolder, hBackup, hSettings, hDiscord, hMyDiscord, hBBDiscord, hSeamless, hNormal, hPlay, hNotes, hUpN, hUpS, hUpdate, hSelfUp;
        public const string AppVersion = "1.1.1"; // bump for every release of the launcher
        UpdateInfo upL;

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);

        public MainForm()
        {
            Text = "Bloodborne Launcher";
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(W, H);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(6, 5, 8);
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            bg = Res.Img("bg.jpg");
            icoNormal = Res.Img("normal.png");
            icoSeamless = Res.Img("seamless.png");
            bbsc = cfg.BBSCVersion();
            player = cfg.PlayerName();

            hClose = Add(new Rectangle(14, 10, 40, 32), delegate { Close(); }, I18n.T("إغلاق"));
            hMin = Add(new Rectangle(60, 10, 40, 32), delegate { WindowState = FormWindowState.Minimized; }, I18n.T("تصغير"));
            hMax = Add(new Rectangle(106, 10, 40, 32), delegate { ToggleBig(); }, I18n.T("ملء الشاشة"));

            int rx = W - 70;
            hHome = Add(new Rectangle(rx, 76, 52, 52), delegate { ShowPage("home"); }, I18n.T("الرئيسية"));
            hFolder = new Hot(Rectangle.Empty, null, null); // folder menu merged into the saves page
            hBackup = Add(new Rectangle(rx, 140, 52, 52), delegate { ShowPage(page == "saves" ? "home" : "saves"); }, I18n.T("ملفات الحفظ"));
            hGraphics = Add(new Rectangle(rx, 204, 52, 52), delegate { ShowPage(page == "graphics" ? "home" : "graphics"); }, I18n.T("الرسوميات"));
            hControls = Add(new Rectangle(rx, 268, 52, 52), delegate { ShowPage(page == "controls" ? "home" : "controls"); }, I18n.T("التحكم"));
            hOnline = Add(new Rectangle(rx, 332, 52, 52), delegate { ShowPage(page == "online" ? "home" : "online"); }, I18n.T("اللعب عبر الإنترنت"));
            hSettings = Add(new Rectangle(rx, 396, 52, 52), delegate { ShowPage(page == "settings" ? "home" : "settings"); }, I18n.T("الإعدادات"));
            hMyDiscord = Add(new Rectangle(rx, H - 192, 52, 52), delegate
            {
                if (string.IsNullOrEmpty(cfg.MyDiscord)) MessageBox.Show(this, I18n.T("رابط ديسكوردك ما انحط للحين (MyDiscord في launcher.ini)."), "Bloodborne Launcher");
                else Open(cfg.MyDiscord);
            }, I18n.T("ديسكوردي"));
            hDiscord = Add(new Rectangle(rx, H - 132, 52, 52), delegate { Open(cfg.Discord); }, I18n.T("ديسكورد Seamless Co-op"));
            hBBDiscord = Add(new Rectangle(rx, H - 72, 52, 52), delegate { Open(cfg.BBportDiscord); }, I18n.T("ديسكورد BBport"));
            BuildGraphicsHots();
            BuildControlsHots();
            BuildOnlineHots();
            BuildSavesHots();

            hSeamless = Add(new Rectangle(40, H - 168, 200, 44), delegate { SetMode("seamless"); }, null);
            hNormal = Add(new Rectangle(248, H - 168, 200, 44), delegate { SetMode("normal"); }, null);
            hPlay = Add(new Rectangle(40, H - 112, 408, 76), delegate { Play(); }, null);
            hUpdate = Add(Rectangle.Empty, delegate { if (cfg.Mode == "seamless") UpdateSeamless(); else UpdateNormal(); }, I18n.T("نزل تحديث جديد - اضغط عشان تحدّث"));

            hNotes = Add(new Rectangle(830, H - 196, 150, 24), delegate { OpenNotes(); }, I18n.T("صفحة الإصدار"));
            hUpN = Add(new Rectangle(830, H - 160, 340, 30), delegate { UpdateNormal(); }, null);
            hUpS = Add(new Rectangle(830, H - 126, 340, 30), delegate { UpdateSeamless(); }, null);
            hSelfUp = Add(Rectangle.Empty, delegate { SelfUpdate(); }, I18n.T("نزلت نسخة جديدة من اللانشر"));
            foreach (Hot h in new Hot[] { hSeamless, hNormal, hPlay, hUpN, hUpS, hUpdate, hSelfUp }) h.Page = "home";
            hNotes.Page = "none"; // release-notes link removed

            MethodInvoker[] acts = new MethodInvoker[] {
                delegate { LaunchNormal(false); },
                delegate { OpenSeamlessLauncher(false); },
                delegate { OpenFolder(Path.GetDirectoryName(cfg.BBportExe)); },
                delegate { OpenFolder(Path.GetDirectoryName(cfg.SeamlessCmd)); },
                delegate { StartUpdateCheck(); },
                delegate { PickGame(); } };
            for (int i = 0; i < 6; i++)
            {
                setBtns[i] = Add(new Rectangle(80, SetRowY(i + 1) + 22, 150, 40), acts[i], null);
                setBtns[i].Page = "settings";
            }
            hLang = Add(new Rectangle(80, SetRowY(0) + 22, 240, 40), delegate { langOpen = !langOpen; Invalidate(); }, null);
            hLang.Page = "settings";
            langHots = new Hot[I18n.Codes.Length];
            for (int i = 0; i < I18n.Codes.Length; i++)
            {
                string lc = I18n.Codes[i];
                langHots[i] = new Hot(new Rectangle(80, SetRowY(0) + 64 + i * 32, 240, 32), delegate { langOpen = false; SetLanguage(lc); }, null);
                langHots[i].Page = "settings";
            }

            BuildMenus();
            status = I18n.T("جاهز للعب");
            try { if (File.Exists(Application.ExecutablePath + ".old")) File.Delete(Application.ExecutablePath + ".old"); } catch { }
            try { string su = Path.Combine(Settings.BaseDir, "_selfupdate"); if (Directory.Exists(su)) Directory.Delete(su, true); } catch { }
            if (cfg.CheckUpdates) StartUpdateCheck();
        }

        Hot Add(Rectangle r, MethodInvoker a, string t) { Hot h = new Hot(r, a, t); hots.Add(h); return h; }

        void BuildMenus()
        {
            savesMenu = NewMenu();
            savesMenu.Items.Add(I18n.T("حفظ BBport"), null, delegate { OpenFolder(cfg.NormalSaveDir()); });
            savesMenu.Items.Add(I18n.T("حفظ Seamless Co-op (shadPS4)"), null, delegate { OpenFolder(cfg.SeamlessSaveDir()); });
            savesMenu.Items.Add(new ToolStripSeparator());
            savesMenu.Items.Add(I18n.T("مجلد النسخ الاحتياطية"), null, delegate { Directory.CreateDirectory(cfg.BackupDir); OpenFolder(cfg.BackupDir); });

        }

        static ContextMenuStrip NewMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.RightToLeft = RightToLeft.Yes;
            m.Font = new Font("Segoe UI", 10.5f);
            return m;
        }

        // ---------- painting ----------

        protected override void OnPaintBackground(PaintEventArgs e) { }

        // ---------- full screen: the 1280x720 picture is drawn to a bitmap and scaled up ----------
        bool big;
        Rectangle normalBounds;
        float sc = 1f;
        int ox, oy;
        Bitmap frame;

        void ToggleBig()
        {
            if (!big)
            {
                normalBounds = Bounds;
                big = true;
                Bounds = Screen.FromControl(this).Bounds;
            }
            else
            {
                big = false;
                Bounds = normalBounds;
            }
            ComputeScale();
            Invalidate();
        }

        void ComputeScale()
        {
            if (!big) { sc = 1f; ox = oy = 0; }
            else
            {
                sc = Math.Min(ClientSize.Width / (float)W, ClientSize.Height / (float)H);
                ox = (ClientSize.Width - (int)(W * sc)) / 2;
                oy = (ClientSize.Height - (int)(H * sc)) / 2;
            }
            LayoutBoxes();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); if (big) { ComputeScale(); Invalidate(); } }

        MouseEventArgs M(MouseEventArgs e)
        {
            if (!big) return e;
            return new MouseEventArgs(e.Button, e.Clicks, (int)((e.X - ox) / sc), (int)((e.Y - oy) / sc), e.Delta);
        }

        void LayoutBoxes()
        {
            foreach (TextBox t in new TextBox[] { tbName, tbPass, tbMail })
            {
                if (t == null || !(t.Tag is Rectangle)) continue;
                Rectangle r = (Rectangle)t.Tag;
                t.Font = new Font("Segoe UI", 12f * sc);
                t.Bounds = new Rectangle(ox + (int)(r.X * sc), oy + (int)(r.Y * sc), (int)(r.Width * sc), (int)(r.Height * sc));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!big) { PaintScene(e.Graphics); return; }
            if (frame == null) frame = new Bitmap(W, H);
            using (Graphics fg = Graphics.FromImage(frame)) PaintScene(fg);
            e.Graphics.Clear(Color.Black);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(frame, new Rectangle(ox, oy, (int)(W * sc), (int)(H * sc)));
        }

        void PaintScene(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            if (bg != null) g.DrawImage(bg, new Rectangle(0, 0, W, H)); else g.Clear(BackColor);

            using (Pen p = new Pen(Color.FromArgb(120, Theme.GoldDim))) g.DrawRectangle(p, 0, 0, W - 1, H - 1);
            DrawTitleBar(g);
            DrawRail(g);
            if (page != "home")
            {
                // Pages scroll with the mouse wheel when their content is taller than the window.
                string pid = PageId();
                if (pid != lastPageId) { lastPageId = pid; scrollY = 0; lastContentBottom = 0; }
                GraphicsState gs = g.Save();
                g.SetClip(new Rectangle(1, 54, W - 92, H - 55));
                g.TranslateTransform(0, -scrollY);
                contentBottom = 0;
                if (page == "settings") DrawSettings(g);
                else if (page == "graphics") DrawGraphics(g);
                else if (page == "controls") DrawControls(g);
                else if (page == "online") DrawOnline(g);
                else if (page == "saves") DrawSaves(g);
                g.Restore(gs);
                if (contentBottom != lastContentBottom) { lastContentBottom = contentBottom; Invalidate(); }
                int newMax = Math.Max(0, contentBottom + 60 - H);
                if (newMax != maxScroll) { maxScroll = newMax; if (scrollY > maxScroll) { scrollY = maxScroll; Invalidate(); } }
                if (maxScroll > 0)
                {
                    int trackH = H - 90, thumbH = Math.Max(40, trackH * (H - 60) / (H - 60 + maxScroll));
                    int ty = 70 + (trackH - thumbH) * scrollY / maxScroll;
                    FillRound(g, new Rectangle(W - 98, 70, 6, trackH), 3, Color.FromArgb(60, 255, 255, 255), Color.Empty, 0);
                    FillRound(g, new Rectangle(W - 98, ty, 6, thumbH), 3, Color.FromArgb(220, Theme.Gold), Color.Empty, 0);
                }
                return;
            }
            DrawBadges(g);
            DrawPlayArea(g);
            DrawStatus(g);
            DrawUpdates(g);
        }

        static void Txt(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags extra)
        {
            if (!I18n.Rtl) extra &= ~TextFormatFlags.RightToLeft;
            TextRenderer.DrawText(g, s, f, r, c, TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | extra);
        }

        static void Glyph(Graphics g, string glyph, Rectangle r, Color c, float size)
        {
            using (Font f = new Font("Segoe MDL2 Assets", size))
                TextRenderer.DrawText(g, glyph, f, r, c, TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        static void FillRound(Graphics g, Rectangle r, float rad, Color fill, Color border, float bw)
        {
            using (GraphicsPath p = Res.Round(r, rad))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (bw > 0) using (Pen pen = new Pen(border, bw)) g.DrawPath(pen, p);
            }
        }

        void DrawTitleBar(Graphics g)
        {
            FillRound(g, hClose.R, 6, hover == hClose ? Color.FromArgb(220, Theme.Blood) : Color.FromArgb(70, 0, 0, 0), Color.Empty, 0);
            Glyph(g, "\uE8BB", hClose.R, Theme.Text, 10f);
            FillRound(g, hMin.R, 6, hover == hMin ? Color.FromArgb(230, Theme.Blood) : Color.FromArgb(170, Theme.BloodDark), Color.Empty, 0);
            Glyph(g, "\uE921", hMin.R, Theme.Text, 10f);
            FillRound(g, hMax.R, 6, hover == hMax ? Color.FromArgb(230, Theme.Blood) : Color.FromArgb(170, Theme.BloodDark), Color.Empty, 0);
            Glyph(g, big ? "\uE923" : "\uE922", hMax.R, Theme.Text, 10f);

            if (icoNormal != null) g.DrawImage(icoNormal, new Rectangle(W - 46, 12, 28, 28));
            using (Font f = new Font(Res.Cinzel(), 13f, FontStyle.Bold))
                Txt(g, "BLOODBORNE  ·  LAUNCHER", f, new Rectangle(W - 470, 8, 412, 36), Theme.GoldLight, TextFormatFlags.Right);
            using (Pen p = new Pen(Color.FromArgb(90, Theme.Gold))) g.DrawLine(p, 12, 52, W - 12, 52);
        }

        void DrawRail(Graphics g)
        {
            Rectangle rail = new Rectangle(W - 82, 64, 76, H - 76);
            FillRound(g, rail, 14, Color.FromArgb(130, 8, 6, 10), Color.FromArgb(70, Theme.GoldDim), 1f);
            RailButton(g, hHome, "\uE80F", page == "home");
            RailButton(g, hBackup, "\uE8B7", page == "saves");
            RailButton(g, hGraphics, "\uE7F4", page == "graphics");
            RailButton(g, hControls, "\uE7FC", page == "controls");
            RailButton(g, hOnline, "\uE774", page == "online");
            RailButton(g, hSettings, "\uE713", page == "settings");
            RailButton(g, hMyDiscord, "\uE716", false);
            RailButton(g, hDiscord, "\uE8BD", false);
            RailButton(g, hBBDiscord, "\uE8F2", false);
        }

        void RailButton(Graphics g, Hot h, string glyph, bool active)
        {
            bool hv = hover == h;
            if (IsDown(h))
            {
                Rectangle r = h.R; r.Inflate(-3, -3);
                FillRound(g, r, 10, Color.FromArgb(230, 160, 40, 70), Theme.GoldLight, 2f);
                Glyph(g, glyph, r, Color.White, 15f);
                return;
            }
            if (active || hv)
                FillRound(g, h.R, 10, active ? Color.FromArgb(160, Theme.BloodDark) : Color.FromArgb(100, Theme.BloodDark),
                    active ? Theme.Gold : Color.FromArgb(150, Theme.GoldDim), active ? 2f : 1f);
            Glyph(g, glyph, h.R, active || hv ? Theme.GoldLight : Theme.Text, 17f);
        }

        string BBportLabel()
        {
            return "BBport" + (upN != null && !upN.Available ? " " + upN.Latest : "");
        }

        void DrawBadges(Graphics g)
        {
            using (Font f = new Font("Segoe UI Semibold", 10f, FontStyle.Bold))
            {
                int x = 40;
                foreach (string s in new string[] { "BBSC " + bbsc, BBportLabel() })
                {
                    Size ts = TextRenderer.MeasureText(s, f);
                    Rectangle r = new Rectangle(x, 66, ts.Width + 28, 32);
                    FillRound(g, r, 16, Color.FromArgb(160, 10, 8, 12), Theme.Gold, 1.6f);
                    Txt(g, s, f, r, Theme.GoldLight, TextFormatFlags.HorizontalCenter);
                    x = r.Right + 10;
                }
                // a new version of this launcher: a clickable badge
                if (upL != null && upL.Available)
                {
                    string s = I18n.T("⬇  تحديث اللانشر ") + upL.Latest;
                    Size ts = TextRenderer.MeasureText(s, f);
                    Rectangle r = new Rectangle(x, 66, ts.Width + 34, 32);
                    hSelfUp.R = r;
                    bool hv = hover == hSelfUp;
                    FillRound(g, r, 16, Color.FromArgb(hv ? 250 : 225, Theme.Blood), hv ? Theme.GoldLight : Theme.Gold, hv ? 2.2f : 1.6f);
                    Txt(g, s, f, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
                }
                else hSelfUp.R = Rectangle.Empty;
                if (player != "")
                    Txt(g, I18n.T("الصياد: ") + player, f, new Rectangle(40, 104, 300, 24), Theme.Green, TextFormatFlags.Left | TextFormatFlags.RightToLeft);
            }
        }

        void DrawLogo(Graphics g)
        {
            using (Font f = new Font(Res.Cinzel(), 58f, FontStyle.Bold))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                RectangleF r = new RectangleF(0, 300, W - 90, 110);
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(190, 0, 0, 0)))
                    g.DrawString("BLOODBORNE", f, sh, new RectangleF(r.X + 4, r.Y + 5, r.Width, r.Height), sf);
                using (LinearGradientBrush gb = new LinearGradientBrush(new Rectangle(0, 310, 10, 90),
                    Theme.GoldLight, Color.FromArgb(160, 118, 60), 90f))
                    g.DrawString("BLOODBORNE", f, gb, r, sf);
            }
            using (Font f = new Font(Res.Cinzel(), 12f, FontStyle.Bold))
                Txt(g, "HUNTER'S LAUNCHER", f, new Rectangle(0, 398, W - 90, 26), Theme.Gold, TextFormatFlags.HorizontalCenter);
        }

        void DrawPlayArea(Graphics g)
        {
            bool seam = cfg.Mode == "seamless";
            using (Font f = new Font("Segoe UI", 9.5f))
            {
                string l1 = seam ? I18n.T("Seamless Co-op: العب مع أصحابك، المضيف Hunt Bell والضيف Kin Bell") : I18n.T("BBport: اللعب بدون كوب مع DLSS وكل الإعدادات");
                string l2 = seam ? "BBSC " + bbsc + "  ·  shadPS4  ·  play.seamlesshunt.com" : BBportLabel() + "  ·  DLSS  ·  2K";
                Txt(g, l1, f, new Rectangle(40, H - 214, 408, 20), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
                Txt(g, l2, f, new Rectangle(40, H - 194, 408, 20), Theme.TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            }
            ModeButton(g, hSeamless, "Seamless Co-op", icoSeamless, seam);
            ModeButton(g, hNormal, "BBport", icoNormal, !seam);

            // an update for the selected mode: the play button gets shorter and an update button appears next to it
            UpdateInfo cu = seam ? upS : upN;
            bool need = NeedsInstall();
            bool upd = cu != null && cu.Available && !need && !installing;
            hPlay.R = new Rectangle(40, H - 112, upd ? 290 : 408, 76);
            hUpdate.R = upd ? new Rectangle(338, H - 112, 110, 76) : Rectangle.Empty;
            if (upd)
            {
                Rectangle ur = hUpdate.R;
                if (pressed == hUpdate) ur.Inflate(-3, -3);
                bool uhv = hover == hUpdate;
                FillRound(g, ur, 12, Color.FromArgb(uhv ? 245 : 225, Theme.Blood), uhv ? Theme.GoldLight : Theme.Gold, uhv ? 2.4f : 1.8f);
                Glyph(g, "\uE896", new Rectangle(ur.X, ur.Y + 8, ur.Width, 26), Theme.GoldLight, 15f);
                using (Font f = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold))
                    Txt(g, I18n.T("تحديث"), f, new Rectangle(ur.X, ur.Y + 32, ur.Width, 22), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
                using (Font f = new Font("Segoe UI", 8.5f))
                    Txt(g, cu.Latest, f, new Rectangle(ur.X, ur.Y + 52, ur.Width, 18), Theme.GoldLight, TextFormatFlags.HorizontalCenter);
            }

            Rectangle pr = hPlay.R;
            if (pressed == hPlay) pr.Inflate(-3, -3);
            bool hv = hover == hPlay;
            using (GraphicsPath p = Res.Round(pr, 12))
            {
                using (LinearGradientBrush lb = new LinearGradientBrush(pr,
                    hv ? Color.FromArgb(250, 232, 190) : Color.FromArgb(236, 214, 165),
                    hv ? Color.FromArgb(205, 165, 95) : Color.FromArgb(180, 140, 80), 90f))
                    g.FillPath(lb, p);
                using (Pen pen = new Pen(Color.FromArgb(hv ? 255 : 200, 255, 240, 205), hv ? 2.5f : 1.5f)) g.DrawPath(pen, p);
            }
            if (installing)
            {
                Rectangle fill = new Rectangle(pr.X, pr.Y, Math.Max(12, pr.Width * installPct / 100), pr.Height);
                using (GraphicsPath p = Res.Round(pr, 12))
                {
                    Region old = g.Clip;
                    g.SetClip(p, CombineMode.Intersect);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(150, Theme.Blood))) g.FillRectangle(b, fill);
                    g.Clip = old;
                }
                using (Font f = new Font("Segoe UI Semibold", 15f, FontStyle.Bold))
                    Txt(g, installPct + "%", f, new Rectangle(pr.X, pr.Y + 6, pr.Width, 36), Color.FromArgb(45, 22, 14), TextFormatFlags.HorizontalCenter);
                using (Font f = new Font("Segoe UI", 9f))
                    Txt(g, installMsg, f, new Rectangle(pr.X + 8, pr.Y + 42, pr.Width - 16, 24), Color.FromArgb(60, 30, 18), TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
            }
            else if (need)
            {
                Glyph(g, "\uE896", new Rectangle(pr.X, pr.Y + 6, pr.Width, 34), Color.FromArgb(45, 22, 14), 17f);
                using (Font f = new Font("Segoe UI Semibold", 15f, FontStyle.Bold))
                    Txt(g, I18n.T("تحميل ") + (seam ? "Seamless Co-op" : "BBport"), f, new Rectangle(pr.X, pr.Y + 36, pr.Width, 34), Color.FromArgb(45, 22, 14), TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
            }
            else
                using (Font f = new Font("Segoe UI Semibold", 20f, FontStyle.Bold))
                    Txt(g, I18n.T("تشغيل"), f, pr, Color.FromArgb(45, 22, 14), TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
        }

        void ModeButton(Graphics g, Hot h, string text, Image icon, bool on)
        {
            bool hv = hover == h;
            FillRound(g, h.R, 8,
                on ? Color.FromArgb(220, Theme.BloodDark) : Color.FromArgb(hv ? 180 : 140, 10, 8, 12),
                on ? Theme.Gold : Color.FromArgb(hv ? 200 : 120, Theme.GoldDim), on ? 2f : 1.2f);
            if (icon != null) g.DrawImage(icon, new Rectangle(h.R.X + 10, h.R.Y + 8, 28, 28));
            using (Font f = new Font("Segoe UI Semibold", 11f, FontStyle.Bold))
                Txt(g, text, f, new Rectangle(h.R.X + 40, h.R.Y, h.R.Width - 46, h.R.Height), on ? Theme.GoldLight : Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
        }

        void DrawStatus(Graphics g)
        {
            bool seam = cfg.Mode == "seamless";
            UpdateInfo u = seam ? upS : upN;
            string ver = seam ? "BBSC " + bbsc : BBportLabel();
            string st = installing ? I18n.T("يحمّل ...") : NeedsInstall() ? I18n.T("مو محمّل - اضغط تحميل") : checking ? I18n.T("يتأكد من التحديثات ...") :
                        (u != null && u.Available ? I18n.T("فيه تحديث جديد: ") + u.Latest :
                        (u != null ? I18n.T("محدّث — جاهز للعب") : status));
            Rectangle area = new Rectangle(480, H - 134, 320, 70);
            using (Font f = new Font("Segoe UI Semibold", 10f, FontStyle.Bold))
            {
                Txt(g, ver, f, new Rectangle(area.X, area.Y, 140, 24), Theme.Gold, TextFormatFlags.Left);
                Txt(g, st, f, new Rectangle(area.X + 100, area.Y, area.Width - 100, 24),
                    u != null && u.Available ? Theme.GoldLight : Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
            }
            Rectangle bar = new Rectangle(area.X, area.Y + 32, area.Width, 6);
            FillRound(g, bar, 3, u != null && u.Available ? Theme.Blood : Color.FromArgb(230, Theme.GoldLight), Color.Empty, 0);
            string path = seam ? Path.GetDirectoryName(cfg.SeamlessCmd) : Path.GetDirectoryName(cfg.BBportExe);
            using (Font f = new Font("Consolas", 8.5f))
                Txt(g, path, f, new Rectangle(area.X, area.Y + 44, area.Width, 20), Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.PathEllipsis);
        }

        void DrawUpdates(Graphics g)
        {
            Rectangle head = new Rectangle(990, H - 198, 180, 28);
            using (Font f = new Font("Segoe UI Semibold", 13f, FontStyle.Bold))
                Txt(g, I18n.T("آخر تحديث"), f, head, Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);

            UpdateLine(g, hUpN, "BBport", upN, Color.FromArgb(220, 120, 140));
            UpdateLine(g, hUpS, "Seamless Co-op (BBSC)", upS, Color.FromArgb(120, 160, 230));
            using (Font f = new Font("Segoe UI", 9.5f))
                Txt(g, I18n.T("السيرفر: play.seamlesshunt.com"), f, new Rectangle(830, H - 92, 316, 26), Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
        }

        void UpdateLine(Graphics g, Hot h, string name, UpdateInfo u, Color dot)
        {
            string s;
            Color c = Theme.Text;
            if (checking && u == null) s = name + I18n.T(": يتأكد ...");
            else if (u == null) s = name + I18n.T(": ما قدرت أتأكد");
            else if (u.Available) { s = name + I18n.T(": تحديث جديد ") + u.Latest + I18n.T("  (اضغط)"); c = Theme.GoldLight; }
            else s = name + I18n.T(": محدّث ") + u.Latest;
            bool hv = hover == h && u != null && u.Available;
            if (hv) FillRound(g, h.R, 6, Color.FromArgb(100, Theme.BloodDark), Color.Empty, 0);
            using (Font f = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold))
                Txt(g, s, f, new Rectangle(h.R.X, h.R.Y, h.R.Width - 24, h.R.Height), c, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
            Rectangle d = new Rectangle(h.R.Right - 16, h.R.Y + h.R.Height / 2 - 5, 10, 10);
            PointF[] diamond = new PointF[] {
                new PointF(d.X + 5, d.Y), new PointF(d.Right, d.Y + 5), new PointF(d.X + 5, d.Bottom), new PointF(d.X, d.Y + 5) };
            using (SolidBrush b = new SolidBrush(u != null && u.Available ? Theme.Blood : dot)) g.FillPolygon(b, diamond);
        }

        // ---------- graphics page: normal play (BBport) and Seamless Co-op (shadPS4) ----------

        class SegRow
        {
            public string Title, Key; public bool Json; public string[] Labels, Values;
            public SegRow(string t, string k, bool j, string[] l, string[] v) { Title = t; Key = k; Json = j; Labels = l; Values = v; }
        }

        class Tog
        {
            public string Title, Key; public bool Json;
            public Tog(string t, string k, bool j) { Title = t; Key = k; Json = j; }
        }

        string gtab = "normal", ctab = "normal";

        // Normal play (BBport): bbport.ini, plus fps_mode / fullscreen in BBport's launcher settings.json.
        SegRow[] segN = new SegRow[] {
            new SegRow(I18n.T("الدقة"), "output_res", false,
                new string[] { "1920×1080", "2560×1440", "3440×1440", "3840×2160" },
                new string[] { "1920x1080", "2560x1440", "3440x1440", "3840x2160" }),
            new SegRow(I18n.T("الفريمات"), "fps_mode", true,
                new string[] { "30", "60", "90", I18n.T("مفتوح") }, new string[] { "30", "60", "90", "uncap" }),
            new SegRow(I18n.T("محسّن الصورة"), "upscaler", false,
                new string[] { "DLSS", "FSR 3", "FSR 4", "TAA" }, new string[] { "dlss", "fsr3", "fsr4", "taa" }),
            new SegRow(I18n.T("الجودة"), "preset", false,
                new string[] { "Native AA", "Quality", "Balanced", "Performance", "Ultra Perf" },
                new string[] { "0", "1", "2", "3", "4" }),
            new SegRow(I18n.T("تفاصيل المجسّمات"), "model_lod", false,
                new string[] { I18n.T("الأعلى"), I18n.T("عادي"), I18n.T("منخفض"), I18n.T("الأقل") }, new string[] { "-2", "0", "1", "2" }) };

        Tog[] togN = new Tog[] {
            new Tog(I18n.T("ملء الشاشة"), "fullscreen", true),
            new Tog(I18n.T("عداد الفريمات"), "show_fps", false),
            new Tog(I18n.T("ضبابية الحركة"), "effect_motion_blur", false),
            new Tog(I18n.T("عمق المجال (DoF)"), "effect_dof", false),
            new Tog(I18n.T("الظلال الديناميكية"), "effect_dynamic_shadows", false),
            new Tog(I18n.T("الإضاءة المحيطة (SSAO)"), "effect_ssao", false),
            new Tog(I18n.T("الانعكاسات (SSR)"), "effect_ssr", false),
            new Tog(I18n.T("تشوّه الألوان"), "effect_chromatic_aberration", false),
            new Tog(I18n.T("تنعيم اللعبة (AA)"), "effect_game_aa", false) };

        // Seamless (shadPS4): patches\shadPS4\Bloodborne.xml and custom_configs\CUSA03173.json.
        // Keys: s_* = special rows, p:NAME = patch on, p!NAME = patch off (effect on), c:KEY = config bool.
        SegRow[] segS = new SegRow[] {
            new SegRow(I18n.T("الدقة"), "s_res", false,
                new string[] { "1920×1080", "2560×1440", "3440×1440", "3840×2160" },
                new string[] { "1920x1080", "2560x1440", "3440x1440", "3840x2160" }),
            new SegRow(I18n.T("الفريمات"), "s_fps", false,
                new string[] { "30", "60", "90", I18n.T("مفتوح") }, new string[] { "30", "60", "90", "uncap" }),
            new SegRow(I18n.T("وضع الشاشة"), "s_mode", false,
                new string[] { I18n.T("نافذة"), I18n.T("ملء الشاشة"), I18n.T("بلا إطار") }, new string[] { "windowed", "fullscreen", "borderless" }),
            new SegRow(I18n.T("تفاصيل المجسّمات"), "s_lod", false,
                new string[] { I18n.T("الأعلى"), I18n.T("عادي"), I18n.T("منخفض"), I18n.T("الأقل") }, new string[] { "-2", "0", "1", "2" }) };

        Tog[] togS = new Tog[] {
            new Tog(I18n.T("عداد الفريمات"), "c:show_fps_counter", false),
            new Tog(I18n.T("حدّة الصورة (RCAS)"), "c:rcas_enabled", false),
            new Tog(I18n.T("ضبابية الحركة"), "p!Disable Motion Blur (perf increase)", false),
            new Tog(I18n.T("عمق المجال (DoF)"), "p!Disable DoF", false),
            new Tog(I18n.T("الظلال الديناميكية"), "p!Disable Dynamic Light Shadows (perf increase)", false),
            new Tog(I18n.T("الإضاءة المحيطة (SSAO)"), "p!Disable SSAO", false),
            new Tog(I18n.T("الانعكاسات (SSR)"), "p:Enable Screen Space Reflections (READ NOTE)", false),
            new Tog(I18n.T("تشوّه الألوان"), "p!Disable Chromatic Aberration", false),
            new Tog(I18n.T("تنعيم اللعبة (AA)"), "p!Disable AA", false),
            new Tog(I18n.T("تخطي المقدمة"), "p:Skip Intro", false) };

        Dictionary<string, List<Hot>[]> segHotsBy = new Dictionary<string, List<Hot>[]>();
        Dictionary<string, Hot[]> togHotsBy = new Dictionary<string, Hot[]>();
        Hot hGTabN, hGTabS, hAutoGfx;
        Dictionary<string, string> ini = new Dictionary<string, string>();
        string fpsMode = "60";
        bool fullscreen;
        string sXml = "", sCfg = "";

        static int GRowY(int i) { return 150 + i * 84; }
        static int TRowY(int i) { return 178 + i * 44; }

        string IniPath { get { return Path.Combine(Path.GetDirectoryName(cfg.BBportExe), "bbport.ini"); } }
        static string AppDir { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } }
        static string JsonPath { get { return Path.Combine(AppDir, @"bbport-launcher\settings.json"); } }
        static string ShadDir { get { return Path.Combine(AppDir, "shadPS4"); } }
        static string SXmlPath { get { return Path.Combine(ShadDir, @"patches\shadPS4\Bloodborne.xml"); } }
        static string SCfgPath { get { return Path.Combine(ShadDir, @"custom_configs\CUSA03173.json"); } }

        void BuildGraphicsHots()
        {
            hGTabN = Add(new Rectangle(80, 84, 170, 36), delegate { gtab = "normal"; gDrop = -1; LoadGraphics(); Invalidate(); }, null);
            hGTabS = Add(new Rectangle(258, 84, 170, 36), delegate { gtab = "seamless"; gDrop = -1; LoadGraphics(); Invalidate(); }, null);
            hGTabN.Page = hGTabS.Page = "graphics";
            hAutoGfx = Add(new Rectangle(440, 84, 200, 36), delegate { AutoGraphicsButton(); }, I18n.T("يقرأ مواصفات جهازك ويضبط الرسوميات على حسبها"));
            hAutoGfx.Page = "graphics";
            BuildNormalGraphics();
            BuildTab("seamless", segS, togS);
        }

        void BuildTab(string tab, SegRow[] rows, Tog[] tg)
        {
            List<Hot>[] seg = new List<Hot>[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                seg[i] = new List<Hot>();
                SegRow row = rows[i];
                int n = row.Values.Length, gap = 6, x0 = 640, width = 510;
                int w = (width - (n - 1) * gap) / n;
                for (int j = 0; j < n; j++)
                {
                    string val = row.Values[j];
                    Hot h = Add(new Rectangle(x0 + j * (w + gap), GRowY(i) + 32, w, 36), delegate { SetGraphic(row.Key, row.Json, val); }, null);
                    h.Page = "graphics:" + tab;
                    seg[i].Add(h);
                }
            }
            Hot[] th = new Hot[tg.Length];
            for (int k = 0; k < tg.Length; k++)
            {
                Tog t = tg[k];
                th[k] = Add(new Rectangle(100, TRowY(k), 490, 40), delegate { FlipToggle(t); }, null);
                th[k].Page = "graphics:" + tab;
            }
            segHotsBy[tab] = seg;
            togHotsBy[tab] = th;
        }

        void LoadGraphics()
        {
            ini.Clear();
            try
            {
                foreach (string raw in File.ReadAllLines(IniPath))
                {
                    string l = raw.Trim();
                    int eq = l.IndexOf('=');
                    if (l.StartsWith("#") || eq < 1) continue;
                    ini[l.Substring(0, eq).Trim()] = l.Substring(eq + 1).Trim();
                }
            }
            catch { }
            string f = Settings.ReadJsonString(JsonPath, "fps_mode");
            fpsMode = string.IsNullOrEmpty(f) ? "60" : f;
            try
            {
                Match m = Regex.Match(File.ReadAllText(JsonPath), "\"fullscreen\"\\s*:\\s*(true|false)");
                fullscreen = m.Success && m.Groups[1].Value == "true";
            }
            catch { fullscreen = false; }
            LoadJsonVals();
            if (gtab == "seamless") EnsurePatches();
            try { sXml = File.ReadAllText(SXmlPath); } catch { sXml = ""; }
            try { sCfg = File.ReadAllText(SCfgPath); } catch { sCfg = ""; }
        }

        string IniGet(string k, string def) { string v; return ini.TryGetValue(k, out v) ? v : def; }

        // --- shadPS4 patch file and game config helpers ---

        // Patch names change a little between versions of the patch file ("Model LOD -2 (Highest)" /
        // "(Highest model detail)"), so the part in brackets at the end is not compared.
        static Regex PatchRe(string name)
        {
            string b = Regex.Replace(name, @"\s*\([^)]*\)\s*$", "");
            return new Regex("(<Metadata\\b[^>]*\\bName=\"" + Regex.Escape(b) + "(?:\\s*\\([^\"]*\\))?\"[^>]*\\bisEnabled=\")(true|false)(\")", RegexOptions.IgnoreCase);
        }

        // A new PC has no shadPS4 patch file until its own launcher downloads one: fetch the official
        // Bloodborne.xml (shadps4-emu/ps4_cheats) and give every patch an isEnabled switch.
        static bool patchTried, patchesWritten;
        // the version of the patch file BBSC 0.0.2 runs with (shadps4-emu/ps4_cheats, July 2026)
        const string PatchCommit = "5a659cc8b259dbcd805c5e5b0b2328e8593158a9";
        const string PatchUrl = "https://raw.githubusercontent.com/shadps4-emu/ps4_cheats/" + PatchCommit + "/PATCHES/Bloodborne.xml";
        const string PatchMark = "<!-- bbl:" + PatchCommit + " -->";
        static bool EnsurePatches()
        {
            if (File.Exists(SXmlPath))
            {
                // replace a patch file this launcher wrote from another version, or the newer upstream one
                // (its patches crash the game in BBSC's emulator); keep anything else as it is
                string have = "";
                try { have = File.ReadAllText(SXmlPath); } catch { }
                bool ours = have.Contains("<!-- bbl:");
                bool stale = (ours && !have.Contains(PatchMark)) || (!ours && have.Contains("Highest model detail"));
                if (!stale || patchTried) { EnsurePatchIndex(); return true; }
                try { File.Copy(SXmlPath, SXmlPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); } catch { }
            }
            if (patchTried) return File.Exists(SXmlPath);
            patchTried = true;
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                string xml;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "BloodborneLauncher/2.0";
                    wc.Encoding = Encoding.UTF8;
                    xml = wc.DownloadString(PatchUrl);
                }
                if (!xml.Contains("<Metadata")) return File.Exists(SXmlPath);
                xml = Regex.Replace(xml, "<Metadata\\b[^>]*>", delegate (Match m)
                {
                    string tag = m.Value;
                    if (tag.Contains("isEnabled=")) return tag;
                    return tag.EndsWith("/>") ? tag.Substring(0, tag.Length - 2) + " isEnabled=\"false\"/>" : tag.Substring(0, tag.Length - 1) + " isEnabled=\"false\">";
                });
                int at = xml.IndexOf("<Patch>");
                if (at >= 0) xml = xml.Insert(at, PatchMark + "\n");
                Directory.CreateDirectory(Path.GetDirectoryName(SXmlPath));
                File.WriteAllText(SXmlPath, xml, new UTF8Encoding(false));
                EnsurePatchIndex();
                patchesWritten = true;
                return true;
            }
            catch { return false; }
        }

        // shadPS4 finds a game's patch file through files.json next to it, and stops with an error
        // when that file is missing or empty.
        static void EnsurePatchIndex()
        {
            try
            {
                string idx = Path.Combine(Path.GetDirectoryName(SXmlPath), "files.json");
                string cur = File.Exists(idx) ? File.ReadAllText(idx) : "";
                if (cur.Contains("\"Bloodborne.xml\"")) return;
                List<string> ids = new List<string>();
                foreach (Match m in Regex.Matches(File.ReadAllText(SXmlPath), "<ID>\\s*(CUSA\\d+)\\s*</ID>"))
                    if (!ids.Contains(m.Groups[1].Value)) ids.Add(m.Groups[1].Value);
                if (!ids.Contains("CUSA03173")) ids.Add("CUSA03173");
                string entry = "\"Bloodborne.xml\": [\"" + string.Join("\", \"", ids.ToArray()) + "\"]";
                string t = cur.Trim();
                if (t.StartsWith("{") && t.EndsWith("}") && t.Length > 2)
                    t = t.Substring(0, t.Length - 1).TrimEnd() + ",\n    " + entry + "\n}";
                else
                    t = "{\n    " + entry + "\n}";
                File.WriteAllText(idx, t, new UTF8Encoding(false));
            }
            catch { }
        }

        bool PatchOn(string name)
        {
            Match m = PatchRe(name).Match(sXml);
            return m.Success && m.Groups[2].Value == "true";
        }

        void PatchSet(string name, bool on)
        {
            sXml = PatchRe(name).Replace(sXml, "${1}" + (on ? "true" : "false") + "${3}", 1);
        }

        void SaveXml()
        {
            try { if (sXml != "") File.WriteAllText(SXmlPath, sXml, new UTF8Encoding(false)); }
            catch (Exception ex) { Fail(ex); }
        }

        string CfgValue(string key)
        {
            Match m = Regex.Match(sCfg, "\"" + key + "\"\\s*:\\s*(\"[^\"]*\"|true|false|-?\\d+)");
            return m.Success ? m.Groups[1].Value.Trim('"') : null;
        }

        void CfgSet(string key, string jsonValue)
        {
            Regex re = new Regex("(\"" + key + "\"\\s*:\\s*)(\"[^\"]*\"|true|false|-?\\d+)");
            if (re.IsMatch(sCfg)) sCfg = re.Replace(sCfg, "${1}" + jsonValue, 1);
            else sCfg = new Regex("(\"General\"\\s*:\\s*\\{)").Replace(sCfg, "${1}\r\n        \"" + key + "\": " + jsonValue + ",", 1);
        }

        void SaveCfg()
        {
            try { if (sCfg != "") File.WriteAllText(SCfgPath, sCfg, new UTF8Encoding(false)); }
            catch (Exception ex) { Fail(ex); }
        }

        static readonly string[] ResPatches = {
            "Resolution Patch 640x360 (16:9)", "Resolution Patch 960x540 (16:9)", "Resolution Patch 1280x720 (16:9)",
            "Resolution Patch 1440x810 (16:9)", "Resolution Patch 1600x900 (16:9)", "Resolution Patch 2560x1440 (16:9)",
            "Resolution Patch 3840x2160 (16:9)", "Resolution Patch 1280x800 (16:10)", "Resolution Patch 1920x1200 (16:10)",
            "Resolution Patch 2560x1600 (16:10)", "Resolution Patch 2560x1080 (21:9)", "Resolution Patch 3440x1440 (21:9)",
            "Resolution Patch 5120x2160 (21:9)", "Resolution Patch 3840x1080 (32:9)", "Resolution Patch 5120x1440 (32:9)",
            "Resolution Patch 1280x960 (4:3)", "Resolution Patch 1440x1080 (4:3)" };
        static readonly string[] GridPatches = {
            "1280x800 Light Grid For SteamDeck (READ NOTES)", "1080p Light Grid (READ NOTES)",
            "1440p Light Grid (READ NOTES)", "4k Light Grid (READ NOTES)" };
        static readonly string[] FpsPatches = { "30 FPS++", "60FPS (no deltatime)", "60 FPS++", "90 FPS++", "Uncap FPS++" };
        static readonly string[] LodPatches = { "Model LOD -2 (Highest)", "Model LOD 1 (Lower)", "Model LOD 2 (Lowest)" };

        string SeamlessGet(string key)
        {
            if (key == "s_res")
            {
                if (PatchOn("Resolution Patch 3840x2160 (16:9)")) return "3840x2160";
                if (PatchOn("Resolution Patch 3440x1440 (21:9)")) return "3440x1440";
                if (PatchOn("Resolution Patch 2560x1440 (16:9)")) return "2560x1440";
                foreach (string r in ResPatches) if (PatchOn(r)) return "other";
                return "1920x1080";
            }
            if (key == "s_fps")
            {
                if (PatchOn("Uncap FPS++")) return "uncap";
                if (PatchOn("90 FPS++")) return "90";
                if (PatchOn("60 FPS++") || PatchOn("60FPS (no deltatime)")) return "60";
                return "30";
            }
            if (key == "s_mode")
            {
                if (CfgValue("full_screen") != "true") return "windowed";
                string m = CfgValue("full_screen_mode") ?? "";
                return m.IndexOf("Borderless", StringComparison.OrdinalIgnoreCase) >= 0 ? "borderless" : "fullscreen";
            }
            if (key == "s_lod")
            {
                if (PatchOn("Model LOD -2 (Highest)")) return "-2";
                if (PatchOn("Model LOD 1 (Lower)")) return "1";
                if (PatchOn("Model LOD 2 (Lowest)")) return "2";
                return "0";
            }
            if (key.StartsWith("p:")) return PatchOn(key.Substring(2)) ? "1" : "0";
            if (key.StartsWith("p!")) return PatchOn(key.Substring(2)) ? "0" : "1";
            if (key.StartsWith("c:")) return CfgValue(key.Substring(2)) == "true" ? "1" : "0";
            return "";
        }

        void SeamlessSet(string key, string val)
        {
            if (sXml == "" && (key.StartsWith("s_") || key.StartsWith("p")) && key != "s_mode")
            {
                Missing(SXmlPath);
                return;
            }
            if (key == "s_res")
            {
                foreach (string r in ResPatches) PatchSet(r, false);
                foreach (string gp in GridPatches) PatchSet(gp, false);
                string res = null, grid = "1080p Light Grid (READ NOTES)";
                if (val == "2560x1440") { res = "Resolution Patch 2560x1440 (16:9)"; grid = "1440p Light Grid (READ NOTES)"; }
                if (val == "3440x1440") { res = "Resolution Patch 3440x1440 (21:9)"; grid = "1440p Light Grid (READ NOTES)"; }
                if (val == "3840x2160") { res = "Resolution Patch 3840x2160 (16:9)"; grid = "4k Light Grid (READ NOTES)"; }
                if (res != null) PatchSet(res, true);
                PatchSet(grid, true);
                string[] wh = val.Split('x');
                CfgSet("window_width", wh[0]);
                CfgSet("window_height", wh[1]);
                // above 1080p the game needs extra GPU memory in the emulator, or it crashes once it draws
                int cur = 0;
                int.TryParse(CfgValue("extra_dmem_in_mbytes") ?? "0", out cur);
                if (val != "1920x1080" && cur < 8000) CfgSet("extra_dmem_in_mbytes", "8000");
                SaveXml(); SaveCfg();
            }
            else if (key == "s_fps")
            {
                foreach (string fp in FpsPatches) PatchSet(fp, false);
                if (val == "30") PatchSet("30 FPS++", true);
                if (val == "60") PatchSet("60 FPS++", true);
                if (val == "90") PatchSet("90 FPS++", true);
                if (val == "uncap") PatchSet("Uncap FPS++", true);
                CfgSet("vblank_frequency", val == "90" ? "90" : (val == "uncap" ? "999" : "60"));
                SaveXml(); SaveCfg();
            }
            else if (key == "s_mode")
            {
                CfgSet("full_screen", val == "windowed" ? "false" : "true");
                CfgSet("full_screen_mode", val == "windowed" ? "\"Windowed\"" : (val == "borderless" ? "\"Fullscreen (Borderless)\"" : "\"Fullscreen\""));
                SaveCfg();
            }
            else if (key == "s_lod")
            {
                foreach (string lp in LodPatches) PatchSet(lp, false);
                if (val == "-2") PatchSet("Model LOD -2 (Highest)", true);
                if (val == "1") PatchSet("Model LOD 1 (Lower)", true);
                if (val == "2") PatchSet("Model LOD 2 (Lowest)", true);
                SaveXml();
            }
            else if (key.StartsWith("p:") || key.StartsWith("p!"))
            {
                bool effectOn = val == "1";
                PatchSet(key.Substring(2), key.StartsWith("p:") ? effectOn : !effectOn);
                SaveXml();
            }
            else if (key.StartsWith("c:"))
            {
                CfgSet(key.Substring(2), val == "1" ? "true" : "false");
                SaveCfg();
            }
            Invalidate();
        }

        string GetGraphic(string key, bool json)
        {
            if (gtab == "seamless") return SeamlessGet(key);
            if (json) return key == "fps_mode" ? fpsMode : (fullscreen ? "1" : "0");
            return IniGet(key, "");
        }

        void SetGraphic(string key, bool json, string val)
        {
            if (gtab == "seamless") { SeamlessSet(key, val); return; }
            if (json)
            {
                if (key == "fps_mode") { if (JsonSet("fps_mode", "\"" + val + "\"")) fpsMode = val; }
            }
            else IniSet(key, val);
            Invalidate();
        }

        void FlipToggle(Tog t)
        {
            if (gtab == "seamless") { SeamlessSet(t.Key, SeamlessGet(t.Key) == "1" ? "0" : "1"); return; }
            if (t.Json)
            {
                if (JsonSet("fullscreen", fullscreen ? "false" : "true")) fullscreen = !fullscreen;
                Invalidate();
                return;
            }
            IniSet(t.Key, IniGet(t.Key, "0") == "1" ? "0" : "1");
        }

        void IniSetRaw(string k, string v) { IniSet(k, v); }

        void ChangeFov(int d)
        {
            int v;
            if (!int.TryParse(IniGet("fov", "43"), out v)) v = 43;
            v = Math.Max(30, Math.Min(90, v + d));
            IniSet("fov", v.ToString());
        }

        void IniSet(string k, string v)
        {
            if (!Directory.Exists(Path.GetDirectoryName(IniPath))) return; // BBport is not installed
            try
            {
                List<string> lines = new List<string>(File.Exists(IniPath) ? File.ReadAllLines(IniPath) : new string[0]);
                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                    if (lines[i].Trim().StartsWith(k + "=")) { lines[i] = k + "=" + v; found = true; break; }
                if (!found) lines.Add(k + "=" + v);
                File.WriteAllLines(IniPath, lines.ToArray(), new UTF8Encoding(false));
                ini[k] = v;
            }
            catch (Exception ex) { Fail(ex); }
            Invalidate();
        }

        // Replaces one value in BBport's launcher settings.json, keeping the rest of the file as it is.
        bool JsonSet(string key, string jsonValue)
        {
            try
            {
                string txt = File.ReadAllText(JsonPath);
                Regex re = new Regex("(\"" + key + "\"\\s*:\\s*)(\"[^\"]*\"|true|false|-?\\d+)");
                if (!re.IsMatch(txt)) return false;
                File.WriteAllText(JsonPath, re.Replace(txt, "${1}" + jsonValue, 1), new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex) { if (InvokeRequired) BeginInvoke((MethodInvoker)delegate { Fail(ex); }); else Fail(ex); return false; }
        }

        // Page frame shared by graphics and controls: panel, title with the mode it applies to, two tabs.
        int scrollY, maxScroll, contentBottom, lastContentBottom;
        string lastPageId = "";

        // Lowest y a page draws at (logical, before scrolling).
        void UseHeight(int bottom) { if (bottom > contentBottom) contentBottom = bottom; }

        void DrawTabbedPanel(Graphics g, string title, string tab, Hot tabN, Hot tabS, string note)
        {
            int ph = Math.Max(H - 100, lastContentBottom + 30 - 70);
            Rectangle panel = new Rectangle(40, 70, W - 140, ph);
            UseHeight(H - 60);
            FillRound(g, panel, 16, Color.FromArgb(205, 8, 6, 10), Color.FromArgb(150, Theme.GoldDim), 1.4f);
            string who = tab == "seamless" ? I18n.T("خاص بـ Seamless Co-op") : I18n.T("خاص بـ BBport");
            using (Font f = new Font("Segoe UI Semibold", 19f, FontStyle.Bold))
                Txt(g, title, f, new Rectangle(panel.X + 460, panel.Y + 12, panel.Width - 500, 40), Theme.GoldLight, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
            using (Font f = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold))
                Txt(g, who + (tab == "seamless" ? "  (shadPS4)" : ""), f,
                    new Rectangle(panel.X + 460, panel.Y + 46, panel.Width - 500, 20), Theme.Gold, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
            Segment(g, tabN, "BBport", tab == "normal");
            Segment(g, tabS, "Seamless Co-op", tab == "seamless");
            using (Pen p = new Pen(Color.FromArgb(110, Theme.Gold))) g.DrawLine(p, panel.X + 30, panel.Y + 72, panel.Right - 30, panel.Y + 72);
            using (Font f = new Font("Segoe UI", 9.5f))
                Txt(g, note, f, new Rectangle(panel.X + 30, panel.Bottom - 38, panel.Width - 60, 26), Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
        }

        // Graphics and controls of a mode that is not installed yet: a note instead of the settings.
        bool ModeMissing(bool seam) { return seam ? !File.Exists(cfg.SeamlessEmu) : !File.Exists(cfg.BBportExe); }

        bool PageLocked()
        {
            if (page == "graphics") return ModeMissing(gtab == "seamless");
            if (page == "controls") return ModeMissing(ctab == "seamless");
            return false;
        }

        bool DrawNotInstalled(Graphics g, bool seam)
        {
            if (!ModeMissing(seam)) return false;
            using (Font f = new Font("Segoe UI Semibold", 13f, FontStyle.Bold))
                Txt(g, seam ? I18n.T("Seamless Co-op مو محمّل للحين - حمّله من الصفحة الرئيسية بزر «تحميل Seamless Co-op»") : I18n.T("BBport مو محمّل للحين - حمّله من الصفحة الرئيسية بزر «تحميل BBport»"), f, new Rectangle(80, 300, W - 220, 40), Theme.GoldLight, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
            return true;
        }

        void DrawGraphics(Graphics g)
        {
            bool seam = gtab == "seamless";
            DrawTabbedPanel(g, I18n.T("الرسوميات"), gtab, hGTabN, hGTabS, seam
                ? I18n.T("تنحفظ في إعدادات shadPS4 وتتطبّق من التشغيل الجاي في وضع Seamless · خلّ الفريمات 60 للكل في الكوب")
                : I18n.T("تنحفظ في BBport وتتطبّق من التشغيل الجاي في وضع BBport · DLSS يحتاج NVIDIA RTX · سكّر لانشر BBport قبل التغيير"));
            if (DrawNotInstalled(g, seam)) return;
            Segment(g, hAutoGfx, I18n.T("⚙  تلقائي حسب جهازك"), false);
            if (!seam) { DrawNormalGraphics(g); return; }
            using (Pen p = new Pen(Color.FromArgb(110, Theme.Gold))) g.DrawLine(p, 615, 160, 615, H - 80);

            if (seam && sXml == "")
            {
                using (Font f = new Font("Segoe UI Semibold", 12f, FontStyle.Bold))
                    Txt(g, I18n.T("ما لقيت ملف باتشات shadPS4 - شغّل Seamless مرة وحدة أول"), f, new Rectangle(80, 300, W - 220, 40), Theme.GoldLight, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
                return;
            }

            SegRow[] rows = seam ? segS : segN;
            Tog[] tg = seam ? togS : togN;
            List<Hot>[] seg = segHotsBy[gtab];
            Hot[] th = togHotsBy[gtab];

            for (int i = 0; i < rows.Length; i++)
            {
                SegRow row = rows[i];
                string cur = GetGraphic(row.Key, row.Json);
                using (Font f = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold))
                    Txt(g, row.Title, f, new Rectangle(640, GRowY(i), 510, 28), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                for (int j = 0; j < row.Values.Length; j++)
                    Segment(g, seg[i][j], row.Labels[j], row.Values[j] == cur);
            }

            if (!seam)
            {
                int fi = rows.Length;
                using (Font f = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold))
                    Txt(g, I18n.T("زاوية الرؤية (FOV)"), f, new Rectangle(640, GRowY(fi), 510, 28), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                Segment(g, hFovMinus, "−", false);
                Segment(g, hFovPlus, "+", false);
                Segment(g, hFovReset, I18n.T("افتراضي (43)"), IniGet("fov", "43") == "43");
                using (Font f = new Font("Segoe UI Semibold", 14f, FontStyle.Bold))
                    Txt(g, IniGet("fov", "43") + "°", f, new Rectangle(692, hFovMinus.R.Y, 184, 36), Theme.GoldLight, TextFormatFlags.HorizontalCenter);
            }

            using (Font f = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold))
                Txt(g, I18n.T("الشاشة والمؤثرات"), f, new Rectangle(100, 146, 490, 28), Theme.Gold, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
            for (int k = 0; k < tg.Length; k++)
            {
                bool on = GetGraphic(tg[k].Key, tg[k].Json) == "1";
                Switch(g, th[k], tg[k].Title, on);
            }
        }

        // ---------- graphics, normal play: every BBport option, one scrolling column ----------
        // Kind: H header, D dropdown, T toggle, S stepper. Json = BBport's launcher settings.json, else bbport.ini.
        class GOpt
        {
            public char Kind; public string Title, Key; public bool Json; public string[] Labels, Values;
            public double Min, Max, Step; public string Fmt, Def;
            public Hot Box; public Hot[] Items; public Hot Minus, Plus, Reset; public int Y;
            public GOpt(char k, string t) { Kind = k; Title = t; }
        }
        static GOpt GH(string t) { return new GOpt('H', t); }
        static GOpt GD(string t, string k, bool j, string[] l, string[] v) { GOpt o = new GOpt('D', t); o.Key = k; o.Json = j; o.Labels = l; o.Values = v; return o; }
        static GOpt GT(string t, string k, bool j) { GOpt o = new GOpt('T', t); o.Key = k; o.Json = j; return o; }
        static GOpt GS(string t, string k, double min, double max, double step, string fmt, string def) { GOpt o = new GOpt('S', t); o.Key = k; o.Min = min; o.Max = max; o.Step = step; o.Fmt = fmt; o.Def = def; return o; }

        GOpt[] gN = new GOpt[] {
            GH(I18n.T("التكبير والجودة")),
            GD(I18n.T("أداة التكبير"), "upscaler", false,
                new string[] { "DLSS (NVIDIA GeForce RTX)", I18n.T("FSR 4.1.1 (تجريبي)"), "FSR 4", "FSR 3.1", "TAA", I18n.T("بدون") },
                new string[] { "dlss", "fsr411", "fsr4", "fsr3", "taa", "off" }),
            GD(I18n.T("إعداد الجودة"), "preset", false,
                new string[] { "Native AA (×1.0)", "Quality (×1.5)", "Balanced (×1.7)", "Performance (×2.0)", "Ultra Performance (×3.0)" },
                new string[] { "0", "1", "2", "3", "4" }),
            GD(I18n.T("دقة الإخراج"), "output_res", false,
                new string[] { "1280 × 720", "1600 × 900", "1920 × 1080", "2560 × 1080 (21:9)", "2560 × 1440", "3440 × 1440 (21:9)", "3840 × 2160 (4K)", "5120 × 1440 (32:9)", "5120 × 2160 (21:9)" },
                new string[] { "1280x720", "1600x900", "1920x1080", "2560x1080", "2560x1440", "3440x1440", "3840x2160", "5120x1440", "5120x2160" }),
            GD(I18n.T("طريقة FSR 4.1.1"), "fsr411_backend", false,
                new string[] { "Vulkan (fsr4vk)", "AMD DX12" }, new string[] { "vk", "dx12" }),
            GD(I18n.T("موضع الواجهة (الشاشات العريضة)"), "hud_position", false,
                new string[] { I18n.T("حواف الشاشة"), I18n.T("بين الاثنين"), I18n.T("الوسط (16:9)") }, new string[] { "100", "50", "0" }),
            GD(I18n.T("تغيير الدقة أثناء اللعب"), "live_resolution", false,
                new string[] { I18n.T("تلقائي (حسب البطاقة)"), I18n.T("مباشر داخل اللعبة"), I18n.T("عند التشغيل فقط") }, new string[] { "auto", "1", "0" }),
            GT(I18n.T("الحدّة (RCAS)"), "sharpen", false),
            GS(I18n.T("قوة الحدّة"), "sharpness", 0, 1, 0.05, "0.00", "0.50"),
            GT(I18n.T("متجهات حركة الأجسام (أوضح للأجسام المتحركة)"), "object_motion", false),
            GT(I18n.T("ذاكرة متجهات الحركة المؤقتة (تجريبي)"), "object_motion_cache", false),

            GH(I18n.T("المؤثرات")),
            GT(I18n.T("الإضاءة المحيطة (SSAO)"), "effect_ssao", false),
            GT(I18n.T("التنعيم الخاص باللعبة (AA)"), "effect_game_aa", false),
            GT(I18n.T("ظلال الأضواء المتحركة"), "effect_dynamic_shadows", false),
            GT(I18n.T("انعكاسات الشاشة (SSR)"), "effect_ssr", false),
            GT(I18n.T("عمق المجال (DoF)"), "effect_dof", false),
            GT(I18n.T("ضبابية الحركة"), "effect_motion_blur", false),
            GT(I18n.T("تشوّه الألوان"), "effect_chromatic_aberration", false),
            GD(I18n.T("تفاصيل المجسّمات (LOD)"), "model_lod", false,
                new string[] { I18n.T("الأعلى (-2)"), I18n.T("عادي (0)"), I18n.T("منخفض (1)"), I18n.T("الأقل (2)") }, new string[] { "-2", "0", "1", "2" }),
            GS(I18n.T("زاوية الرؤية (FOV)"), "fov", 30, 90, 1, "0", "43"),

            GH(I18n.T("معدل الإطارات")),
            GD(I18n.T("معدل الإطارات"), "fps_mode", true,
                new string[] { "30 FPS", "60 FPS", "90 FPS", I18n.T("بلا حد") }, new string[] { "30", "60", "90", "uncap" }),
            GD(I18n.T("حد الإطارات (وضع بلا حد)"), "frame_cap", true,
                new string[] { I18n.T("تلقائي (حد الشاشة، أقصى 120)"), "60", "90", "120", "144", "165", "240" },
                new string[] { "", "60", "90", "120", "144", "165", "240" }),
            GD(I18n.T("إطارات مسبقة لبطاقة الرسوميات"), "frames_ahead", true,
                new string[] { I18n.T("1 (افتراضي)"), "2", "3" }, new string[] { "", "2", "3" }),
            GD(I18n.T("طريقة العرض"), "present_mode", true,
                new string[] { I18n.T("Mailbox (سريع، بلا تمزّق)"), "Fifo (V-Sync)", "FifoRelaxed", I18n.T("Immediate (أقل تأخير، فيه تمزّق)") },
                new string[] { "Mailbox", "Fifo", "FifoRelaxed", "Immediate" }),

            GH(I18n.T("توليد الإطارات")),
            GD(I18n.T("توليد الإطارات"), "frame_gen", false,
                new string[] { I18n.T("إيقاف"), I18n.T("تلقائي"), I18n.T("DLSS (RTX 40 وأحدث)"), "FSR 3.1", "AMD ML-FG 4.0.1 (RX 9000)" },
                new string[] { "off", "auto", "dlss", "fsr3", "fsr401" }),
            GD(I18n.T("المضاعف"), "frame_gen_multiplier", false,
                new string[] { I18n.T("2× (إطار مولّد واحد)"), "3×", "4× (RTX 50)" }, new string[] { "2", "3", "4" }),
            GT(I18n.T("خلّ المضاعف يناسب تردد الشاشة"), "frame_gen_fit_display", false),

            GH(I18n.T("الشاشة واللعبة")),
            GT(I18n.T("ملء الشاشة"), "fullscreen", true),
            GT("HDR", "hdr", true),
            GT(I18n.T("عداد الفريمات"), "show_fps", false),
            GT(I18n.T("تخطي المقدمة"), "skip_intro", false),
            GT(I18n.T("كتم الصوت لما تطلع من اللعبة"), "mute_unfocused", false),
            GT(I18n.T("خيار «الخروج لسطح المكتب» في القائمة"), "quit_to_desktop", false),
            GT(I18n.T("الولادة من جديد عند الدمية"), "rebirth", false) };

        int gDrop = -1; // index in gN of the open dropdown
        HashSet<Hot> repeatHots = new HashSet<Hot>(); // +/- buttons that repeat while held
        Dictionary<string, string> jsonVals = new Dictionary<string, string>();

        void BuildNormalGraphics()
        {
            int y = 150;
            for (int i = 0; i < gN.Length; i++)
            {
                GOpt o = gN[i]; int idx = i;
                if (o.Kind == 'H') { if (i > 0) y += 14; o.Y = y; y += 40; continue; }
                o.Y = y;
                if (o.Kind == 'D')
                {
                    o.Box = Add(new Rectangle(240, y + 2, 420, 34), delegate { gDrop = gDrop == idx ? -1 : idx; Invalidate(); }, null);
                    o.Box.Page = "graphics:normal";
                    o.Items = new Hot[o.Values.Length];
                    for (int j = 0; j < o.Values.Length; j++)
                    {
                        string v = o.Values[j];
                        o.Items[j] = new Hot(new Rectangle(240, y + 38 + j * 32, 420, 32), delegate { gDrop = -1; GSet(o, v); }, null);
                        o.Items[j].Page = "graphics:normal";
                    }
                }
                else if (o.Kind == 'T')
                {
                    o.Box = Add(new Rectangle(240, y, 910, 38), delegate { GSet(o, GGet(o) == "1" ? "0" : "1"); }, null);
                    o.Box.Page = "graphics:normal";
                }
                else if (o.Kind == 'S')
                {
                    if (o.Key == "fov")
                    {
                        hFovMinus = o.Minus = Add(new Rectangle(240, y + 2, 44, 34), delegate { GStep(o, -1); }, null);
                        hFovPlus = o.Plus = Add(new Rectangle(390, y + 2, 44, 34), delegate { GStep(o, 1); }, null);
                        hFovReset = o.Reset = Add(new Rectangle(442, y + 2, 140, 34), delegate { GSet(o, o.Def); }, null);
                    }
                    else
                    {
                        o.Minus = Add(new Rectangle(240, y + 2, 44, 34), delegate { GStep(o, -1); }, null);
                        o.Plus = Add(new Rectangle(390, y + 2, 44, 34), delegate { GStep(o, 1); }, null);
                        o.Reset = Add(new Rectangle(442, y + 2, 140, 34), delegate { GSet(o, o.Def); }, null);
                    }
                    o.Minus.Page = o.Plus.Page = o.Reset.Page = "graphics:normal";
                    repeatHots.Add(o.Minus); repeatHots.Add(o.Plus);
                }
                y += 44;
            }
        }

        string GGet(GOpt o)
        {
            if (!o.Json) return IniGet(o.Key, o.Kind == 'S' ? o.Def : (o.Kind == 'T' ? "0" : ""));
            string v;
            return jsonVals.TryGetValue(o.Key, out v) ? v : "";
        }

        void GSet(GOpt o, string v)
        {
            if (!o.Json) { IniSet(o.Key, v); return; }
            bool isBool = o.Kind == 'T';
            string jv = isBool ? (v == "1" ? "true" : "false") : "\"" + JsonEscape(v) + "\"";
            if (JsonSet(o.Key, jv)) jsonVals[o.Key] = v;
            else MessageBox.Show(this, I18n.T("ما قدرت أغيّر «") + o.Title + I18n.T("» - افتح لانشر BBport مرة وحدة وبعدها جرّب."), I18n.T("الرسوميات"),
                MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, I18n.MsgOpt);
            if (o.Key == "fps_mode") fpsMode = v;
            if (o.Key == "fullscreen") fullscreen = v == "1";
            Invalidate();
        }

        void GStep(GOpt o, int dir)
        {
            double v;
            if (!double.TryParse(GGet(o), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) v = double.Parse(o.Def, CultureInfo.InvariantCulture);
            v = Math.Round(Math.Max(o.Min, Math.Min(o.Max, v + dir * o.Step)) / o.Step) * o.Step;
            GSet(o, v.ToString(o.Fmt, CultureInfo.InvariantCulture));
        }

        void LoadJsonVals()
        {
            jsonVals.Clear();
            string txt = "";
            try { txt = File.ReadAllText(JsonPath); } catch { }
            foreach (GOpt o in gN)
            {
                if (!o.Json) continue;
                Match m = Regex.Match(txt, "\"" + o.Key + "\"\\s*:\\s*(\"(?:[^\"\\\\]|\\\\.)*\"|true|false|-?\\d+)");
                if (!m.Success) continue;
                string v = m.Groups[1].Value;
                if (v == "true") v = "1"; else if (v == "false") v = "0"; else v = Regex.Unescape(v.Trim('"'));
                jsonVals[o.Key] = v;
            }
        }

        void DrawNormalGraphics(Graphics g)
        {
            using (Font fh = new Font("Segoe UI Semibold", 13f, FontStyle.Bold))
            using (Font fl = new Font("Segoe UI Semibold", 11f, FontStyle.Bold))
            using (Font fc = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold))
            {
                foreach (GOpt o in gN)
                {
                    if (o.Kind == 'H')
                    {
                        Txt(g, o.Title, fh, new Rectangle(700, o.Y, 450, 30), Theme.Gold, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                        using (Pen p = new Pen(Color.FromArgb(90, Theme.GoldDim))) g.DrawLine(p, 90, o.Y + 16, 900, o.Y + 16);
                        continue;
                    }
                    if (o.Kind == 'T') { Switch(g, o.Box, o.Title, GGet(o) == "1"); continue; }
                    Txt(g, o.Title, fl, new Rectangle(700, o.Y + 4, 450, 30), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                    if (o.Kind == 'D')
                    {
                        string cur = GGet(o);
                        int ci = Array.IndexOf(o.Values, cur);
                        bool open = gDrop >= 0 && gN[gDrop] == o, hv = hover == o.Box || open;
                        FillRound(g, o.Box.R, 8, Color.FromArgb(200, 24, 22, 28), hv ? Theme.Gold : Color.FromArgb(150, Theme.GoldDim), hv ? 1.6f : 1.1f);
                        Txt(g, ci >= 0 ? o.Labels[ci] : (cur == "" ? "—" : cur), fc, new Rectangle(o.Box.R.X + 14, o.Box.R.Y, o.Box.R.Width - 50, o.Box.R.Height), Theme.Text, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                        Glyph(g, open ? "" : "", new Rectangle(o.Box.R.Right - 34, o.Box.R.Y, 26, o.Box.R.Height), Theme.Gold, 10f);
                    }
                    else if (o.Kind == 'S')
                    {
                        string cur = GGet(o);
                        Segment(g, o.Minus, "−", false);
                        Segment(g, o.Plus, "+", false);
                        Segment(g, o.Reset, I18n.T("افتراضي (") + o.Def + ")", cur == o.Def);
                        using (Font fv = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold))
                            Txt(g, cur + (o.Key == "fov" ? "°" : ""), fv, new Rectangle(o.Minus.R.Right, o.Minus.R.Y, o.Plus.R.X - o.Minus.R.Right, o.Minus.R.Height), Theme.GoldLight, TextFormatFlags.HorizontalCenter);
                    }
                }
                GOpt last = gN[gN.Length - 1];
                UseHeight(last.Y + 44 + 60);

                if (gDrop >= 0)
                {
                    GOpt o = gN[gDrop];
                    string cur = GGet(o);
                    Rectangle list = new Rectangle(o.Box.R.X, o.Box.R.Bottom + 2, o.Box.R.Width, o.Values.Length * 32);
                    FillRound(g, list, 8, Color.FromArgb(250, 22, 20, 26), Theme.GoldDim, 1.2f);
                    for (int i = 0; i < o.Items.Length; i++)
                    {
                        Hot it = o.Items[i];
                        bool sel = o.Values[i] == cur, hv = hover == it;
                        if (sel || hv)
                        {
                            Rectangle r = it.R; r.Inflate(-3, -2);
                            FillRound(g, r, 6, sel ? Color.FromArgb(230, Theme.Blood) : Color.FromArgb(120, Theme.BloodDark), Color.Empty, 0);
                        }
                        Txt(g, o.Labels[i], fc, new Rectangle(it.R.X + 14, it.R.Y, it.R.Width - 28, it.R.Height), sel || hv ? Color.White : Theme.Text, TextFormatFlags.Left);
                    }
                    UseHeight(list.Bottom + 40);
                }
            }
        }

        // ---------- automatic graphics from the PC's hardware ----------

        class HwInfo
        {
            public string Gpu = "?"; public double Vram, Ram; public int Sw, Sh, Tier; // tier 0 low, 1 mid, 2 high
            public bool Nvidia, Rtx, AmdRx9;
            public string TierName { get { return Tier == 2 ? I18n.T("قوي") : (Tier == 1 ? I18n.T("متوسط") : I18n.T("خفيف")); } }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MemStatus { public uint Len, Load; public ulong Total, Avail, PageTotal, PageAvail, VirtTotal, VirtAvail, VirtExt; }
        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MemStatus m);

        static HwInfo DetectHardware()
        {
            HwInfo hw = new HwInfo();
            try { MemStatus m = new MemStatus(); m.Len = (uint)Marshal.SizeOf(typeof(MemStatus)); if (GlobalMemoryStatusEx(ref m)) hw.Ram = m.Total / 1073741824.0; } catch { }
            try { Rectangle b = Screen.PrimaryScreen.Bounds; hw.Sw = b.Width; hw.Sh = b.Height; } catch { hw.Sw = 1920; hw.Sh = 1080; }
            // display adapters in the registry: name and memory, the strongest one wins
            try
            {
                using (Microsoft.Win32.RegistryKey cls = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                {
                    if (cls != null)
                        foreach (string sub in cls.GetSubKeyNames())
                        {
                            if (sub.Length != 4) continue;
                            try
                            {
                                using (Microsoft.Win32.RegistryKey k = cls.OpenSubKey(sub))
                                {
                                    string name = k.GetValue("DriverDesc") as string;
                                    if (string.IsNullOrEmpty(name) || name.Contains("Basic") || name.Contains("Virtual") || name.Contains("Parsec") || name.Contains("Remote")) continue;
                                    double vram = 0;
                                    object q = k.GetValue("HardwareInformation.qwMemorySize");
                                    if (q is long) vram = (long)q / 1073741824.0;
                                    else if (q is byte[] && ((byte[])q).Length >= 8) vram = BitConverter.ToInt64((byte[])q, 0) / 1073741824.0;
                                    else { object d = k.GetValue("HardwareInformation.MemorySize"); if (d is int) vram = (uint)(int)d / 1073741824.0; else if (d is byte[] && ((byte[])d).Length >= 4) vram = BitConverter.ToUInt32((byte[])d, 0) / 1073741824.0; }
                                    int t = GpuTier(name, vram);
                                    if (hw.Gpu == "?" || t > hw.Tier || (t == hw.Tier && vram > hw.Vram)) { hw.Gpu = name; hw.Vram = vram; hw.Tier = t; }
                                }
                            }
                            catch { }
                        }
                }
            }
            catch { }
            string n = hw.Gpu.ToUpperInvariant();
            hw.Nvidia = n.Contains("NVIDIA") || n.Contains("GEFORCE");
            hw.Rtx = n.Contains("RTX");
            hw.AmdRx9 = Regex.IsMatch(n, @"RX\s*9\d{3}");
            if (hw.Ram > 0 && hw.Ram < 12 && hw.Tier > 0) hw.Tier--; // little memory: one step down
            return hw;
        }

        static int GpuTier(string name, double vram)
        {
            string n = name.ToUpperInvariant();
            if (Regex.IsMatch(n, @"RTX\s*(30[6-9]0|40[6-9]0|50[6-9]0|A[2-6]000|[2-9]000\s*ADA)") || Regex.IsMatch(n, @"RX\s*(6[789]\d0|7[6-9]\d0|9\d{3})") || Regex.IsMatch(n, @"ARC\s*B")) return 2;
            if (Regex.IsMatch(n, @"RTX\s*(20\d0|3050|4050|5050)|GTX\s*(16\d0|10[78]0)|RX\s*(5[67]\d0|6[56]\d0|7600|VEGA)|ARC\s*A[57]")) return 1;
            if (vram >= 10) return 2;
            if (vram >= 6) return 1;
            return 0;
        }

        static string PickRes(HwInfo hw, bool normal)
        {
            bool wide = hw.Sh > 0 && hw.Sw * 9 > hw.Sh * 17; // wider than 16:9
            if (hw.Tier == 2)
            {
                if (hw.Sw >= 3840 && !wide) return "3840x2160";
                if (wide && hw.Sw >= 3440) return "3440x1440";
                if (normal && wide && hw.Sw >= 2560) return "2560x1080";
                if (hw.Sw >= 2560) return "2560x1440";
                return "1920x1080";
            }
            if (hw.Tier == 1)
            {
                if (normal && wide && hw.Sw >= 3440) return "3440x1440"; // BBport upscales, so the panel's own size
                if (hw.Sw >= 2560 && !wide) return "2560x1440";
                return "1920x1080";
            }
            return "1920x1080";
        }

        // Writes graphics settings that suit the hardware. Seamless: shadPS4 patches; normal: bbport.ini.
        string ApplyAutoGraphics(bool seamless, HwInfo hw)
        {
            LoadGraphics();
            string res = PickRes(hw, !seamless);
            string lod = hw.Tier == 2 ? "-2" : (hw.Tier == 1 ? "0" : "1");
            bool heavy = hw.Tier == 0;
            if (seamless)
            {
                if (sXml == "") return null;
                SeamlessSet("s_res", res);
                SeamlessSet("s_fps", "60");
                SeamlessSet("s_lod", lod);
                SeamlessSet("p!Disable SSAO", heavy ? "0" : "1");
                SeamlessSet("p!Disable Dynamic Light Shadows (perf increase)", heavy ? "0" : "1");
                SeamlessSet("p!Disable Motion Blur (perf increase)", "0");
                SeamlessSet("p!Disable DoF", heavy ? "0" : "1");
                SeamlessSet("c:rcas_enabled", "1");
                return res.Replace("x", "×") + I18n.T(" · 60 FPS · تفاصيل ") + (hw.Tier == 2 ? I18n.T("الأعلى") : (hw.Tier == 1 ? I18n.T("عادي") : I18n.T("منخفض")));
            }
            string up = hw.Rtx ? "dlss" : (hw.AmdRx9 ? "fsr4" : "fsr3");
            string preset = hw.Tier == 2 ? "0" : (hw.Tier == 1 ? "1" : "3");
            IniSet("upscaler", up);
            IniSet("preset", preset);
            IniSet("output_res", res);
            IniSet("model_lod", lod);
            IniSet("sharpen", "1");
            IniSet("effect_ssao", heavy ? "0" : "1");
            IniSet("effect_dynamic_shadows", heavy ? "0" : "1");
            IniSet("effect_motion_blur", "0");
            IniSet("effect_dof", heavy ? "0" : "1");
            IniSet("frame_gen", "off");
            if (JsonSet("fps_mode", "\"60\"")) fpsMode = "60";
            LoadJsonVals();
            return res.Replace("x", "×") + " · 60 FPS · " + (up == "dlss" ? "DLSS" : (up == "fsr4" ? "FSR 4" : "FSR 3.1")) + " " +
                   new string[] { "Native AA", "Quality", "Balanced", "Performance" }[int.Parse(preset)];
        }

        void AutoGraphicsButton()
        {
            bool seam = gtab == "seamless";
            if (ModeMissing(seam)) return;
            HwInfo hw = DetectHardware();
            string info = I18n.T("كرت الشاشة: ") + hw.Gpu + (hw.Vram > 0 ? " (" + Math.Round(hw.Vram) + " GB)" : "") +
                          I18n.T("\nالرام: ") + Math.Round(hw.Ram) + " GB" + I18n.T("\nالشاشة: ") + hw.Sw + "×" + hw.Sh +
                          I18n.T("\n\nجهازك: ") + hw.TierName;
            if (MessageBox.Show(this, info + I18n.T("\n\nأضبط رسوميات ") + (seam ? "Seamless Co-op" : "BBport") + I18n.T(" على حسب جهازك؟\n(بتتغيّر الإعدادات الحالية)"),
                    I18n.T("إعدادات تلقائية"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1,
                    I18n.MsgOpt) != DialogResult.Yes) return;
            string done = ApplyAutoGraphics(seam, hw);
            if (done == null) { Missing(SXmlPath); return; }
            if (seam) { cfg.AutoGfx = true; cfg.Save(); }
            MessageBox.Show(this, I18n.T("تم ✔\n\n") + done, I18n.T("إعدادات تلقائية"), MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, I18n.MsgOpt);
            Invalidate();
        }

        void Switch(Graphics g, Hot h, string title, bool on)
        {
            if (IsDown(h)) FillRound(g, h.R, 8, Color.FromArgb(150, Theme.Blood), Theme.Gold, 1.2f);
            else if (hover == h) FillRound(g, h.R, 8, Color.FromArgb(70, Theme.BloodDark), Color.Empty, 0);
            using (Font f = new Font("Segoe UI", 11f))
                Txt(g, title, f, new Rectangle(h.R.X + 80, h.R.Y, h.R.Width - 92, h.R.Height), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
            Rectangle sw = new Rectangle(h.R.X + 12, h.R.Y + (h.R.Height - 24) / 2, 52, 24);
            FillRound(g, sw, 12, on ? Theme.Blood : Color.FromArgb(120, 90, 90, 95), on ? Theme.Gold : Color.FromArgb(120, 160, 160, 160), 1f);
            int kx = on ? sw.Right - 21 : sw.X + 3;
            using (SolidBrush b = new SolidBrush(on ? Theme.GoldLight : Color.FromArgb(220, 220, 220))) g.FillEllipse(b, kx, sw.Y + 3, 18, 18);
        }

        void Segment(Graphics g, Hot h, string text, bool on)
        {
            bool hv = hover == h;
            if (IsDown(h))
            {
                Rectangle r = h.R;
                r.Inflate(-2, -2);
                FillRound(g, r, 7, Color.FromArgb(255, 160, 40, 70), Theme.GoldLight, 2.2f);
                using (Font f = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold))
                    Txt(g, text, f, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
                return;
            }
            FillRound(g, h.R, 7,
                on ? Color.FromArgb(230, Theme.Blood) : Color.FromArgb(hv ? 190 : 140, 20, 16, 20),
                on ? Theme.Gold : Color.FromArgb(hv ? 210 : 120, Theme.GoldDim), on ? 1.8f : 1.1f);
            using (Font f = new Font("Segoe UI Semibold", 10f, FontStyle.Bold))
                Txt(g, text, f, h.R, on || hv ? Theme.GoldLight : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
        }

        // ---------- controls page: keyboard and mouse bindings (shadPS4 input syntax, both modes) ----------

        static readonly string[] ActKeys = {
            "cross", "circle", "square", "triangle", "r1", "r2", "l1", "l2", "l3", "r3", "options", "touchpad_left", "touchpad_right",
            "axis_left_y_minus", "axis_left_y_plus", "axis_left_x_minus", "axis_left_x_plus",
            "axis_right_y_minus", "axis_right_y_plus", "axis_right_x_minus", "axis_right_x_plus",
            "pad_up", "pad_down", "pad_left", "pad_right", "touchpad_center" };
        static readonly string[] ActNames = {
            I18n.T("✕  تفاعل"), I18n.T("○  تفادي / ركض"), I18n.T("□  استخدام الغرض"), I18n.T("△  قارورة الدم"), I18n.T("R1  هجوم خفيف"), I18n.T("R2  هجوم قوي"),
            I18n.T("L1  تحويل السلاح"), I18n.T("L2  السلاح الناري"), I18n.T("L3  ضغط العصا اليسرى"), I18n.T("R3  تثبيت الهدف"), I18n.T("OPTIONS  القائمة"),
            I18n.T("اللمس (يسار)  الإيماءات"), I18n.T("اللمس (يمين)"),
            I18n.T("حركة: أمام"), I18n.T("حركة: خلف"), I18n.T("حركة: يسار"), I18n.T("حركة: يمين"),
            I18n.T("كاميرا: فوق"), I18n.T("كاميرا: تحت"), I18n.T("كاميرا: يسار"), I18n.T("كاميرا: يمين"),
            I18n.T("↑  أعلى"), I18n.T("↓  أسفل"), I18n.T("←  يسار"), I18n.T("→  يمين"), I18n.T("اللمس (وسط)") };
        static readonly HashSet<string> PadInputs = new HashSet<string>(new string[] {
            "cross", "circle", "square", "triangle", "l1", "r1", "l2", "r2", "l3", "r3", "options", "back",
            "pad_up", "pad_down", "pad_left", "pad_right", "axis_left_x", "axis_left_y", "axis_right_x", "axis_right_y",
            "lpaddle_high", "lpaddle_low", "rpaddle_high", "rpaddle_low", "touchpad", "touchpad_left", "touchpad_center", "touchpad_right" });

        Hot hPadN, hKbN, hPadS, hKbS;
        Hot[] promptHots;
        Hot hPromptBox;
        bool promptOpen;
        static readonly string[] PromptVals = { "keyboard", "playstation", "xbox" };
        static readonly string[] PromptNames = { I18n.T("لوحة المفاتيح والماوس"), I18n.T("يد تحكم PlayStation"), I18n.T("يد تحكم Xbox") };

        // "Play with" dropdown: keyboard & mouse / PlayStation pad / Xbox pad.
        static string PadPrefPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "seamless-pad.txt"); } }
        string GetPlayWith()
        {
            if (GetDevice() == "keyboard") return "keyboard";
            string pad = "playstation";
            try
            {
                if (ctab == "normal")
                {
                    foreach (string raw in File.ReadAllLines(IniPath)) { string l = raw.Trim(); if (l.StartsWith("button_prompts=")) pad = l.Substring(15).Trim(); }
                }
                else if (File.Exists(PadPrefPath)) pad = File.ReadAllText(PadPrefPath).Trim();
            }
            catch { }
            return pad == "xbox" ? "xbox" : "playstation";
        }
        void SetPlayWith(string v)
        {
            SetDevice(v == "keyboard" ? "keyboard" : "controller");
            if (ctab == "normal") IniSetRaw("button_prompts", v);
            else if (v != "keyboard") { try { File.WriteAllText(PadPrefPath, v); } catch { } }
            Invalidate();
        }
        Hot hCTabN, hCTabS, hQuitSet, hQuitReset, hCtlReset, hSensMinus, hSensPlus, hSensReset;
        const string QuitCap = "__quit__";
        const string QuitDefault = "lctrl, lshift, end";
        Dictionary<string, Hot[]> addHotsBy = new Dictionary<string, Hot[]>();
        List<Hot> chipHots = new List<Hot>();
        string capturing; // action waiting for a key, or null
        bool swallowMouseUp;

        static int CRowY(int i) { return 148 + (i % 13) * 33; }
        static int CColX(int i) { return i < 13 ? 640 : 90; }

        string InputPath(string tab)
        {
            if (tab == "normal") return Path.Combine(Path.GetDirectoryName(cfg.BBportExe), "input.ini");
            string dir = Path.Combine(ShadDir, "input_config");
            string game = Path.Combine(dir, "CUSA03173.ini");
            string unified = null;
            try { Match m = Regex.Match(File.ReadAllText(Path.Combine(ShadDir, "config.json")), "\"use_unified_input_config\"\\s*:\\s*(true|false)"); if (m.Success) unified = m.Groups[1].Value; } catch { }
            if (File.Exists(game) && unified == "false") return game;
            return Path.Combine(dir, "default.ini");
        }


        // shadPS4's quit shortcut (Seamless): hotkey_quit in input_config\global.ini.
        static string GlobalIniPath { get { return Path.Combine(ShadDir, @"input_config\global.ini"); } }

        static string GetQuit()
        {
            try
            {
                foreach (string raw in File.ReadAllLines(GlobalIniPath))
                {
                    string l = raw.Trim();
                    int eq = l.IndexOf('=');
                    if (!l.StartsWith("#") && eq > 0 && l.Substring(0, eq).Trim() == "hotkey_quit") return l.Substring(eq + 1).Trim();
                }
            }
            catch { }
            return QuitDefault;
        }

        void SetQuit(string combo)
        {
            try
            {
                List<string> lines = new List<string>(File.Exists(GlobalIniPath) ? File.ReadAllLines(GlobalIniPath) : new string[0]);
                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    string l = lines[i].Trim();
                    int eq = l.IndexOf('=');
                    if (!l.StartsWith("#") && eq > 0 && l.Substring(0, eq).Trim() == "hotkey_quit")
                    {
                        if (found) { lines.RemoveAt(i); i--; continue; }
                        lines[i] = "hotkey_quit = " + combo; found = true;
                    }
                }
                if (!found) lines.Add("hotkey_quit = " + combo);
                File.WriteAllLines(GlobalIniPath, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (Exception ex) { Fail(ex); }
            Invalidate();
        }

        static string PrettyCombo(string c)
        {
            string[] parts = c.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p == "lctrl" || p == "rctrl") p = "Ctrl";
                else if (p == "lshift" || p == "rshift") p = "Shift";
                else if (p == "lalt" || p == "ralt") p = "Alt";
                else if (p.Length > 0) p = char.ToUpperInvariant(p[0]) + p.Substring(1);
                parts[i] = p;
            }
            return string.Join(" + ", parts);
        }

        // All keyboard/mouse bindings (not controller ones) of an action in a bindings file.
        static List<string> BindingsOf(string[] lines, string act)
        {
            List<string> r = new List<string>();
            foreach (string raw in lines)
            {
                string l = raw.Trim();
                if (l.StartsWith("#")) continue;
                int eq = l.IndexOf('=');
                if (eq < 1 || l.Substring(0, eq).Trim() != act) continue;
                string v = l.Substring(eq + 1).Trim();
                if (!PadInputs.Contains(v)) r.Add(v);
            }
            return r;
        }

        string[] ReadInput(string tab)
        {
            try { return File.ReadAllLines(InputPath(tab)); } catch { return new string[0]; }
        }

        void AddBinding(string act, string key)
        {
            string path = InputPath(ctab);
            try
            {
                List<string> lines = new List<string>(File.Exists(path) ? File.ReadAllLines(path) : new string[0]);
                foreach (string b in BindingsOf(lines.ToArray(), act)) if (b == key) return;
                lines.Add(act + " = " + key);
                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (Exception ex) { Fail(ex); }
        }

        void RemoveBinding(string act, string key)
        {
            string path = InputPath(ctab);
            try
            {
                List<string> lines = new List<string>(File.ReadAllLines(path));
                for (int i = 0; i < lines.Count; i++)
                {
                    string l = lines[i].Trim();
                    int eq = l.IndexOf('=');
                    if (l.StartsWith("#") || eq < 1) continue;
                    if (l.Substring(0, eq).Trim() == act && l.Substring(eq + 1).Trim() == key) { lines.RemoveAt(i); break; }
                }
                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (Exception ex) { Fail(ex); }
            Invalidate();
        }

        string DefaultInput(string tab)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"defaults\" + (tab == "seamless" ? "seamless" : "normal") + "-input.ini");
        }

        // "Play with": controller or keyboard and mouse. Both always work; this switches the mouse camera
        // (BBport: mouse_camera direct/off + mouse_to_joystick, plus play_with in its launcher settings;
        //  shadPS4: a mouse_to_joystick = right line).
        string GetDevice()
        {
            bool mouse = false;
            foreach (string raw in ReadInput(ctab))
            {
                string l = raw.Trim();
                int eq = l.IndexOf('=');
                if (l.StartsWith("#") || eq < 1) continue;
                string k = l.Substring(0, eq).Trim(), v = l.Substring(eq + 1).Trim();
                if (k == "mouse_to_joystick" && v != "none" && v != "off") mouse = true;
                if (ctab == "normal" && k == "mouse_camera" && v == "off") return "controller";
            }
            return mouse ? "keyboard" : "controller";
        }

        // Dark Souls III keyboard and mouse layout (the one BBport uses), for shadPS4 too.
        static readonly string[] Ds3Layout = {
            "# Keyboard and mouse: Dark Souls III layout (Bloodborne Launcher)",
            "axis_left_x_minus = a", "axis_left_x_plus = d", "axis_left_y_minus = w", "axis_left_y_plus = s",
            "leftjoystick_halfmode = lalt",
            "axis_right_x_minus = j", "axis_right_x_plus = l", "axis_right_y_minus = i", "axis_right_y_plus = k",
            "mouse_to_joystick = right", "mouse_movement_params = 0.2, 2.5, 0.125",
            "circle = space", "l3 = c", "r3 = q", "r3 = middlebutton",
            "r1 = leftbutton", "r2 = lshift, leftbutton", "l1 = rightbutton", "l2 = lshift, rightbutton", "l2 = lctrl",
            "cross = e", "cross = enter", "square = r", "triangle = f",
            "pad_up = up", "pad_up = mousewheelup", "pad_down = down", "pad_down = mousewheeldown",
            "pad_left = left", "pad_left = lshift, mousewheelup", "pad_right = right", "pad_right = lshift, mousewheeldown",
            "options = tab", "options = escape", "touchpad_left = g", "touchpad_right = backspace" };

        // Keeps controller bindings and other settings, replaces every keyboard/mouse binding with the DS3 layout.
        static List<string> WithDs3(IEnumerable<string> lines)
        {
            List<string> outl = new List<string>();
            foreach (string raw in lines)
            {
                string t = raw.Trim();
                if (t.StartsWith("#") || t == "") { if (!t.Contains("Dark Souls III") && !t.Contains("Keyboard bindings") && !t.Contains("without a keypad")) outl.Add(raw); continue; }
                int eq = t.IndexOf('=');
                if (eq < 1) { outl.Add(raw); continue; }
                string k = t.Substring(0, eq).Trim(), v = t.Substring(eq + 1).Trim();
                if (k == "mouse_movement_params") continue; // the layout brings its own
                bool binding = Array.IndexOf(ActKeys, k) >= 0 || k == "leftjoystick_halfmode" || k == "rightjoystick_halfmode" || k == "mouse_to_joystick";
                if (binding && !PadInputs.Contains(v) && !v.StartsWith("axis_")) continue; // a keyboard/mouse binding: dropped
                outl.Add(raw);
            }
            outl.Add("");
            outl.AddRange(Ds3Layout);
            return outl;
        }

        void SetDevice(string dev)
        {
            string path = InputPath(ctab);
            bool kb = dev == "keyboard";
            if (kb && ctab == "seamless")
            {
                try
                {
                    string[] cur = File.Exists(path) ? File.ReadAllLines(path) : new string[0];
                    if (File.Exists(path)) File.Copy(path, path + ".bak-before-ds3", true);
                    File.WriteAllLines(path, WithDs3(cur).ToArray(), new UTF8Encoding(false));
                }
                catch (Exception ex) { Fail(ex); }
                Invalidate();
                return;
            }
            try
            {
                List<string> lines = new List<string>(File.Exists(path) ? File.ReadAllLines(path) : new string[0]);
                bool hasJoy = false, hasCam = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    string t = lines[i].Trim();
                    bool commented = t.StartsWith("#");
                    string body = commented ? t.TrimStart('#').Trim() : t;
                    int eq = body.IndexOf('=');
                    if (eq < 1) continue;
                    string k = body.Substring(0, eq).Trim();
                    if (k == "mouse_to_joystick")
                    {
                        if (kb) { if (!hasJoy) { lines[i] = "mouse_to_joystick = right"; hasJoy = true; } else if (!commented) { lines.RemoveAt(i); i--; } }
                        else if (!commented) lines[i] = "# " + t;
                    }
                    else if (k == "mouse_camera" && ctab == "normal" && !commented)
                    {
                        lines[i] = "mouse_camera = " + (kb ? "direct" : "off");
                        hasCam = true;
                    }
                }
                if (kb && !hasJoy) lines.Add("mouse_to_joystick = right");
                if (ctab == "normal" && !hasCam) lines.Add("mouse_camera = " + (kb ? "direct" : "off"));
                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
                if (ctab == "normal") JsonSet("play_with", kb ? "\"keyboard\"" : "\"controller\"");
            }
            catch (Exception ex) { Fail(ex); }
            Invalidate();
        }

        // Mouse look speed: the 2nd value of mouse_movement_params (deadzone, speed, speed offset).
        // BBport's direct mouse camera (mouse_camera = direct) has its own speed: mouse_camera_sensitivity.
        bool DirectCamera()
        {
            if (ctab != "normal") return false;
            foreach (string raw in ReadInput(ctab))
            {
                string l = raw.Trim();
                int eq = l.IndexOf('=');
                if (!l.StartsWith("#") && eq > 0 && l.Substring(0, eq).Trim() == "mouse_camera") return l.Substring(eq + 1).Trim() == "direct";
            }
            return false;
        }

        double GetSens()
        {
            if (DirectCamera())
            {
                foreach (string raw in ReadInput(ctab))
                {
                    string l = raw.Trim();
                    int eq = l.IndexOf('=');
                    double d;
                    if (!l.StartsWith("#") && eq > 0 && l.Substring(0, eq).Trim() == "mouse_camera_sensitivity" &&
                        double.TryParse(l.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
                }
                return 1.0;
            }
            foreach (string raw in ReadInput(ctab))
            {
                string l = raw.Trim();
                if (l.StartsWith("#") || !l.StartsWith("mouse_movement_params")) continue;
                string[] v = l.Substring(l.IndexOf('=') + 1).Split(',');
                double d;
                if (v.Length >= 2 && double.TryParse(v[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            }
            return 1.0;
        }

        void SetSens(double speed)
        {
            speed = Math.Round(Math.Max(0.1, Math.Min(5.0, speed)), 1);
            string path = InputPath(ctab);
            if (DirectCamera())
            {
                try
                {
                    List<string> ls = new List<string>(File.ReadAllLines(path));
                    string ln = "mouse_camera_sensitivity = " + speed.ToString("0.0", CultureInfo.InvariantCulture);
                    int at = -1;
                    for (int i = 0; i < ls.Count; i++)
                    {
                        string l = ls[i].Trim();
                        int eq = l.IndexOf('=');
                        if (l.StartsWith("#") || eq < 1 || l.Substring(0, eq).Trim() != "mouse_camera_sensitivity") continue;
                        if (at < 0) at = i; else { ls.RemoveAt(i); i--; }
                    }
                    if (at >= 0) ls[at] = ln;
                    else
                    {
                        int cam = ls.FindIndex(delegate (string x) { return x.Trim().StartsWith("mouse_camera"); });
                        ls.Insert(cam >= 0 ? cam + 1 : ls.Count, ln);
                    }
                    File.WriteAllLines(path, ls.ToArray(), new UTF8Encoding(false));
                }
                catch (Exception ex) { Fail(ex); }
                Invalidate();
                return;
            }
            try
            {
                List<string> lines = new List<string>(File.Exists(path) ? File.ReadAllLines(path) : new string[0]);
                string dz = "0.5", off = "0.125";
                int at = -1;
                for (int i = 0; i < lines.Count; i++)
                {
                    string l = lines[i].Trim();
                    if (l.StartsWith("#") || !l.StartsWith("mouse_movement_params")) continue;
                    string[] v = l.Substring(l.IndexOf('=') + 1).Split(',');
                    if (v.Length >= 1) dz = v[0].Trim();
                    if (v.Length >= 3) off = v[2].Trim();
                    if (at < 0) at = i; else { lines.RemoveAt(i); i--; }
                }
                string line = "mouse_movement_params = " + dz + ", " + speed.ToString("0.0", CultureInfo.InvariantCulture) + ", " + off;
                if (at >= 0) lines[at] = line; else lines.Add(line);
                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (Exception ex) { Fail(ex); }
            Invalidate();
        }

        void ChangeSens(double d) { SetSens(GetSens() + d); }

        void ResetControls()
        {
            string def = DefaultInput(ctab), cur = InputPath(ctab);
            if (!File.Exists(def)) { Missing(def); return; }
            DialogResult r = MessageBox.Show(this,
                I18n.T("ترجّع أزرار ") + (ctab == "seamless" ? "Seamless Co-op" : "BBport") + I18n.T(" لوضعها الافتراضي؟\nأي تغيير سويته بيروح."),
                I18n.T("الأزرار الافتراضية"), MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2, I18n.MsgOpt);
            if (r != DialogResult.Yes) return;
            try
            {
                if (File.Exists(cur)) File.Copy(cur, cur + ".bak-before-reset", true);
                File.Copy(def, cur, true);
            }
            catch (Exception ex) { Fail(ex); }
            capturing = null;
            Invalidate();
        }

        // Rows of the controls page, in groups like BBport's launcher. "" key = group heading.
        static readonly string[] RowKeys = {
            "", "axis_left_y_minus", "axis_left_y_plus", "axis_left_x_minus", "axis_left_x_plus", "leftjoystick_halfmode",
                "circle", "l3", "r3", "axis_right_y_minus", "axis_right_y_plus", "axis_right_x_minus", "axis_right_x_plus",
            "", "r1", "r2", "l1", "l2",
            "", "cross", "square", "triangle", "pad_up", "pad_down", "pad_left", "pad_right", "options",
                "touchpad_left", "touchpad_right", "touchpad_center" };
        static readonly string[] RowNames = {
            I18n.T("الحركة والكاميرا"), I18n.T("تحرك للأمام"), I18n.T("تحرك للخلف"), I18n.T("تحرك لليسار"), I18n.T("تحرك لليمين"), I18n.T("المشي (اضغط مع الحركة)"),
                I18n.T("(○) تفادي / ركض (اضغط مستمر)"), I18n.T("(L3) قفز أثناء الركض"), I18n.T("(R3) تثبيت الهدف / إعادة ضبط الكاميرا"),
                I18n.T("الكاميرا لأعلى"), I18n.T("الكاميرا لأسفل"), I18n.T("الكاميرا لليسار"), I18n.T("الكاميرا لليمين"),
            I18n.T("القتال"), I18n.T("(R1) هجوم"), I18n.T("(R2) هجوم قوي"), I18n.T("(L1) تحويل السلاح"), I18n.T("(L2) السلاح الناري"),
            I18n.T("الأغراض والقوائم"), I18n.T("(✕) تفاعل / تأكيد في القوائم"), I18n.T("(□) استخدام الغرض السريع"), I18n.T("(△) قارورة الدم"),
                I18n.T("زر الاتجاه لأعلى"), I18n.T("(↓) تبديل الغرض السريع"), I18n.T("(←) تبديل سلاح اليد اليسرى"), I18n.T("(→) تبديل سلاح اليد اليمنى"),
                I18n.T("(Options) القائمة"), I18n.T("(اللمس يسار) الإيماءات"), I18n.T("(اللمس يمين)"), I18n.T("(اللمس وسط)") };

        static int[] rowY;
        static int optY;
        int captureSlot;
        string pendingMod;
        Dictionary<Hot, string> slotAct = new Dictionary<Hot, string>();
        Dictionary<Hot, int> slotIdx = new Dictionary<Hot, int>();
        Dictionary<string, Hot[,]> slotsBy = new Dictionary<string, Hot[,]>();
        static readonly int[] SlotX = { 90, 342, 594 };
        const int SlotW = 244;

        static void LayoutRows()
        {
            if (rowY != null) return;
            rowY = new int[RowKeys.Length];
            int y = 150;
            for (int i = 0; i < RowKeys.Length; i++)
            {
                if (RowKeys[i] == "") { if (i > 0) y += 12; rowY[i] = y; y += 32; }
                else { rowY[i] = y; y += 34; }
            }
            optY = y + 16;
        }

        void BuildControlsHots()
        {
            LayoutRows();
            hCTabN = Add(new Rectangle(80, 84, 170, 36), delegate { ctab = "normal"; capturing = null; Invalidate(); }, null);
            hCTabS = Add(new Rectangle(258, 84, 170, 36), delegate { ctab = "seamless"; capturing = null; Invalidate(); }, null);
            hCTabN.Page = hCTabS.Page = "controls";
            foreach (string tab in new string[] { "normal", "seamless" })
            {
                Hot[,] grid = new Hot[RowKeys.Length, 3];
                for (int i = 0; i < RowKeys.Length; i++)
                {
                    if (RowKeys[i] == "") continue;
                    for (int k = 0; k < 3; k++)
                    {
                        string act = RowKeys[i]; int slot = k;
                        Hot h = Add(new Rectangle(SlotX[k], rowY[i] + 2, SlotW, 30), delegate { capturing = act; captureSlot = slot; pendingMod = null; Invalidate(); },
                            I18n.T("اضغط وبعدها اضغط الزر · كليك يمين يمسح"));
                        h.Page = "controls:" + tab;
                        slotAct[h] = act; slotIdx[h] = slot;
                        grid[i, k] = h;
                    }
                }
                slotsBy[tab] = grid;
            }
            int dy = optY + 40, sy = optY + 128, qy = optY + 172, ry = optY + 222;
            hSensMinus = Add(new Rectangle(520, sy, 40, 30), delegate { ChangeSens(-0.1); }, null);
            hSensPlus = Add(new Rectangle(660, sy, 40, 30), delegate { ChangeSens(0.1); }, null);
            hSensReset = Add(new Rectangle(706, sy, 110, 30), delegate { SetSens(1.0); }, I18n.T("يرجّعها 1.0"));
            hSensMinus.Page = hSensPlus.Page = hSensReset.Page = null; // both tabs (checked in HitTest)
            hPromptBox = Add(new Rectangle(560, dy, 376, 32), delegate { promptOpen = !promptOpen; Invalidate(); }, null);
            hPromptBox.Page = "controls";
            promptHots = new Hot[PromptVals.Length]; // dropdown rows, only active while it is open
            for (int i = 0; i < PromptVals.Length; i++)
            {
                string v = PromptVals[i];
                promptHots[i] = new Hot(new Rectangle(560, dy + 34 + i * 32, 376, 32), delegate { promptOpen = false; SetPlayWith(v); }, null);
                promptHots[i].Page = "controls";
            }
            hQuitSet = Add(new Rectangle(640, qy, 100, 30), delegate { capturing = QuitCap; Invalidate(); }, I18n.T("اضغط وبعدها اضغط الاختصار الجديد"));
            hQuitReset = Add(new Rectangle(746, qy, 100, 30), delegate { SetQuit(QuitDefault); }, I18n.T("يرجّعه Ctrl + Shift + End"));
            hQuitSet.Page = hQuitReset.Page = "controls:seamless";
            hCtlReset = Add(new Rectangle(90, ry, 220, 32), delegate { ResetControls(); }, I18n.T("يرجّع أزرار هذا الوضع لوضعها الافتراضي"));
            hCtlReset.Page = null; // both tabs (checked in HitTest)
        }

        static string PrettyKey(string b)
        {
            string[] parts = b.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                switch (p)
                {
                    case "leftbutton": p = "Left click"; break;
                    case "rightbutton": p = "Right click"; break;
                    case "middlebutton": p = "Wheel click"; break;
                    case "mousewheelup": p = "Wheel up"; break;
                    case "mousewheeldown": p = "Wheel down"; break;
                    case "sidebuttonback": p = "Mouse 4"; break;
                    case "sidebuttonforward": p = "Mouse 5"; break;
                    case "lshift": case "rshift": p = "Shift"; break;
                    case "lctrl": case "rctrl": p = "Ctrl"; break;
                    case "lalt": p = "Left Alt"; break;
                    case "ralt": p = "Right Alt"; break;
                    case "space": p = "Space"; break;
                    case "enter": p = "Enter"; break;
                    case "escape": p = "Esc"; break;
                    case "tab": p = "Tab"; break;
                    case "backspace": p = "Backspace"; break;
                    case "up": p = "↑"; break;
                    case "down": p = "↓"; break;
                    case "left": p = "←"; break;
                    case "right": p = "→"; break;
                    default: p = p.Length <= 2 ? p.ToUpperInvariant() : char.ToUpperInvariant(p[0]) + p.Substring(1); break;
                }
                parts[i] = p;
            }
            return string.Join(" + ", parts);
        }

        // Replaces the n-th keyboard/mouse binding of an action, or adds one.
        void SetSlotBinding(string act, int slot, string key)
        {
            string path = InputPath(ctab);
            try
            {
                List<string> lines = new List<string>(File.Exists(path) ? File.ReadAllLines(path) : new string[0]);
                int n = 0, at = -1;
                for (int i = 0; i < lines.Count; i++)
                {
                    string l = lines[i].Trim();
                    int eq = l.IndexOf('=');
                    if (l.StartsWith("#") || eq < 1 || l.Substring(0, eq).Trim() != act) continue;
                    string v = l.Substring(eq + 1).Trim();
                    if (PadInputs.Contains(v) || v.StartsWith("axis_")) continue;
                    if (v == key && n != slot) return; // already bound in another slot
                    if (n == slot) { at = i; break; }
                    n++;
                }
                if (key == null) { if (at >= 0) lines.RemoveAt(at); }
                else if (at >= 0) lines[at] = act + " = " + key;
                else lines.Add(act + " = " + key);
                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (Exception ex) { Fail(ex); }
            Invalidate();
        }

        static string RowName(string act)
        {
            int i = Array.IndexOf(RowKeys, act);
            return i >= 0 ? RowNames[i] : act;
        }

        void DrawControls(Graphics g)
        {
            LayoutRows();
            bool seam = ctab == "seamless";
            DrawTabbedPanel(g, I18n.T("التحكم"), ctab, hCTabN, hCTabS, seam
                ? I18n.T("لوحة المفاتيح والماوس في Seamless · اضغط على خانة وبعدها اضغط الزر · كليك يمين يمسح · F8 داخل اللعبة يعيد تحميل الأزرار")
                : I18n.T("لوحة المفاتيح والماوس في BBport · اضغط على خانة وبعدها اضغط الزر · كليك يمين يمسح · F8 داخل اللعبة يعيد تحميل الأزرار"));
            if (DrawNotInstalled(g, seam)) return;

            string[] lines = ReadInput(ctab);
            Hot[,] grid = slotsBy[ctab];
            using (Font fh = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold))
            using (Font fl = new Font("Segoe UI", 10.5f))
            using (Font fc = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold))
            {
                for (int i = 0; i < RowKeys.Length; i++)
                {
                    int y = rowY[i];
                    if (RowKeys[i] == "")
                    {
                        Txt(g, RowNames[i], fh, new Rectangle(860, y, 290, 30), Theme.Gold, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                        continue;
                    }
                    Txt(g, RowNames[i], fl, new Rectangle(850, y + 2, 300, 30), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
                    List<string> binds = BindingsOf(lines, RowKeys[i]);
                    for (int k = 0; k < 3; k++)
                    {
                        Hot h = grid[i, k];
                        bool cap = capturing == RowKeys[i] && captureSlot == k;
                        bool hv = hover == h;
                        string txt = cap ? I18n.T("اضغط زر ...") : (k < binds.Count ? PrettyKey(binds[k]) : "—");
                        FillRound(g, h.R, 6,
                            cap ? Color.FromArgb(230, Theme.Blood) : (hv ? Color.FromArgb(200, 40, 30, 38) : Color.FromArgb(185, 34, 32, 40)),
                            cap || hv ? Theme.Gold : Color.FromArgb(120, Theme.GoldDim), cap || hv ? 1.8f : 1f);
                        Txt(g, txt, fc, h.R, k < binds.Count || cap ? Theme.Text : Theme.TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
                    }
                }
            }

            // buttons & prompts section (device + prompt icons), then mouse & options
            using (Font fh = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold))
            {
                Txt(g, I18n.T("الأزرار والرموز"), fh, new Rectangle(860, optY, 290, 30), Theme.Gold, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                Txt(g, I18n.T("الماوس والخيارات"), fh, new Rectangle(860, optY + 88, 290, 30), Theme.Gold, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                using (Pen pl = new Pen(Color.FromArgb(70, Theme.GoldDim), 1f))
                {
                    g.DrawLine(pl, 90, optY + 15, 850, optY + 15);
                    g.DrawLine(pl, 90, optY + 103, 850, optY + 103);
                }
            }
            {
                string cur = GetPlayWith();
                using (Font fl = new Font("Segoe UI", 10.5f))
                    Txt(g, I18n.T("تلعب باستخدام"), fl, new Rectangle(950, hPromptBox.R.Y + 1, 200, 30), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                int ci = Math.Max(0, Array.IndexOf(PromptVals, cur));
                bool hvb = hover == hPromptBox || promptOpen;
                FillRound(g, hPromptBox.R, 8, Color.FromArgb(200, 24, 22, 28), hvb ? Theme.Gold : Color.FromArgb(150, Theme.GoldDim), hvb ? 1.6f : 1.1f);
                using (Font fc = new Font("Segoe UI Semibold", 11f, FontStyle.Bold))
                    Txt(g, PromptNames[ci], fc, new Rectangle(hPromptBox.R.X + 14, hPromptBox.R.Y, hPromptBox.R.Width - 50, hPromptBox.R.Height), Theme.Text, TextFormatFlags.Left);
                Glyph(g, promptOpen ? "\uE70E" : "\uE70D", new Rectangle(hPromptBox.R.Right - 34, hPromptBox.R.Y, 26, hPromptBox.R.Height), Theme.Gold, 10f);
            }
            using (Font fl = new Font("Segoe UI", 10.5f))
            using (Font fv = new Font("Segoe UI Semibold", 12f, FontStyle.Bold))
            {
                int sy = hSensMinus.R.Y;
                double sens = GetSens();
                Txt(g, I18n.T("حساسية الماوس (") + (DirectCamera() ? I18n.T("كاميرا مباشرة 1:1") : I18n.T("الماوس كعصا التحكم")) + ")", fl,
                    new Rectangle(850, sy, 300, 30), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                Txt(g, sens.ToString("0.0", CultureInfo.InvariantCulture), fv, new Rectangle(564, sy, 92, 30), Theme.GoldLight, TextFormatFlags.HorizontalCenter);
                Segment(g, hSensMinus, "−", false);
                Segment(g, hSensPlus, "+", false);
                Segment(g, hSensReset, I18n.T("افتراضي (1.0)"), Math.Abs(sens - 1.0) < 0.05);

                if (seam)
                {
                    int py = hQuitSet.R.Y;
                    Txt(g, I18n.T("اختصار الخروج من اللعبة"), fl, new Rectangle(1000, py, 150, 30), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                    Rectangle chip = new Rectangle(852, py + 2, 142, 26);
                    FillRound(g, chip, 6, Color.FromArgb(185, 34, 32, 40), Color.FromArgb(150, Theme.GoldDim), 1f);
                    using (Font fc = new Font("Segoe UI Semibold", 10f, FontStyle.Bold))
                        Txt(g, PrettyCombo(GetQuit()), fc, chip, Theme.GoldLight, TextFormatFlags.HorizontalCenter);
                    Segment(g, hQuitSet, I18n.T("تغيير"), capturing == QuitCap);
                    Segment(g, hQuitReset, I18n.T("افتراضي"), GetQuit() == QuitDefault);
                }
            }
            Segment(g, hCtlReset, I18n.T("↺  الأزرار الافتراضية"), false);
            UseHeight(hCtlReset.R.Bottom + 50);

            if (promptOpen)
            {
                Rectangle list = new Rectangle(560, hPromptBox.R.Bottom + 2, 376, PromptVals.Length * 32);
                FillRound(g, list, 8, Color.FromArgb(250, 22, 20, 26), Theme.GoldDim, 1.2f);
                string cur2 = GetPlayWith();
                using (Font fo = new Font("Segoe UI Semibold", 11f, FontStyle.Bold))
                {
                    for (int i = 0; i < PromptVals.Length; i++)
                    {
                        Hot o = promptHots[i];
                        bool sel = PromptVals[i] == cur2, hv = hover == o;
                        if (sel || hv)
                        {
                            Rectangle r = o.R; r.Inflate(-3, -2);
                            FillRound(g, r, 6, sel ? Color.FromArgb(230, Theme.Blood) : Color.FromArgb(120, Theme.BloodDark), Color.Empty, 0);
                        }
                        Txt(g, PromptNames[i], fo, new Rectangle(o.R.X + 14, o.R.Y, o.R.Width - 28, o.R.Height), sel || hv ? Color.White : Theme.Text, TextFormatFlags.Left);
                    }
                }
                UseHeight(list.Bottom + 40);
            }

            if (capturing == QuitCap)
            {
                Rectangle box = new Rectangle(W / 2 - 300, H / 2 - 60 + scrollY, 600, 120);
                FillRound(g, box, 14, Color.FromArgb(240, 14, 10, 14), Theme.Gold, 2f);
                using (Font f = new Font("Segoe UI Semibold", 14f, FontStyle.Bold))
                    Txt(g, I18n.T("اضغط اختصار الخروج الجديد (مثل Ctrl + Shift + End)"), f,
                        new Rectangle(box.X + 20, box.Y + 18, box.Width - 40, 44), Theme.GoldLight, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
                using (Font f = new Font("Segoe UI", 10.5f))
                    Txt(g, I18n.T("Esc للإلغاء"), f, new Rectangle(box.X + 20, box.Y + 70, box.Width - 40, 30), Theme.TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
            }
        }

        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
        static bool Down(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        // .NET key -> shadPS4 input name.
        static string KeyName(Keys k)
        {
            if (k >= Keys.A && k <= Keys.Z) return k.ToString().ToLowerInvariant();
            if (k >= Keys.D0 && k <= Keys.D9) return ((int)(k - Keys.D0)).ToString();
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "kp" + (int)(k - Keys.NumPad0);
            if (k >= Keys.F1 && k <= Keys.F12) return "f" + ((int)(k - Keys.F1) + 1);
            switch (k)
            {
                case Keys.Space: return "space";
                case Keys.Enter: return "enter";
                case Keys.Tab: return "tab";
                case Keys.Back: return "backspace";
                case Keys.Insert: return "insert";
                case Keys.Delete: return "delete";
                case Keys.Home: return "home";
                case Keys.End: return "end";
                case Keys.PageUp: return "pgup";
                case Keys.PageDown: return "pgdown";
                case Keys.Up: return "up";
                case Keys.Down: return "down";
                case Keys.Left: return "left";
                case Keys.Right: return "right";
                case Keys.ShiftKey: case Keys.LShiftKey: case Keys.RShiftKey: return Down(0xA1) ? "rshift" : "lshift";
                case Keys.ControlKey: case Keys.LControlKey: case Keys.RControlKey: return Down(0xA3) ? "rctrl" : "lctrl";
                case Keys.Menu: case Keys.LMenu: case Keys.RMenu: return Down(0xA5) ? "ralt" : "lalt";
                case Keys.Capital: return "capslock";
                case Keys.Add: return "kpplus";
                case Keys.Subtract: return "kpminus";
                case Keys.Multiply: return "kpmultiply";
                case Keys.Divide: return "kpdivide";
                case Keys.Decimal: return "kpperiod";
                case Keys.Oemcomma: return "comma";
                case Keys.OemPeriod: return "period";
                case Keys.OemMinus: return "minus";
                case Keys.Oemplus: return "equals";
                case Keys.OemQuestion: return "slash";
                case Keys.OemSemicolon: return "semicolon";
                case Keys.OemQuotes: return "apostrophe";
                case Keys.OemOpenBrackets: return "lbracket";
                case Keys.OemCloseBrackets: return "rbracket";
                case Keys.OemPipe: return "backslash";
                case Keys.Oemtilde: return "grave";
            }
            return null;
        }

        void FinishCapture(string key)
        {
            string act = capturing;
            capturing = null;
            pendingMod = null;
            if (act != null && key != null) SetSlotBinding(act, captureSlot, key);
            Invalidate();
        }

        // Adds the modifiers held right now: "lshift, leftbutton".
        static string WithMods(string name)
        {
            if (name == null) return null;
            List<string> c = new List<string>();
            if (Down(0xA2) || Down(0xA3)) c.Add(Down(0xA3) ? "rctrl" : "lctrl");
            if (Down(0xA0) || Down(0xA1)) c.Add(Down(0xA1) ? "rshift" : "lshift");
            if (Down(0xA4) || Down(0xA5)) c.Add(Down(0xA5) ? "ralt" : "lalt");
            c.Remove(name);
            c.Add(name);
            return string.Join(", ", c.ToArray());
        }

        static bool IsModifier(Keys k)
        {
            return k == Keys.ShiftKey || k == Keys.ControlKey || k == Keys.Menu || k == Keys.LShiftKey || k == Keys.RShiftKey ||
                   k == Keys.LControlKey || k == Keys.RControlKey || k == Keys.LMenu || k == Keys.RMenu;
        }

        // A modifier pressed and released alone (e.g. Left Alt for walking) is bound by itself.
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (capturing != null && capturing != QuitCap && pendingMod != null && IsModifier(e.KeyCode)) { FinishCapture(pendingMod); e.Handled = true; return; }
            base.OnKeyUp(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (capturing != null && (msg.Msg == 0x100 || msg.Msg == 0x104))
            {
                Keys k = keyData & Keys.KeyCode;
                if (k == Keys.Escape) { capturing = null; Invalidate(); return true; }
                string name = KeyName(k);
                if (capturing == QuitCap)
                {
                    bool modifier = k == Keys.ShiftKey || k == Keys.ControlKey || k == Keys.Menu ||
                                    k == Keys.LShiftKey || k == Keys.RShiftKey || k == Keys.LControlKey ||
                                    k == Keys.RControlKey || k == Keys.LMenu || k == Keys.RMenu;
                    if (modifier || name == null) return true; // wait for the main key
                    List<string> combo = new List<string>();
                    if (Down(0xA2)) combo.Add("lctrl"); else if (Down(0xA3)) combo.Add("rctrl");
                    if (Down(0xA0)) combo.Add("lshift"); else if (Down(0xA1)) combo.Add("rshift");
                    if (Down(0xA4)) combo.Add("lalt"); else if (Down(0xA5)) combo.Add("ralt");
                    combo.Add(name);
                    capturing = null;
                    SetQuit(string.Join(", ", combo.ToArray()));
                    return true;
                }
                if (IsModifier(k)) { pendingMod = name; return true; } // wait: alone, or with another key
                if (name != null) FinishCapture(WithMods(name));
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            e = M(e);
            if (capturing == QuitCap) return;
            if (capturing != null) { FinishCapture(WithMods(e.Delta > 0 ? "mousewheelup" : "mousewheeldown")); return; }
            if (page != "home" && maxScroll > 0)
            {
                scrollY = Math.Max(0, Math.Min(maxScroll, scrollY - e.Delta / 120 * 60));
                hover = HitTest(e.Location);
                Invalidate();
            }
            base.OnMouseWheel(e);
        }

        bool CaptureMouse(MouseEventArgs e)
        {
            if (capturing == null) return false;
            if (capturing == QuitCap) { swallowMouseUp = true; return true; } // keyboard only
            string name = null;
            if (e.Button == MouseButtons.Left) name = "leftbutton";
            else if (e.Button == MouseButtons.Right) name = "rightbutton";
            else if (e.Button == MouseButtons.Middle) name = "middlebutton";
            else if (e.Button == MouseButtons.XButton1) name = "sidebuttonback";
            else if (e.Button == MouseButtons.XButton2) name = "sidebuttonforward";
            swallowMouseUp = true;
            FinishCapture(WithMods(name));
            return true;
        }

        // ---------- online page: normal play (BBport / The Hunter's Dream) and Seamless (shadNet account) ----------

        string otab = "normal";
        string thdCode; // Discord sign-in code while waiting for approval
        bool codeCopied;
        Hot hCopyCode, hOpenLink;
        string thdPage;
        Hot hOTabN, hOTabS, hOnN, hPoolThd, hPoolShad, hDiscordSign, hTestN, hOnS, hLogin, hRegister, hTestS;
        TextBox tbName, tbPass, tbMail;
        string onlineMsg = "";
        Color onlineMsgColor = Theme.TextDim;
        bool signBusy, testBusy, acctBusy;
        string testMsg = "";
        Color testMsgColor = Theme.TextDim;
        const string SeamHost = "play.seamlesshunt.com";
        const int SeamPort = 31313;

        static string UsersPath { get { return Path.Combine(ShadDir, "users.json"); } }
        string AccountTool
        {
            get
            {
                // shipped next to the launcher (account\), or the one from the BBSC BBport package
                string own = Path.Combine(Settings.BaseDir, @"account\shadnet-sample.exe");
                if (File.Exists(own)) return own;
                return Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(cfg.SeamlessCmd)), @"BBSC-0.0.2-BBport\account\shadnet-sample.exe");
            }
        }

        void BuildOnlineHots()
        {
            hOTabN = Add(new Rectangle(80, 84, 170, 36), delegate { SetOnlineTab("normal"); }, null);
            hOTabS = Add(new Rectangle(258, 84, 170, 36), delegate { SetOnlineTab("seamless"); }, null);
            hOTabN.Page = hOTabS.Page = "online";

            hOnN = Add(new Rectangle(640, 160, 510, 44), delegate { ToggleOnlineNormal(); }, null);
            hPoolThd = Add(new Rectangle(640, 262, 252, 38), delegate { JsonSet("online_pool", "\"huntersdream\""); Invalidate(); }, null);
            hPoolShad = Add(new Rectangle(898, 262, 252, 38), delegate { JsonSet("online_pool", "\"shadnet\""); Invalidate(); }, null);
            hDiscordSign = Add(new Rectangle(100, 190, 300, 44), delegate { DiscordSignIn(); }, null);
            hTestN = Add(new Rectangle(100, 246, 300, 44), delegate { TestServers(new string[] { "thehuntersdream.com:443", "srv.shadps4.net:31313" }); }, null);
            hOpenLink = Add(new Rectangle(100, 0, 170, 34), delegate { if (thdPage != null) Open(thdPage); }, null);
            hCopyCode = Add(new Rectangle(100, 0, 170, 34), delegate { CopyCode(); }, null);
            foreach (Hot h in new Hot[] { hOnN, hPoolThd, hPoolShad, hDiscordSign, hTestN, hCopyCode, hOpenLink }) h.Page = "online:normal";
            hOpenLink.R = new Rectangle(110, 326, 170, 30);
            hCopyCode.R = new Rectangle(110, 548, 170, 34);

            hOnS = Add(new Rectangle(640, 160, 510, 44), delegate { ToggleOnlineSeamless(); }, null);
            hLogin = Add(new Rectangle(640, 540, 250, 44), delegate { Account(false); }, null);
            hRegister = Add(new Rectangle(900, 540, 250, 44), delegate { Account(true); }, null);
            hTestS = Add(new Rectangle(100, 190, 300, 44), delegate {
                TestServers(new string[] { SeamHost + ":31313", SeamHost + ":31315", SeamHost + ":20443" }); }, null);
            foreach (Hot h in new Hot[] { hOnS, hLogin, hRegister, hTestS }) h.Page = "online:seamless";

            tbName = NewBox(new Rectangle(640, 340, 340, 30), false);
            tbPass = NewBox(new Rectangle(640, 410, 340, 30), true);
            tbMail = NewBox(new Rectangle(640, 480, 340, 30), false);
        }

        TextBox NewBox(Rectangle r, bool password)
        {
            TextBox t = new TextBox();
            t.Bounds = r;
            t.Tag = r; // its place in the 1280x720 layout
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = Color.FromArgb(24, 20, 26);
            t.ForeColor = Theme.Text;
            t.Font = new Font("Segoe UI", 12f);
            t.UseSystemPasswordChar = password;
            t.Visible = false;
            Controls.Add(t);
            return t;
        }

        void UpdateBoxes()
        {
            bool show = page == "online" && otab == "seamless";
            tbName.Visible = tbPass.Visible = tbMail.Visible = show;
            if (show && tbName.Text == "") tbName.Text = SeamlessUser();
        }

        void SetOnlineTab(string t) { otab = t; onlineMsg = ""; testMsg = ""; UpdateBoxes(); Invalidate(); }

        string JsonBool(string file, string key)
        {
            try
            {
                Match m = Regex.Match(File.ReadAllText(file), "\"" + key + "\"\\s*:\\s*(true|false)");
                return m.Success ? m.Groups[1].Value : null;
            }
            catch { return null; }
        }

        string SeamlessUser()
        {
            try
            {
                string txt = File.ReadAllText(UsersPath);
                Match m = Regex.Match(txt, "\"shadnet_npid\"\\s*:\\s*\"([^\"]*)\"");
                return m.Success ? m.Groups[1].Value : "";
            }
            catch { return ""; }
        }

        void ToggleOnlineNormal()
        {
            bool on = JsonBool(JsonPath, "online") == "true";
            JsonSet("online", on ? "false" : "true");
            Invalidate();
        }

        void ToggleOnlineSeamless()
        {
            try { sCfg = File.ReadAllText(SCfgPath); } catch { Missing(SCfgPath); return; }
            bool on = CfgValue("connected_to_network") == "true" && CfgValue("shad_net_enabled") == "true";
            CfgSet("connected_to_network", on ? "false" : "true");
            CfgSet("shad_net_enabled", on ? "false" : "true");
            SaveCfg();
            Invalidate();
        }

        // Discord sign-in for The Hunter's Dream, the way bbhost/BBport do it: a device code to approve on the website.
        void DiscordSignIn()
        {
            if (signBusy) return;
            signBusy = true;
            SetOnlineMsg(I18n.T("يطلب كود تسجيل الدخول ..."), Theme.TextDim);
            Thread th = new Thread(delegate ()
            {
                string result; bool ok = false;
                try
                {
                    string dev = "bbport on " + Environment.MachineName;
                    string start = null, used = null;
                    foreach (string b in new string[] { "https://thehuntersdream.com", "https://thehuntersdream.com:18671" })
                    {
                        try { start = ThdPost(b + "/auth/device/start", "{\"DeviceName\":\"" + JsonEscape(dev) + "\"}"); used = b; break; }
                        catch { }
                    }
                    if (start == null) throw new Exception(I18n.T("سيرفر The Hunter's Dream ما رد - جرّب بعد شوي"));
                    string code = JStr(start, "DeviceCode"), user = JStr(start, "UserCode");
                    if (code == null || user == null) throw new Exception(I18n.T("السيرفر ما أعطى كود"));
                    string page = JStr(start, "VerificationUri");
                    if (string.IsNullOrEmpty(page)) page = "https://thehuntersdream.com/link";
                    int interval = Math.Max(2, JInt(start, "Interval", 5));
                    int expires = JInt(start, "ExpiresIn", 600);
                    string pg = page, uc = user;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        thdCode = uc;
                        thdPage = pg;
                        CopyCode();
                        SetOnlineMsg("", Theme.GoldLight);
                        Open(pg);
                    });
                    DateTime deadline = DateTime.Now.AddSeconds(expires);
                    while (true)
                    {
                        if (DateTime.Now > deadline) throw new Exception(I18n.T("انتهت مدة الكود - جرّب مرة ثانية"));
                        Thread.Sleep(interval * 1000);
                        string r = ThdPost(used + "/auth/device/poll", "{\"DeviceCode\":\"" + JsonEscape(code) + "\"}");
                        string st = (JStr(r, "Status") ?? "").ToLowerInvariant();
                        if (st == "approved")
                        {
                            string id = JStr(r, "OnlineId"), tok = JStr(r, "Token");
                            if (string.IsNullOrEmpty(tok)) throw new Exception(I18n.T("السيرفر ما أعطى توكن"));
                            JsonSet("thd_online_id", "\"" + JsonEscape(id ?? "") + "\"");
                            JsonSet("thd_token", "\"" + JsonEscape(tok).Replace("$", "$$") + "\"");
                            result = I18n.T("✓ تم تسجيل الدخول لـ The Hunter's Dream باسم ") + id;
                            ok = true;
                            break;
                        }
                        if (st == "slow_down") interval += 2;
                        else if (st == "denied") throw new Exception(I18n.T("تم رفض تسجيل الدخول"));
                        else if (st == "expired") throw new Exception(I18n.T("انتهت مدة الكود - جرّب مرة ثانية"));
                    }
                }
                catch (Exception ex) { result = "✗ " + ex.Message; }
                string m = result; bool good = ok;
                try { BeginInvoke((MethodInvoker)delegate { signBusy = false; thdCode = null; SetOnlineMsg(m, good ? Theme.Green : Theme.GoldLight); }); } catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        // The three steps of the Discord sign-in, shown while the launcher waits for the approval.
        void DrawSignSteps(Graphics g)
        {
            Rectangle box = new Rectangle(100, 312, 490, 330);
            FillRound(g, box, 12, Color.FromArgb(215, 12, 9, 14), Color.FromArgb(170, Theme.GoldDim), 1.2f);
            string[] titles = { I18n.T("١  اربط حسابك الديسكورد"), I18n.T("٢  اختر اسمك"), I18n.T("٣  الصق الكود") };
            string[] descs = {
                I18n.T("في الصفحة اضغط زر «SIGN IN WITH DISCORD TO LINK»"),
                I18n.T("اكتب الاسم اللي تبيه يطلع للاعبين في الموقع"),
                I18n.T("الصق الكود في الموقع (انسخ تلقائياً) ووافق") };
            int[] ys = { 324, 394, 464 };
            using (Font ft = new Font("Segoe UI Semibold", 12f, FontStyle.Bold))
            using (Font fd = new Font("Segoe UI", 9.5f))
            {
                for (int i = 0; i < 3; i++)
                {
                    Rectangle dot = new Rectangle(box.Right - 40, ys[i] + 6, 22, 22);
                    using (SolidBrush b = new SolidBrush(Theme.Blood)) g.FillEllipse(b, dot);
                    using (Pen pn = new Pen(Theme.Gold, 1.5f)) g.DrawEllipse(pn, dot);
                    Txt(g, titles[i], ft, new Rectangle(box.X + 190, ys[i], box.Width - 240, 30), Theme.GoldLight, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                    Txt(g, descs[i], fd, new Rectangle(box.X + 14, ys[i] + 28, box.Width - 60, 22), Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
                }
            }
            Segment(g, hOpenLink, I18n.T("فتح صفحة الربط"), false);
            using (Font fc = new Font("Consolas", 26f, FontStyle.Bold))
                Txt(g, thdCode, fc, new Rectangle(box.X + 190, 536, box.Width - 210, 56), Theme.GoldLight, TextFormatFlags.HorizontalCenter);
            Segment(g, hCopyCode, codeCopied ? I18n.T("✓ انسخ الكود") : I18n.T("نسخ الكود"), codeCopied);
            using (Font fw = new Font("Segoe UI", 9.5f))
                Txt(g, I18n.T("ينتظر موافقتك في الموقع ..."), fw, new Rectangle(box.X + 14, box.Bottom - 30, box.Width - 28, 24), Theme.TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
        }

        void CopyCode()
        {
            if (string.IsNullOrEmpty(thdCode)) return;
            try { Clipboard.SetText(thdCode); codeCopied = true; }
            catch { codeCopied = false; }
            Invalidate();
        }

        static string ThdPost(string url, string json)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                wc.Headers[HttpRequestHeader.UserAgent] = "bbport-launcher/BloodborneLauncher";
                wc.Encoding = Encoding.UTF8;
                return wc.UploadString(url, "POST", json);
            }
        }

        static string JStr(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase);
            return m.Success ? Regex.Unescape(m.Groups[1].Value) : null;
        }

        static int JInt(string json, string key, int def)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase);
            int v;
            return m.Success && int.TryParse(m.Groups[1].Value, out v) ? v : def;
        }

        void SetOnlineMsg(string s, Color c) { onlineMsg = s; onlineMsgColor = c; Invalidate(); }

        void TestServers(string[] targets)
        {
            if (testBusy) return;
            testBusy = true;
            testMsg = I18n.T("يختبر الاتصال ..."); testMsgColor = Theme.TextDim; Invalidate();
            Thread th = new Thread(delegate ()
            {
                StringBuilder sb = new StringBuilder();
                bool all = true;
                foreach (string t in targets)
                {
                    string host = t.Substring(0, t.LastIndexOf(':'));
                    int port = int.Parse(t.Substring(t.LastIndexOf(':') + 1));
                    bool ok = false;
                    try
                    {
                        using (System.Net.Sockets.TcpClient c = new System.Net.Sockets.TcpClient())
                        {
                            IAsyncResult ar = c.BeginConnect(host, port, null, null);
                            ok = ar.AsyncWaitHandle.WaitOne(5000) && c.Connected;
                        }
                    }
                    catch { ok = false; }
                    if (!ok) all = false;
                    sb.Append((ok ? I18n.T("✓ متاح   ") : I18n.T("✗ ما يوصل   ")) + t + "\n");
                }
                string res = sb.ToString().TrimEnd();
                bool good = all;
                try { BeginInvoke((MethodInvoker)delegate { testBusy = false; testMsg = res; testMsgColor = good ? Theme.Green : Theme.GoldLight; Invalidate(); }); } catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        static string JsonEscape(string s) { return s.Replace("\\", "\\\\").Replace("\"", "\\\""); }

        // Seamless account: create (register) and/or check (login) on the BBSC server, then save it for shadPS4 user 1000.
        void Account(bool create)
        {
            if (acctBusy) return;
            string name = tbName.Text.Trim(), pass = tbPass.Text, mail = tbMail.Text.Trim();
            if (!Regex.IsMatch(name, "^[A-Za-z0-9_-]{3,16}$")) { SetOnlineMsg(I18n.T("الاسم لازم يكون من 3 إلى 16 حرف إنجليزي أو رقم أو - أو _"), Theme.GoldLight); return; }
            if (pass.Length < 1) { SetOnlineMsg(I18n.T("اكتب كلمة المرور"), Theme.GoldLight); return; }
            string tool = AccountTool;
            if (!File.Exists(tool)) { Missing(tool); return; }
            if (mail != "" && !Regex.IsMatch(mail, "^[^@\\s\"]+@[^@\\s\"]+\\.[^@\\s\"]+$"))
            {
                SetOnlineMsg(I18n.T("الإيميل مو صحيح (لازم يكون مثل name@example.com) - أو خلّه فاضي"), Theme.GoldLight);
                return;
            }
            if (mail == "") mail = name + "@bbsc.local"; // the server wants a different email for every account
            acctBusy = true;
            SetOnlineMsg(create ? I18n.T("يسوي الحساب ...") : I18n.T("يسجّل الدخول ..."), Theme.TextDim);
            Thread th = new Thread(delegate ()
            {
                string msg; bool ok = false;
                try
                {
                    string ip = null;
                    foreach (IPAddress a in Dns.GetHostAddresses(SeamHost))
                        if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { ip = a.ToString(); break; }
                    if (ip == null) throw new Exception(I18n.T("ما لقيت السيرفر - تأكد من الإنترنت"));
                    if (create)
                    {
                        string o = RunTool(tool, ip + " " + SeamPort + " register " + Q(name) + " " + Q(pass) + " " + Q(mail));
                        if (o.IndexOf("Account created successfully", StringComparison.OrdinalIgnoreCase) < 0)
                            throw new Exception(I18n.T("السيرفر ما سوّى الحساب: ") + Why(o) + "\n" + Tail(o));
                    }
                    string l = RunTool(tool, ip + " " + SeamPort + " login " + Q(name) + " " + Q(pass));
                    if (l.IndexOf("[login] OK", StringComparison.OrdinalIgnoreCase) < 0)
                        throw new Exception(I18n.T("تسجيل الدخول فشل: الاسم أو كلمة المرور غلط، أو السيرفر طافي\n") + Tail(l));
                    SaveSeamlessAccount(name, pass);
                    ok = true;
                    msg = (create ? I18n.T("✓ انسوى الحساب ") : I18n.T("✓ تم تسجيل الدخول ")) + name + I18n.T(" وانحفظ لـ Seamless");
                }
                catch (Exception ex) { msg = "✗ " + ex.Message; }
                string m2 = msg; bool ok2 = ok;
                try { BeginInvoke((MethodInvoker)delegate { acctBusy = false; if (ok2) tbPass.Text = ""; SetOnlineMsg(m2, ok2 ? Theme.Green : Theme.GoldLight); }); } catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        static string Tail(string o)
        {
            string[] ls = o.Replace("\r", "").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int from = Math.Max(0, ls.Length - 3);
            return string.Join("\n", ls, from, ls.Length - from).Trim();
        }

        static string Why(string o)
        {
            string l = o.ToLowerInvariant();
            if (l.Contains("email")) return I18n.T("الإيميل مستخدم في حساب ثاني أو مو صحيح - جرّب إيميل ثاني أو خلّه فاضي");
            if (l.Contains("exist") || l.Contains("taken") || l.Contains("already")) return I18n.T("الاسم مأخوذ - جرّب اسم ثاني");
            if (l.Contains("timeout") || l.Contains("failed") || l.Contains("refused")) return I18n.T("السيرفر ما رد");
            return I18n.T("شوف رد السيرفر تحت");
        }

        static string Q(string s) { return "\"" + s.Replace("\"", "\\\"") + "\""; }

        static string RunTool(string exe, string args)
        {
            ProcessStartInfo si = new ProcessStartInfo(exe, args);
            si.WorkingDirectory = Path.GetDirectoryName(exe);
            si.UseShellExecute = false;
            si.CreateNoWindow = true;
            si.RedirectStandardOutput = true;
            si.RedirectStandardError = true;
            using (Process p = Process.Start(si))
            {
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                if (!p.WaitForExit(20000)) { try { p.Kill(); } catch { } }
                return o;
            }
        }

        // shadPS4 users.json: the first user (1000) gets the shadNet name/password and shadNet on.
        static void SaveSeamlessAccount(string name, string pass)
        {
            string txt = File.ReadAllText(UsersPath);
            File.Copy(UsersPath, UsersPath + ".bak-launcher", true);
            txt = new Regex("(\"shadnet_npid\"\\s*:\\s*)\"[^\"]*\"").Replace(txt, "${1}\"" + JsonEscape(name) + "\"", 1);
            txt = new Regex("(\"shadnet_password\"\\s*:\\s*)\"(?:[^\"\\\\]|\\\\.)*\"").Replace(txt, "${1}\"" + JsonEscape(pass).Replace("$", "$$") + "\"", 1);
            txt = new Regex("(\"shadnet_enabled\"\\s*:\\s*)(true|false)").Replace(txt, "${1}true", 1);
            File.WriteAllText(UsersPath, txt, new UTF8Encoding(false));
        }

        void DrawOnline(Graphics g)
        {
            bool seam = otab == "seamless";
            DrawTabbedPanel(g, I18n.T("اللعب عبر الإنترنت"), otab, hOTabN, hOTabS, seam
                ? I18n.T("حساب Seamless على سيرفر BBSC · كل اللي تلعب معهم لازم يكونون على نفس السيرفر والإصدار")
                : I18n.T("BBport عبر The Hunter's Dream (رسائل، بقع دم، أشباح، أجراس) · سكّر لانشر BBport وأنت تسجّل الدخول"));
            using (Pen p = new Pen(Color.FromArgb(110, Theme.Gold))) g.DrawLine(p, 615, 160, 615, H - 80);

            using (Font fb = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold))
            using (Font fr = new Font("Segoe UI", 10.5f))
            {
                if (!seam)
                {
                    Switch(g, hOnN, I18n.T("اللعب عبر الإنترنت"), JsonBool(JsonPath, "online") == "true");
                    Txt(g, I18n.T("مجموعة اللعب"), fb, new Rectangle(640, 230, 510, 28), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                    string pool = Settings.ReadJsonString(JsonPath, "online_pool") ?? "";
                    Segment(g, hPoolThd, "The Hunter's Dream", pool == "huntersdream");
                    Segment(g, hPoolShad, "shadNet", pool == "shadnet");
                    string thd = Settings.ReadJsonString(JsonPath, "thd_online_id") ?? "";
                    string tok = Settings.ReadJsonString(JsonPath, "thd_token") ?? "";
                    Txt(g, I18n.T("حساب The Hunter's Dream"), fb, new Rectangle(640, 330, 510, 28), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                    Txt(g, thd == "" ? I18n.T("ما سجّلت دخول") : thd + (tok != "" ? I18n.T("   ✓ مسجّل") : I18n.T("   (سجّل دخول من جديد)")), fr,
                        new Rectangle(640, 362, 510, 26), tok != "" ? Theme.Green : Theme.GoldLight, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                    Segment(g, hDiscordSign, I18n.T("تسجيل الدخول بـ Discord"), signBusy);
                    Segment(g, hTestN, I18n.T("اختبار الاتصال"), testBusy);
                    if (!string.IsNullOrEmpty(thdCode)) { DrawSignSteps(g); return; }
                }
                else
                {
                    try { sCfg = File.ReadAllText(SCfgPath); } catch { sCfg = ""; }
                    Switch(g, hOnS, I18n.T("اللعب عبر الإنترنت"), CfgValue("connected_to_network") == "true" && CfgValue("shad_net_enabled") == "true");
                    Txt(g, I18n.T("السيرفر:  ") + SeamHost + ":" + SeamPort, fr, new Rectangle(640, 218, 510, 26), Theme.TextDim, TextFormatFlags.Right);
                    string cur = SeamlessUser();
                    Txt(g, cur == "" ? I18n.T("الحساب الحالي: ما فيه") : I18n.T("الحساب الحالي:  ") + cur + "   ✓", fb, new Rectangle(640, 252, 510, 28),
                        cur == "" ? Theme.GoldLight : Theme.Green, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                    Txt(g, I18n.T("الاسم"), fr, new Rectangle(990, 340, 160, 30), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                    Txt(g, I18n.T("كلمة المرور"), fr, new Rectangle(990, 410, 160, 30), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                    Txt(g, I18n.T("الإيميل (اختياري)"), fr, new Rectangle(990, 480, 160, 30), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                    using (Font fs = new Font("Segoe UI", 9f))
                    {
                        Txt(g, I18n.T("3-16 حرف إنجليزي أو رقم أو - أو _"), fs, new Rectangle(640, 372, 340, 18), Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.RightToLeft);
                        Txt(g, I18n.T("استخدم كلمة مرور ما تستخدمها في مكان ثاني"), fs, new Rectangle(640, 442, 340, 18), Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.RightToLeft);
                    }
                    Segment(g, hLogin, I18n.T("تسجيل الدخول"), false);
                    Segment(g, hRegister, I18n.T("حساب جديد"), false);
                    Segment(g, hTestS, I18n.T("اختبار الاتصال"), testBusy);
                }
                if (testMsg != "")
                    TextRenderer.DrawText(g, testMsg, fr, new Rectangle(100, seam ? 244 : 300, 490, 80), testMsgColor, TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping |
                        TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.WordBreak);
                if (onlineMsg != "")
                    TextRenderer.DrawText(g, onlineMsg, fb, new Rectangle(100, 390, 490, 165), onlineMsgColor, TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping |
                        TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.WordBreak);
            }
        }

        // ---------- saves page: characters of each mode, backups and restore ----------

        class SaveChar
        {
            public int Slot, Level, Echoes, Insight, Deaths;
            public string Name;
            public int[] Stats; // vitality, endurance, strength, skill, bloodtinge, arcane
            public DateTime Changed;
        }

        string stab = "normal";
        Hot hSTabN, hSTabS, hBackupNow, hOpenSaves;
        List<Hot> saveHots = new List<Hot>();
        string saveMsg = "";
        Color saveMsgColor = Theme.TextDim;
        List<SaveChar> charCache;
        string charCacheTab;

        void BuildSavesHots()
        {
            hSTabN = Add(new Rectangle(80, 84, 170, 36), delegate { stab = "normal"; charCache = null; saveMsg = ""; Invalidate(); }, null);
            hSTabS = Add(new Rectangle(258, 84, 170, 36), delegate { stab = "seamless"; charCache = null; saveMsg = ""; Invalidate(); }, null);
            hSTabN.Page = hSTabS.Page = "saves";
            hBackupNow = Add(new Rectangle(80, 160, 200, 40), delegate { BackupMode(stab); }, null);
            hOpenSaves = Add(new Rectangle(290, 160, 200, 40), delegate { OpenFolder(SaveDirOf(stab)); }, null);
            hBackupNow.Page = hOpenSaves.Page = "saves";
        }

        string SaveDirOf(string tab) { return tab == "seamless" ? cfg.SeamlessSaveDir() : cfg.NormalSaveDir(); }
        static string ZipNameOf(string tab) { return tab == "seamless" ? "seamless-shadps4.zip" : "normal-bbport.zip"; }

        // Bloodborne character slot (userdata0000-0009): name is UTF-16 with the level 0x18 bytes before it,
        // the six stats every 8 bytes from name-0x68 (one gap), insight at name-0x24, blood echoes at name-0x14.
        static SaveChar ReadChar(string file, int slot)
        {
            try
            {
                byte[] d = File.ReadAllBytes(file);
                bool any = false;
                for (int i = 0; i < 64 && i < d.Length; i++) if (d[i] != 0) { any = true; break; }
                if (!any) return null;
                int lim = Math.Min(d.Length - 40, 0x20000);
                for (int o = 0x80; o < lim; o += 2)
                {
                    int n = 0;
                    while (n < 16 && o + n * 2 + 1 < d.Length && d[o + n * 2] >= 0x20 && d[o + n * 2] <= 0x7e && d[o + n * 2 + 1] == 0) n++;
                    if (n < 3 || d[o + n * 2] != 0 || d[o + n * 2 + 1] != 0 || (o >= 2 && d[o - 2] != 0)) continue;
                    int lvl = BitConverter.ToInt32(d, o - 0x18);
                    if (lvl < 1 || lvl > 800) continue;
                    int[] st = new int[6];
                    int[] offs = { 0x68, 0x60, 0x50, 0x48, 0x40, 0x38 };
                    bool ok = true;
                    for (int k = 0; k < 6; k++) { st[k] = BitConverter.ToInt32(d, o - offs[k]); if (st[k] < 1 || st[k] > 99) ok = false; }
                    if (!ok) continue;
                    SaveChar c = new SaveChar();
                    c.Slot = slot; c.Level = lvl; c.Stats = st;
                    c.Name = Encoding.Unicode.GetString(d, o, n * 2);
                    c.Insight = BitConverter.ToInt32(d, o - 0x24);
                    c.Echoes = BitConverter.ToInt32(d, o - 0x14);
                    c.Deaths = o + 0x10d28 + 4 <= d.Length ? BitConverter.ToInt32(d, o + 0x10d28) : -1; // death counter
                    if (c.Deaths < 0 || c.Deaths > 999999) c.Deaths = -1;
                    c.Changed = File.GetLastWriteTime(file);
                    return c;
                }
            }
            catch { }
            return null;
        }

        List<SaveChar> Characters(string tab)
        {
            if (charCache != null && charCacheTab == tab) return charCache;
            List<SaveChar> r = new List<SaveChar>();
            string dir = SaveDirOf(tab);
            for (int i = 0; i < 10; i++)
            {
                string f = Path.Combine(dir, "userdata" + i.ToString("0000"));
                if (!File.Exists(f)) continue;
                SaveChar c = ReadChar(f, i + 1);
                if (c != null) r.Add(c);
            }
            charCache = r; charCacheTab = tab;
            return r;
        }

        List<string> BackupsOf(string tab)
        {
            List<string> r = new List<string>();
            try
            {
                if (!Directory.Exists(cfg.BackupDir)) return r;
                foreach (string d in Directory.GetDirectories(cfg.BackupDir))
                    if (File.Exists(Path.Combine(d, ZipNameOf(tab)))) r.Add(d);
                r.Sort(); r.Reverse();
            }
            catch { }
            return r;
        }

        string BackupMode(string tab)
        {
            string dir = SaveDirOf(tab);
            if (!Directory.Exists(dir)) { saveMsg = I18n.T("ما لقيت مجلد الحفظ"); saveMsgColor = Theme.GoldLight; Invalidate(); return null; }
            try
            {
                string dest = Path.Combine(cfg.BackupDir, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Directory.CreateDirectory(dest);
                ZipFile.CreateFromDirectory(dir, Path.Combine(dest, ZipNameOf(tab)), CompressionLevel.Optimal, true);
                saveMsg = I18n.T("✓ انحفظت نسخة احتياطية: ") + Path.GetFileName(dest);
                saveMsgColor = Theme.Green;
                Invalidate();
                return dest;
            }
            catch (Exception ex) { saveMsg = "✗ " + ex.Message; saveMsgColor = Theme.GoldLight; Invalidate(); return null; }
        }

        static bool GameRunning()
        {
            foreach (string n in new string[] { "shadPS4", "bb-probe" })
                try { if (Process.GetProcessesByName(n).Length > 0) return true; } catch { }
            return false;
        }

        void RestoreBackup(string folder)
        {
            if (GameRunning())
            {
                MessageBox.Show(this, I18n.T("سكّر اللعبة أول، وبعدها استرجع النسخة."), I18n.T("استرجاع"), MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1, I18n.MsgOpt);
                return;
            }
            DialogResult r = MessageBox.Show(this,
                I18n.T("ترجّع حفظ ") + (stab == "seamless" ? "Seamless Co-op" : "BBport") + I18n.T(" لنسخة ") + Path.GetFileName(folder) + I18n.T("؟\n\n") +
                I18n.T("حفظك الحالي بينحفظ نسخة احتياطية قبل، عشان تقدر ترجع له."),
                I18n.T("استرجاع"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2, I18n.MsgOpt);
            if (r != DialogResult.Yes) return;
            string dir = SaveDirOf(stab);
            string tmp = Path.Combine(Path.GetTempPath(), "bb-restore-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (Directory.Exists(dir)) BackupMode(stab);
                ZipFile.ExtractToDirectory(Path.Combine(folder, ZipNameOf(stab)), tmp);
                string src = tmp;
                string[] subs = Directory.GetDirectories(tmp);
                if (subs.Length == 1 && Directory.GetFiles(tmp).Length == 0) src = subs[0]; // zip made with its base folder
                CopyTree(src, dir);
                charCache = null;
                saveMsg = I18n.T("✓ رجع الحفظ لنسخة ") + Path.GetFileName(folder);
                saveMsgColor = Theme.Green;
            }
            catch (Exception ex) { saveMsg = "✗ " + ex.Message; saveMsgColor = Theme.GoldLight; }
            finally { try { Directory.Delete(tmp, true); } catch { } }
            Invalidate();
        }

        static void CopyTree(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (string d in Directory.GetDirectories(src)) CopyTree(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        static string Stamp(string folder)
        {
            DateTime t;
            if (DateTime.TryParseExact(Path.GetFileName(folder), "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                return t.ToString("yyyy/MM/dd  HH:mm", CultureInfo.InvariantCulture);
            return Path.GetFileName(folder);
        }

        void DrawSaves(Graphics g)
        {
            bool seam = stab == "seamless";
            DrawTabbedPanel(g, I18n.T("ملفات الحفظ"), stab, hSTabN, hSTabS,
                I18n.T("الاسترجاع يحفظ نسختك الحالية أول · سكّر اللعبة قبل الاسترجاع · ") + SaveDirOf(stab));
            using (Pen p = new Pen(Color.FromArgb(110, Theme.Gold))) g.DrawLine(p, 510, 160, 510, H - 80);

            hots.RemoveAll(delegate (Hot h) { return saveHots.Contains(h); });
            saveHots.Clear();

            Segment(g, hBackupNow, I18n.T("نسخة احتياطية الحين"), false);
            Segment(g, hOpenSaves, I18n.T("فتح مجلد الحفظ"), false);

            using (Font fb = new Font("Segoe UI Semibold", 11f, FontStyle.Bold))
            using (Font fr = new Font("Segoe UI", 10f))
            {
                if (saveMsg != "") Txt(g, saveMsg, fb, new Rectangle(80, 206, 410, 26), saveMsgColor, TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
                Txt(g, I18n.T("النسخ الاحتياطية"), fb, new Rectangle(80, 236, 410, 26), Theme.Gold, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                List<string> bks = BackupsOf(stab);
                if (bks.Count == 0) Txt(g, I18n.T("ما فيه نسخ للحين"), fr, new Rectangle(80, 266, 410, 26), Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                for (int i = 0; i < bks.Count; i++)
                {
                    int y = 266 + i * 34;
                    string folder = bks[i];
                    Txt(g, Stamp(folder), fr, new Rectangle(190, y, 300, 30), Theme.Text, TextFormatFlags.Right);
                    Hot h = new Hot(new Rectangle(80, y + 2, 100, 26), delegate { RestoreBackup(folder); }, I18n.T("يرجّع الحفظ لهذي النسخة"));
                    h.Page = "saves:" + stab;
                    saveHots.Add(h); hots.Add(h);
                    Segment(g, h, I18n.T("استرجاع"), false);
                    UseHeight(y + 30 + 50);
                }
            }

            List<SaveChar> chars = Characters(stab);
            if (chars.Count == 0)
            {
                using (Font f = new Font("Segoe UI Semibold", 12f, FontStyle.Bold))
                    Txt(g, I18n.T("ما فيه شخصيات في حفظ هذا الوضع"), f, new Rectangle(530, 300, 620, 40), Theme.GoldLight, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
                return;
            }
            string[] statNames = { "Vitality", "Endurance", "Strength", "Skill", "Bloodtinge", "Arcane" };
            using (Font fh = new Font("Segoe UI Semibold", 12f, FontStyle.Bold))
                Txt(g, I18n.T("شخصياتك (") + chars.Count + ")", fh, new Rectangle(530, 150, 620, 28), Theme.Gold, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
            int cardH = 140;
            for (int i = 0; i < chars.Count; i++)
            {
                SaveChar c = chars[i];
                Rectangle card = new Rectangle(530, 184 + i * (cardH + 10), 620, cardH);
                FillRound(g, card, 10, Color.FromArgb(185, 18, 15, 20), Color.FromArgb(120, Theme.GoldDim), 1f);
                UseHeight(card.Bottom + 50);
                using (Font fn = new Font("Segoe UI Semibold", 15f, FontStyle.Bold))
                using (Font fl = new Font(Res.Cinzel(), 14f, FontStyle.Bold))
                using (Font fs = new Font("Segoe UI", 9f))
                using (Font fv = new Font("Segoe UI Semibold", 13f, FontStyle.Bold))
                {
                    Txt(g, c.Name, fn, new Rectangle(card.X + 16, card.Y + 8, 300, 30), Theme.Text, TextFormatFlags.Left);
                    Txt(g, "Level " + c.Level, fl, new Rectangle(card.Right - 216, card.Y + 8, 200, 30), Theme.GoldLight, TextFormatFlags.Right);
                    Txt(g, I18n.T("الخانة ") + c.Slot + "  ·  " + c.Changed.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture) +
                        "  ·  Blood Echoes " + c.Echoes + "  ·  Insight " + c.Insight + (c.Deaths >= 0 ? I18n.T("  ·  مرات الموت ") + c.Deaths : ""), fs,
                        new Rectangle(card.X + 16, card.Y + 38, card.Width - 32, 20), Theme.TextDim, TextFormatFlags.Left);
                    int bw = (card.Width - 32 - 5 * 6) / 6;
                    for (int k = 0; k < 6; k++)
                    {
                        Rectangle b = new Rectangle(card.X + 16 + k * (bw + 6), card.Y + 62, bw, cardH - 74);
                        FillRound(g, b, 6, Color.FromArgb(200, 10, 8, 12), Color.Empty, 0);
                        Txt(g, statNames[k], fs, new Rectangle(b.X + 8, b.Y + 4, b.Width - 8, 18), Theme.TextDim, TextFormatFlags.Left);
                        Txt(g, c.Stats[k].ToString(), fv, new Rectangle(b.X + 8, b.Y + 22, b.Width - 8, b.Height - 26), Theme.Text, TextFormatFlags.Left);
                    }
                }
            }
        }

        // ---------- settings page ----------

        static int SetRowY(int i) { return 146 + i * 86; }

        void ShowPage(string p)
        {
            string tab = cfg.Mode == "seamless" ? "seamless" : "normal";
            if (p == "graphics" && page != "graphics") { gtab = tab; LoadGraphics(); }
            if (p == "controls" && page != "controls") ctab = tab;
            if (p == "saves" && page != "saves") { stab = tab; charCache = null; saveMsg = ""; }
            if (p == "online" && page != "online") { otab = tab; if (!signBusy) onlineMsg = ""; testMsg = ""; }
            capturing = null;
            page = p; hover = null;
            if (p == "home") player = cfg.PlayerName();
            scrollY = 0; lastContentBottom = 0;
            promptOpen = false;
            gDrop = -1;
            langOpen = false;
            UpdateBoxes();
            Invalidate();
        }

        string PageId()
        {
            if (page == "graphics") return "graphics:" + gtab;
            if (page == "controls") return "controls:" + ctab;
            if (page == "online") return "online:" + otab;
            if (page == "saves") return "saves:" + stab;
            return page;
        }

        void DrawSettings(Graphics g)
        {
            Rectangle panel = new Rectangle(40, 70, W - 140, Math.Max(H - 100, SetRowY(7) - 70));
            UseHeight(panel.Bottom + 20);
            FillRound(g, panel, 16, Color.FromArgb(205, 8, 6, 10), Color.FromArgb(150, Theme.GoldDim), 1.4f);
            using (Font f = new Font("Segoe UI Semibold", 20f, FontStyle.Bold))
                Txt(g, I18n.T("الإعدادات"), f, new Rectangle(panel.X + 40, panel.Y + 18, panel.Width - 80, 50), Theme.GoldLight, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
            using (Pen p = new Pen(Color.FromArgb(110, Theme.Gold))) g.DrawLine(p, panel.X + 30, panel.Y + 74, panel.Right - 30, panel.Y + 74);

            string upd = checking ? I18n.T("يتأكد الحين ...") :
                ("BBport: " + (upN == null ? I18n.T("؟") : (upN.Available ? I18n.T("تحديث جديد ") + upN.Latest : I18n.T("محدّث ") + upN.Latest)) +
                 "   ·   BBSC: " + (upS == null ? I18n.T("؟") : (upS.Available ? I18n.T("تحديث جديد ") + upS.Latest : I18n.T("محدّث ") + upS.Latest)) +
                 I18n.T("   ·   اللانشر: ") + AppVersion + (upL != null && upL.Available ? I18n.T(" (تحديث جديد ") + upL.Latest + ")" : ""));
            string[] titles = { I18n.T("لانشر BBport"), I18n.T("لانشر BBSC"), I18n.T("مجلد BBport"), I18n.T("مجلد المود"), I18n.T("التحديثات"), I18n.T("مجلد اللعبة") };
            string[] descs = {
                I18n.T("إعدادات BBport: الرسوميات، الأزرار، الأونلاين، وتحديث BBport"),
                I18n.T("إعدادات Seamless Co-op: الحساب، السيرفر، والباتشات"),
                Path.GetDirectoryName(cfg.BBportExe),
                Path.GetDirectoryName(cfg.SeamlessCmd),
                upd,
                File.Exists(cfg.GameEboot) ? Path.GetDirectoryName(cfg.GameEboot) : I18n.T("ما انحدد للحين - اختار مجلد Bloodborne حقك (فيه eboot.bin)") };
            string[] btns = { I18n.T("فتح"), I18n.T("فتح"), I18n.T("فتح"), I18n.T("فتح"), I18n.T("تحقق الحين"), I18n.T("تغيير") };
            // language: the first row
            {
                int y = SetRowY(0);
                using (Font f = new Font("Segoe UI Semibold", 13f, FontStyle.Bold))
                    Txt(g, I18n.T("اللغة") + "  ·  Language", f, new Rectangle(340, y + 10, panel.Right - 380, 30), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                using (Font f = new Font("Segoe UI", 10.5f))
                    Txt(g, I18n.T("لغة اللانشر - يعيد فتح اللانشر بعد التغيير"), f, new Rectangle(340, y + 44, panel.Right - 380, 26), Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
                bool hvl = hover == hLang || langOpen;
                FillRound(g, hLang.R, 8, Color.FromArgb(200, 24, 22, 28), hvl ? Theme.Gold : Color.FromArgb(150, Theme.GoldDim), hvl ? 1.6f : 1.1f);
                using (Font f = new Font("Segoe UI Semibold", 11f, FontStyle.Bold))
                    Txt(g, I18n.Names[Math.Max(0, Array.IndexOf(I18n.Codes, I18n.Code))], f, new Rectangle(hLang.R.X + 14, hLang.R.Y, hLang.R.Width - 50, hLang.R.Height), Theme.Text, TextFormatFlags.Left);
                Glyph(g, langOpen ? "\uE70E" : "\uE70D", new Rectangle(hLang.R.Right - 34, hLang.R.Y, 26, hLang.R.Height), Theme.Gold, 10f);
                using (Pen p = new Pen(Color.FromArgb(60, Theme.GoldDim))) g.DrawLine(p, panel.X + 30, y + 80, panel.Right - 30, y + 80);
            }
            for (int i = 0; i < 6; i++)
            {
                int y = SetRowY(i + 1);
                using (Font f = new Font("Segoe UI Semibold", 13f, FontStyle.Bold))
                    Txt(g, titles[i], f, new Rectangle(260, y + 10, panel.Right - 300, 30), Theme.Text, TextFormatFlags.Right | TextFormatFlags.RightToLeft);
                bool path = i == 2 || i == 3 || (i == 5 && File.Exists(cfg.GameEboot));
                using (Font f = path ? new Font("Consolas", 10f) : new Font("Segoe UI", 10.5f))
                    Txt(g, descs[i], f, new Rectangle(260, y + 44, panel.Right - 300, 26), Theme.TextDim,
                        path ? TextFormatFlags.Right | TextFormatFlags.PathEllipsis : TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
                Hot b = setBtns[i];
                bool hv = hover == b;
                FillRound(g, b.R, 8, hv ? Color.FromArgb(220, Theme.BloodDark) : Color.FromArgb(150, 14, 10, 14),
                    hv ? Theme.Gold : Color.FromArgb(170, Theme.GoldDim), hv ? 2f : 1.3f);
                using (Font f = new Font("Segoe UI Semibold", 11f, FontStyle.Bold))
                    Txt(g, btns[i], f, b.R, hv ? Theme.GoldLight : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.RightToLeft);
                if (i < 5)
                    using (Pen p = new Pen(Color.FromArgb(60, Theme.GoldDim))) g.DrawLine(p, panel.X + 30, y + 80, panel.Right - 30, y + 80);
            }
            if (langOpen)
            {
                Rectangle list = new Rectangle(hLang.R.X, hLang.R.Bottom + 2, hLang.R.Width, langHots.Length * 32);
                FillRound(g, list, 8, Color.FromArgb(250, 22, 20, 26), Theme.GoldDim, 1.2f);
                using (Font f = new Font("Segoe UI Semibold", 11f, FontStyle.Bold))
                    for (int i = 0; i < langHots.Length; i++)
                    {
                        Hot o = langHots[i];
                        bool sel = I18n.Codes[i] == I18n.Code, hv = hover == o;
                        if (sel || hv) { Rectangle r = o.R; r.Inflate(-3, -2); FillRound(g, r, 6, sel ? Color.FromArgb(230, Theme.Blood) : Color.FromArgb(120, Theme.BloodDark), Color.Empty, 0); }
                        Txt(g, I18n.Names[i], f, new Rectangle(o.R.X + 14, o.R.Y, o.R.Width - 28, o.R.Height), sel || hv ? Color.White : Theme.Text, TextFormatFlags.Left);
                    }
                UseHeight(list.Bottom + 30);
            }
        }

        // ---------- mouse ----------

        Hot HitTest(Point raw)
        {
            string id = PageId();
            bool inContent = raw.Y >= 54 && raw.X < W - 92;
            Point p = new Point(raw.X, raw.Y + (page == "home" ? 0 : scrollY));
            if (!inContent && page != "home") p = new Point(-9999, -9999); // page buttons are clipped outside the content area
            foreach (Hot h in hots) // rail and title bar: never scrolled
                if (h.Page == null && h != hCtlReset && h != hSensMinus && h != hSensPlus && h != hSensReset && h.R.Contains(raw)) return h;
            if (PageLocked())
            {
                foreach (Hot t in new Hot[] { hGTabN, hGTabS, hCTabN, hCTabS })
                    if (t != null && (t.Page == page) && t.R.Contains(p)) return t;
                return null;
            }
            if (langOpen && page == "settings")
            {
                foreach (Hot o in langHots) if (o.R.Contains(p)) return o;
                if (hLang.R.Contains(p)) return hLang;
                return null;
            }
            if (gDrop >= 0 && page == "graphics" && gtab == "normal")
            {
                foreach (Hot o in gN[gDrop].Items) if (o.R.Contains(p)) return o;
                if (gN[gDrop].Box.R.Contains(p)) return gN[gDrop].Box;
                return null;
            }
            if (promptOpen && page == "controls")
            {
                foreach (Hot o in promptHots) if (o.R.Contains(p)) return o;
                if (hPromptBox.R.Contains(p)) return hPromptBox;
                return null;
            }
            if (hCtlReset != null && hCtlReset.R.Contains(p)) return page == "controls" && capturing == null ? hCtlReset : null;
            foreach (Hot sh in new Hot[] { hSensMinus, hSensPlus, hSensReset })
                if (sh != null && sh.R.Contains(p)) return page == "controls" && capturing == null ? sh : null;
            if (string.IsNullOrEmpty(thdCode) && id == "online:normal" &&
                ((hCopyCode != null && hCopyCode.R.Contains(p)) || (hOpenLink != null && hOpenLink.R.Contains(p)))) return null;
            foreach (Hot h in hots) if ((h.Page == page || h.Page == id) && h.R.Contains(p)) return h;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            e = M(e);
            Hot h = HitTest(e.Location);
            if (h != hover)
            {
                hover = h;
                Cursor = h != null ? Cursors.Hand : Cursors.Default;
                tip.SetToolTip(this, h != null ? h.Tip : null);
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = null; pressed = null; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            e = M(e);
            if (CaptureMouse(e)) return;
            if (promptOpen && HitTest(e.Location) == null) { promptOpen = false; Invalidate(); return; }
            if (gDrop >= 0 && HitTest(e.Location) == null) { gDrop = -1; Invalidate(); return; }
            if (langOpen && HitTest(e.Location) == null) { langOpen = false; Invalidate(); return; }
            if (e.Button == MouseButtons.Right && page == "controls")
            {
                Hot rh = HitTest(e.Location);
                if (rh != null && slotAct.ContainsKey(rh)) { SetSlotBinding(slotAct[rh], slotIdx[rh], null); Flash(rh); }
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            Hot h = HitTest(e.Location);
            if (h == null && e.Y < 52 && !big)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 2, 0); // drag the window by its title bar
                return;
            }
            pressed = h;
            if (h != null && (h == hSensMinus || h == hSensPlus || repeatHots.Contains(h)))
            {
                // hold to keep changing: once now, then repeat after a short pause
                h.Click();
                repeatHot = h;
                if (repeatTimer == null)
                {
                    repeatTimer = new System.Windows.Forms.Timer();
                    repeatTimer.Tick += delegate
                    {
                        repeatTimer.Interval = 70;
                        if (repeatHot != null && repeatHot.Click != null && (MouseButtons & MouseButtons.Left) != 0) repeatHot.Click();
                        else { repeatTimer.Stop(); repeatHot = null; }
                    };
                }
                repeatTimer.Stop();
                repeatTimer.Interval = 400;
                repeatTimer.Start();
            }
            Invalidate();
        }

        Hot repeatHot;
        System.Windows.Forms.Timer repeatTimer;

        protected override void OnMouseUp(MouseEventArgs e)
        {
            e = M(e);
            if (swallowMouseUp) { swallowMouseUp = false; return; }
            if (repeatHot != null)
            {
                repeatTimer.Stop();
                repeatHot = null; pressed = null;
                Invalidate();
                return; // already applied while held
            }
            Hot h = HitTest(e.Location);
            Hot was = pressed;
            pressed = null;
            Invalidate();
            if (h != null && was != null && h.R == was.R && h.Page == was.Page && h.Click != null) { Flash(h); h.Click(); }
        }

        // ---------- actions ----------

        void SetMode(string m) { cfg.Mode = m; cfg.Save(); player = cfg.PlayerName(); Invalidate(); }

        void Play()
        {
            if (installing) return;
            if (NeedsInstall()) { StartInstall(); return; }
            if (cfg.Mode == "normal") PlayNormal(); else LaunchSeamless();
        }

        // ---------- download & install a mod (for a copy of the launcher on a friend's PC) ----------

        bool installing;
        string installTag;
        int installPct;
        string installMsg = "";

        bool NeedsInstall()
        {
            return cfg.Mode == "normal" ? !File.Exists(cfg.BBportExe) : !File.Exists(cfg.SeamlessEmu);
        }

        void InstallUI(string msg, int pct)
        {
            try { BeginInvoke((MethodInvoker)delegate { installMsg = msg; installPct = pct; Invalidate(); }); } catch { }
        }

        void StartInstall()
        {
            bool seam = cfg.Mode == "seamless";
            string name = seam ? "Seamless Co-op (BBSC)" : "BBport";
            string target = Path.Combine(Settings.BaseDir, seam ? "BBSC" : "BBport");
            if (MessageBox.Show(this,
                    name + I18n.T(" مو موجود على جهازك.\n\n") +
                    I18n.T("بيتحمّل آخر إصدار من صفحته الرسمية في GitHub ويتركّب هنا:\n") + target + "\n\n" +
                    I18n.T("اللعبة نفسها مو داخلة: تحتاج نسختك من Bloodborne (CUSA03173 إصدار 1.09).\n\nنبدأ التحميل؟"),
                    I18n.T("تحميل ") + name, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1,
                    I18n.MsgOpt) != DialogResult.Yes) return;
            installing = true; installPct = 0; installMsg = I18n.T("يدوّر على آخر إصدار ..."); Invalidate();
            Thread th = new Thread(delegate ()
            {
                string err = null, page = null, done = null, tagv = null;
                try
                {
                    string[] r = Releases.Find(seam);
                    if (r == null) throw new Exception(I18n.T("ما لقيت إصدار في GitHub"));
                    page = r[2];
                    tagv = r[1];
                    if (r[0] == null) throw new Exception(I18n.T("ما لقيت ملف zip في صفحة الإصدار"));
                    string tmp = Path.Combine(Settings.BaseDir, "_download");
                    Directory.CreateDirectory(tmp);
                    string zip = Path.Combine(tmp, Path.GetFileName(r[0]));
                    Download(r[0], zip, name + " " + r[1]);
                    InstallUI(I18n.T("يفك الضغط ..."), 100);
                    string ex = Path.Combine(tmp, "x");
                    if (Directory.Exists(ex)) Directory.Delete(ex, true);
                    ZipFile.ExtractToDirectory(zip, ex);
                    string root = ex;
                    if (Directory.GetDirectories(ex).Length == 1 && Directory.GetFiles(ex).Length == 0) root = Directory.GetDirectories(ex)[0];
                    if (Directory.Exists(target)) Directory.Move(target, target + ".old-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                    Directory.Move(root, target);
                    try { Directory.Delete(tmp, true); } catch { }
                    done = target;
                }
                catch (Exception e) { err = e.Message; }
                try { BeginInvoke((MethodInvoker)delegate { installTag = tagv; InstallDone(seam, done, err, page); }); } catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        void Download(string url, string file, string label)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url);
            rq.UserAgent = "BloodborneLauncher/2.0";
            rq.AllowAutoRedirect = true;
            using (HttpWebResponse rs = (HttpWebResponse)rq.GetResponse())
            using (Stream src = rs.GetResponseStream())
            using (FileStream dst = File.Create(file))
            {
                long total = rs.ContentLength, got = 0;
                byte[] buf = new byte[1 << 16];
                int n, last = -1;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, n);
                    got += n;
                    int pct = total > 0 ? (int)(got * 100 / total) : 0;
                    if (pct != last)
                    {
                        last = pct;
                        InstallUI(I18n.T("يحمّل ") + label + "  ·  " + (got >> 20) + (total > 0 ? " / " + (total >> 20) : "") + " MB", pct);
                    }
                }
            }
        }

        void InstallDone(bool seam, string dir, string err, string page)
        {
            installing = false; installMsg = "";
            if (err != null || dir == null)
            {
                MessageBox.Show(this, I18n.T("ما قدرت أحمّله تلقائياً:\n") + err + I18n.T("\n\nبفتح لك صفحة التحميل، نزّل ملف الـ zip وفكّه في:\n") +
                    Path.Combine(Settings.BaseDir, seam ? "BBSC" : "BBport"),
                    "Bloodborne Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, I18n.MsgOpt);
                Open(page ?? (seam ? "https://github.com/CLSAMG22/BBSC/releases" : "https://github.com/Supermedo/bloodborne_pc/releases"));
                Invalidate();
                return;
            }
            if (seam)
            {
                string cmd = Settings.FindFile(dir, "PLAY BBSC.cmd");
                if (cmd != null)
                {
                    cfg.SeamlessCmd = cmd;
                    cfg.SeamlessEmu = Path.Combine(Path.GetDirectoryName(cmd), @"emulator\shadPS4.exe");
                }
                cfg.Save();
                bbsc = cfg.BBSCVersion();
                if (!File.Exists(cfg.GameEboot)) PickGame();
                MessageBox.Show(this,
                    I18n.T("تم تحميل Seamless Co-op.\n\n") +
                    I18n.T("الحين بتفتح نافذة سوداء تجهّز اللعبة للسيرفر (مرة وحدة بس):\n") +
                    I18n.T("- لما تسألك عن الجدار الناري اكتب y واضغط Enter\n") +
                    I18n.T("- وبعدها اضغط Enter وتتسكّر النافذة\n\n") +
                    I18n.T("بعدها سوّ حسابك من صفحة الأونلاين هنا في اللانشر، وتقدر تلعب."),
                    "Seamless Co-op", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, I18n.MsgOpt);
                if (File.Exists(cfg.SeamlessCmd))
                {
                    try
                    {
                        // only the package's first-start scripts (server, firewall), not BBSC's own launcher
                        string tools = Path.Combine(Path.GetDirectoryName(cfg.SeamlessCmd), "tools");
                        string ps = "powershell -NoProfile -ExecutionPolicy Bypass -File ";
                        ProcessStartInfo si = new ProcessStartInfo("cmd.exe", "/c \"" + ps + "\"" + Path.Combine(tools, "migrate-public-server.ps1") + "\" & " +
                            ps + "\"" + Path.Combine(tools, "first-start.ps1") + "\"\"");
                        si.WorkingDirectory = Path.GetDirectoryName(cfg.SeamlessCmd);
                        si.UseShellExecute = true;
                        Process.Start(si);
                    }
                    catch (Exception ex) { Fail(ex); }
                }
            }
            else
            {
                string exe = Settings.FindFile(dir, "Bloodborne.exe");
                if (exe != null) cfg.BBportExe = exe;
                cfg.BBportVer = installTag ?? "";
                cfg.Save();
                MessageBox.Show(this,
                    I18n.T("تم تحميل BBport.\n\n") +
                    I18n.T("الحين بيفتح لانشر BBport أول مرة:\n") +
                    I18n.T("- من «Game & effects» اختار مجلد اللعبة حقك\n") +
                    I18n.T("- اضبط الإعدادات واضغط PLAY مرة وحدة\n\n") +
                    I18n.T("بعدها تقدر تلعب من هنا على طول."),
                    "BBport", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, I18n.MsgOpt);
                LaunchNormal(false);
            }
            StartUpdateCheck();
            Invalidate();
        }

        void SetLanguage(string lc)
        {
            if (lc == I18n.Code) return;
            cfg.Lang = lc;
            cfg.Save();
            try
            {
                ProcessStartInfo si = new ProcessStartInfo(Application.ExecutablePath);
                si.WorkingDirectory = Settings.BaseDir;
                si.UseShellExecute = true;
                Process.Start(si);
                Application.Exit();
            }
            catch (Exception ex) { Fail(ex); }
        }

        // ---------- update this launcher itself ----------

        void SelfUpdate()
        {
            if (installing || upL == null || !upL.Available) return;
            if (MessageBox.Show(this, I18n.T("نزلت نسخة جديدة من اللانشر: ") + upL.Latest + I18n.T(" (عندك ") + AppVersion + ").\n\n" +
                    I18n.T("بيتحمّل التحديث ويرجع يفتح اللانشر بالنسخة الجديدة.\nإعداداتك ومودّاتك تبقى زي ما هي.\n\nنحدّث الحين؟"),
                    I18n.T("تحديث اللانشر"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1,
                    I18n.MsgOpt) != DialogResult.Yes) return;
            installing = true; installPct = 0; installMsg = I18n.T("يحمّل تحديث اللانشر ..."); Invalidate();
            string url = upL.Zip, ver = upL.Latest;
            Thread th = new Thread(delegate ()
            {
                string err = null, dir = null;
                try
                {
                    string tmp = Path.Combine(Settings.BaseDir, "_selfupdate");
                    if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                    Directory.CreateDirectory(tmp);
                    string zip = Path.Combine(tmp, "update.zip");
                    Download(url, zip, I18n.T("اللانشر ") + ver);
                    InstallUI(I18n.T("يفك الضغط ..."), 100);
                    dir = Path.Combine(tmp, "x");
                    ZipFile.ExtractToDirectory(zip, dir);
                }
                catch (Exception e) { err = e.Message; }
                try { BeginInvoke((MethodInvoker)delegate { SelfUpdateApply(dir, err); }); } catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        void SelfUpdateApply(string dir, string err)
        {
            installing = false; installMsg = ""; Invalidate();
            string me = Application.ExecutablePath;
            string neu = err == null ? Settings.FindFile(dir, Path.GetFileName(me)) ?? Settings.FindFile(dir, "Bloodborne Launcher.exe") : null;
            if (neu == null)
            {
                MessageBox.Show(this, I18n.T("ما قدرت أحدّث اللانشر تلقائياً") + (err != null ? ":\n" + err : ".") + I18n.T("\n\nبفتح لك صفحة التحديث."),
                    I18n.T("تحديث اللانشر"), MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, I18n.MsgOpt);
                if (upL != null) Open(upL.Url);
                return;
            }
            try
            {
                // a running exe can be renamed but not overwritten: move it aside, put the new one in its place
                string old = me + ".old";
                if (File.Exists(old)) File.Delete(old);
                File.Move(me, old);
                File.Copy(neu, me);
                string src = Path.GetDirectoryName(neu);
                foreach (string f in Directory.GetFiles(src))
                {
                    string n = Path.GetFileName(f);
                    if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || n.Equals("launcher.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    File.Copy(f, Path.Combine(Settings.BaseDir, n), true);
                }
                string defs = Path.Combine(src, "defaults");
                if (Directory.Exists(defs))
                {
                    Directory.CreateDirectory(Path.Combine(Settings.BaseDir, "defaults"));
                    foreach (string f in Directory.GetFiles(defs)) File.Copy(f, Path.Combine(Settings.BaseDir, "defaults", Path.GetFileName(f)), true);
                }
                ProcessStartInfo si = new ProcessStartInfo(me);
                si.WorkingDirectory = Settings.BaseDir;
                si.UseShellExecute = true;
                Process.Start(si);
                Application.Exit();
            }
            catch (Exception ex)
            {
                try { if (!File.Exists(me) && File.Exists(me + ".old")) File.Move(me + ".old", me); } catch { }
                Fail(ex);
            }
        }

        // Puts the game's folder in shadPS4's game list (config.json install_dirs), so BBSC's own
        // launcher shows the game too.
        static void AddGameToShadList(string eboot)
        {
            try
            {
                string cfgPath = Path.Combine(ShadDir, "config.json");
                if (!File.Exists(cfgPath) || !File.Exists(eboot)) return;
                string game = Path.GetDirectoryName(eboot);
                string parent = Path.GetDirectoryName(game);
                string txt = File.ReadAllText(cfgPath);
                string orig = txt;
                foreach (string dir in new string[] { parent, game })
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    string js = dir.Replace('\\', '/');
                    if (txt.IndexOf("\"" + js + "\"", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    string entry = "{ \"enabled\": true, \"path\": \"" + js.Replace("\"", "\\\"") + "\" }";
                    Match m = Regex.Match(txt, "\"install_dirs\"\\s*:\\s*\\[(\\s*\\])?");
                    if (m.Success)
                        txt = txt.Substring(0, m.Index + m.Length - (m.Groups[1].Success ? m.Groups[1].Length : 0)) + entry + (m.Groups[1].Success ? "]" : ",") +
                              txt.Substring(m.Index + m.Length);
                    else
                    {
                        Match g = Regex.Match(txt, "\"General\"\\s*:\\s*\\{");
                        if (!g.Success) return;
                        txt = txt.Insert(g.Index + g.Length, "\n    \"install_dirs\": [" + entry + "],");
                    }
                }
                if (txt != orig) File.WriteAllText(cfgPath, txt, new UTF8Encoding(false));
            }
            catch { }
        }

        // The player's own game folder (the one with eboot.bin).
        bool PickGame()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = I18n.T("اختار مجلد Bloodborne حقك (المجلد اللي فيه eboot.bin و sce_sys)");
                d.ShowNewFolderButton = false;
                if (File.Exists(cfg.GameEboot)) d.SelectedPath = Path.GetDirectoryName(cfg.GameEboot);
                while (d.ShowDialog(this) == DialogResult.OK)
                {
                    string e = Settings.FindFile(d.SelectedPath, "eboot.bin");
                    if (e != null)
                    {
                        cfg.GameEboot = e;
                        cfg.Save();
                        AddGameToShadList(e);
                        try
                        {
                            if (File.Exists(JsonPath) && string.IsNullOrEmpty(Settings.ReadJsonString(JsonPath, "game_dir")))
                                JsonSet("game_dir", "\"" + JsonEscape(Path.GetDirectoryName(e)) + "\"");
                        }
                        catch { }
                        Invalidate();
                        return true;
                    }
                    MessageBox.Show(this, I18n.T("ما لقيت eboot.bin في هذا المجلد.\nاختار مجلد اللعبة نفسه (فيه eboot.bin و sce_sys و dvdroot_ps4)."),
                        I18n.T("مجلد اللعبة"), MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, I18n.MsgOpt);
                }
            }
            return false;
        }

        void AfterLaunch(string what)
        {
            status = I18n.T("تم تشغيل ") + what;
            Invalidate();
            if (cfg.MinimizeOnPlay) WindowState = FormWindowState.Minimized;
        }

        // Starts the game straight away with the settings saved in BBport's launcher
        // ("Play Bloodborne.exe", or "Bloodborne.exe --play" on builds without it).
        void PlayNormal()
        {
            string dir = Path.GetDirectoryName(cfg.BBportExe);
            string quick = Path.Combine(dir, "Play Bloodborne.exe");
            ProcessStartInfo si;
            if (File.Exists(quick)) si = new ProcessStartInfo(quick);
            else if (File.Exists(cfg.BBportExe)) si = new ProcessStartInfo(cfg.BBportExe, "--play");
            else { Missing(cfg.BBportExe); return; }
            si.WorkingDirectory = dir;
            si.UseShellExecute = true;
            try { Process.Start(si); AfterLaunch("Bloodborne (BBport)"); }
            catch (Exception ex) { Fail(ex); }
        }

        // BBport's own launcher: settings, updates.
        void LaunchNormal(bool minimize)
        {
            if (!File.Exists(cfg.BBportExe)) { Missing(cfg.BBportExe); return; }
            ProcessStartInfo si = new ProcessStartInfo(cfg.BBportExe);
            si.WorkingDirectory = Path.GetDirectoryName(cfg.BBportExe);
            si.UseShellExecute = true;
            try { Process.Start(si); if (minimize) AfterLaunch("BBport"); }
            catch (Exception ex) { Fail(ex); }
        }

        // Starts the game straight away with BBSC's emulator (BBSC is built into it).
        void LaunchSeamless() { LaunchSeamless(false); }

        void LaunchSeamless(bool retry)
        {
            if (File.Exists(cfg.SeamlessEmu) && !File.Exists(cfg.GameEboot) && !PickGame()) return;
            EnsurePatches();
            AddGameToShadList(cfg.GameEboot);
            if ((!cfg.AutoGfx || patchesWritten) && File.Exists(SXmlPath))
            {
                patchesWritten = false;
                try { if (ApplyAutoGraphics(true, DetectHardware()) != null) { cfg.AutoGfx = true; cfg.Save(); } } catch { }
            }
            // high resolution patches need extra GPU memory in the emulator (set it for setups made before)
            try
            {
                LoadGraphics();
                if (sCfg != "" && sXml != "" && SeamlessGet("s_res") != "1920x1080")
                {
                    int dm = 0;
                    int.TryParse(CfgValue("extra_dmem_in_mbytes") ?? "0", out dm);
                    if (dm < 8000) { CfgSet("extra_dmem_in_mbytes", "8000"); SaveCfg(); }
                }
            }
            catch { }
            if (File.Exists(cfg.SeamlessEmu) && File.Exists(cfg.GameEboot))
            {
                ProcessStartInfo gi = new ProcessStartInfo(cfg.SeamlessEmu, "--game \"" + cfg.GameEboot + "\"");
                gi.WorkingDirectory = Path.GetDirectoryName(cfg.SeamlessEmu);
                gi.UseShellExecute = false;
                gi.CreateNoWindow = true; // no log console, only the game window
                try
                {
                    Process gp = Process.Start(gi);
                    HideLogWindow(Path.GetDirectoryName(cfg.SeamlessEmu));
                    AfterLaunch("Seamless Co-op");
                    // the first start on a new PC sometimes closes right after the black window: start it once more
                    Thread wt = new Thread(delegate ()
                    {
                        bool quick = false;
                        try { quick = gp.WaitForExit(30000); } catch { }
                        if (!quick) return;
                        try { if (gp.ExitCode == 0) return; } catch { } // closed normally
                        try { BeginInvoke((MethodInvoker)delegate { SeamlessClosedEarly(retry); }); } catch { }
                    });
                    wt.IsBackground = true;
                    wt.Start();
                }
                catch (Exception ex) { Fail(ex); }
                return;
            }
            OpenSeamlessLauncher(true);
        }

        void SeamlessClosedEarly(bool wasRetry)
        {
            if (!wasRetry)
            {
                status = I18n.T("اللعبة طفت أول ما اشتغلت، يعيد تشغيلها ...");
                Invalidate();
                LaunchSeamless(true);
                return;
            }
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            if (MessageBox.Show(this, I18n.T("اللعبة طفت بعد ما اشتغلت بثواني، مرتين.\n\nجرّب تشغّلها مرة ثانية. ولو تكرر، أرسل سجل shadPS4 (shad_log.txt) في ديسكورد Seamless.\n\nتفتح مجلد السجل؟"),
                    "Seamless Co-op", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, I18n.MsgOpt) == DialogResult.Yes)
                OpenFolder(Path.Combine(ShadDir, "log"));
        }

        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);

        // The emulator may open its own log console (also inside Windows Terminal): hide it for 30 s after start.
        static void HideLogWindow(string emuDir)
        {
            string dir = emuDir.ToLowerInvariant();
            Thread th = new Thread(delegate ()
            {
                for (int i = 0; i < 60; i++)
                {
                    EnumWindows(delegate (IntPtr h, IntPtr l)
                    {
                        if (!IsWindowVisible(h)) return true;
                        StringBuilder cls = new StringBuilder(256), title = new StringBuilder(512);
                        GetClassName(h, cls, cls.Capacity);
                        GetWindowText(h, title, title.Capacity);
                        string c = cls.ToString(), t = title.ToString().ToLowerInvariant();
                        bool console = c == "ConsoleWindowClass" || c == "CASCADIA_HOSTING_WINDOW_CLASS";
                        if (console && (t.Contains("shadps4") || t.Contains(dir) || t.StartsWith(dir.Substring(0, Math.Min(dir.Length, 20)))))
                            ShowWindow(h, 0);
                        return true;
                    }, IntPtr.Zero);
                    Thread.Sleep(500);
                }
            });
            th.IsBackground = true;
            th.Start();
        }

        // BBSC's own launcher (shadPS4 launcher): settings, account, patches.
        void OpenSeamlessLauncher(bool minimize)
        {
            if (!File.Exists(cfg.SeamlessCmd)) { Missing(cfg.SeamlessCmd); return; }
            ProcessStartInfo si = new ProcessStartInfo("cmd.exe", "/c \"\"" + cfg.SeamlessCmd + "\"\"");
            si.WorkingDirectory = Path.GetDirectoryName(cfg.SeamlessCmd);
            si.UseShellExecute = false;
            si.CreateNoWindow = true;
            try { Process.Start(si); if (minimize) AfterLaunch(I18n.T("لانشر BBSC")); }
            catch (Exception ex) { Fail(ex); }
        }

        void StartUpdateCheck()
        {
            if (checking) return;
            checking = true;
            Invalidate();
            string exe = cfg.BBportExe, ver = bbsc, repo = cfg.LauncherRepo, bbVer = cfg.BBportVer;
            Thread th = new Thread(delegate ()
            {
                UpdateInfo n = null, s = null, l = null;
                try { n = Updates.BBport(exe, bbVer); } catch { }
                try { s = Updates.BBSC(ver); } catch { }
                if (!string.IsNullOrEmpty(repo)) try { l = Updates.Launcher(repo, AppVersion); } catch { }
                try { BeginInvoke((MethodInvoker)delegate { upN = n; upS = s; upL = l; checking = false; Invalidate(); }); } catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        void OpenNotes()
        {
            UpdateInfo u = cfg.Mode == "seamless" ? upS : upN;
            if (u != null && u.Url != "") Open(u.Url);
            else Open(cfg.Mode == "seamless" ? "https://github.com/CLSAMG22/BBSC/releases" : "https://github.com/Supermedo/bloodborne_pc/releases");
        }

        void UpdateNormal()
        {
            if (upN == null) return;
            if (!upN.Available) { Open(upN.Url); return; }
            DialogResult r = MessageBox.Show(this,
                I18n.T("نزل BBport ") + upN.Latest + ".\n\n" +
                I18n.T("نعم: يفتح لانشر BBport، واضغط فيه زر «تحديث» تحت على اليسار (يحتفظ بالحفظ والإعدادات).\n") +
                I18n.T("لا: يفتح صفحة التحديث في المتصفح."),
                I18n.T("تحديث BBport"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, I18n.MsgOpt);
            if (r == DialogResult.Yes) LaunchNormal(false);
            else if (r == DialogResult.No) Open(upN.Url);
        }

        void UpdateSeamless()
        {
            if (upS == null) return;
            if (!upS.Available) { Open(upS.Url); return; }
            MessageBox.Show(this,
                I18n.T("نزل BBSC ") + upS.Latest + I18n.T(" (عندك ") + bbsc + ").\n\n" +
                I18n.T("بفتح لك صفحة التحديث. نزّل ملف BBSC-...-Play.zip\n") +
                I18n.T("وتذكّر: كل اللي تلعب معهم لازم يكونون على نفس الإصدار."),
                I18n.T("تحديث المود"), MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, I18n.MsgOpt);
            Open(upS.Url);
        }

        void OpenFolder(string dir)
        {
            if (!Directory.Exists(dir))
            {
                MessageBox.Show(this, I18n.T("المجلد غير موجود:\n") + dir, "Bloodborne Launcher",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, I18n.MsgOpt);
                return;
            }
            Open(dir);
        }

        void Open(string target) { Open(target, null); }

        void Open(string target, string args)
        {
            try
            {
                ProcessStartInfo si = args == null ? new ProcessStartInfo(target) : new ProcessStartInfo(target, args);
                si.UseShellExecute = true;
                Process.Start(si);
            }
            catch (Exception ex) { Fail(ex); }
        }

        void Backup()
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string dest = Path.Combine(cfg.BackupDir, stamp);
            List<string> done = new List<string>();
            List<string> skipped = new List<string>();
            try
            {
                Directory.CreateDirectory(dest);
                ZipIfExists(cfg.NormalSaveDir(), Path.Combine(dest, "normal-bbport.zip"), "BBport", done, skipped);
                ZipIfExists(cfg.SeamlessSaveDir(), Path.Combine(dest, "seamless-shadps4.zip"), "Seamless", done, skipped);
                if (done.Count == 0) { try { Directory.Delete(dest); } catch { } }
                string msg = done.Count > 0
                    ? I18n.T("تم حفظ نسخة احتياطية (") + string.Join(I18n.T("، "), done.ToArray()) + I18n.T(") في:\n") + dest
                    : I18n.T("ما لقيت ملفات حفظ.");
                if (skipped.Count > 0) msg += I18n.T("\n\nما لقيت: ") + string.Join(I18n.T("، "), skipped.ToArray());
                status = done.Count > 0 ? I18n.T("تمت النسخة الاحتياطية") : I18n.T("ما لقيت ملفات حفظ");
                Invalidate();
                MessageBox.Show(this, msg, I18n.T("نسخة احتياطية"), MessageBoxButtons.OK, MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button1, I18n.MsgOpt);
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void ZipIfExists(string dir, string zip, string label, List<string> done, List<string> skipped)
        {
            if (Directory.Exists(dir)) { ZipFile.CreateFromDirectory(dir, zip, CompressionLevel.Optimal, true); done.Add(label); }
            else skipped.Add(label);
        }

        void Missing(string path)
        {
            MessageBox.Show(this, I18n.T("ما لقيت الملف:\n") + path + I18n.T("\n\nعدّل المسار في ملف launcher.ini جنب اللانشر"),
                "Bloodborne Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1,
                I18n.MsgOpt);
        }

        void Fail(Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Bloodborne Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
