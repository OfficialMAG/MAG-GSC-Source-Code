// MULTI COD MAG INJECTOR GSC - mises a jour, desactivation a distance et compteur d'utilisateurs
// (fichier commun, identique dans le launcher et dans chaque MAG GSC INJECTOR).
// Au demarrage, version.json est lu sur le serveur MAG (/version?suite=gsc) en arriere-plan.
// Si la version publiee pour ce programme est plus recente que la sienne, une fenetre ouvre le lien de telechargement
// dans le navigateur : la mise a jour est OBLIGATOIRE (pas de bouton « Plus tard »), le programme se ferme ensuite.
// Depot prive ou inexistant, pas d'internet, fichier invalide : rien ne s'affiche.
//
// Avertissement anti-revente (MagWarnWindow) : au lancement du launcher, et d'un injecteur ouvert sans le launcher
// (le launcher pose MAG_GSC_LAUNCHER=1 pour les injecteurs qu'il lance).
//
// version.json : { "launcher": { "version": "1.0", "url": "https://www.mediafire.com/...", "fr": "nouveautes", "en": "what's new" },
//                  "BO4": { ... }, "BOCW": { ... }, ... }
// « fr » / « en » : un texte ou une liste de lignes. « url » absente ou invalide = page du depot.
//
// Desactivation a distance : bloc « desactivation » de version.json, par exemple
//   "desactivation": { "tout": false, "launcher": false, "BO4": true, "fr": "message", "en": "message" }
// « tout » = launcher + tous les injecteurs. Un programme desactive affiche « Tool desactive » (avec le message) et se ferme ;
// un programme deja ouvert relit version.json toutes les 5 minutes. Le dernier etat lu est garde sur le PC
// (%APPDATA%\MAG_GSC\desactivation.txt) : sans internet, un tool desactive le reste. Remettre false = reactive.
//
// Bannière (launcher) et message à tous : blocs « banniere » et « message » (voir MagNews).
//
// Compteur d'utilisateurs : bloc « stats » de version.json { "url": "https://..." } (vide = pas de compteur).
// Chaque minute, signal anonyme POST /ping {id tire au hasard sur le PC, programme (« gsc_BO4 »...), version, periode 60} ;
// la reponse donne le nombre en ligne de la suite GSC (affiche dans le launcher). Hors ligne 1 min 30 apres le dernier
// signal, ou tout de suite a la fermeture (POST /bye). Le serveur est le meme que celui des tools RTM, mais les chiffres
// des deux suites sont separes. Coupe dans les Parametres du launcher = plus de signal.
// Dev by UrBaaZz and Zerty.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

public class MagRelease { public string Key, Version, Url, NotesFr, NotesEn; }
public class MagUpdateItem { public string Name, From, To, Notes; }

// Etat de desactivation lu dans version.json (ou garde sur le PC depuis la derniere lecture).
public class MagKill
{
    public bool All;
    public readonly HashSet<string> Keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public string Fr, En;
    public bool Disabled(string key) { return All || Keys.Contains(key); }
    public bool Any { get { return All || Keys.Count > 0; } }
    public string Message(Func<string, string, string> tr)
    {
        string m = tr(Fr ?? En, En ?? Fr);
        return !string.IsNullOrEmpty(m) ? m : tr("UrBaaZz et Zerty ont désactivé ce tool pour le moment.", "UrBaaZz and Zerty have disabled this tool for now.");
    }
}

public static class MagUpdate
{
    public const string Product = "MULTI COD MAG INJECTOR GSC";
    // version.json garde par le serveur MAG ; les zips sont sur MediaFire
    public const string Server = "https://serveur-mag.invalid", Suite = "gsc";
    public static string RepoUrl { get { return "https://discord.gg/m7e6S2agGU"; } } // lien de secours si « url » est vide
    public static string ManifestUrl
    {
        get
        {
            string test = Environment.GetEnvironmentVariable("MAG_GSC_UPDATE_URL"); // tests seulement
            return !string.IsNullOrEmpty(test) ? test : Server + "/version?suite=" + Suite;
        }
    }

    // version de ce programme (AssemblyVersion de l'exe)
    public static Version Current { get { var a = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly(); return Norm(a.GetName().Version); } }

    // « 1.4 » -> 1.4.0.0 (sinon Version(« 1.4 ») est plus petit que 1.4.0.0)
    public static Version Parse(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var p = s.Trim().TrimStart('v', 'V').Split('.');
        if (p.Length < 1 || p.Length > 4) return null;
        var n = new int[4];
        for (int i = 0; i < p.Length; i++) if (!int.TryParse(p[i], NumberStyles.None, CultureInfo.InvariantCulture, out n[i])) return null;
        return new Version(n[0], n[1], n[2], n[3]);
    }
    public static Version Norm(Version v) { return v == null ? null : new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision)); }
    // 1.4.0.0 -> « 1.4 », 1.5.1.0 -> « 1.5.1 »
    public static string Show(Version v)
    {
        if (v == null) return "?";
        v = Norm(v);
        return v.Revision > 0 ? v.ToString(4) : v.Build > 0 ? v.ToString(3) : v.ToString(2);
    }
    public static bool IsNewer(string published, Version installed)
    {
        var p = Parse(published);
        return p != null && installed != null && p > Norm(installed);
    }
    // seul un lien http(s) est ouvert ; sinon la page du depot
    public static string SafeUrl(string url)
    {
        Uri u;
        return !string.IsNullOrEmpty(url) && Uri.TryCreate(url.Trim(), UriKind.Absolute, out u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp) ? u.AbsoluteUri : RepoUrl;
    }

    // telechargement + lecture de version.json ; null en cas d'echec (aucune erreur montree)
    public static Dictionary<string, MagRelease> Fetch(int timeoutMs) { MagKill k; return Fetch(timeoutMs, out k); }
    // kill = null : version.json pas lu (pas d'internet, depot prive...) ; sinon l'etat de desactivation publie
    public static Dictionary<string, MagRelease> Fetch(int timeoutMs, out MagKill kill)
    {
        kill = null;
        try
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { } // TLS 1.2
            string url = ManifestUrl;
            url += (url.Contains("?") ? "&" : "?") + "t=" + DateTime.UtcNow.Ticks; // toujours la derniere version, pas une copie en cache
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "MAG-GSC"; req.Timeout = timeoutMs; req.ReadWriteTimeout = timeoutMs;
            req.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var st = resp.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                var buf = new byte[8192]; int n;
                while ((n = st.Read(buf, 0, buf.Length)) > 0) { ms.Write(buf, 0, n); if (ms.Length > 256 * 1024) return null; }
                return ParseManifest(Encoding.UTF8.GetString(ms.ToArray()), out kill);
            }
        }
        catch { return null; }
    }

    public static Dictionary<string, MagRelease> ParseManifest(string text) { MagKill k; return ParseManifest(text, out k); }
    public static Dictionary<string, MagRelease> ParseManifest(string text, out MagKill kill)
    {
        kill = null;
        try
        {
            var root = new MagJson((text ?? "").TrimStart('﻿')).Parse() as Dictionary<string, object>;
            if (root == null) return null;
            var d = new Dictionary<string, MagRelease>(StringComparer.OrdinalIgnoreCase);
            var k = new MagKill(); // fichier lu : rien de desactive s'il n'y a pas de bloc « desactivation »
            object dv; var dd = root.TryGetValue("desactivation", out dv) ? dv as Dictionary<string, object> : null;
            if (dd != null)
                foreach (var kv in dd)
                {
                    if (kv.Key.Equals("fr", StringComparison.OrdinalIgnoreCase)) k.Fr = Str(dd, kv.Key);
                    else if (kv.Key.Equals("en", StringComparison.OrdinalIgnoreCase)) k.En = Str(dd, kv.Key);
                    else if (Truthy(kv.Value)) { if (kv.Key.Equals("tout", StringComparison.OrdinalIgnoreCase) || kv.Key.Equals("all", StringComparison.OrdinalIgnoreCase)) k.All = true; else k.Keys.Add(kv.Key); }
                }
            foreach (var kv in root)
            {
                var o = kv.Value as Dictionary<string, object>; if (o == null) continue;
                var r = new MagRelease { Key = kv.Key, Version = Str(o, "version"), Url = Str(o, "url"), NotesFr = Str(o, "fr"), NotesEn = Str(o, "en") };
                if (Parse(r.Version) != null) d[kv.Key] = r;
            }
            object sv; var sd = root.TryGetValue("stats", out sv) ? sv as Dictionary<string, object> : null;
            string su = sd != null ? Str(sd, "url") : null; Uri u;
            MagNews.Read(root); // bannière du launcher et message à tous
            MagStats.Url = !string.IsNullOrEmpty(su) && Uri.TryCreate(su.Trim(), UriKind.Absolute, out u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp) ? u.AbsoluteUri.TrimEnd('/') : null;
            kill = k;
            return d;
        }
        catch { return null; }
    }
    static bool Truthy(object v)
    {
        if (v is bool) return (bool)v;
        string s = (Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").Trim().ToLowerInvariant();
        return s == "true" || s == "oui" || s == "yes" || s == "1";
    }

    // ---- dernier etat de desactivation lu, garde sur le PC (un tool desactive le reste sans internet) ----
    static string KillFile
    {
        get
        {
            string test = Environment.GetEnvironmentVariable("MAG_GSC_KILL_FILE"); // tests seulement
            return !string.IsNullOrEmpty(test) ? test : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MAG_GSC", "desactivation.txt");
        }
    }
    public static MagKill LoadKill()
    {
        try
        {
            if (!File.Exists(KillFile)) return null;
            var k = new MagKill();
            foreach (var l in File.ReadAllLines(KillFile, Encoding.UTF8))
            {
                if (l.StartsWith("fr=")) k.Fr = l.Substring(3).Replace("\\n", "\n");
                else if (l.StartsWith("en=")) k.En = l.Substring(3).Replace("\\n", "\n");
                else if (l.Trim() == "*") k.All = true;
                else if (l.Trim().Length > 0) k.Keys.Add(l.Trim());
            }
            return k;
        }
        catch { return null; }
    }
    public static void SaveKill(MagKill k)
    {
        if (k == null) return;
        try
        {
            if (!k.Any) { if (File.Exists(KillFile)) File.Delete(KillFile); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(KillFile));
            var lines = new List<string>();
            if (k.All) lines.Add("*");
            foreach (var x in k.Keys) lines.Add(x);
            if (k.Fr != null) lines.Add("fr=" + k.Fr.Replace("\n", "\\n"));
            if (k.En != null) lines.Add("en=" + k.En.Replace("\n", "\\n"));
            File.WriteAllLines(KillFile, lines.ToArray(), new UTF8Encoding(false));
        }
        catch { }
    }
    static string Str(Dictionary<string, object> o, string k)
    {
        object v; if (!o.TryGetValue(k, out v) || v == null) return null;
        var l = v as List<object>;
        if (l != null) { var sb = new StringBuilder(); foreach (var x in l) { if (sb.Length > 0) sb.Append('\n'); sb.Append(Convert.ToString(x, CultureInfo.InvariantCulture)); } return sb.ToString(); }
        return Convert.ToString(v, CultureInfo.InvariantCulture);
    }
    public static string Notes(MagRelease r, Func<string, string, string> tr)
    {
        return tr(r.NotesFr ?? r.NotesEn, r.NotesEn ?? r.NotesFr);
    }

    // Injecteurs : a appeler une fois la fenetre principale affichee. key = « BO4 », « BOCW »...
    // Desactive (version.json ou dernier etat garde) : fenetre « Tool desactive » puis fermeture ;
    // sinon, mise a jour publiee : fenetre « Mise a jour disponible ».
    public static void CheckAsync(Window owner, string key, string name, Color accent, Color accent2, Func<string, string, string> tr)
    {
        if (owner == null) return;
        new MagWatch(owner, key, name, accent, accent2, tr).Start();
    }
}

// Compteur d'utilisateurs (voir en-tete). Tout echec est silencieux ; Online = -1 tant que rien n'est connu.
public static class MagStats
{
    public const string Suite = "gsc"; // le serveur separe les tools RTM et les injecteurs GSC
    public static string Url;   // depuis version.json ; null = pas de compteur
    public static TimeSpan Every = TimeSpan.FromSeconds(60); // periode du signal (envoyee au serveur : hors ligne apres 1,5 periode)
    public static volatile bool Blocked; // programme desactive a distance : plus de signal
    static Timer timer; static string current;
    public static int Online = -1;
    public static Dictionary<string, int> Apps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public static event Action Changed = delegate { };

    static string AppData { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } }
    // reglage du compteur : propre a la suite GSC ; numero du PC : le meme que celui des tools RTM (un PC = une installation)
    static string Dir
    {
        get
        {
            string test = Environment.GetEnvironmentVariable("MAG_GSC_DATA_DIR"); // tests seulement
            return !string.IsNullOrEmpty(test) ? test : Path.Combine(AppData, "MAG_GSC");
        }
    }
    static string IdDir
    {
        get
        {
            string test = Environment.GetEnvironmentVariable("MAG_GSC_DATA_DIR");
            return !string.IsNullOrEmpty(test) ? test : Path.Combine(AppData, "MAG_TOOL");
        }
    }
    // compteur.txt = « 0 » : plus aucun signal (choix dans les Parametres du launcher, valable pour tous les injecteurs)
    public static bool Enabled
    {
        get { try { string f = Path.Combine(Dir, "compteur.txt"); return !File.Exists(f) || File.ReadAllText(f).Trim() != "0"; } catch { return true; } }
        set { try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, "compteur.txt"), value ? "1" : "0"); } catch { } }
    }
    // numero anonyme du PC, tire au hasard une seule fois
    public static string InstallId
    {
        get
        {
            try
            {
                string f = Path.Combine(IdDir, "id.txt");
                if (File.Exists(f)) { string old = File.ReadAllText(f).Trim(); if (Regex.IsMatch(old, "^[0-9a-f]{32}$")) return old; }
                string id = Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(IdDir); File.WriteAllText(f, id);
                return id;
            }
            catch { return null; }
        }
    }

    // apres la premiere lecture de version.json : signal tout de suite, puis a chaque periode (fil d'arriere-plan)
    public static void Begin(string app)
    {
        if (timer != null) return;
        current = app;
        Send(app);
        timer = new Timer(delegate { if (!Blocked) Send(current); }, null, Every, Every);
    }

    // fermeture du programme : il n'est plus compte en ligne (attend au plus 1,5 s la reponse)
    public static void Bye()
    {
        if (timer != null) { timer.Dispose(); timer = null; }
        string url = Url, app = current;
        if (string.IsNullOrEmpty(url) || app == null || !Enabled) return;
        current = null;
        var th = new Thread(delegate ()
        {
            try
            {
                string id = InstallId; if (id == null) return;
                var req = (HttpWebRequest)WebRequest.Create(url + "/bye");
                req.Method = "POST"; req.ContentType = "application/json"; req.UserAgent = "MAG-GSC"; req.Timeout = 1500; req.ReadWriteTimeout = 1500;
                var body = Encoding.UTF8.GetBytes("{\"id\":\"" + id + "\",\"app\":\"" + Suite + "_" + app + "\"}");
                using (var rs = req.GetRequestStream()) rs.Write(body, 0, body.Length);
                using (req.GetResponse()) { }
            }
            catch { }
        }) { IsBackground = true, Name = "MAG bye" };
        th.Start();
        th.Join(1500);
    }

    // fil d'arriere-plan : signal (ou simple lecture du nombre en ligne si le compteur est coupe)
    public static void Send(string app)
    {
        string url = Url;
        if (string.IsNullOrEmpty(url)) return;
        if (!Enabled && app != "launcher") return; // compteur coupe : seul le launcher lit encore le nombre en ligne
        try
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            string id = Enabled ? InstallId : null;
            HttpWebRequest req;
            if (id != null)
            {
                req = (HttpWebRequest)WebRequest.Create(url + "/ping");
                req.Method = "POST"; req.ContentType = "application/json";
                var body = Encoding.UTF8.GetBytes("{\"id\":\"" + id + "\",\"app\":\"" + Suite + "_" + app + "\",\"v\":\"" + MagUpdate.Show(MagUpdate.Current) + "\",\"p\":" + (int)Every.TotalSeconds + "}");
                req.UserAgent = "MAG-GSC"; req.Timeout = 8000; req.ReadWriteTimeout = 8000;
                using (var rs = req.GetRequestStream()) rs.Write(body, 0, body.Length);
            }
            else
            {
                req = (HttpWebRequest)WebRequest.Create(url + "/online?suite=" + Suite);
                req.UserAgent = "MAG-GSC"; req.Timeout = 8000; req.ReadWriteTimeout = 8000;
            }
            string text;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) text = rd.ReadToEnd();
            var o = new MagJson(text).Parse() as Dictionary<string, object>;
            MagNews.Read(o); // le serveur rapporte aussi la bannière et le message à tous
            object on; if (o == null || !o.TryGetValue("online", out on) || !(on is double)) return;
            var apps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            object av; var ad = o.TryGetValue("apps", out av) ? av as Dictionary<string, object> : null;
            if (ad != null) foreach (var kv in ad) if (kv.Value is double) apps[kv.Key] = (int)(double)kv.Value;
            Apps = apps; Online = (int)(double)on;
            Changed();
        }
        catch { }
    }
}

// Surveillance d'un injecteur : lecture de version.json au demarrage puis toutes les 5 minutes.
public class MagWatch
{
    readonly Window owner; readonly string key, name; readonly Color accent, accent2; readonly Func<string, string, string> tr;
    MagBlockWindow block; bool updateDone; DispatcherTimer timer;
    public static TimeSpan Every = TimeSpan.FromMinutes(5);

    public MagWatch(Window owner, string key, string name, Color accent, Color accent2, Func<string, string, string> tr)
    { this.owner = owner; this.key = key; this.name = name; this.accent = accent; this.accent2 = accent2; this.tr = tr; }

    public void Start()
    {
        MagNews.Attach(owner, accent, accent2, tr);
        var saved = MagUpdate.LoadKill();
        Check(); // la lecture part avant le blocage (la fenetre de blocage est modale)
        timer = new DispatcherTimer(Every, DispatcherPriority.Background, delegate { Check(); }, owner.Dispatcher);
        if (saved != null && saved.Disabled(key)) ShowBlock(saved);
        else if (!MagWarnWindow.FromLauncher) MagWarnWindow.Show(owner, tr);
    }

    void Check()
    {
        var th = new Thread(delegate ()
        {
            MagKill kill; var all = MagUpdate.Fetch(8000, out kill);
            if (kill == null) return; // version.json pas lu : rien ne change
            MagUpdate.SaveKill(kill);
            MagStats.Blocked = kill.Disabled(key);
            if (!kill.Disabled(key)) MagStats.Begin(key); // compteur d'utilisateurs (signal anonyme chaque minute)
            try { owner.Dispatcher.BeginInvoke(new Action(delegate { OnResult(all, kill); })); } catch { }
        }) { IsBackground = true, Name = "MAG update" };
        th.Start();
    }

    void OnResult(Dictionary<string, MagRelease> all, MagKill kill)
    {
        if (MagWarnWindow.After(delegate { OnResult(all, kill); })) return; // avertissement ouvert : apres sa fermeture
        try
        {
            if (kill.Disabled(key)) { if (block != null) block.SetMessage(kill); else ShowBlock(kill); return; }
            if (block != null)
            {
                // reactive pendant que la fenetre de blocage est ouverte : le tool reste ouvert
                var b = block; block = null; b.Reenable();
                owner.Dispatcher.BeginInvoke(new Action(delegate { OnResult(all, kill); }));
                return;
            }
            if (updateDone || !owner.IsLoaded) { MagNews.ShowPending(); return; }
            updateDone = true;
            MagRelease r;
            if (all == null || !all.TryGetValue(key, out r) || !MagUpdate.IsNewer(r.Version, MagUpdate.Current)) { MagNews.ShowPending(); return; }
            var items = new List<MagUpdateItem> { new MagUpdateItem { Name = name, From = MagUpdate.Show(MagUpdate.Current), To = MagUpdate.Show(MagUpdate.Parse(r.Version)), Notes = MagUpdate.Notes(r, tr) } };
            new MagUpdateWindow(owner, accent, accent2, tr, items, MagUpdate.SafeUrl(r.Url)).ShowDialog();
            // mise a jour obligatoire : le tool ne s'utilise plus tant qu'il n'est pas a jour
            if (timer != null) timer.Stop();
            owner.Close();
        }
        catch { }
    }

    void ShowBlock(MagKill kill)
    {
        if (!owner.IsLoaded) return;
        var b = new MagBlockWindow(owner, tr, name, kill);
        block = b;
        b.ShowDialog();
        if (block == b) block = null;
        if (!b.Reenabled) { if (timer != null) timer.Stop(); try { owner.Close(); } catch { } }
    }
}

// Fenetre « Mise a jour disponible » (verre bleu nuit comme le launcher, couleur du programme).
public class MagUpdateWindow : Window
{
    internal static readonly FontFamily TitleFont = new FontFamily("Bahnschrift SemiBold, Bahnschrift, Segoe UI");
    internal static readonly FontFamily TextFont = new FontFamily("Segoe UI Variable Text, Segoe UI");
    internal static readonly FontFamily IconFont = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
    readonly string url;
    public bool Downloaded { get; private set; }

    internal static SolidColorBrush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
    internal static Color C(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
    internal static Color A(Color c, byte a) { return Color.FromArgb(a, c.R, c.G, c.B); }
    internal static TextBlock T(string s, double size, Brush fg, FontFamily f) { return new TextBlock { Text = s, FontSize = size, Foreground = fg, FontFamily = f, TextWrapping = TextWrapping.Wrap }; }

    // carte de dialogue : verre bleu nuit, bord degrade de la couleur, halo
    internal static Border Card(Color accent)
    {
        return new Border
        {
            Margin = new Thickness(28), CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1.2), Padding = new Thickness(28, 24, 28, 22),
            BorderBrush = new LinearGradientBrush(A(accent, 210), Color.FromArgb(0x22, 0xC8, 0xD4, 0xFF), 45),
            Background = new LinearGradientBrush(C("#FA0D1428"), C("#FA060A15"), 90),
            Effect = new DropShadowEffect { Color = accent, BlurRadius = 42, ShadowDepth = 0, Opacity = 0.5 }
        };
    }
    internal static void Frame(Window w, Window owner, string title)
    {
        w.Title = title;
        w.WindowStyle = WindowStyle.None; w.AllowsTransparency = true; w.Background = Brushes.Transparent; w.ResizeMode = ResizeMode.NoResize;
        w.Width = 560; w.SizeToContent = SizeToContent.Height; w.FontFamily = TextFont; w.UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(w, TextFormattingMode.Ideal);
        if (owner != null && owner.IsVisible) { w.Owner = owner; w.WindowStartupLocation = WindowStartupLocation.CenterOwner; w.ShowInTaskbar = false; }
        else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (owner != null && owner.Icon != null) w.Icon = owner.Icon;
    }
    internal static Grid Head(string glyph, Color a1, Color a2, string title, string sub)
    {
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var ic = T(glyph, 22, Brushes.White, IconFont); ic.HorizontalAlignment = HorizontalAlignment.Center; ic.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(new Border { Width = 50, Height = 50, CornerRadius = new CornerRadius(15), Background = new LinearGradientBrush(a1, a2, 45), Child = ic, VerticalAlignment = VerticalAlignment.Center, Effect = new DropShadowEffect { Color = a1, BlurRadius = 22, ShadowDepth = 0, Opacity = 0.6 } });
        var ht = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        ht.Children.Add(T(title, 23, Brushes.White, TitleFont));
        var s = T(sub, 13.5, B("#9BA4BA"), TextFont); s.Margin = new Thickness(0, 2, 0, 0);
        ht.Children.Add(s);
        Grid.SetColumn(ht, 1); head.Children.Add(ht);
        return head;
    }

    public MagUpdateWindow(Window owner, Color accent, Color accent2, Func<string, string, string> tr, List<MagUpdateItem> items, string url)
    {
        this.url = url;
        Frame(this, owner, tr("Mise à jour disponible", "Update available"));
        var card = Card(accent);
        card.MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
        var st = new StackPanel();
        st.Children.Add(Head("", accent, accent2, tr("Mise à jour disponible", "Update available"), tr("Une nouvelle version de " + MagUpdate.Product + " est sortie.", "A new version of " + MagUpdate.Product + " is out.")));

        // ce qui change
        var list = new StackPanel();
        foreach (var it in items)
        {
            var box = new Border { CornerRadius = new CornerRadius(14), Background = B("#12C8D4FF"), BorderBrush = new SolidColorBrush(A(accent, 80)), BorderThickness = new Thickness(1), Padding = new Thickness(16, 12, 16, 13), Margin = new Thickness(0, 12, 0, 0) };
            var bs = new StackPanel();
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var nm = T(it.Name, 15.5, Brushes.White, TitleFont); nm.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(nm);
            var ver = new TextBlock { FontFamily = TitleFont, FontSize = 15, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            ver.Inlines.Add(new System.Windows.Documents.Run("V" + it.From) { Foreground = B("#8D95AB") });
            ver.Inlines.Add(new System.Windows.Documents.Run("    ") { FontFamily = IconFont, FontSize = 11, Foreground = new SolidColorBrush(accent) });
            ver.Inlines.Add(new System.Windows.Documents.Run("V" + it.To) { Foreground = new SolidColorBrush(accent) });
            Grid.SetColumn(ver, 1); row.Children.Add(ver);
            bs.Children.Add(row);
            if (!string.IsNullOrEmpty(it.Notes))
            {
                var notes = T(it.Notes.Replace("\r\n", "\n").Trim(), 13, B("#B9C0CF"), TextFont); notes.Margin = new Thickness(0, 7, 0, 0); notes.LineHeight = 19;
                bs.Children.Add(notes);
            }
            box.Child = bs;
            list.Children.Add(box);
        }
        st.Children.Add(new ScrollViewer { Content = list, MaxHeight = 330, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false });

        var must = T(tr("Mise à jour obligatoire : le tool ne peut plus être utilisé tant qu'il n'est pas à jour.", "Mandatory update: the tool can't be used until it is up to date."), 13, new SolidColorBrush(accent), TitleFont);
        must.Margin = new Thickness(2, 14, 0, 0);
        st.Children.Add(must);
        var hint = T(tr("Le téléchargement s'ouvre dans ton navigateur (MediaFire), puis le tool se ferme.", "The download opens in your browser (MediaFire), then the tool closes."), 12.5, B("#7C849A"), TextFont);
        hint.Margin = new Thickness(2, 6, 0, 0);
        st.Children.Add(hint);

        // boutons
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var dl = new StackPanel { Orientation = Orientation.Horizontal };
        var dlIc = T("", 14, Brushes.White, IconFont); dlIc.VerticalAlignment = VerticalAlignment.Center; dlIc.Margin = new Thickness(0, 1, 9, 0);
        dl.Children.Add(dlIc);
        dl.Children.Add(T(tr("Télécharger", "Download"), 14, Brushes.White, TitleFont));
        var down = Button(dl, new LinearGradientBrush(accent, accent2, 0), new LinearGradientBrush(Lighter(accent), Lighter(accent2), 0), null);
        down.Effect = new DropShadowEffect { Color = accent, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.45 };
        down.MouseLeftButtonUp += delegate { Download(); };
        btns.Children.Add(down);
        st.Children.Add(btns);

        card.Child = st;
        Content = card;
        PreviewKeyDown += delegate (object s, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) e.Handled = true; // pas de « Plus tard »
            else if (e.Key == Key.Enter) { e.Handled = true; Download(); }
        };
    }

    internal static Color Lighter(Color c) { return Color.FromRgb((byte)Math.Min(255, c.R + 28), (byte)Math.Min(255, c.G + 28), (byte)Math.Min(255, c.B + 28)); }

    internal static Border Button(UIElement content, Brush normal, Brush hover, Brush border)
    {
        var b = new Border { Child = content, CornerRadius = new CornerRadius(12), Padding = new Thickness(20, 10, 20, 11), Background = normal, Cursor = Cursors.Hand, MinWidth = 120 };
        if (border != null) { b.BorderBrush = border; b.BorderThickness = new Thickness(1); }
        var fe = content as FrameworkElement; if (fe != null) fe.HorizontalAlignment = HorizontalAlignment.Center;
        b.MouseEnter += delegate { b.Background = hover; };
        b.MouseLeave += delegate { b.Background = normal; };
        b.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e) { e.Handled = true; };
        return b;
    }

    void Download()
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); Downloaded = true; }
        catch { try { Clipboard.SetText(url); } catch { } }
        Close();
    }
}

// Fenetre « Tool desactive » : message des developpeurs, Discord, Fermer (= le programme se ferme).
public class MagBlockWindow : Window
{
    public const string DiscordUrl = "https://discord.gg/m7e6S2agGU";
    public bool Reenabled { get; private set; }
    readonly TextBlock msg; readonly Func<string, string, string> tr;

    public MagBlockWindow(Window owner, Func<string, string, string> tr, string name, MagKill kill)
    {
        this.tr = tr;
        var red = MagUpdateWindow.C("#FF4D5E"); var red2 = MagUpdateWindow.C("#B3123A");
        MagUpdateWindow.Frame(this, owner, tr("Tool désactivé", "Tool disabled"));
        var card = MagUpdateWindow.Card(red);
        card.MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
        var st = new StackPanel();
        st.Children.Add(MagUpdateWindow.Head("", red, red2, tr("Tool désactivé", "Tool disabled"), name));

        var box = new Border { CornerRadius = new CornerRadius(14), Background = MagUpdateWindow.B("#12C8D4FF"), BorderBrush = new SolidColorBrush(MagUpdateWindow.A(red, 80)), BorderThickness = new Thickness(1), Padding = new Thickness(16, 12, 16, 13), Margin = new Thickness(0, 14, 0, 0) };
        msg = MagUpdateWindow.T("", 14, MagUpdateWindow.B("#E4E7EF"), MagUpdateWindow.TextFont); msg.LineHeight = 20;
        box.Child = msg;
        st.Children.Add(box);
        SetMessage(kill);
        var hint = MagUpdateWindow.T(tr("Plus d'infos sur le serveur Discord.", "More info on the Discord server."), 12.5, MagUpdateWindow.B("#7C849A"), MagUpdateWindow.TextFont);
        hint.Margin = new Thickness(2, 14, 0, 0);
        st.Children.Add(hint);

        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var discord = MagUpdateWindow.Button(MagUpdateWindow.T("Discord", 14, MagUpdateWindow.B("#D5D9E3"), MagUpdateWindow.TitleFont), MagUpdateWindow.B("#16C8D4FF"), MagUpdateWindow.B("#2CC8D4FF"), MagUpdateWindow.B("#2AC8D4FF"));
        discord.MouseLeftButtonUp += delegate { try { Process.Start(new ProcessStartInfo(DiscordUrl) { UseShellExecute = true }); } catch { } };
        btns.Children.Add(discord);
        var close = MagUpdateWindow.Button(MagUpdateWindow.T(tr("Fermer", "Close"), 14, Brushes.White, MagUpdateWindow.TitleFont), new LinearGradientBrush(red, red2, 0), new LinearGradientBrush(MagUpdateWindow.Lighter(red), MagUpdateWindow.Lighter(red2), 0), null);
        close.Margin = new Thickness(10, 0, 0, 0);
        close.MouseLeftButtonUp += delegate { Close(); };
        btns.Children.Add(close);
        st.Children.Add(btns);
        card.Child = st;
        Content = card;
        PreviewKeyDown += delegate (object s, KeyEventArgs e) { if (e.Key == Key.Escape || e.Key == Key.Enter) { e.Handled = true; Close(); } };
    }

    public void SetMessage(MagKill kill) { msg.Text = (kill != null ? kill.Message(tr) : tr("Ce tool est désactivé.", "This tool is disabled.")).Replace("\r\n", "\n").Trim(); }
    // reactive (version.json) : la fenetre se ferme et le programme continue
    public void Reenable() { Reenabled = true; try { Close(); } catch { } }
}

// Bannière du launcher et message à tous : blocs « banniere » et « message » de version.json (réglés depuis le serveur).
// Le signal du compteur (chaque minute) rapporte aussi ces deux blocs : un nouveau message arrive en 1 à 2
// minutes chez tous les programmes ouverts. Un message n'est montré qu'une fois par PC (%APPDATA%\MAG_GSC\message_vu.txt,
// commun au launcher et aux injecteurs).
//   "banniere": { "actif": true, "fr": "texte", "en": "text", "couleur": "#FF7A1A", "lien": "https://..." }
//   "message":  { "actif": true, "id": "lq2x8k", "fr": "texte", "en": "text", "lien": "https://..." }
public class MagBanner { public string Fr, En, Color, Link; }
public class MagMessage { public string Id, Fr, En, Link; }
public static class MagNews
{
    public static volatile MagBanner Banner;   // null = pas de bannière
    public static volatile MagMessage Message; // null = pas de message en cours
    public static event Action Changed = delegate { };
    static Window owner; static Color a1, a2; static Func<string, string, string> tr;
    static bool showing;

    static string SeenFile
    {
        get
        {
            string test = Environment.GetEnvironmentVariable("MAG_GSC_DATA_DIR"); // tests seulement
            return Path.Combine(!string.IsNullOrEmpty(test) ? test : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MAG_GSC"), "message_vu.txt");
        }
    }
    static bool Seen(string id) { try { if (!File.Exists(SeenFile)) return false; foreach (var l in File.ReadAllLines(SeenFile)) if (l.Trim() == id) return true; } catch { } return false; }
    static void MarkSeen(string id)
    {
        try
        {
            var l = new List<string>(); if (File.Exists(SeenFile)) l.AddRange(File.ReadAllLines(SeenFile));
            l.Add(id); if (l.Count > 30) l.RemoveRange(0, l.Count - 30);
            Directory.CreateDirectory(Path.GetDirectoryName(SeenFile)); File.WriteAllLines(SeenFile, l.ToArray());
        }
        catch { }
    }

    static string S(Dictionary<string, object> o, string k)
    {
        object v; if (o == null || !o.TryGetValue(k, out v) || v == null) return null;
        var l = v as List<object>;
        if (l != null) { var sb = new StringBuilder(); foreach (var x in l) { if (sb.Length > 0) sb.Append('\n'); sb.Append(Convert.ToString(x, CultureInfo.InvariantCulture)); } return sb.ToString(); }
        string s = Convert.ToString(v, CultureInfo.InvariantCulture); return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
    static bool On(Dictionary<string, object> o)
    {
        object v; if (!o.TryGetValue("actif", out v)) return true;
        if (v is bool) return (bool)v;
        string s = (Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").Trim().ToLowerInvariant();
        return s == "true" || s == "oui" || s == "yes" || s == "1";
    }
    static string Link(string u) { Uri x; return !string.IsNullOrEmpty(u) && Uri.TryCreate(u, UriKind.Absolute, out x) && (x.Scheme == Uri.UriSchemeHttps || x.Scheme == Uri.UriSchemeHttp) ? x.AbsoluteUri : null; }

    // version.json ou réponse du signal : un bloc absent ne change rien (ancien serveur), null / inactif = retiré
    public static void Read(Dictionary<string, object> root)
    {
        if (root == null) return;
        bool changed = false; object v;
        if (root.TryGetValue("banniere", out v))
        {
            var o = v as Dictionary<string, object>; MagBanner b = null;
            if (o != null && On(o) && (S(o, "fr") ?? S(o, "en")) != null) b = new MagBanner { Fr = S(o, "fr"), En = S(o, "en"), Color = S(o, "couleur"), Link = Link(S(o, "lien")) };
            if (Key(b) != Key(Banner)) { Banner = b; changed = true; }
        }
        if (root.TryGetValue("message", out v))
        {
            var o = v as Dictionary<string, object>; MagMessage m = null;
            if (o != null && On(o) && S(o, "id") != null && (S(o, "fr") ?? S(o, "en")) != null) m = new MagMessage { Id = S(o, "id"), Fr = S(o, "fr"), En = S(o, "en"), Link = Link(S(o, "lien")) };
            if (Key(m) != Key(Message)) { Message = m; changed = true; }
        }
        if (!changed) return;
        Changed();
        var w = owner; if (w != null) try { w.Dispatcher.BeginInvoke(new Action(ShowPending)); } catch { }
    }
    static string Key(MagBanner b) { return b == null ? "" : b.Fr + "\u0001" + b.En + "\u0001" + b.Color + "\u0001" + b.Link; }
    static string Key(MagMessage m) { return m == null ? "" : m.Id + "\u0001" + m.Fr + "\u0001" + m.En + "\u0001" + m.Link; }

    // fenêtre principale du programme (launcher ou injecteur) : les messages s'affichent par-dessus
    public static void Attach(Window w, Color accent, Color accent2, Func<string, string, string> t) { owner = w; a1 = accent; a2 = accent2; tr = t; }

    // fil de la fenêtre : montre le message en cours s'il n'a pas encore été vu sur ce PC
    public static void ShowPending()
    {
        var w = owner; var m = Message;
        if (w == null || m == null || showing || MagStats.Blocked || !w.IsLoaded || !w.IsVisible || w.WindowState == WindowState.Minimized) return;
        if (MagWarnWindow.After(ShowPending)) return; // avertissement ouvert : après sa fermeture
        foreach (Window x in w.OwnedWindows) if (x.IsVisible) return; // une autre fenêtre (mise à jour, désactivation) est ouverte
        if (Seen(m.Id)) return;
        MarkSeen(m.Id);
        showing = true;
        try { new MagNewsWindow(w, a1, a2, tr, m).ShowDialog(); } catch { }
        finally { showing = false; }
        if (Message != null && Message.Id != m.Id) w.Dispatcher.BeginInvoke(new Action(ShowPending));
    }
}

// Fenêtre « Message de l'équipe MAG » (même verre que les fenêtres de mise à jour).
public class MagNewsWindow : Window
{
    public MagNewsWindow(Window owner, Color accent, Color accent2, Func<string, string, string> tr, MagMessage m)
    {
        MagUpdateWindow.Frame(this, owner, tr("Message de l'équipe MAG", "Message from the MAG team"));
        var card = MagUpdateWindow.Card(accent);
        card.MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
        var st = new StackPanel();
        st.Children.Add(MagUpdateWindow.Head("", accent, accent2, tr("Message de l'équipe MAG", "Message from the MAG team"), "UrBaaZz & ZERTY"));
        var box = new Border { CornerRadius = new CornerRadius(14), Background = MagUpdateWindow.B("#12C8D4FF"), BorderBrush = new SolidColorBrush(MagUpdateWindow.A(accent, 80)), BorderThickness = new Thickness(1), Padding = new Thickness(16, 12, 16, 13), Margin = new Thickness(0, 14, 0, 0) };
        var msg = MagUpdateWindow.T(tr(m.Fr ?? m.En, m.En ?? m.Fr).Replace("\r\n", "\n").Trim(), 14.5, MagUpdateWindow.B("#E9ECF3"), MagUpdateWindow.TextFont); msg.LineHeight = 21;
        box.Child = new ScrollViewer { Content = msg, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
        st.Children.Add(box);

        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        if (m.Link != null)
        {
            var open = MagUpdateWindow.Button(MagUpdateWindow.T(tr("Ouvrir le lien", "Open the link"), 14, MagUpdateWindow.B("#D5D9E3"), MagUpdateWindow.TitleFont), MagUpdateWindow.B("#16C8D4FF"), MagUpdateWindow.B("#2CC8D4FF"), MagUpdateWindow.B("#2AC8D4FF"));
            open.MouseLeftButtonUp += delegate { try { Process.Start(new ProcessStartInfo(m.Link) { UseShellExecute = true }); } catch { } };
            btns.Children.Add(open);
        }
        var ok = MagUpdateWindow.Button(MagUpdateWindow.T("OK", 14, Brushes.White, MagUpdateWindow.TitleFont), new LinearGradientBrush(accent, accent2, 0), new LinearGradientBrush(MagUpdateWindow.Lighter(accent), MagUpdateWindow.Lighter(accent2), 0), null);
        ok.Margin = new Thickness(10, 0, 0, 0);
        ok.Effect = new DropShadowEffect { Color = accent, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.45 };
        ok.MouseLeftButtonUp += delegate { Close(); };
        btns.Children.Add(ok);
        st.Children.Add(btns);
        card.Child = st;
        Content = card;
        PreviewKeyDown += delegate (object s, KeyEventArgs e) { if (e.Key == Key.Escape || e.Key == Key.Enter) { e.Handled = true; Close(); } };
    }
}

// Avertissement anti-revente : MAG GSC INJECTOR n'a que deux createurs, UrBaaZz et ZERTY.
// Grande fenetre coloree (bord arc-en-ciel anime), Discord officiel + Discord des Owners, drapeaux FR / EN
// pour traduire le message, « J'ai compris » apres 3 s.
public class MagWarnWindow : Window
{
    public const string ServerUrl = "https://discord.gg/m7e6S2agGU", ServerShort = "discord.gg/m7e6S2agGU";
    static MagWarnWindow current;
    static readonly List<Action> pending = new List<Action>();
    readonly Border card;
    bool en, ready;
    int left = 3;
    TextBlock okText; Border ok;

    // injecteur lance par le launcher (qui a deja montre l'avertissement)
    public static bool FromLauncher { get { return Environment.GetEnvironmentVariable("MAG_GSC_LAUNCHER") == "1"; } }
    public static void MarkLauncher() { try { Environment.SetEnvironmentVariable("MAG_GSC_LAUNCHER", "1"); } catch { } }
    // avertissement ouvert : l'action attend sa fermeture (true) ; sinon rien (false)
    public static bool After(Action a) { if (current == null) return false; pending.Add(a); return true; }

    public static void Show(Window owner, Func<string, string, string> tr)
    {
        if (current != null || owner == null || !owner.IsLoaded) return;
        var w = new MagWarnWindow(owner, tr);
        current = w;
        try { w.ShowDialog(); } catch { }
        current = null;
        var p = pending.ToArray(); pending.Clear();
        foreach (var a in p) { try { owner.Dispatcher.BeginInvoke(a); } catch { } }
    }

    static readonly Color Red = MagUpdateWindow.C("#FF3B5C"), Orange = MagUpdateWindow.C("#FF9F1C"), Yellow = MagUpdateWindow.C("#FFE14D"),
        Purple = MagUpdateWindow.C("#A78BFA"), Cyan = MagUpdateWindow.C("#22D3EE"), Blurple = MagUpdateWindow.C("#5865F2");

    // langue de la fenetre (au depart celle du programme, puis celle des drapeaux)
    string T(string fr, string e) { return en ? e : fr; }

    MagWarnWindow(Window owner, Func<string, string, string> tr)
    {
        en = tr("fr", "en") == "en";
        MagUpdateWindow.Frame(this, owner, T("Attention aux arnaques", "Beware of scams"));
        Width = 680;

        // bord arc-en-ciel qui tourne + halo qui passe de l'orange au rouge
        var rot = new RotateTransform(0, 0.5, 0.5);
        var rainbow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1), RelativeTransform = rot };
        rainbow.GradientStops.Add(new GradientStop(Red, 0));
        rainbow.GradientStops.Add(new GradientStop(Orange, 0.3));
        rainbow.GradientStops.Add(new GradientStop(Yellow, 0.5));
        rainbow.GradientStops.Add(new GradientStop(Purple, 0.75));
        rainbow.GradientStops.Add(new GradientStop(Cyan, 1));
        var glow = new DropShadowEffect { Color = Orange, BlurRadius = 48, ShadowDepth = 0, Opacity = 0.75 };
        card = new Border
        {
            Margin = new Thickness(30), CornerRadius = new CornerRadius(22), BorderThickness = new Thickness(3), Padding = new Thickness(28, 24, 28, 22),
            BorderBrush = rainbow, Background = new LinearGradientBrush(MagUpdateWindow.C("#FC1A0B1C"), MagUpdateWindow.C("#FC070A16"), 90), Effect = glow
        };
        card.MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
        Loaded += delegate
        {
            rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(4)) { RepeatBehavior = RepeatBehavior.Forever });
            var ca = new ColorAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(3), RepeatBehavior = RepeatBehavior.Forever };
            ca.KeyFrames.Add(new LinearColorKeyFrame(Orange, KeyTime.FromPercent(0)));
            ca.KeyFrames.Add(new LinearColorKeyFrame(Red, KeyTime.FromPercent(0.5)));
            ca.KeyFrames.Add(new LinearColorKeyFrame(Orange, KeyTime.FromPercent(1)));
            glow.BeginAnimation(DropShadowEffect.ColorProperty, ca);
        };
        Content = card;
        Build();

        // « J'ai compris » apres 3 s
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        t.Tick += delegate
        {
            left--;
            if (left <= 0) { t.Stop(); ready = true; }
            UpdateOk();
        };
        Loaded += delegate { t.Start(); };
        Closing += delegate (object s, System.ComponentModel.CancelEventArgs e) { if (!ready) e.Cancel = true; };
        PreviewKeyDown += delegate (object s, KeyEventArgs e) { if (e.Key == Key.Escape || e.Key == Key.Enter) { e.Handled = true; if (ready) Close(); } };
    }

    // contenu de la carte dans la langue choisie (refait a chaque clic sur un drapeau)
    void Build()
    {
        Title = T("Attention aux arnaques", "Beware of scams");
        var st = new StackPanel();

        // en-tete : icone + titre en degrade + drapeaux
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var ic = MagUpdateWindow.T("", 26, Brushes.White, MagUpdateWindow.IconFont); ic.HorizontalAlignment = HorizontalAlignment.Center; ic.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(16), Background = new LinearGradientBrush(Red, Orange, 45), Child = ic, VerticalAlignment = VerticalAlignment.Center, Effect = new DropShadowEffect { Color = Red, BlurRadius = 24, ShadowDepth = 0, Opacity = 0.7 } });
        var ht = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var title = MagUpdateWindow.T(T("ATTENTION AUX ARNAQUES", "BEWARE OF SCAMS"), 27, Brushes.White, MagUpdateWindow.TitleFont);
        var tg = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        tg.GradientStops.Add(new GradientStop(Red, 0)); tg.GradientStops.Add(new GradientStop(Orange, 0.5)); tg.GradientStops.Add(new GradientStop(Yellow, 1));
        title.Foreground = tg;
        ht.Children.Add(title);
        var sub = MagUpdateWindow.T(T("Message officiel de UrBaaZz et ZERTY", "Official message from UrBaaZz and ZERTY"), 13.5, MagUpdateWindow.B("#C9CFDD"), MagUpdateWindow.TextFont); sub.Margin = new Thickness(0, 2, 0, 0);
        ht.Children.Add(sub);
        Grid.SetColumn(ht, 1); head.Children.Add(ht);
        var flags = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10, 2, 0, 0) };
        flags.Children.Add(FlagButton(false));
        flags.Children.Add(FlagButton(true));
        Grid.SetColumn(flags, 2); head.Children.Add(flags);
        st.Children.Add(head);

        // message
        var box = new Border { CornerRadius = new CornerRadius(14), Background = new LinearGradientBrush(MagUpdateWindow.A(Red, 0x2E), MagUpdateWindow.A(Orange, 0x1C), 0), BorderBrush = new SolidColorBrush(MagUpdateWindow.A(Red, 150)), BorderThickness = new Thickness(1.2), Padding = new Thickness(18, 14, 18, 15), Margin = new Thickness(0, 18, 0, 0) };
        var msg = new TextBlock { FontFamily = MagUpdateWindow.TextFont, FontSize = 15, Foreground = MagUpdateWindow.B("#F1F3F8"), TextWrapping = TextWrapping.Wrap, LineHeight = 22 };
        msg.Inlines.Add(new Run(T("Des personnes malveillantes essaient de vendre ", "Malicious people are trying to sell ")));
        msg.Inlines.Add(new Run("MAG GSC INJECTOR") { FontFamily = MagUpdateWindow.TitleFont, Foreground = new SolidColorBrush(Yellow) });
        msg.Inlines.Add(new Run(".\n"));
        msg.Inlines.Add(new Run(T("Les seuls créateurs sont ", "The only creators are ")) { FontFamily = MagUpdateWindow.TitleFont });
        msg.Inlines.Add(new Run("UrBaaZz") { FontFamily = MagUpdateWindow.TitleFont, Foreground = new SolidColorBrush(Purple) });
        msg.Inlines.Add(new Run(T(" et ", " and ")) { FontFamily = MagUpdateWindow.TitleFont });
        msg.Inlines.Add(new Run("ZERTY") { FontFamily = MagUpdateWindow.TitleFont, Foreground = new SolidColorBrush(Cyan) });
        msg.Inlines.Add(new Run(".\n"));
        msg.Inlines.Add(new Run(T("Si quelqu'un d'autre te le vend, c'est une arnaque : ne paie rien et préviens-nous sur le Discord officiel.",
                                  "If anyone else sells it to you, it's a scam: don't pay anything and tell us on the official Discord.")) { Foreground = MagUpdateWindow.B("#FFB4A8") });
        box.Child = msg;
        st.Children.Add(box);

        // Discord officiel + Discord des Owners
        st.Children.Add(Row("", T("Serveur Discord officiel", "Official Discord server"), ServerShort, Blurple, T("Rejoindre", "Join"), delegate (Border b)
        {
            try { Process.Start(new ProcessStartInfo(ServerUrl) { UseShellExecute = true }); } catch { try { Clipboard.SetText(ServerUrl); } catch { } }
        }));
        st.Children.Add(Row("", T("Discord de l'Owner UrBaaZz", "Owner UrBaaZz's Discord"), "urbaazz", Purple, T("Copier", "Copy"), delegate (Border b) { CopyName(b, "urbaazz"); }));
        st.Children.Add(Row("", T("Discord de l'Owner ZERTY", "Owner ZERTY's Discord"), "jlzerty", Cyan, T("Copier", "Copy"), delegate (Border b) { CopyName(b, "jlzerty"); }));

        okText = MagUpdateWindow.T("", 15, Brushes.White, MagUpdateWindow.TitleFont);
        ok = MagUpdateWindow.Button(okText, new LinearGradientBrush(Red, Orange, 0), new LinearGradientBrush(MagUpdateWindow.Lighter(Red), MagUpdateWindow.Lighter(Orange), 0), null);
        ok.HorizontalAlignment = HorizontalAlignment.Right; ok.Margin = new Thickness(0, 20, 0, 0); ok.MinWidth = 170;
        ok.Effect = new DropShadowEffect { Color = Orange, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.5 };
        ok.MouseLeftButtonUp += delegate { if (ready) Close(); };
        st.Children.Add(ok);
        UpdateOk();

        card.Child = st;
    }

    void UpdateOk()
    {
        okText.Text = T("J'ai compris", "I understand") + (ready ? "" : " (" + left + ")");
        ok.Opacity = ready ? 1 : 0.55;
        ok.Cursor = ready ? Cursors.Hand : Cursors.Arrow;
    }

    // drapeau francais ou anglais : change la langue du message
    FrameworkElement FlagButton(bool english)
    {
        var c = new Canvas { Width = 30, Height = 20, ClipToBounds = true };
        Action<double, double, double, double, string> rect = delegate (double x, double y, double w, double h, string col)
        {
            var r = new System.Windows.Shapes.Rectangle { Width = w, Height = h, Fill = MagUpdateWindow.B(col) };
            Canvas.SetLeft(r, x); Canvas.SetTop(r, y); c.Children.Add(r);
        };
        if (english)
        {
            rect(0, 0, 30, 20, "#012169");
            foreach (var th in new[] { 4.0, 1.6 })
                foreach (var l in new[] { new[] { 0.0, 0.0, 30.0, 20.0 }, new[] { 30.0, 0.0, 0.0, 20.0 } })
                    c.Children.Add(new System.Windows.Shapes.Line { X1 = l[0], Y1 = l[1], X2 = l[2], Y2 = l[3], StrokeThickness = th, Stroke = MagUpdateWindow.B(th > 2 ? "#FFFFFF" : "#C8102E") });
            rect(0, 7, 30, 6, "#FFFFFF"); rect(12, 0, 6, 20, "#FFFFFF");
            rect(0, 8.4, 30, 3.2, "#C8102E"); rect(13.4, 0, 3.2, 20, "#C8102E");
        }
        else { rect(0, 0, 10, 20, "#0055A4"); rect(10, 0, 10, 20, "#FFFFFF"); rect(20, 0, 10, 20, "#EF4135"); }
        c.Clip = new RectangleGeometry(new Rect(0, 0, 30, 20), 3, 3);
        bool sel = english == en;
        var b = new Border
        {
            Child = c, Padding = new Thickness(3), Margin = new Thickness(6, 0, 0, 0), CornerRadius = new CornerRadius(6), Cursor = Cursors.Hand,
            BorderThickness = new Thickness(1.5), BorderBrush = sel ? new SolidColorBrush(Yellow) : Brushes.Transparent,
            Background = sel ? MagUpdateWindow.B("#22FFFFFF") : Brushes.Transparent, Opacity = sel ? 1 : 0.5,
            ToolTip = english ? "English" : "Français"
        };
        b.MouseEnter += delegate { b.Opacity = 1; };
        b.MouseLeave += delegate { b.Opacity = english == en ? 1 : 0.5; };
        b.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e) { e.Handled = true; };
        b.MouseLeftButtonUp += delegate { if (en != english) { en = english; Build(); } };
        return b;
    }

    Border Row(string glyph, string label, string value, Color color, string action, Action<Border> click)
    {
        var row = new Border { CornerRadius = new CornerRadius(14), Background = new LinearGradientBrush(MagUpdateWindow.A(color, 0x26), MagUpdateWindow.C("#10C8D4FF"), 0), BorderBrush = new SolidColorBrush(MagUpdateWindow.A(color, 110)), BorderThickness = new Thickness(1), Padding = new Thickness(14, 10, 12, 10), Margin = new Thickness(0, 10, 0, 0) };
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var ic = MagUpdateWindow.T(glyph, 17, Brushes.White, MagUpdateWindow.IconFont); ic.HorizontalAlignment = HorizontalAlignment.Center; ic.VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(11), Background = new SolidColorBrush(color), Child = ic, VerticalAlignment = VerticalAlignment.Center, Effect = new DropShadowEffect { Color = color, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.6 } });
        var tx = new StackPanel { Margin = new Thickness(14, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        tx.Children.Add(MagUpdateWindow.T(label, 12.5, MagUpdateWindow.B("#AEB6C8"), MagUpdateWindow.TextFont));
        tx.Children.Add(MagUpdateWindow.T(value, 17, new SolidColorBrush(Lift(color)), MagUpdateWindow.TitleFont));
        Grid.SetColumn(tx, 1); g.Children.Add(tx);
        var bt = MagUpdateWindow.Button(MagUpdateWindow.T(action, 13.5, Brushes.White, MagUpdateWindow.TitleFont), new SolidColorBrush(MagUpdateWindow.A(color, 0xB0)), new SolidColorBrush(color), null);
        bt.MinWidth = 104; bt.Padding = new Thickness(16, 8, 16, 9); bt.VerticalAlignment = VerticalAlignment.Center;
        bt.MouseLeftButtonUp += delegate { click(bt); };
        Grid.SetColumn(bt, 2); g.Children.Add(bt);
        row.Child = g;
        return row;
    }

    static Color Lift(Color c) { return Color.FromRgb((byte)Math.Min(255, c.R + 40), (byte)Math.Min(255, c.G + 40), (byte)Math.Min(255, c.B + 40)); }

    void CopyName(Border b, string name)
    {
        var t = b.Child as TextBlock; if (t == null) return;
        try { Clipboard.SetText(name); } catch { }
        string old = T("Copier", "Copy");
        t.Text = T("Copié !", "Copied!");
        var d = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        d.Tick += delegate { d.Stop(); t.Text = old; };
        d.Start();
    }
}

// Lecteur JSON minimal : objets, listes, textes, nombres, true / false / null.
public class MagJson
{
    readonly string s; int i;
    public MagJson(string text) { s = text ?? ""; }
    public object Parse() { var v = Value(0); Ws(); if (i != s.Length) throw new FormatException("JSON"); return v; }
    void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
    void Expect(char c) { Ws(); if (i >= s.Length || s[i] != c) throw new FormatException("JSON"); i++; }
    bool Lit(string w) { if (string.CompareOrdinal(s, i, w, 0, w.Length) == 0) { i += w.Length; return true; } return false; }
    object Value(int depth)
    {
        if (depth > 32) throw new FormatException("JSON");
        Ws(); if (i >= s.Length) throw new FormatException("JSON");
        char c = s[i];
        if (c == '{')
        {
            i++; var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase); Ws();
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(); string k = Str(); Expect(':'); d[k] = Value(depth + 1); Ws();
                if (i < s.Length && s[i] == ',') { i++; continue; }
                Expect('}'); return d;
            }
        }
        if (c == '[')
        {
            i++; var l = new List<object>(); Ws();
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(depth + 1)); Ws();
                if (i < s.Length && s[i] == ',') { i++; continue; }
                Expect(']'); return l;
            }
        }
        if (c == '"') return Str();
        if (Lit("true")) return true;
        if (Lit("false")) return false;
        if (Lit("null")) return null;
        int st = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        if (i == st) throw new FormatException("JSON");
        return double.Parse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture);
    }
    string Str()
    {
        if (i >= s.Length || s[i] != '"') throw new FormatException("JSON");
        i++; var sb = new StringBuilder();
        while (true)
        {
            if (i >= s.Length) throw new FormatException("JSON");
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }
            if (i >= s.Length) throw new FormatException("JSON");
            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    if (i + 4 > s.Length) throw new FormatException("JSON");
                    sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); i += 4; break;
                default: sb.Append(e); break;
            }
        }
    }
}
