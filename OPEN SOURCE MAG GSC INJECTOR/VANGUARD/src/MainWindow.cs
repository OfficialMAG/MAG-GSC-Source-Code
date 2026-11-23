// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using IOPath = System.IO.Path;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: AssemblyTitle(ToolInfo.WindowTitle)]
[assembly: AssemblyProduct("MAG GSC INJECTOR")]
[assembly: AssemblyCompany("UrBaaZz & Zerty")]
[assembly: AssemblyCopyright("Dev by UrBaaZz and Zerty")]
[assembly: AssemblyVersion(ToolInfo.Version)]
[assembly: AssemblyFileVersion(ToolInfo.Version)]

abstract class Page : ScrollViewer
{
    protected MainWindow W; protected Core Core;
    public string Title, Glyph, Subtitle;
    protected CardGrid Grid2 = new CardGrid();
    protected Page(MainWindow w, string title, string glyph, string subtitle)
    {
        W = w; Core = w.Core; Title = title; Glyph = glyph; Subtitle = subtitle;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto; HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled; Focusable = false;
        var st = new StackPanel { Margin = new Thickness(18, 8, 18, 24) };
        var h = Ui.T(title, 30, Ui.TextMain, Ui.Title, FontWeights.SemiBold); h.Margin = new Thickness(8, 0, 0, 0);
        h.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.6 };
        st.Children.Add(h);
        if (!string.IsNullOrEmpty(subtitle)) { var s = Ui.T(subtitle, 13.5, Ui.TextSub); s.Margin = new Thickness(8, 2, 0, 8); s.TextWrapping = TextWrapping.Wrap; st.Children.Add(s); }
        else h.Margin = new Thickness(8, 0, 0, 10);
        st.Children.Add(Grid2);
        Content = st;
    }
    public virtual void OnShow() { }
    public virtual void OnHide() { }
    public virtual void OnStatus() { }
    public virtual void OnClose() { }
    protected bool NeedReady() { if (Core.Ready) return true; W.Toast(L.T("Connecte-toi d'abord à la PS4 (page Accueil)", "Connect to the PS4 first (Home page)"), Ui.Warn); return false; }
    protected void Do(string what, Action work, Action done = null)
    {
        if (!NeedReady()) return;
        Core.Post(delegate
        {
            try { work(); Ui.Run(delegate { if (done != null) done(); }); }
            catch (Exception ex) { Ui.Run(delegate { W.Toast(what + " : " + ex.Message, Ui.Danger); }); }
        });
    }
}

class MainWindow : Window
{
    public readonly Core Core = new Core();
    readonly string dir = AppDomain.CurrentDomain.BaseDirectory;
    string cfgPath;
    public readonly Dictionary<string, string> Cfg = new Dictionary<string, string>();

    Grid root, pageHost, mainGrid;
    Border sidebar, toast, navIndicator, statusPill;
    StackPanel navList, badgeText;
    TextBlock toastText, statusText, maxGlyph;
    Ellipse statusDot;
    DispatcherTimer toastTimer;
    readonly List<Page> pages = new List<Page>();
    readonly List<Border> navItems = new List<Border>();
    readonly List<TextBlock> navLabels = new List<TextBlock>();
    Page current;
    bool compact;
    public readonly List<string> LogLines = new List<string>();
    public event Action LogChanged = delegate { };

    const string GlyphMin = "", GlyphMax = "", GlyphRestore = "", GlyphClose = "";

    public MainWindow()
    {
        Ui.Disp = Dispatcher;
        cfgPath = IOPath.Combine(dir, "MAG_GSC_INJECTOR.cfg");
        LoadCfg();
        L.En = LangArg != null ? LangArg == "en" : CfgGet("lang", "fr") == "en";
        Title = ToolInfo.WindowTitle;
        var wa = SystemParameters.WorkArea;
        Width = Math.Min(1500, Math.Max(900, wa.Width * 0.82)); Height = Math.Min(940, Math.Max(600, wa.Height * 0.86));
        MinWidth = 720; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize;
        Background = Ui.B("#05070E"); UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 52, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false });
        try { var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("mag.ico"); if (s != null) Icon = BitmapFrame.Create(s); } catch { }
        Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(Ui.Styles));

        Core.Log += delegate (string text, bool err) { Ui.Run(delegate { AddLog(text, err); }); };
        Core.TraceLine += delegate (string text) { Ui.Run(delegate { AddLog(text, false); }); };
        Core.StatusChanged += delegate { Ui.Run(OnStatus); };

        BuildUi();
        SourceInitialized += delegate { Dwm(); };
        StateChanged += delegate
        {
            root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            maxGlyph.Text = WindowState == WindowState.Maximized ? GlyphRestore : GlyphMax;
        };
        SizeChanged += delegate { Relayout(); };
        Loaded += delegate { Relayout(); Show(0, false); };
        MagFx.Attach(this);

        ContentRendered += delegate { if (!capturing) MagUpdate.CheckAsync(this, ToolInfo.AppId, "MAG GSC INJECTOR " + ToolInfo.Game, Ui.Accent, Ui.Accent2, L.T); };
        Closing += delegate { SaveCfg(); Core.Stop(); if (!capturing) MagStats.Bye(); };
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    void Dwm() { try { var h = new WindowInteropHelper(this).Handle; int dark = 1, round = 2; DwmSetWindowAttribute(h, 20, ref dark, 4); DwmSetWindowAttribute(h, 33, ref round, 4); } catch { } }

    void LoadCfg()
    {
        try { if (File.Exists(cfgPath)) foreach (var l in File.ReadAllLines(cfgPath)) { int i = l.IndexOf('='); if (i > 0) Cfg[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim(); } } catch { }
    }
    public void SaveCfg()
    {
        try
        {
            var lines = new List<string>(); foreach (var kv in Cfg) lines.Add(kv.Key + "=" + kv.Value);
            File.WriteAllLines(cfgPath, lines.ToArray());
        }
        catch { }
    }
    public string CfgGet(string k, string def) { string v; return Cfg.TryGetValue(k, out v) ? v : def; }

    Canvas deco;
    FrameworkElement ribbon, ribbonEcho, ribbonTop;

    void BuildUi()
    {
        root = new Grid(); Content = root;
        var bg = new Grid { IsHitTestVisible = false, ClipToBounds = true };
        bg.Children.Add(new Rectangle { Fill = Ui.Grad(Ui.C("#0B1122"), Ui.C("#04060C"), 90) });

        var grid = new DrawingBrush { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 44, 44), ViewportUnits = BrushMappingMode.Absolute, Opacity = 0.55 };
        var gg = new GeometryGroup(); gg.Children.Add(new LineGeometry(new Point(0, 0), new Point(44, 0))); gg.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, 44)));
        grid.Drawing = new GeometryDrawing(null, new Pen(Ui.B("#0CFFFFFF"), 1), gg);
        bg.Children.Add(new Rectangle { Fill = grid });
        deco = new Canvas(); bg.Children.Add(deco);

        ribbon = Ribbon(24, 0.55, true); ribbonEcho = Ribbon(6, 0.4, false); ribbonTop = Ribbon(5, 0.2, false);
        deco.Children.Add(ribbon); deco.Children.Add(ribbonEcho); deco.Children.Add(ribbonTop);

        var glow = new RadialGradientBrush { Center = new Point(0.06, 1.04), GradientOrigin = new Point(0.06, 1.04), RadiusX = 0.5, RadiusY = 0.6 };
        glow.GradientStops.Add(new GradientStop(Ui.A(Ui.Accent, 34), 0)); glow.GradientStops.Add(new GradientStop(Ui.A(Ui.Accent, 0), 1));
        bg.Children.Add(new Rectangle { Fill = glow });
        var vig = new RadialGradientBrush { RadiusX = 0.9, RadiusY = 0.9 };
        vig.GradientStops.Add(new GradientStop(Ui.C("#00000000"), 0.5)); vig.GradientStops.Add(new GradientStop(Ui.C("#B0000000"), 1));
        bg.Children.Add(new Rectangle { Fill = vig });
        root.Children.Add(bg);
        BuildMain();
    }

    static FrameworkElement Ribbon(double band, double opacity, bool halo)
    {
        var g = new Grid { Width = band * 3, Height = 3200, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(36), Opacity = opacity };
        Color[] c = { Ui.FlagBlue, Ui.FlagWhite, Ui.FlagRed };
        if (halo)
        {
            var h = new Grid { Margin = new Thickness(-30, 0, -30, 0), Effect = new BlurEffect { Radius = 46 }, Opacity = 0.8 };
            for (int i = 0; i < 3; i++) { h.ColumnDefinitions.Add(new ColumnDefinition()); var r = new Rectangle { Fill = Ui.B(c[i]) }; Grid.SetColumn(r, i); h.Children.Add(r); }
            g.Children.Add(h);
        }
        var bands = new Grid();
        for (int i = 0; i < 3; i++) { bands.ColumnDefinitions.Add(new ColumnDefinition()); var r = new Rectangle { Fill = Ui.B(c[i]) }; Grid.SetColumn(r, i); bands.Children.Add(r); }
        g.Children.Add(bands);
        var shine = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        shine.GradientStops.Add(new GradientStop(Ui.C("#00FFFFFF"), 0.40)); shine.GradientStops.Add(new GradientStop(Ui.C("#70FFFFFF"), 0.5)); shine.GradientStops.Add(new GradientStop(Ui.C("#00FFFFFF"), 0.60));
        var st = new TranslateTransform(0, -1); shine.RelativeTransform = st;
        g.Children.Add(new Rectangle { Fill = shine });
        Ui.LoopFx(st, TranslateTransform.YProperty, -0.6, 0.6, 7, false);

        var fade = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        fade.GradientStops.Add(new GradientStop(Ui.C("#FF000000"), 0)); fade.GradientStops.Add(new GradientStop(Ui.C("#FF000000"), 0.512)); fade.GradientStops.Add(new GradientStop(Ui.C("#00000000"), 0.585));
        g.OpacityMask = fade;
        return g;
    }

    void BuildMain()
    {
        var main = new Grid(); mainGrid = main;
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        root.Children.Add(main);
        main.Children.Add(TopBar());

        var body = new Grid(); Grid.SetRow(body, 1); main.Children.Add(body);
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(Sidebar());
        pageHost = new Grid { ClipToBounds = true }; Grid.SetColumn(pageHost, 1); body.Children.Add(pageHost);

        main.Children.Add(Footer());

        pages.Add(new PageHome(this));
        pages.Add(new PageGsc(this));
        pages.Add(new PageAbout(this));
        for (int i = 0; i < pages.Count; i++) AddNav(i);

        toast = new Border
        {
            CornerRadius = new CornerRadius(14), Background = Ui.B("#F20B1122"), BorderThickness = new Thickness(1), Padding = new Thickness(18, 11, 18, 12),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(20, 0, 20, 54), MaxWidth = 720,
            Opacity = 0, Visibility = Visibility.Collapsed, IsHitTestVisible = false, RenderTransform = new TranslateTransform(),
            Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 0, Opacity = 0.5 }
        };
        toastText = Ui.T("", 13.5, Ui.TextMain); toastText.TextWrapping = TextWrapping.Wrap; toastText.TextAlignment = TextAlignment.Center;
        toast.Child = toastText; root.Children.Add(toast);
        toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3400) };
        toastTimer.Tick += delegate
        {
            toastTimer.Stop();
            var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
            a.Completed += delegate { if (toast.Opacity < 0.05) toast.Visibility = Visibility.Collapsed; };
            toast.BeginAnimation(OpacityProperty, a);
        };
        OnStatus();
    }

    UIElement TopBar()
    {
        var bar = new Grid { Margin = new Thickness(16, 0, 0, 0) };
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(Ui.Logo(30));
        var name = Ui.T("MAG GSC INJECTOR", 16, Ui.TextMain, Ui.Title, FontWeights.SemiBold); name.VerticalAlignment = VerticalAlignment.Center; name.Margin = new Thickness(10, 0, 6, 0);
        left.Children.Add(name);
        left.Children.Add(new Border { CornerRadius = new CornerRadius(7), Background = Ui.Grad(Ui.Accent, Ui.Accent2, 0), Padding = new Thickness(7, 1, 7, 2), VerticalAlignment = VerticalAlignment.Center, Child = Ui.T("V1", 11.5, Brushes.White, Ui.Title, FontWeights.SemiBold) });
        var game = new Border { CornerRadius = new CornerRadius(8), Background = Ui.B(Ui.A(Ui.Accent, 30)), BorderBrush = Ui.B(Ui.A(Ui.Accent, 110)), BorderThickness = new Thickness(1), Padding = new Thickness(9, 2, 9, 3), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = Ui.T(ToolInfo.Game + "  •  PS4", 11.5, Ui.B(Ui.Accent), Ui.Title, FontWeights.SemiBold) };
        left.Children.Add(game);
        bar.Children.Add(left);

        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        var langs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        langs.Children.Add(LangButton(L.FlagFr(), "Français", false));
        langs.Children.Add(LangButton(L.FlagEn(), "English", true));
        right.Children.Add(langs);
        statusDot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        statusText = Ui.T("", 12.5, Ui.TextMain, Ui.Title); statusText.VerticalAlignment = VerticalAlignment.Center;
        var sp = new StackPanel { Orientation = Orientation.Horizontal }; sp.Children.Add(statusDot); sp.Children.Add(statusText);
        statusPill = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 5, 14, 6), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(1), Child = sp, Cursor = Cursors.Hand };
        statusPill.MouseLeftButtonUp += delegate { Show(0, true); };
        WindowChrome.SetIsHitTestVisibleInChrome(statusPill, true);
        right.Children.Add(statusPill);
        right.Children.Add(Caption(GlyphMin, false, delegate { WindowState = WindowState.Minimized; }));
        var mx = Caption(GlyphMax, false, delegate { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; });
        maxGlyph = (TextBlock)((Grid)((Border)((Grid)mx.Child).Children[1]).Child).Children[0];
        right.Children.Add(mx);
        right.Children.Add(Caption(GlyphClose, true, delegate { Close(); }));
        bar.Children.Add(right);
        return bar;
    }

    Border LangButton(FrameworkElement flagIcon, string name, bool en)
    {
        bool on = L.En == en;
        var b = new Border
        {
            CornerRadius = new CornerRadius(6), Padding = new Thickness(3), Margin = new Thickness(2, 0, 2, 0), BorderThickness = new Thickness(1.5),
            BorderBrush = Ui.B(on ? Ui.Accent : Ui.C("#00FFFFFF")), Background = Ui.B(on ? Ui.A(Ui.Accent, 34) : Ui.C("#00FFFFFF")),
            Child = flagIcon, Cursor = Cursors.Hand, ToolTip = name, Opacity = on ? 1 : 0.5, VerticalAlignment = VerticalAlignment.Center
        };
        if (!on)
        {
            b.MouseEnter += delegate { Ui.Anim(b, OpacityProperty, 0.9, 140); };
            b.MouseLeave += delegate { Ui.Anim(b, OpacityProperty, 0.5, 200); };
            b.MouseLeftButtonUp += delegate { SetLang(en); };
        }
        WindowChrome.SetIsHitTestVisibleInChrome(b, true);
        return b;
    }

    public void SetLang(bool en)
    {
        if (L.En == en) return;
        L.En = en; Cfg["lang"] = en ? "en" : "fr"; SaveCfg();
        int idx = Math.Max(0, pages.IndexOf(current));
        string mg = maxGlyph.Text;
        foreach (var p in pages) p.OnClose();
        toastTimer.Stop(); Ui.StopLoop(statusDot, OpacityProperty);
        root.Children.Remove(mainGrid); root.Children.Remove(toast);
        pages.Clear(); navItems.Clear(); navLabels.Clear(); navHeaders.Clear(); current = null;
        LogChanged = delegate { };
        BuildMain();
        maxGlyph.Text = mg;
        if (compact) ApplyCompact(true, false);
        Show(idx, false);
        Ok(L.T("Langue : français", "Language: English"));
    }

    FlatButton Caption(string glyph, bool danger, Action click)
    {
        var t = Ui.Icon(glyph, 11, Ui.B("#C9CEDA")); t.HorizontalAlignment = HorizontalAlignment.Center;
        var host = new Grid(); host.Children.Add(t);
        var b = new FlatButton(host, Ui.C("#00FFFFFF"), danger ? Ui.C("#E0E81123") : Ui.C("#22FFFFFF"), 0, new Thickness(0)) { Width = 50, Height = 52 };
        b.Click += click;
        WindowChrome.SetIsHitTestVisibleInChrome(b, true);
        return b;
    }

    UIElement Sidebar()
    {
        sidebar = new Border { Width = 236, Margin = new Thickness(12, 4, 6, 8), CornerRadius = new CornerRadius(16), Background = Ui.Grad(Ui.C("#B00B1124"), Ui.C("#C407091A"), 90), BorderBrush = Ui.Grad(Ui.C("#2CFFFFFF"), Ui.C("#0CFFFFFF"), 90), BorderThickness = new Thickness(1) };
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var bar = new Border { Width = 3, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-1, 10, 0, 10), Background = Ui.B(Ui.Accent), Effect = new DropShadowEffect { Color = Ui.Accent, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 } };
        navIndicator = new Border { Height = 40, VerticalAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(10), Margin = new Thickness(8, 0, 8, 0), Background = Ui.Grad(Ui.A(Ui.Accent, 58), Ui.A(Ui.Accent2, 14), 0), BorderBrush = Ui.B(Ui.A(Ui.Accent, 120)), BorderThickness = new Thickness(1), RenderTransform = new TranslateTransform(0, 10), IsHitTestVisible = false, Child = bar };
        navList = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        var navScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        var navHost = new Grid(); navHost.Children.Add(navIndicator); navHost.Children.Add(navList); navScroll.Content = navHost;
        g.Children.Add(navScroll);

        var badge = new Border { Margin = new Thickness(10, 0, 10, 10), Padding = new Thickness(10, 9, 10, 9), CornerRadius = new CornerRadius(12), Background = Ui.B("#14C8D4FF"), BorderBrush = Ui.B("#1EC8D4FF"), BorderThickness = new Thickness(1) };
        var brow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var mf = Ui.MiniFlag(27, 18); mf.VerticalAlignment = VerticalAlignment.Center; brow.Children.Add(mf);
        badgeText = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        badgeText.Children.Add(Ui.T(L.T("FAIT EN FRANCE", "MADE IN FRANCE"), 11.5, Ui.TextMain, Ui.Title, FontWeights.SemiBold));
        badgeText.Children.Add(Ui.T("UrBaaZz & Zerty", 11, Ui.TextSub, Ui.Title));
        brow.Children.Add(badgeText);
        badge.Child = brow;
        Grid.SetRow(badge, 1); g.Children.Add(badge);
        sidebar.Child = g;
        return sidebar;
    }

    static readonly Dictionary<int, string> Sections = new Dictionary<int, string> { { 1, "I N J E C T I O N" }, { 2, "I N F O S" } };
    readonly List<TextBlock> navHeaders = new List<TextBlock>();
    void AddNav(int i)
    {
        var p = pages[i];
        string sec;
        if (Sections.TryGetValue(i, out sec))
        {
            var h = Ui.T(sec, 10.5, Ui.TextDim, Ui.Title, FontWeights.SemiBold); h.Margin = new Thickness(22, 12, 0, 6); h.Tag = sec;
            navList.Children.Add(h); navHeaders.Add(h);
        }
        var row = new Grid { Height = 40 };
        var ic = Ui.Icon(p.Glyph, 16, Ui.TextSub); ic.Margin = new Thickness(18, 0, 0, 0); ic.HorizontalAlignment = HorizontalAlignment.Left;
        row.Children.Add(ic);
        var lb = Ui.T(p.Title, 14, Ui.TextSub, Ui.Title); lb.VerticalAlignment = VerticalAlignment.Center; lb.Margin = new Thickness(50, 0, 0, 0);
        row.Children.Add(lb);
        var item = new Border { Height = 40, Margin = new Thickness(8, 0, 8, 2), CornerRadius = new CornerRadius(10), Background = Ui.B("#00FFFFFF"), Child = row, Cursor = Cursors.Hand, ToolTip = p.Title };
        var hover = new SolidColorBrush(Ui.C("#00FFFFFF")); item.Background = hover;
        item.MouseEnter += delegate { Ui.ColorTo(hover, Ui.C("#12FFFFFF"), 140); };
        item.MouseLeave += delegate { Ui.ColorTo(hover, Ui.C("#00FFFFFF"), 200); };
        int k = i; item.MouseLeftButtonUp += delegate { Show(k, true); };
        navItems.Add(item); navLabels.Add(lb);
        navList.Children.Add(item);
    }

    public void Show(int i, bool animate)
    {
        if (i < 0 || i >= pages.Count) return;
        var p = pages[i];
        if (current == p) return;
        var old = current; current = p;
        for (int k = 0; k < navItems.Count; k++)
        {
            bool on = k == i;
            navLabels[k].Foreground = on ? Brushes.White : Ui.TextSub;
            ((TextBlock)((Grid)navItems[k].Child).Children[0]).Foreground = on ? Ui.B(Ui.Accent) : Ui.TextSub;
        }
        var target = navItems[i];
        Dispatcher.BeginInvoke(new Action(delegate
        {
            var pt = target.TranslatePoint(new Point(0, 0), (UIElement)navIndicator.Parent);
            Ui.Anim((TranslateTransform)navIndicator.RenderTransform, TranslateTransform.YProperty, pt.Y, animate ? 320 : 1, new CubicEase { EasingMode = EasingMode.EaseInOut });
        }), DispatcherPriority.Loaded);
        if (old != null)
        {
            old.OnHide();
            var o = old; var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(140));
            fade.Completed += delegate { if (current != o) pageHost.Children.Remove(o); };
            o.BeginAnimation(OpacityProperty, fade);
        }
        if (!pageHost.Children.Contains(p)) pageHost.Children.Add(p);
        p.Opacity = 0; var tt = new TranslateTransform(0, 18); p.RenderTransform = tt;
        Ui.Anim(p, OpacityProperty, 1, 280, null, 60);
        Ui.Anim(tt, TranslateTransform.YProperty, 0, 380, new CubicEase { EasingMode = EasingMode.EaseOut }, 60);
        p.ScrollToTop();
        p.OnShow();
    }
    public void ShowPage<T>() where T : Page { for (int i = 0; i < pages.Count; i++) if (pages[i] is T) Show(i, true); }

    UIElement Footer()
    {
        var f = new Grid(); Grid.SetRow(f, 2);
        var inner = new Grid { Margin = new Thickness(22, 0, 22, 0) };
        f.Children.Add(inner);
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(Brand.Credit(13));
        left.Children.Add(Contact.FooterPill(delegate (string m) { Ok(m); }));
        inner.Children.Add(left);
        var ver = Ui.T("MAG GSC INJECTOR v" + MagUpdate.Show(MagUpdate.Current) + "  •  " + ToolInfo.GameFull + " (" + ToolInfo.Update + ")  •  PS4", 12, Ui.TextDim, Ui.Title); ver.HorizontalAlignment = HorizontalAlignment.Right; ver.VerticalAlignment = VerticalAlignment.Center;
        inner.Children.Add(ver);
        return f;
    }

    void Relayout()
    {
        double w = ActualWidth, h = ActualHeight;
        Place(ribbon, w * 0.80, h * 0.66); Place(ribbonEcho, w * 0.80 + 74, h * 0.66); Place(ribbonTop, w * 0.06, h * 0.04);
        bool c = ActualWidth < 1060;
        if (c != compact) { compact = c; ApplyCompact(c, true); }
    }

    static void Place(FrameworkElement e, double cx, double cy) { Canvas.SetLeft(e, cx - e.Width / 2); Canvas.SetTop(e, cy - e.Height / 2); }

    void ApplyCompact(bool c, bool animate)
    {
        if (animate) Ui.Anim(sidebar, WidthProperty, c ? 66 : 236, 260); else { sidebar.BeginAnimation(WidthProperty, null); sidebar.Width = c ? 66 : 236; }
        foreach (var l in navLabels) l.Visibility = c ? Visibility.Collapsed : Visibility.Visible;
        foreach (var l in navHeaders) l.Text = c ? "•" : l.Tag as string ?? l.Text;
        badgeText.Visibility = c ? Visibility.Collapsed : Visibility.Visible;
    }

    void OnStatus()
    {
        bool ok = Core.Ready;
        var col = ok ? Ui.Accent : Core.Busy ? Ui.Warn : Ui.C("#FF5A6E");
        statusDot.Fill = Ui.B(col);
        statusPill.Background = Ui.B(Ui.A(col, 26)); statusPill.BorderBrush = Ui.B(Ui.A(col, 100));
        UpdateStatusText();
        if (ok || Core.Busy) Ui.Loop(statusDot, OpacityProperty, 1, 0.3, 1, true); else Ui.StopLoop(statusDot, OpacityProperty);
        if (ok) SaveCfg();
        foreach (var p in pages) p.OnStatus();
    }

    void UpdateStatusText()
    {
        if (!Core.Ready) { statusText.Text = StateText(Core.State); return; }
        statusText.Text = L.T("Connecté", "Connected") + "  •  " + Core.Ip;
    }

    static string StateText(string s)
    {
        switch (s)
        {
            case "Déconnecté": return L.T(s, "Disconnected");
            case "Connexion perdue": return L.T(s, "Connection lost");
            case "Connexion...": return L.T(s, "Connecting...");
            case "Jeu introuvable": return L.T(s, "Game not found");
            case "Connecté": return L.T(s, "Connected");
            case "Échec": return L.T(s, "Failed");
            default: return s;
        }
    }

    void AddLog(string text, bool err)
    {
        LogLines.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + (err ? "⚠ " : "") + text);
        if (LogLines.Count > 2000) LogLines.RemoveAt(0);
        LogChanged();
        if (err) Toast(text, Ui.Danger);
    }

    public void Toast(string text, Color accent)
    {
        toastText.Text = text;
        toast.BorderBrush = Ui.B(Ui.A(accent, 160)); ((DropShadowEffect)toast.Effect).Color = accent;
        toast.Visibility = Visibility.Visible;
        var tt = (TranslateTransform)toast.RenderTransform;
        Ui.Anim(toast, OpacityProperty, 1, 200);
        tt.Y = 22; Ui.Anim(tt, TranslateTransform.YProperty, 0, 360, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 });
        toastTimer.Stop(); toastTimer.Start();
    }
    public void Ok(string text) { Toast(text, Ui.Accent); }

    bool capturing;
    public void Capture(string path, int w, int h, int page, bool connected)
    {
        MagFx.NoPause();
        capturing = true;
        Width = w; Height = h; WindowStartupLocation = WindowStartupLocation.Manual; Left = -30000; Top = 0; ShowActivated = false; ShowInTaskbar = false;
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(connected ? 2600 : 1600) };
        int step = 0;
        Loaded += delegate { Show(page, false); t.Start(); };
        t.Tick += delegate
        {
            if (connected && step++ == 0) { current = null; Show(page, false); t.Interval = TimeSpan.FromMilliseconds(1800); return; }
            t.Stop();
            var rtb = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(path)) enc.Save(fs);
            Close();
        };
    }

    static string LangArg;

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "--magselftest") { SelfTest(args.Length > 1 ? args[1] : "selftest.txt"); return; }
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        int li = Array.IndexOf(args, "--lang"); if (li >= 0 && li + 1 < args.Length) LangArg = args[li + 1];
        var w = new MainWindow();
        int ci = Array.IndexOf(args, "--connect");
        if (ci >= 0 && ci + 1 < args.Length) { string ipArg = args[ci + 1]; w.Loaded += delegate { w.Core.Connect(ipArg); }; }
        if (args.Length >= 4 && args[0] == "--capture") w.Capture(args[1], int.Parse(args[2]), int.Parse(args[3]), args.Length > 4 && !args[4].StartsWith("--") ? int.Parse(args[4]) : 0, ci >= 0);
        app.Run(w);
    }

    static void SelfTest(string outPath)
    {
        var sb = new System.Text.StringBuilder();
        try
        {
            foreach (var m in new[] { Injector.GameMode.Multiplayer, Injector.GameMode.Zombies })
            {
                Injector.Mode = m;
                string err; var s = Injector.Parse(Injector.Menu(), out err);
                sb.AppendLine(m + " : " + (s != null ? "OK bytecode=" + s.Bytecode.Length + " o" : "ERREUR " + err));
            }
            sb.AppendLine("RESULTAT " + (sb.ToString().IndexOf("ERREUR") < 0 ? "OK" : "ECHEC"));
        }
        catch (Exception ex) { sb.AppendLine("EXCEPTION " + ex.GetType().Name + " " + ex.Message); sb.AppendLine("RESULTAT ECHEC"); }
        try { File.WriteAllText(outPath, sb.ToString()); } catch { }
    }
}
