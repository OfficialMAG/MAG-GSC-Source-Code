// MULTI COD MAG INJECTOR GSC - launcher des injecteurs GSC PS4 (BO1, BO2, BO3, BO4, BOCW, Ghosts, AW, IW, MWR, WWII).
// WPF construit en code : meme theme que les MAG GSC INJECTOR (verre bleu nuit, ruban bleu / blanc / rouge discret),
// orange pour les Black Ops, vert pour les autres opus. Interface vectorielle qui s'adapte a la fenetre et a l'ecran.
// Mises a jour, desactivation a distance et compteur d'utilisateurs : UpdateCheck.cs (version.json sur le serveur MAG).
// Dev By UrBaaZz and Zerty.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
[assembly: AssemblyTitle("MULTI COD MAG INJECTOR GSC")]
[assembly: AssemblyProduct("MULTI COD MAG INJECTOR GSC")]
[assembly: AssemblyCompany("UrBaaZz & Zerty")]
[assembly: AssemblyCopyright("Dev By UrBaaZz and Zerty")]
[assembly: AssemblyVersion("1.0.11.0")]
[assembly: AssemblyFileVersion("1.0.11.0")]

public class GameInfo
{
    public string Id, Short, Name, Studio, Year;
    public bool BlackOps;          // orange (Black Ops) ou vert (autres opus)
    public Color Accent, Accent2;
    public string[] ChipsFr, ChipsEn; // contenu de l'injecteur (vide = « GSC », « PS4 »)
    public string ConfiguredExe;
}

// Carte d'un jeu : dessinee a 236 x 344 puis mise a l'echelle par un Viewbox (nette a toutes les tailles).
class GameCard : Viewbox
{
    public const double DW = 236, DH = 344;
    public readonly GameInfo Game;
    public event Action<GameCard> Clicked;
    readonly ScaleTransform scale = new ScaleTransform(1, 1);
    readonly TranslateTransform lift = new TranslateTransform(0, 0);
    readonly Border glowHost;              // lueur de survol : couche mise en cache (opacite animee, flou jamais recalcule)
    readonly DropShadowEffect barGlow;
    readonly TranslateTransform shine = new TranslateTransform(-300, 0);
    readonly RotateTransform hexTurn = new RotateTransform(0);
    readonly Border bar, pill;
    readonly TextBlock barText, barIcon, barArrow, pillText, info;
    readonly Ellipse pillDot, topGlow;
    readonly FrameworkElement big;
    public bool Available { get; private set; }
    public bool Outdated; // une version plus recente de l'injecteur est publiee (version.json)
    public bool Killed;   // desactive a distance (bloc « desactivation » de version.json)
    int online;

    public GameCard(GameInfo g)
    {
        Game = g;
        Stretch = Stretch.Uniform;
        Cursor = Cursors.Hand;
        RenderTransformOrigin = new Point(0.5, 0.5);
        var tg = new TransformGroup(); tg.Children.Add(scale); tg.Children.Add(lift); RenderTransform = tg;
        var a = g.Accent;

        var root = new Grid { Width = DW, Height = DH };
        // lueur de survol : un cadre transparent avec juste une ombre coloree, rasterise une fois (BitmapCache).
        // On anime SON opacite (compositing GPU) au lieu de l'opacite d'un DropShadowEffect (qui recalculerait le
        // flou a chaque image). C'est ce qui rendait la navigation saccadee.
        glowHost = new Border
        {
            CornerRadius = new CornerRadius(22), BorderThickness = new Thickness(1.4), Background = Brushes.Transparent,
            BorderBrush = Ui.B(Ui.A(a, 190)), IsHitTestVisible = false, Opacity = 0,
            CacheMode = new BitmapCache(),
            Effect = new DropShadowEffect { Color = a, BlurRadius = 26, ShadowDepth = 0, Opacity = 0.95 }
        };
        root.Children.Add(glowHost);
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(22), BorderThickness = new Thickness(1.3),
            Background = Ui.Grad(Ui.C("#EC0F172E"), Ui.C("#F4060A16"), 90),
            BorderBrush = Ui.Grad(Ui.A(a, 160), Ui.C("#18C8D4FF"), 60)
        });

        var inner = new Grid { Width = DW, Height = DH, Clip = new RectangleGeometry(new Rect(0, 0, DW, DH), 22, 22), IsHitTestVisible = false };
        root.Children.Add(inner);
        // halo de la couleur du jeu en haut a droite
        topGlow = new Ellipse { Width = 300, Height = 300, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(80, -165, 0, 0), Fill = new RadialGradientBrush(Ui.A(a, 110), Ui.A(a, 0)) };
        inner.Children.Add(topGlow);
        // grille tactique tres discrete
        var gb = new DrawingBrush { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 22, 22), ViewportUnits = BrushMappingMode.Absolute };
        var gg = new GeometryGroup(); gg.Children.Add(new LineGeometry(new Point(0, 0), new Point(22, 0))); gg.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, 22)));
        gb.Drawing = new GeometryDrawing(null, new Pen(Ui.B("#0BFFFFFF"), 1), gg);
        inner.Children.Add(new Rectangle { Fill = gb });
        // hexagone MAG en filigrane (tourne un peu au survol)
        var hexes = new Grid { Width = 230, Height = 230, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(108, 118, 0, 0), RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = hexTurn };
        hexes.Children.Add(new System.Windows.Shapes.Path { Data = Ui.Hex, Stretch = Stretch.Uniform, Stroke = Ui.B(a), StrokeThickness = 2.4, Opacity = 0.16 });
        hexes.Children.Add(new System.Windows.Shapes.Path { Data = Ui.Hex, Stretch = Stretch.Uniform, Stroke = Ui.B(a), StrokeThickness = 1.6, Opacity = 0.10, Margin = new Thickness(46) });
        hexes.Children.Add(new System.Windows.Shapes.Path { Data = Ui.Hex, Stretch = Stretch.Uniform, Fill = new RadialGradientBrush(Ui.A(a, 40), Ui.A(a, 0)), Margin = new Thickness(20) });
        inner.Children.Add(hexes);
        // reflet qui traverse la carte au survol
        inner.Children.Add(new Rectangle
        {
            Width = 90, Height = 560, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
            Fill = new LinearGradientBrush(new GradientStopCollection { new GradientStop(Ui.C("#00FFFFFF"), 0), new GradientStop(Ui.C("#2CFFFFFF"), 0.5), new GradientStop(Ui.C("#00FFFFFF"), 1) }, 0),
            RenderTransform = new TransformGroup { Children = { new RotateTransform(18), shine } }
        });
        // bas assombri sous le bouton
        var fade = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        fade.GradientStops.Add(new GradientStop(Ui.C("#00040710"), 0.55)); fade.GradientStops.Add(new GradientStop(Ui.C("#B0040710"), 1));
        inner.Children.Add(new Rectangle { Fill = fade });

        // textes
        var stack = new StackPanel { Margin = new Thickness(22, 20, 22, 0), IsHitTestVisible = false };
        stack.Children.Add(new Border { Height = 2, Width = 46, CornerRadius = new CornerRadius(1), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12), Background = Ui.Grad(a, Ui.A(g.Accent2, 0), 0) });
        stack.Children.Add(Ui.T(Ui.Spaced("CALL OF DUTY"), 10.5, Ui.TextSub, Ui.Title));
        var bt = Ui.T(g.Short, 70, Ui.Grad(Colors.White, a, 90), Ui.Title, FontWeights.SemiBold);
        // ombre fixe du gros sigle : rasterisee une fois (ne change jamais) au lieu d'etre re-floutee a chaque image
        bt.Effect = new DropShadowEffect { Color = a, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.5 };
        bt.CacheMode = new BitmapCache();
        big = new Viewbox { Child = bt, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = 192, Height = 76, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-3, 0, 0, -2) };
        stack.Children.Add(big);
        var nm = Ui.T(g.Name, 17.5, Ui.TextMain, Ui.Title, FontWeights.SemiBold);
        stack.Children.Add(new Viewbox { Child = nm, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = 192, Height = 24, HorizontalAlignment = HorizontalAlignment.Left });
        info = Ui.T("", 12, Ui.TextSub, Ui.Text); info.Margin = new Thickness(0, 3, 0, 0);
        stack.Children.Add(info);
        var chips = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
        var cl = L.En ? g.ChipsEn : g.ChipsFr;
        foreach (var f in cl ?? new[] { "GSC", "PS4" })
            chips.Children.Add(new Border { CornerRadius = new CornerRadius(8), Background = Ui.B("#12C8D4FF"), BorderBrush = Ui.B(Ui.A(a, 55)), BorderThickness = new Thickness(1), Padding = new Thickness(8, 3, 8, 4), Margin = new Thickness(0, 0, 6, 6), Child = Ui.T(f, 10.5, Ui.B("#C2C9DA"), Ui.Text) });
        stack.Children.Add(chips);
        root.Children.Add(stack);

        // bas : etat + bouton
        var bottom = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(18, 0, 18, 18), IsHitTestVisible = false };
        pillDot = new Ellipse { Width = 7, Height = 7, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        pillText = Ui.T("", 10.5, Brushes.White, Ui.Title, FontWeights.SemiBold);
        var pillRow = new StackPanel { Orientation = Orientation.Horizontal };
        pillRow.Children.Add(pillDot); pillRow.Children.Add(pillText);
        pill = new Border { CornerRadius = new CornerRadius(20), Padding = new Thickness(10, 4, 12, 4), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(4, 0, 0, 12), Child = pillRow };
        bottom.Children.Add(pill);
        barIcon = Ui.Icon("", 13, Brushes.White); barIcon.Margin = new Thickness(0, 1, 9, 0);
        barText = Ui.T("", 13.5, Brushes.White, Ui.Title, FontWeights.SemiBold); barText.VerticalAlignment = VerticalAlignment.Center;
        barArrow = Ui.Icon("", 12, Brushes.White); barArrow.Margin = new Thickness(10, 1, 0, 0); barArrow.RenderTransform = new TranslateTransform(0, 0);
        var barRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        barRow.Children.Add(barIcon); barRow.Children.Add(barText); barRow.Children.Add(barArrow);
        barGlow = new DropShadowEffect { Color = a, BlurRadius = 12, ShadowDepth = 0, Opacity = 0 };
        bar = new Border { Height = 48, CornerRadius = new CornerRadius(14), Child = barRow, Effect = barGlow };
        bottom.Children.Add(bar);
        root.Children.Add(bottom);

        Child = root;
        Opacity = 0;
        MouseEnter += delegate { Hover(true); };
        MouseLeave += delegate { Hover(false); Press(false); };
        MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e) { Press(true); e.Handled = true; };
        MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e) { Press(false); e.Handled = true; if (IsMouseOver && Clicked != null) Clicked(this); };
        SetAvailable(false);
    }

    // compteur d'utilisateurs : personnes en ligne sur cet injecteur
    public void SetOnline(int n) { online = n; PaintInfo(); }
    void PaintInfo()
    {
        info.Inlines.Clear();
        info.Inlines.Add(new Run(Game.Studio + "  •  " + Game.Year));
        if (online > 0) info.Inlines.Add(new Run("  •  " + online + L.T(" en ligne", " online")) { Foreground = Ui.B(Ui.Online) });
    }

    public void SetAvailable(bool ok)
    {
        Available = ok;
        var a = Game.Accent;
        bool off = ok && Killed, go = ok && !Killed;
        var pc = off ? Ui.C("#FF4D5E") : ok && Outdated ? Ui.Warn : a;
        pill.Background = Ui.B(Ui.A(ok ? pc : Ui.C("#8D95AB"), 30));
        pill.BorderBrush = Ui.B(Ui.A(ok ? pc : Ui.C("#8D95AB"), 105));
        pillText.Text = ok ? (off ? L.T("DÉSACTIVÉ", "DISABLED") : Outdated ? L.T("MISE À JOUR DISPO", "UPDATE AVAILABLE") : L.T("DISPONIBLE", "AVAILABLE")) : L.T("EN PRÉPARATION", "IN PROGRESS");
        pillText.Foreground = ok ? Ui.B(pc) : Ui.B("#AEB5C7");
        pillDot.Fill = ok ? Ui.B(pc) : Ui.B("#6F778C");
        if (go) Ui.Loop(pillDot, OpacityProperty, 1, 0.25, 0.9, true); else Ui.StopLoop(pillDot, OpacityProperty);
        bar.Background = go ? (Brush)Ui.Grad(a, Game.Accent2, 0) : off ? Ui.B("#14FF4D5E") : Ui.B("#10C8D4FF");
        bar.BorderBrush = go ? null : Ui.B(off ? Ui.A(Ui.C("#FF4D5E"), 100) : Ui.C("#24C8D4FF"));
        bar.BorderThickness = new Thickness(go ? 0 : 1);
        barGlow.Opacity = go ? 0.4 : 0;
        barIcon.Text = go ? "" : off ? "" : "";
        barText.Text = go ? L.T("LANCER", "LAUNCH") : off ? L.T("DÉSACTIVÉ", "DISABLED") : L.T("BIENTÔT DISPONIBLE", "COMING SOON");
        var fg = go ? Brushes.White : off ? Ui.B("#FF8A96") : Ui.B("#7C8399");
        barText.Foreground = fg; barIcon.Foreground = fg;
        barArrow.Visibility = go ? Visibility.Visible : Visibility.Collapsed;
        topGlow.Opacity = go ? 1 : 0.45;
        big.Opacity = go ? 1 : 0.8;
        PaintInfo();
    }

    public void Hover(bool on)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Ui.Anim(scale, ScaleTransform.ScaleXProperty, on ? 1.04 : 1, 260, ease);
        Ui.Anim(scale, ScaleTransform.ScaleYProperty, on ? 1.04 : 1, 260, ease);
        Ui.Anim(lift, TranslateTransform.YProperty, on ? -10 : 0, 260, ease);
        Ui.Anim(glowHost, OpacityProperty, on ? (Available && !Killed ? 0.85 : 0.35) : 0, 300);
        Ui.Anim(hexTurn, RotateTransform.AngleProperty, on ? 14 : 0, 700, ease);
        Ui.Anim((TranslateTransform)barArrow.RenderTransform, TranslateTransform.XProperty, on ? 5 : 0, 260, ease);
        if (Available && !Killed) Ui.Anim(barGlow, DropShadowEffect.OpacityProperty, on ? 0.75 : 0.4, 260);
        if (on) shine.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-300, 440, TimeSpan.FromMilliseconds(780)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
    }

    void Press(bool down)
    {
        Ui.Anim(scale, ScaleTransform.ScaleXProperty, down ? 0.97 : (IsMouseOver ? 1.04 : 1), 120);
        Ui.Anim(scale, ScaleTransform.ScaleYProperty, down ? 0.97 : (IsMouseOver ? 1.04 : 1), 120);
    }

    public void Enter(double delayMs)
    {
        Opacity = 0; lift.Y = 44;
        Ui.Anim(this, OpacityProperty, 1, 520, null, delayMs);
        Ui.Anim(lift, TranslateTransform.YProperty, 0, 720, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 }, delayMs);
    }

    public void Pulse()
    {
        var p = new DoubleAnimation(1, 1.07, TimeSpan.FromMilliseconds(170)) { AutoReverse = true, EasingFunction = new QuadraticEase() };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, p); scale.BeginAnimation(ScaleTransform.ScaleYProperty, p);
        glowHost.BeginAnimation(OpacityProperty, new DoubleAnimation(1, IsMouseOver ? 0.85 : 0, TimeSpan.FromMilliseconds(700)));
    }
}

// Barre de filtres : pastilles avec un surlignage qui glisse vers le choix.
class FilterBar : Border
{
    public event Action<int> Changed;
    readonly Border hl; readonly TranslateTransform hlPos = new TranslateTransform();
    readonly StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
    readonly List<Border> items = new List<Border>();
    readonly List<TextBlock> labels = new List<TextBlock>(), counts = new List<TextBlock>();
    public int Selected { get; private set; }

    public FilterBar(string[] names, int selected)
    {
        CornerRadius = new CornerRadius(15); Padding = new Thickness(4); BorderThickness = new Thickness(1);
        Background = Ui.Grad(Ui.C("#B00B1124"), Ui.C("#C407091A"), 90); BorderBrush = Ui.Grad(Ui.C("#2CFFFFFF"), Ui.C("#0CFFFFFF"), 90);
        HorizontalAlignment = HorizontalAlignment.Center;
        var g = new Grid();
        hl = new Border
        {
            CornerRadius = new CornerRadius(11), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, RenderTransform = hlPos,
            Background = Ui.Grad(Ui.A(Ui.Accent, 80), Ui.A(Ui.Accent2, 34), 0), BorderBrush = Ui.B(Ui.A(Ui.Accent, 150)), BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { Color = Ui.Accent, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.35 }
        };
        g.Children.Add(hl);
        for (int i = 0; i < names.Length; i++)
        {
            var lb = Ui.T(names[i], 13, Ui.TextSub, Ui.Title, FontWeights.SemiBold); lb.VerticalAlignment = VerticalAlignment.Center;
            var ct = Ui.T("", 11, Ui.TextSub, Ui.Title, FontWeights.SemiBold); ct.VerticalAlignment = VerticalAlignment.Center;
            var cb = new Border { CornerRadius = new CornerRadius(8), Background = Ui.B("#16C8D4FF"), Padding = new Thickness(6, 0, 6, 1), Margin = new Thickness(8, 0, 0, 0), Child = ct, VerticalAlignment = VerticalAlignment.Center };
            var sp = new StackPanel { Orientation = Orientation.Horizontal }; sp.Children.Add(lb); sp.Children.Add(cb);
            var it = new Border { Padding = new Thickness(15, 7, 13, 8), CornerRadius = new CornerRadius(11), Background = Brushes.Transparent, Cursor = Cursors.Hand, Child = sp };
            int k = i;
            it.MouseEnter += delegate { if (Selected != k) lb.Foreground = Ui.TextMain; };
            it.MouseLeave += delegate { if (Selected != k) lb.Foreground = Ui.TextSub; };
            it.MouseLeftButtonUp += delegate { Select(k, true); if (Changed != null) Changed(k); };
            WindowChrome.SetIsHitTestVisibleInChrome(it, true);
            items.Add(it); labels.Add(lb); counts.Add(ct);
            row.Children.Add(it);
        }
        g.Children.Add(row);
        Child = g;
        Selected = Math.Max(0, Math.Min(names.Length - 1, selected));
        Loaded += delegate { Select(Selected, false); };
        SizeChanged += delegate { Select(Selected, false); };
    }

    public void SetCounts(int[] c) { for (int i = 0; i < counts.Count && i < c.Length; i++) counts[i].Text = c[i].ToString(); }

    public void Select(int i, bool animate)
    {
        Selected = i;
        for (int k = 0; k < items.Count; k++) { labels[k].Foreground = k == i ? Brushes.White : Ui.TextSub; counts[k].Foreground = k == i ? Brushes.White : Ui.TextSub; }
        Dispatcher.BeginInvoke(new Action(delegate
        {
            var it = items[Selected];
            if (it.ActualWidth <= 0) return;
            var pt = it.TranslatePoint(new Point(0, 0), row);
            hl.Width = it.ActualWidth; hl.Height = it.ActualHeight;
            if (animate) Ui.Anim(hlPos, TranslateTransform.XProperty, pt.X, 320, new CubicEase { EasingMode = EasingMode.EaseInOut });
            else { hlPos.BeginAnimation(TranslateTransform.XProperty, null); hlPos.X = pt.X; }
        }), DispatcherPriority.Loaded);
    }
}

class LauncherWindow : Window
{
    static GameInfo G(string id, string sh, string name, string studio, string year, bool bo, string a1, string a2)
    {
        return new GameInfo { Id = id, Short = sh, Name = name, Studio = studio, Year = year, BlackOps = bo, Accent = Ui.C(a1), Accent2 = Ui.C(a2) };
    }
    // ordre demande par UrBaaZz : Black Ops puis les autres opus
    readonly List<GameInfo> games = new List<GameInfo>
    {
        G("BO1", "BO1", "Black Ops", "Treyarch", "2010", true, "#FFB13D", "#FF7A1A"),
        G("BO2", "BO2", "Black Ops II", "Treyarch", "2012", true, "#FF9F33", "#FF5E1F"),
        new GameInfo { Id = "BO3", Short = "BO3", Name = "Black Ops III", Studio = "Treyarch", Year = "2015", BlackOps = true, Accent = Ui.C("#FF8C29"), Accent2 = Ui.C("#FF4A24"),
            ChipsFr = new[] { "Multijoueur", "Zombies", "Campagne" }, ChipsEn = new[] { "Multiplayer", "Zombies", "Campaign" } },
        new GameInfo { Id = "BO4", Short = "BO4", Name = "Black Ops 4", Studio = "Treyarch", Year = "2018", BlackOps = true, Accent = Ui.C("#FF7A1A"), Accent2 = Ui.C("#FF3D2E"),
            ChipsFr = new[] { "Multijoueur", "Zombies", "MAG Menu" }, ChipsEn = new[] { "Multiplayer", "Zombies", "MAG Menu" } },
        G("BOCW", "BOCW", "Black Ops Cold War", "Treyarch & Raven", "2020", true, "#FF6838", "#E8323C"),
        new GameInfo { Id = "GHOSTS", Short = "GHOSTS", Name = "Ghosts", Studio = "Infinity Ward", Year = "2013", BlackOps = false, Accent = Ui.C("#B8DA4C"), Accent2 = Ui.C("#6FA83A"),
            ChipsFr = new[] { "Multijoueur", "Extinction", "MAG Menu" }, ChipsEn = new[] { "Multiplayer", "Extinction", "MAG Menu" } },
        new GameInfo { Id = "AW", Short = "AW", Name = "Advanced Warfare", Studio = "Sledgehammer", Year = "2014", BlackOps = false, Accent = Ui.C("#93D25A"), Accent2 = Ui.C("#3FA35A"),
            ChipsFr = new[] { "Multijoueur", "Exo Zombies", "MAG Menu" }, ChipsEn = new[] { "Multiplayer", "Exo Zombies", "MAG Menu" } },
        new GameInfo { Id = "IW", Short = "IW", Name = "Infinite Warfare", Studio = "Infinity Ward", Year = "2016", BlackOps = false, Accent = Ui.C("#72D39A"), Accent2 = Ui.C("#2E9E6E"),
            ChipsFr = new[] { "Multijoueur", "Zombies", "MAG Menu" }, ChipsEn = new[] { "Multiplayer", "Zombies", "MAG Menu" } },
        G("MWR", "MWR", "Modern Warfare Remastered", "Raven", "2016", false, "#9BCB3C", "#3FA35A"),
        new GameInfo { Id = "WW2", Short = "WWII", Name = "WWII", Studio = "Sledgehammer", Year = "2017", BlackOps = false, Accent = Ui.C("#C6C95C"), Accent2 = Ui.C("#7C8A2E"),
            ChipsFr = new[] { "Multijoueur", "Zombies", "MAG Menu" }, ChipsEn = new[] { "Multiplayer", "Zombies", "MAG Menu" } },
        new GameInfo { Id = "VANGUARD", Short = "VANGUARD", Name = "Vanguard", Studio = "Sledgehammer", Year = "2021", BlackOps = false, Accent = Ui.C("#D5AF63"), Accent2 = Ui.C("#79623A"),
            ChipsFr = new[] { "Multijoueur", "Zombies", "MAG Menu" }, ChipsEn = new[] { "Multiplayer", "Zombies", "MAG Menu" } },
        new GameInfo { Id = "MW2019", Short = "MW2019", Name = "Modern Warfare", Studio = "Infinity Ward", Year = "2019", BlackOps = false, Accent = Ui.C("#85AD87"), Accent2 = Ui.C("#3E6251"),
            ChipsFr = new[] { "Multijoueur", "MAG Menu" }, ChipsEn = new[] { "Multiplayer", "MAG Menu" } },
        new GameInfo { Id = "MWII", Short = "MWII", Name = "Modern Warfare II", Studio = "Infinity Ward", Year = "2022", BlackOps = false, Accent = Ui.C("#92C947"), Accent2 = Ui.C("#416D28"),
            ChipsFr = new[] { "Multijoueur", "MAG Menu" }, ChipsEn = new[] { "Multiplayer", "MAG Menu" } },
    };
    const string AppName = "MULTI COD MAG INJECTOR GSC";
    readonly string dir = AppDomain.CurrentDomain.BaseDirectory;
    string cfgPath;
    bool minimizeAfterLaunch;
    int filter;

    Grid root, content, overlay;
    StackPanel cardRows, header;
    ScrollViewer cardScroll;
    FilterBar filterBar;
    readonly List<GameCard> cards = new List<GameCard>();
    readonly List<GameCard> shown = new List<GameCard>();
    TextBlock titleBig, titleSmall, subtitle, maxGlyph, toastText, onlineText, footVer;
    Border toast, settingsPanel, onlinePill;
    Rectangle settingsDim;
    StackPanel settingsList;
    DispatcherTimer toastTimer;
    Canvas deco;
    FrameworkElement ribbon, ribbonEcho, ribbonTop;
    int lastCols = -1;
    bool started, capturing;

    public LauncherWindow()
    {
        Ui.Disp = Dispatcher;
        cfgPath = IOPath.Combine(dir, "MULTI_COD_MAG_INJECTOR_GSC.cfg");
        Title = AppName;
        var wa = SystemParameters.WorkArea;
        Width = Math.Min(1460, Math.Max(860, wa.Width * 0.84));
        Height = Math.Min(920, Math.Max(600, wa.Height * 0.88));
        MinWidth = 560; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize;
        Background = Ui.B("#05070E"); UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 52, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false });
        try { var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("mag.ico"); if (s != null) Icon = BitmapFrame.Create(s); } catch { }
        Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(Ui.Styles));

        LoadCfg();
        MagStats.Changed += delegate { Dispatcher.BeginInvoke(new Action(UpdateOnline)); };
        MagNews.Changed += delegate { Dispatcher.BeginInvoke(new Action(RefreshBanner)); }; // bannière réglée depuis le serveur
        foreach (var g in games) { try { Directory.CreateDirectory(IOPath.Combine(dir, "Tools", g.Id)); } catch { } }

        BuildUi();
        SourceInitialized += delegate { Dwm(); };
        StateChanged += delegate { SyncState(); };
        Activated += delegate { RefreshAvailability(); };
        SizeChanged += delegate { Relayout(); };
        MagWarnWindow.MarkLauncher(); // les injecteurs lances d'ici ne remontrent pas l'avertissement
        Loaded += delegate
        {
            Relayout(); StartAnimations();
            if (capturing) return;
            // mises a jour, desactivation et compteur : apres l'animation d'entree, en arriere-plan
            var ut = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            ut.Tick += delegate { ut.Stop(); StartWatch(); };
            ut.Start();
        };
        KeyDown += delegate (object s, KeyEventArgs e) { if (e.Key == Key.Escape && settingsPanel.Visibility == Visibility.Visible) ShowSettings(false); };
        Closing += delegate { if (!capturing) MagStats.Bye(); }; // compteur : hors ligne tout de suite
        MagFx.Attach(this);
    }

    void SyncState()
    {
        root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        maxGlyph.Text = WindowState == WindowState.Maximized ? "" : "";
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    void Dwm() { try { var h = new WindowInteropHelper(this).Handle; int dark = 1, round = 2; DwmSetWindowAttribute(h, 20, ref dark, 4); DwmSetWindowAttribute(h, 33, ref round, 4); } catch { } }

    // ---------- langue ----------
    Border LangButton(FrameworkElement flag, string name, bool en)
    {
        bool on = L.En == en;
        var b = new Border
        {
            CornerRadius = new CornerRadius(6), Padding = new Thickness(3), Margin = new Thickness(2, 0, 2, 0), BorderThickness = new Thickness(1.5),
            BorderBrush = Ui.B(on ? Ui.Accent : Ui.C("#00FFFFFF")), Background = Ui.B(on ? Ui.A(Ui.Accent, 34) : Ui.C("#00FFFFFF")),
            Child = flag, Cursor = Cursors.Hand, ToolTip = name, Opacity = on ? 1 : 0.5, VerticalAlignment = VerticalAlignment.Center
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

    // changement de langue : toute l'interface est reconstruite dans la nouvelle langue (les cartes rejouent leur entree)
    void SetLang(bool en)
    {
        if (L.En == en) return;
        L.En = en; SaveCfg();
        if (toastTimer != null) toastTimer.Stop();
        cards.Clear(); shown.Clear(); lastCols = -1; started = false;
        MagFx.StopAll();
        BuildUi();
        SyncState();
        UpdateLayout(); Relayout(); StartAnimations();
        Toast(L.T("Langue : français", "Language: English"), Ui.Accent);
    }

    // ---------- construction ----------
    void BuildUi()
    {
        root = new Grid(); Content = root;
        var bg = new Grid { IsHitTestVisible = false, ClipToBounds = true };
        bg.Children.Add(new Rectangle { Fill = Ui.Grad(Ui.C("#0B1122"), Ui.C("#04060C"), 90) });
        // grille tactique tres discrete
        var grid = new DrawingBrush { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 44, 44), ViewportUnits = BrushMappingMode.Absolute, Opacity = 0.55 };
        var gg = new GeometryGroup(); gg.Children.Add(new LineGeometry(new Point(0, 0), new Point(44, 0))); gg.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, 44)));
        grid.Drawing = new GeometryDrawing(null, new Pen(Ui.B("#0CFFFFFF"), 1), gg);
        bg.Children.Add(new Rectangle { Fill = grid });
        deco = new Canvas(); bg.Children.Add(deco);
        // rappel France : ruban bleu / blanc / rouge qui traverse le bas a droite, filet plus fin en haut a gauche
        ribbon = Ribbon(24, 0.55, true); ribbonEcho = Ribbon(6, 0.4, false); ribbonTop = Ribbon(5, 0.2, false);
        deco.Children.Add(ribbon); deco.Children.Add(ribbonEcho); deco.Children.Add(ribbonTop);
        // les deux couleurs du launcher, tres douces : orange Black Ops en bas a gauche, vert des autres opus en haut a droite
        bg.Children.Add(new Rectangle { Fill = Glow(Ui.Accent, 0.06, 1.04, 36) });
        bg.Children.Add(new Rectangle { Fill = Glow(Ui.Green, 0.95, -0.05, 22) });
        var vig = new RadialGradientBrush { RadiusX = 0.9, RadiusY = 0.9 };
        vig.GradientStops.Add(new GradientStop(Ui.C("#00000000"), 0.5)); vig.GradientStops.Add(new GradientStop(Ui.C("#B0000000"), 1));
        bg.Children.Add(new Rectangle { Fill = vig });
        root.Children.Add(bg);

        var main = new Grid();
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        root.Children.Add(main);
        main.Children.Add(TopBar());

        content = new Grid { Margin = new Thickness(28, 4, 28, 0) };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(content, 1);
        main.Children.Add(content);

        // en-tete : MULTI COD / MAG INJECTOR GSC / phrase / filtres
        header = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 12) };
        header.Children.Add(BuildBanner());
        titleSmall = Ui.T(Ui.Spaced("MULTI COD"), 15, Ui.TextSub, Ui.TitleLight);
        titleSmall.HorizontalAlignment = HorizontalAlignment.Center;
        header.Children.Add(titleSmall);
        titleBig = new TextBlock { FontFamily = Ui.Title, FontWeight = FontWeights.SemiBold, FontSize = 64, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -6, 0, -2) };
        titleBig.Inlines.Add(new Run("MAG INJECTOR ") { Foreground = Ui.Grad(Ui.C("#FFFFFF"), Ui.C("#B9C6E8"), 90) });
        var shimmer = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5), SpreadMethod = GradientSpreadMethod.Repeat };
        shimmer.GradientStops.Add(new GradientStop(Ui.Brand, 0)); shimmer.GradientStops.Add(new GradientStop(Ui.Accent, 0.33));
        shimmer.GradientStops.Add(new GradientStop(Ui.Green, 0.66)); shimmer.GradientStops.Add(new GradientStop(Ui.Brand, 1));
        var shimmerMove = new TranslateTransform(0, 0); shimmer.RelativeTransform = shimmerMove;
        titleBig.Inlines.Add(new Run("GSC") { Foreground = shimmer });
        var tfx = new DropShadowEffect { Color = Ui.Accent, BlurRadius = 26, ShadowDepth = 0, Opacity = 0.4 };
        titleBig.Effect = tfx;
        if (!MagFx.Heavy) titleBig.CacheMode = new BitmapCache(); // lueur du titre fixe (hors mode « Complets ») : rasterisee une fois
        header.Children.Add(titleBig);
        subtitle = Ui.T(L.T("Choisis ton jeu pour lancer son injecteur GSC  •  PS4 GoldHEN", "Pick your game to launch its GSC injector  •  PS4 GoldHEN"), 14, Ui.TextSub, Ui.Text);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        header.Children.Add(subtitle);
        filterBar = new FilterBar(new[] { L.T("Tous les jeux", "All games"), L.T("Disponibles", "Available"), "Black Ops", L.T("Autres opus", "Other titles") }, filter) { Margin = new Thickness(0, 16, 0, 0) };
        filterBar.Changed += delegate (int f) { filter = f; SaveCfg(); ApplyFilter(true); };
        header.Children.Add(filterBar);
        content.Children.Add(header);
        Ui.LoopFx(shimmerMove, TranslateTransform.XProperty, 0, 1, 7, false);
        Ui.LoopFx(tfx, DropShadowEffect.OpacityProperty, 0.25, 0.55, 3.2, true);

        cardScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        Grid.SetRow(cardScroll, 1);
        cardRows = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
        cardScroll.Content = cardRows;
        content.Children.Add(cardScroll);
        foreach (var g in games)
        {
            var c = new GameCard(g);
            c.Clicked += OnCardClick;
            cards.Add(c);
        }

        main.Children.Add(Footer());
        BuildOverlay();
        RefreshAvailability();
        UpdateOnline();
        ApplyFilter(false);
    }

    static RadialGradientBrush Glow(Color c, double x, double y, byte a)
    {
        var g = new RadialGradientBrush { Center = new Point(x, y), GradientOrigin = new Point(x, y), RadiusX = 0.5, RadiusY = 0.6 };
        g.GradientStops.Add(new GradientStop(Ui.A(c, a), 0)); g.GradientStops.Add(new GradientStop(Ui.A(c, 0), 1));
        return g;
    }

    // ruban de 3 bandes (bleu, blanc, rouge) incline, avec un halo flou et un reflet qui glisse (comme les injecteurs)
    static FrameworkElement Ribbon(double band, double opacity, bool halo)
    {
        var g = new Grid { Width = band * 3, Height = 3200, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(36), Opacity = opacity };
        Color[] c = { Ui.FlagBlue, Ui.FlagWhite, Ui.FlagRed };
        if (halo)
        {
            var h = new Grid { Margin = new Thickness(-30, 0, -30, 0), Effect = new BlurEffect { Radius = 46 }, Opacity = 0.8, CacheMode = new BitmapCache() };
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
        // le bas du ruban s'efface avant le pied de page
        var fade = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        fade.GradientStops.Add(new GradientStop(Ui.C("#FF000000"), 0)); fade.GradientStops.Add(new GradientStop(Ui.C("#FF000000"), 0.512)); fade.GradientStops.Add(new GradientStop(Ui.C("#00000000"), 0.585));
        g.OpacityMask = fade;
        return g;
    }
    static void Place(FrameworkElement e, double cx, double cy) { Canvas.SetLeft(e, cx - e.Width / 2); Canvas.SetTop(e, cy - e.Height / 2); }

    UIElement TopBar()
    {
        var bar = new Grid { Margin = new Thickness(16, 0, 0, 0) };
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(Ui.Logo(30));
        var name = new TextBlock { FontFamily = Ui.Title, FontSize = 15.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0) };
        name.Inlines.Add(new Run("MULTI COD MAG INJECTOR ") { Foreground = Ui.TextMain });
        name.Inlines.Add(new Run("GSC") { Foreground = Ui.Grad(Ui.Brand, Ui.Accent, 0) });
        left.Children.Add(name);
        left.Children.Add(new Border { CornerRadius = new CornerRadius(8), Background = Ui.B("#14C8D4FF"), BorderBrush = Ui.B("#26C8D4FF"), BorderThickness = new Thickness(1), Padding = new Thickness(9, 2, 9, 3), VerticalAlignment = VerticalAlignment.Center, Child = Ui.T("PS4  •  GOLDHEN", 11, Ui.TextSub, Ui.Title, FontWeights.SemiBold) });
        bar.Children.Add(left);

        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        // compteur d'utilisateurs en ligne (signal anonyme, voir UpdateCheck.cs) ; cache tant que rien n'est connu
        var od = new Ellipse { Width = 7, Height = 7, Fill = Ui.B(Ui.Online), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        Ui.Loop(od, OpacityProperty, 1, 0.3, 1.1, true);
        onlineText = Ui.T("", 12, Ui.B("#C9F7DA"), Ui.Title, FontWeights.SemiBold); onlineText.VerticalAlignment = VerticalAlignment.Center;
        var orow = new StackPanel { Orientation = Orientation.Horizontal }; orow.Children.Add(od); orow.Children.Add(onlineText);
        onlinePill = new Border { CornerRadius = new CornerRadius(12), Background = Ui.B("#143CF08A"), BorderBrush = Ui.B("#553CF08A"), BorderThickness = new Thickness(1), Padding = new Thickness(10, 3, 12, 4), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Child = orow, Visibility = Visibility.Collapsed };
        WindowChrome.SetIsHitTestVisibleInChrome(onlinePill, true);
        right.Children.Add(onlinePill);
        var langs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        langs.Children.Add(LangButton(L.FlagFr(), "Français", false));
        langs.Children.Add(LangButton(L.FlagEn(), "English", true));
        right.Children.Add(langs);
        var discord = Contact.FooterPill(delegate (string msg) { Toast(msg, Contact.Blurple); });
        discord.Margin = new Thickness(6, 0, 8, 0);
        WindowChrome.SetIsHitTestVisibleInChrome(discord, true);
        right.Children.Add(discord);
        right.Children.Add(Caption("", false, delegate { ShowSettings(true); }, L.T("Paramètres", "Settings")));
        right.Children.Add(Caption("", false, delegate { WindowState = WindowState.Minimized; }, L.T("Réduire", "Minimize")));
        var mx = Caption("", false, delegate { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }, L.T("Agrandir", "Maximize"));
        maxGlyph = (TextBlock)((Grid)((Border)((Grid)mx.Child).Children[1]).Child).Children[0];
        right.Children.Add(mx);
        right.Children.Add(Caption("", true, delegate { Close(); }, L.T("Fermer", "Close")));
        bar.Children.Add(right);
        return bar;
    }

    FlatButton Caption(string glyph, bool danger, Action click, string tip)
    {
        var t = Ui.Icon(glyph, glyph == "" ? 14 : 11, Ui.B("#C9CEDA")); t.HorizontalAlignment = HorizontalAlignment.Center;
        var host = new Grid(); host.Children.Add(t);
        var b = new FlatButton(host, Ui.C("#00FFFFFF"), danger ? Ui.C("#E0E81123") : Ui.C("#22FFFFFF"), 0, new Thickness(0)) { Width = 50, Height = 52, ToolTip = tip };
        b.Click += click;
        WindowChrome.SetIsHitTestVisibleInChrome(b, true);
        return b;
    }

    UIElement Footer()
    {
        var f = new Grid(); Grid.SetRow(f, 2);
        var inner = new Grid { Margin = new Thickness(24, 0, 24, 0) };
        f.Children.Add(new Rectangle { Height = 1, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(24, 0, 24, 0), Fill = new LinearGradientBrush(new GradientStopCollection { new GradientStop(Ui.C("#00C8D4FF"), 0), new GradientStop(Ui.C("#26C8D4FF"), 0.5), new GradientStop(Ui.C("#00C8D4FF"), 1) }, 0) });
        f.Children.Add(inner);
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var credit = new TextBlock { FontFamily = Ui.Title, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center };
        credit.Inlines.Add(new Run("Dev By ") { Foreground = Ui.TextSub });
        credit.Inlines.Add(new Run("UrBaaZz") { Foreground = Ui.Grad(Ui.Brand, Ui.Accent, 0), FontWeight = FontWeights.SemiBold });
        credit.Inlines.Add(new Run(" and ") { Foreground = Ui.TextSub });
        credit.Inlines.Add(new Run("Zerty") { Foreground = Ui.Grad(Ui.Green, Ui.C("#6FD39A"), 0), FontWeight = FontWeights.SemiBold });
        left.Children.Add(credit);
        left.Children.Add(Contact.FooterPill(delegate (string m) { Toast(m, Contact.Blurple); }));
        inner.Children.Add(left);
        // badge « Fait en France » au centre
        var badge = new Border { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(10, 5, 12, 5), CornerRadius = new CornerRadius(10), Background = Ui.B("#10C8D4FF"), BorderBrush = Ui.B("#1EC8D4FF"), BorderThickness = new Thickness(1) };
        var brow = new StackPanel { Orientation = Orientation.Horizontal };
        var mf = Ui.MiniFlag(21, 14); mf.VerticalAlignment = VerticalAlignment.Center; brow.Children.Add(mf);
        var bt = Ui.T(L.T("FAIT EN FRANCE", "MADE IN FRANCE"), 11, Ui.TextMain, Ui.Title, FontWeights.SemiBold); bt.Margin = new Thickness(8, 0, 0, 0); bt.VerticalAlignment = VerticalAlignment.Center;
        brow.Children.Add(bt);
        badge.Child = brow;
        inner.Children.Add(badge);
        footVer = Ui.T("v" + MagUpdate.Show(MagUpdate.Current) + "  •  " + L.T(games.Count + " jeux", games.Count + " games") + "  •  GSC PS4", 12, Ui.TextDim, Ui.Title);
        footVer.HorizontalAlignment = HorizontalAlignment.Right; footVer.VerticalAlignment = VerticalAlignment.Center;
        inner.Children.Add(footVer);
        return f;
    }

    // ---------- mise en page adaptative ----------
    void Relayout()
    {
        if (content == null || ActualWidth <= 0) return;
        double w = ActualWidth, h = ActualHeight;
        Place(ribbon, w * 0.80, h * 0.66); Place(ribbonEcho, w * 0.80 + 74, h * 0.66); Place(ribbonTop, w * 0.06, h * 0.04);

        double t = Math.Max(30, Math.Min(74, Math.Min(w / 15, h / 11)));
        titleBig.FontSize = t;
        titleSmall.FontSize = Math.Max(11, t * 0.22);
        subtitle.FontSize = Math.Max(12, Math.Min(15, t * 0.22));
        subtitle.Visibility = h < 620 ? Visibility.Collapsed : Visibility.Visible;
        header.Margin = new Thickness(0, h < 640 ? 0 : 4, 0, h < 640 ? 8 : 14);
        double side = w < 760 ? 14 : 28;
        content.Margin = new Thickness(side, 4, side, 0);
        footVer.Visibility = w < 980 ? Visibility.Collapsed : Visibility.Visible;

        content.UpdateLayout();
        double availW = Math.Max(200, w - 2 * side - 10);
        double availH = Math.Max(160, cardScroll.ActualHeight > 0 ? cardScroll.ActualHeight - 16 : h - 300);
        int n = Math.Max(1, shown.Count); double ratio = GameCard.DH / GameCard.DW;
        double gap = Math.Max(10, Math.Min(24, availW * 0.016));
        int bestCols = 1; double bestW = 0;
        var cwOf = new double[n + 1];
        for (int cols = 1; cols <= n; cols++)
        {
            int rows = (n + cols - 1) / cols;
            double byW = (availW - gap * cols) / cols;
            double byH = (availH - gap * rows) / rows / ratio;
            cwOf[cols] = Math.Min(Math.Min(byW, byH), 280);
            if (cwOf[cols] >= bestW - 0.5) { bestW = cwOf[cols]; bestCols = cols; }
        }
        // a taille presque egale, des rangees equilibrees (10 cartes : 5 + 5 plutot que 6 + 4)
        int bestEmpty = int.MaxValue;
        for (int cols = 1; cols <= n; cols++)
        {
            if (cwOf[cols] < bestW * 0.93) continue;
            int empty = ((n + cols - 1) / cols) * cols - n;
            if (empty < bestEmpty || (empty == bestEmpty && cwOf[cols] > cwOf[bestCols])) { bestEmpty = empty; bestCols = cols; }
        }
        bestW = cwOf[bestCols];
        if (bestW < 158)
        {
            // fenetre petite : des cartes lisibles et on fait defiler
            bestCols = Math.Max(1, (int)(availW / (176 + gap)));
            bestW = Math.Min(250, (availW - gap * bestCols) / bestCols);
        }
        double cwFinal = Math.Floor(bestW), chFinal = Math.Floor(bestW * ratio);
        if (bestCols != lastCols)
        {
            lastCols = bestCols;
            foreach (var c in cards) { var p = c.Parent as Panel; if (p != null) p.Children.Remove(c); }
            cardRows.Children.Clear();
            StackPanel row = null;
            for (int i = 0; i < shown.Count; i++)
            {
                if (i % bestCols == 0) { row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center }; cardRows.Children.Add(row); }
                row.Children.Add(shown[i]);
            }
        }
        foreach (var c in shown) { c.Width = cwFinal; c.Height = chFinal; c.Margin = new Thickness(gap / 2); }
        if (settingsPanel != null) settingsPanel.Width = Math.Min(480, w - 24);
    }

    // filtre : 0 tous, 1 disponibles, 2 Black Ops, 3 autres opus
    bool Matches(GameCard c, int f)
    {
        if (f == 1) return c.Available && !c.Killed;
        if (f == 2) return c.Game.BlackOps;
        if (f == 3) return !c.Game.BlackOps;
        return true;
    }
    void ApplyFilter(bool animate)
    {
        var next = new List<GameCard>();
        foreach (var c in cards) if (Matches(c, filter)) next.Add(c);
        if (next.Count == 0) foreach (var c in cards) next.Add(c); // rien de disponible : on montre tout
        bool same = next.Count == shown.Count;
        for (int i = 0; same && i < next.Count; i++) if (next[i] != shown[i]) same = false;
        UpdateCounts();
        if (same) return;
        shown.Clear(); shown.AddRange(next);
        lastCols = -1;
        Relayout();
        if (animate) for (int i = 0; i < shown.Count; i++) shown[i].Enter(40 + i * 55);
    }
    void UpdateCounts()
    {
        if (filterBar == null) return;
        var c = new int[4];
        foreach (var k in cards) for (int f = 0; f < 4; f++) if (Matches(k, f)) c[f]++;
        filterBar.SetCounts(c);
    }

    void StartAnimations()
    {
        if (started) return;
        started = true;
        header.Opacity = 0;
        var ht = new TranslateTransform(0, -18); header.RenderTransform = ht;
        Ui.Anim(header, OpacityProperty, 1, 700);
        Ui.Anim(ht, TranslateTransform.YProperty, 0, 800, new CubicEase { EasingMode = EasingMode.EaseOut });
        for (int i = 0; i < shown.Count; i++) shown[i].Enter(260 + i * 80);
    }

    // ---------- injecteurs ----------
    string FindExe(GameInfo g)
    {
        if (!string.IsNullOrEmpty(g.ConfiguredExe) && File.Exists(g.ConfiguredExe)) return g.ConfiguredExe;
        try
        {
            string folder = IOPath.Combine(dir, "Tools", g.Id);
            if (Directory.Exists(folder))
            {
                var exes = Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly);
                if (exes.Length > 0) { Array.Sort(exes, StringComparer.OrdinalIgnoreCase); return exes[0]; }
            }
        }
        catch { }
        return null;
    }

    void RefreshAvailability()
    {
        if (manifest != null) { string u; ApplyUpdates(manifest, out u); }
        else foreach (var c in cards) { c.Killed = killState != null && killState.Disabled(c.Game.Id); c.SetAvailable(FindExe(c.Game) != null); }
        if (settingsList != null && settingsPanel.Visibility == Visibility.Visible) FillSettings();
        if (filterBar != null) ApplyFilter(started);
    }

    // ---------- mises a jour (version.json sur le serveur MAG, voir UpdateCheck.cs) ----------
    Dictionary<string, MagRelease> manifest;
    static Version ExeVersion(string exe)
    {
        try { return MagUpdate.Parse(FileVersionInfo.GetVersionInfo(exe).FileVersion); } catch { return null; }
    }

    // compare le launcher et chaque injecteur installe a version.json ; badge sur les cartes ; liste de ce qui est a mettre a jour
    List<MagUpdateItem> ApplyUpdates(Dictionary<string, MagRelease> all, out string url)
    {
        var items = new List<MagUpdateItem>(); url = null; MagRelease r;
        if (all.TryGetValue("launcher", out r))
        {
            url = r.Url;
            if (MagUpdate.IsNewer(r.Version, MagUpdate.Current))
                items.Add(new MagUpdateItem { Name = "Launcher " + AppName, From = MagUpdate.Show(MagUpdate.Current), To = MagUpdate.Show(MagUpdate.Parse(r.Version)), Notes = MagUpdate.Notes(r, L.T) });
        }
        foreach (var c in cards)
        {
            string exe = FindExe(c.Game);
            c.Outdated = false;
            c.Killed = killState != null && killState.Disabled(c.Game.Id);
            if (exe != null && !c.Killed && all.TryGetValue(c.Game.Id, out r))
            {
                var v = ExeVersion(exe);
                if (v != null && MagUpdate.IsNewer(r.Version, v))
                {
                    c.Outdated = true;
                    items.Add(new MagUpdateItem { Name = "MAG GSC INJECTOR " + c.Game.Short, From = MagUpdate.Show(v), To = MagUpdate.Show(MagUpdate.Parse(r.Version)), Notes = MagUpdate.Notes(r, L.T) });
                    if (url == null) url = r.Url;
                }
            }
            c.SetAvailable(exe != null);
        }
        return items;
    }

    // ---------- desactivation a distance + mises a jour + compteur : au demarrage puis toutes les 5 minutes ----------
    MagKill killState; MagBlockWindow block; bool updateShown; DispatcherTimer watchTimer;

    // compteur d'utilisateurs : pastille du haut (total, detail en infobulle) + nombre sur chaque carte
    void UpdateOnline()
    {
        if (onlinePill == null) return;
        int n = MagStats.Online;
        onlinePill.Visibility = n >= 0 ? Visibility.Visible : Visibility.Collapsed;
        onlineText.Text = n + L.T(" en ligne", " online");
        var parts = new List<string>(); int k;
        foreach (var g in games) if (MagStats.Apps.TryGetValue(g.Id, out k) && k > 0) parts.Add(g.Short + " " + k);
        onlinePill.ToolTip = L.T("Utilisateurs en ligne", "Users online") + (parts.Count > 0 ? " : " + string.Join("  •  ", parts) : "");
        foreach (var c in cards) c.SetOnline(MagStats.Apps.TryGetValue(c.Game.Id, out k) ? k : 0);
    }

    // ---------- bannière (bloc « banniere » de version.json, réglée depuis le serveur) ----------
    Border banner; TextBlock bannerText, bannerGo; string bannerLink;
    Border BuildBanner()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var ic = Ui.T("\uE789", 15, Brushes.White, Ui.Icons); ic.VerticalAlignment = VerticalAlignment.Center; ic.Margin = new Thickness(0, 1, 10, 0);
        row.Children.Add(ic);
        bannerText = Ui.T("", 14.5, Brushes.White, Ui.Title); bannerText.VerticalAlignment = VerticalAlignment.Center; bannerText.TextWrapping = TextWrapping.Wrap; bannerText.MaxWidth = 860;
        row.Children.Add(bannerText);
        bannerGo = Ui.T("\uE8A7", 13, Brushes.White, Ui.Icons); bannerGo.VerticalAlignment = VerticalAlignment.Center; bannerGo.Margin = new Thickness(10, 1, 0, 0);
        row.Children.Add(bannerGo);
        banner = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(18, 8, 18, 9), Margin = new Thickness(0, 0, 0, 12), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center, Child = row, Visibility = Visibility.Collapsed };
        banner.MouseLeftButtonUp += delegate { if (bannerLink != null) try { Process.Start(new ProcessStartInfo(bannerLink) { UseShellExecute = true }); } catch { } };
        RefreshBanner();
        return banner;
    }
    void RefreshBanner()
    {
        if (banner == null) return;
        var b = MagNews.Banner;
        if (b == null) { banner.Visibility = Visibility.Collapsed; return; }
        Color c = Ui.Accent;
        try { if (!string.IsNullOrEmpty(b.Color)) c = (Color)ColorConverter.ConvertFromString(b.Color); } catch { }
        bannerText.Text = L.T(b.Fr ?? b.En, b.En ?? b.Fr);
        bannerLink = b.Link;
        bannerGo.Visibility = b.Link != null ? Visibility.Visible : Visibility.Collapsed;
        banner.Cursor = b.Link != null ? Cursors.Hand : null;
        banner.ToolTip = b.Link;
        banner.Background = new LinearGradientBrush(Ui.A(c, 0x55), Ui.A(c, 0x22), 0);
        banner.BorderBrush = Ui.B(Ui.A(c, 0xC0));
        banner.Effect = new DropShadowEffect { Color = c, BlurRadius = 24, ShadowDepth = 0, Opacity = 0.45 };
        banner.Visibility = Visibility.Visible;
    }

    void StartWatch()
    {
        MagNews.Attach(this, Ui.Accent, Ui.Accent2, L.T); // message à tous (réglé depuis le serveur)
        var saved = MagUpdate.LoadKill(); // dernier etat lu (un injecteur desactive le reste sans internet)
        if (saved != null) { killState = saved; RefreshAvailability(); }
        CheckUpdates(false);
        watchTimer = new DispatcherTimer(MagWatch.Every, DispatcherPriority.Background, delegate { CheckUpdates(false); }, Dispatcher);
        if (saved != null && saved.Disabled("launcher")) ShowBlock(saved);
        else MagWarnWindow.Show(this, L.T); // avertissement anti-revente a chaque lancement
    }

    // manual = bouton « Verifier les mises a jour » des parametres : dit toujours le resultat
    void CheckUpdates(bool manual)
    {
        var th = new Thread(delegate ()
        {
            MagKill kill; var all = MagUpdate.Fetch(8000, out kill);
            if (kill == null)
            {
                if (manual) Dispatcher.BeginInvoke(new Action(delegate { Toast(L.T("Impossible de lire les mises à jour (pas d'internet ou serveur injoignable)", "Could not read the updates (no internet or server unreachable)"), Ui.Warn); }));
                return; // version.json pas lu : rien ne change
            }
            MagUpdate.SaveKill(kill);
            MagStats.Blocked = kill.Disabled("launcher");
            if (!kill.Disabled("launcher")) MagStats.Begin("launcher"); // compteur d'utilisateurs (signal anonyme chaque minute)
            Dispatcher.BeginInvoke(new Action(delegate { OnManifest(all, kill, manual); }));
        }) { IsBackground = true, Name = "MAG update" };
        th.Start();
    }

    void OnManifest(Dictionary<string, MagRelease> all, MagKill kill, bool manual)
    {
        if (MagWarnWindow.After(delegate { OnManifest(all, kill, manual); })) return; // avertissement ouvert : apres sa fermeture
        killState = kill; if (all != null) manifest = all;
        RefreshAvailability();
        if (kill.Disabled("launcher")) { if (block != null) block.SetMessage(kill); else ShowBlock(kill); return; }
        if (block != null)
        {
            // reactive pendant que la fenetre de blocage est ouverte : le launcher reste ouvert
            var b = block; block = null; b.Reenable();
            Dispatcher.BeginInvoke(new Action(delegate { OnManifest(all, kill, manual); }));
            return;
        }
        if (manifest == null || (updateShown && !manual)) { MagNews.ShowPending(); return; }
        updateShown = true;
        string url; var items = ApplyUpdates(manifest, out url);
        if (items.Count > 0)
        {
            new MagUpdateWindow(this, Ui.Accent, Ui.Accent2, L.T, items, MagUpdate.SafeUrl(url)).ShowDialog();
            // mise a jour obligatoire : le launcher ne s'utilise plus tant qu'il n'est pas a jour
            if (watchTimer != null) watchTimer.Stop();
            Close();
        }
        else
        {
            if (manual) Toast(L.T("Tout est à jour", "Everything is up to date") + "  •  v" + MagUpdate.Show(MagUpdate.Current), Ui.Online);
            MagNews.ShowPending();
        }
    }

    void ShowBlock(MagKill kill)
    {
        var b = new MagBlockWindow(this, L.T, "Launcher " + AppName, kill);
        block = b;
        b.ShowDialog();
        if (block == b) block = null;
        if (!b.Reenabled) { if (watchTimer != null) watchTimer.Stop(); Close(); }
    }

    void OnCardClick(GameCard c)
    {
        string exe = FindExe(c.Game);
        if (exe != null && c.Killed && killState != null)
        {
            Toast(L.T("Injecteur GSC " + c.Game.Short + " désactivé  •  ", c.Game.Short + " GSC injector disabled  •  ") + killState.Message(L.T), Ui.C("#FF4D5E"));
            return;
        }
        if (exe == null)
        {
            Toast(L.T("L'injecteur GSC " + c.Game.Short + " arrive bientôt  •  suis le Discord pour sa sortie",
                      "The " + c.Game.Short + " GSC injector is coming soon  •  follow the Discord for its release"), c.Game.Accent);
            return;
        }
        c.Pulse();
        try
        {
            var psi = new ProcessStartInfo(exe) { WorkingDirectory = IOPath.GetDirectoryName(exe), UseShellExecute = true };
            if (!ToolHasLang(exe)) psi.Arguments = "--lang " + (L.En ? "en" : "fr");
            Process.Start(psi);
            Toast(L.T("Lancement de l'injecteur GSC " + c.Game.Short + "…", "Launching the " + c.Game.Short + " GSC injector…"), c.Game.Accent);
            if (minimizeAfterLaunch) { var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) }; t.Tick += delegate { t.Stop(); WindowState = WindowState.Minimized; }; t.Start(); }
        }
        catch (Exception ex) { Toast(L.T("Impossible de lancer ", "Could not launch ") + IOPath.GetFileName(exe) + L.T(" : ", ": ") + ex.Message, Ui.Danger); }
    }

    // l'injecteur a deja sa propre langue (choisie avec ses drapeaux) : « lang= » dans son MAG_GSC_INJECTOR.cfg
    static bool ToolHasLang(string exe)
    {
        try
        {
            string cfg = IOPath.Combine(IOPath.GetDirectoryName(exe), "MAG_GSC_INJECTOR.cfg");
            if (File.Exists(cfg)) foreach (var line in File.ReadAllLines(cfg)) if (line.Trim().StartsWith("lang=")) return true;
        }
        catch { }
        return false;
    }

    // ---------- notification ----------
    void Toast(string text, Color accent)
    {
        toastText.Text = text;
        toast.BorderBrush = Ui.B(Ui.A(accent, 160));
        ((DropShadowEffect)toast.Effect).Color = accent;
        toast.Visibility = Visibility.Visible;
        var tt = (TranslateTransform)toast.RenderTransform;
        Ui.Anim(toast, OpacityProperty, 1, 220);
        tt.Y = 24; Ui.Anim(tt, TranslateTransform.YProperty, 0, 380, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 });
        toastTimer.Stop(); toastTimer.Start();
    }

    // ---------- parametres ----------
    void BuildOverlay()
    {
        overlay = new Grid();
        root.Children.Add(overlay);

        toast = new Border
        {
            CornerRadius = new CornerRadius(14), Background = Ui.B("#F20B1122"), BorderThickness = new Thickness(1), Padding = new Thickness(18, 11, 18, 12),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(20, 0, 20, 62), MaxWidth = 760,
            Opacity = 0, Visibility = Visibility.Collapsed, IsHitTestVisible = false, RenderTransform = new TranslateTransform(),
            Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 0, Opacity = 0.5 }
        };
        toastText = Ui.T("", 13.5, Ui.TextMain, Ui.Text); toastText.TextWrapping = TextWrapping.Wrap; toastText.TextAlignment = TextAlignment.Center;
        toast.Child = toastText;
        overlay.Children.Add(toast);
        toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3400) };
        toastTimer.Tick += delegate
        {
            toastTimer.Stop();
            var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
            a.Completed += delegate { if (toast.Opacity < 0.05) toast.Visibility = Visibility.Collapsed; };
            toast.BeginAnimation(OpacityProperty, a);
        };

        settingsDim = new Rectangle { Fill = Ui.B("#A0000000"), Opacity = 0, Visibility = Visibility.Collapsed };
        settingsDim.MouseLeftButtonUp += delegate { ShowSettings(false); };
        overlay.Children.Add(settingsDim);

        settingsPanel = new Border
        {
            Width = 480, HorizontalAlignment = HorizontalAlignment.Right, Background = Ui.Grad(Ui.C("#F80D1428"), Ui.C("#F8060A15"), 90),
            BorderBrush = Ui.B("#2AC8D4FF"), BorderThickness = new Thickness(1, 0, 0, 0), Visibility = Visibility.Collapsed,
            RenderTransform = new TranslateTransform(500, 0), Effect = new DropShadowEffect { BlurRadius = 50, ShadowDepth = 0, Opacity = 0.6, Color = Colors.Black }
        };
        var sp = new DockPanel { Margin = new Thickness(24, 16, 10, 18), LastChildFill = true };
        var head = new Grid { Margin = new Thickness(0, 0, 14, 8) };
        var ht = new StackPanel { Orientation = Orientation.Horizontal };
        var hic = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(10), Background = Ui.Grad(Ui.A(Ui.Accent, 60), Ui.A(Ui.Accent2, 20), 45), BorderBrush = Ui.B(Ui.A(Ui.Accent, 110)), BorderThickness = new Thickness(1), Child = Ui.Icon("", 15, Ui.B(Ui.Accent)) };
        ((TextBlock)hic.Child).HorizontalAlignment = HorizontalAlignment.Center;
        ht.Children.Add(hic);
        var htt = Ui.T(L.T("Paramètres", "Settings"), 25, Brushes.White, Ui.Title, FontWeights.SemiBold); htt.Margin = new Thickness(12, 0, 0, 0); htt.VerticalAlignment = VerticalAlignment.Center;
        ht.Children.Add(htt);
        head.Children.Add(ht);
        var close = Ui.Icon("", 12, Ui.B("#C9CEDA")); close.HorizontalAlignment = HorizontalAlignment.Center;
        var closeBtn = new FlatButton(close, Ui.C("#00FFFFFF"), Ui.C("#22FFFFFF"), 10, new Thickness(0)) { Width = 38, Height = 38, HorizontalAlignment = HorizontalAlignment.Right };
        closeBtn.Click += delegate { ShowSettings(false); };
        WindowChrome.SetIsHitTestVisibleInChrome(closeBtn, true);
        head.Children.Add(closeBtn);
        DockPanel.SetDock(head, Dock.Top); sp.Children.Add(head);

        settingsList = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        sp.Children.Add(new ScrollViewer { Content = settingsList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false });
        settingsPanel.Child = sp;
        overlay.Children.Add(settingsPanel);
    }

    static TextBlock Section(string text)
    {
        var t = Ui.T(Ui.Spaced(text.ToUpperInvariant()), 10.5, Ui.TextDim, Ui.Title, FontWeights.SemiBold);
        t.Margin = new Thickness(2, 16, 0, 8);
        return t;
    }

    void FillSettings()
    {
        settingsList.Children.Clear();
        // options
        settingsList.Children.Add(Section(L.T("Options", "Options")));
        var opts = new Border { CornerRadius = new CornerRadius(14), Background = Ui.B("#0EC8D4FF"), BorderBrush = Ui.B("#1EC8D4FF"), BorderThickness = new Thickness(1), Padding = new Thickness(14, 8, 14, 8) };
        var os = new StackPanel();
        var tmin = new Toggle(); tmin.IsOn = minimizeAfterLaunch; tmin.Changed += delegate (bool v) { minimizeAfterLaunch = v; SaveCfg(); };
        os.Children.Add(OptRow(L.T("Réduire après le lancement", "Minimize after launching"), L.T("Le launcher se range quand un injecteur s'ouvre.", "The launcher hides when an injector opens."), tmin));
        var tst = new Toggle(); tst.IsOn = MagStats.Enabled; tst.Changed += delegate (bool v) { MagStats.Enabled = v; };
        os.Children.Add(OptRow(L.T("Compteur d'utilisateurs", "Users counter"), L.T("Signal anonyme pour le nombre de personnes en ligne.", "Anonymous signal for the number of people online."), tst));
        var tsp = new Toggle(); tsp.IsOn = MagSplash.Enabled; tsp.Changed += delegate (bool v) { MagSplash.Enabled = v; SaveCfg(); };
        os.Children.Add(OptRow(L.T("Écran de chargement", "Loading screen"), L.T("Animation MAG au lancement du launcher (clic pour la passer).", "MAG animation when the launcher starts (click to skip)."), tsp));
        os.Children.Add(FxRow());
        opts.Child = os;
        settingsList.Children.Add(opts);
        var btns = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        var ob = Ui.Ghost(L.T("Ouvrir le dossier Tools", "Open the Tools folder"), "", delegate { try { Process.Start("explorer.exe", "\"" + IOPath.Combine(dir, "Tools") + "\""); } catch { } });
        ob.Margin = new Thickness(0, 0, 8, 8); btns.Children.Add(ob);
        var ub = Ui.Primary(L.T("Vérifier les mises à jour", "Check for updates"), "", delegate { Toast(L.T("Vérification des mises à jour…", "Checking for updates…"), Ui.Accent); CheckUpdates(true); });
        ub.Margin = new Thickness(0, 0, 8, 8); btns.Children.Add(ub);
        settingsList.Children.Add(btns);

        // emplacement des injecteurs
        settingsList.Children.Add(Section(L.T("Injecteurs GSC", "GSC injectors")));
        var info = Ui.T(L.T("Sans réglage, le launcher prend le premier .exe du dossier Tools\\<JEU> à côté du launcher.", "If not set, the launcher uses the first .exe in the Tools\\<GAME> folder next to the launcher."), 12.5, Ui.TextSub, Ui.Text);
        info.TextWrapping = TextWrapping.Wrap; info.Margin = new Thickness(2, 0, 0, 10);
        settingsList.Children.Add(info);
        foreach (var g in games)
        {
            var game = g;
            string exe = FindExe(g);
            var card = new Border { CornerRadius = new CornerRadius(14), Background = Ui.B("#0EC8D4FF"), BorderBrush = Ui.B(Ui.A(g.Accent, exe != null ? (byte)90 : (byte)40)), BorderThickness = new Thickness(1), Padding = new Thickness(14, 11, 14, 11), Margin = new Thickness(0, 0, 0, 8) };
            var st = new StackPanel();
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = Ui.B(exe != null ? g.Accent : Ui.C("#5F6880")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0) });
            top.Children.Add(Ui.T(g.Short, 15.5, Brushes.White, Ui.Title, FontWeights.SemiBold));
            var exeVer = exe != null ? ExeVersion(exe) : null;
            var nm = Ui.T("  " + g.Name + (exeVer != null ? "  •  V" + MagUpdate.Show(exeVer) : ""), 12.5, Ui.TextSub, Ui.Text); nm.VerticalAlignment = VerticalAlignment.Center;
            top.Children.Add(nm);
            st.Children.Add(top);
            var path = Ui.T(exe ?? L.T("Pas encore installé (Tools\\" + g.Id + "\\)", "Not installed yet (Tools\\" + g.Id + "\\)"), 12, exe != null ? Ui.B("#C9CEDA") : Ui.TextDim, Ui.Text);
            path.TextTrimming = TextTrimming.CharacterEllipsis; path.Margin = new Thickness(0, 5, 0, 8); path.ToolTip = exe;
            st.Children.Add(path);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var browse = new FlatButton(Ui.T(L.T("Choisir le .exe", "Choose the .exe"), 12.5, Brushes.White, Ui.Text, FontWeights.SemiBold), Ui.A(g.Accent, 60), Ui.A(g.Accent, 120), 10, new Thickness(12, 5, 12, 6));
            browse.Click += delegate { Browse(game); };
            row.Children.Add(browse);
            if (!string.IsNullOrEmpty(g.ConfiguredExe))
            {
                var reset = new FlatButton(Ui.T(L.T("Par défaut", "Default"), 12.5, Ui.B("#C9CEDA"), Ui.Text), Ui.C("#14FFFFFF"), Ui.C("#2AFFFFFF"), 10, new Thickness(12, 5, 12, 6)) { Margin = new Thickness(8, 0, 0, 0) };
                reset.Click += delegate { game.ConfiguredExe = null; SaveCfg(); RefreshAvailability(); FillSettings(); };
                row.Children.Add(reset);
            }
            st.Children.Add(row);
            card.Child = st;
            settingsList.Children.Add(card);
        }

        // contact
        settingsList.Children.Add(Section("Contact & Discord"));
        var ci = Ui.T(L.T("Une question, un bug, une idée ? Rejoins le serveur Discord ou ajoute UrBaaZz et Zerty.", "A question, a bug, an idea? Join the Discord server or add UrBaaZz and Zerty."), 12.5, Ui.TextSub, Ui.Text);
        ci.TextWrapping = TextWrapping.Wrap; ci.Margin = new Thickness(2, 0, 0, 10);
        settingsList.Children.Add(ci);
        settingsList.Children.Add(Contact.Body(delegate (string m) { Toast(m, Contact.Blurple); }));
        var credit = Ui.T(AppName + "  •  v" + MagUpdate.Show(MagUpdate.Current) + "  •  Dev By UrBaaZz and Zerty", 12, Ui.TextDim, Ui.Title); credit.Margin = new Thickness(2, 14, 0, 6); credit.TextWrapping = TextWrapping.Wrap;
        settingsList.Children.Add(credit);
    }

    // reglage « Effets visuels » (voir Fx.cs), commun au launcher et a tous les tools MAG
    static UIElement FxRow()
    {
        var st = new StackPanel { Margin = new Thickness(0, 8, 0, 6) };
        st.Children.Add(Ui.T(MagFx.Title(L.En), 14, Ui.TextMain, Ui.Title, FontWeights.SemiBold));
        var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 2) };
        var pills = new List<Border>();
        Action paint = delegate
        {
            foreach (var b in pills)
            {
                bool on = (int)b.Tag == MagFx.Level;
                b.Background = Ui.B(on ? Ui.A(Ui.Accent, 60) : Ui.C("#0EFFFFFF"));
                b.BorderBrush = Ui.B(on ? Ui.A(Ui.Accent, 200) : Ui.C("#20FFFFFF"));
                ((TextBlock)b.Child).Foreground = on ? Brushes.White : Ui.TextSub;
            }
        };
        foreach (int v in new[] { MagFx.Legers, MagFx.Complets, MagFx.Aucun })
        {
            int k = v;
            var t = Ui.T(MagFx.Name(v, L.En) + (v == MagFx.Legers ? L.T(" (conseillé)", " (recommended)") : ""), 12, Ui.TextMain, Ui.Text, FontWeights.SemiBold);
            var b = new Border { Child = t, Tag = v, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(10, 5, 10, 6), Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand };
            b.MouseLeftButtonUp += delegate { MagFx.Set(k); paint(); };
            pills.Add(b); row.Children.Add(b);
        }
        paint();
        st.Children.Add(row);
        var help = Ui.T(MagFx.Help(L.En), 12, Ui.TextSub, Ui.Text); help.TextWrapping = TextWrapping.Wrap;
        st.Children.Add(help);
        return st;
    }

    static Grid OptRow(string title, string desc, UIElement control)
    {
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4), MinHeight = 44 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        st.Children.Add(Ui.T(title, 14, Ui.TextMain, Ui.Text, FontWeights.SemiBold));
        var d = Ui.T(desc, 12, Ui.TextSub); d.TextWrapping = TextWrapping.Wrap; st.Children.Add(d);
        g.Children.Add(st);
        ((FrameworkElement)control).VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1); g.Children.Add(control);
        return g;
    }

    void Browse(GameInfo g)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = L.T("Injecteur GSC ", "GSC injector ") + g.Short, Filter = L.T("Programme (*.exe)|*.exe", "Program (*.exe)|*.exe"), InitialDirectory = IOPath.Combine(dir, "Tools", g.Id) };
        if (dlg.ShowDialog(this) == true) { g.ConfiguredExe = dlg.FileName; SaveCfg(); RefreshAvailability(); FillSettings(); Toast(L.T("Injecteur GSC " + g.Short + " enregistré", g.Short + " GSC injector saved"), g.Accent); }
    }

    void ShowSettings(bool show)
    {
        var tt = (TranslateTransform)settingsPanel.RenderTransform;
        if (show)
        {
            FillSettings();
            settingsDim.Visibility = Visibility.Visible; settingsPanel.Visibility = Visibility.Visible;
            Ui.Anim(settingsDim, OpacityProperty, 1, 260);
            Ui.Anim(tt, TranslateTransform.XProperty, 0, 420, new CubicEase { EasingMode = EasingMode.EaseOut });
        }
        else
        {
            Ui.Anim(settingsDim, OpacityProperty, 0, 240);
            var a = new DoubleAnimation(settingsPanel.ActualWidth + 30, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            a.Completed += delegate { if (tt.X > 10) { settingsDim.Visibility = Visibility.Collapsed; settingsPanel.Visibility = Visibility.Collapsed; } };
            tt.BeginAnimation(TranslateTransform.XProperty, a);
        }
    }

    // ---------- configuration ----------
    public static string LangArg; // --lang fr|en (captures)
    void LoadCfg()
    {
        string lang = null;
        try
        {
            if (File.Exists(cfgPath))
                foreach (var line in File.ReadAllLines(cfgPath))
                {
                    int i = line.IndexOf('='); if (i < 0) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                    if (k == "minimize") minimizeAfterLaunch = v == "1";
                    if (k == "lang") lang = v;
                    if (k == "filter") int.TryParse(v, out filter);
                    foreach (var g in games) if (g.Id == k && v.Length > 0) g.ConfiguredExe = v;
                }
        }
        catch { }
        if (filter < 0 || filter > 3) filter = 0;
        L.En = LangArg != null ? LangArg == "en" : lang != null ? lang == "en" : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "fr";
    }
    void SaveCfg()
    {
        if (capturing) return; // les captures de controle ne touchent pas aux reglages
        try
        {
            var lines = new List<string> { "minimize=" + (minimizeAfterLaunch ? "1" : "0"), "splash=" + (MagSplash.Enabled ? "1" : "0"), "lang=" + (L.En ? "en" : "fr"), "filter=" + filter };
            foreach (var g in games) lines.Add(g.Id + "=" + (g.ConfiguredExe ?? ""));
            File.WriteAllLines(cfgPath, lines.ToArray());
        }
        catch { }
    }

    // ---------- capture d'ecran de controle (option --capture <png> <w> <h> [mode]) ----------
    // modes : hover (survol de BO4), settings, update (lit version.json, comme au demarrage), toast, filtre1..3
    public void Capture(string path, int w, int h, string mode)
    {
        MagFx.NoPause();
        Width = w; Height = h; WindowStartupLocation = WindowStartupLocation.Manual; Left = -30000; Top = 0; ShowActivated = false; ShowInTaskbar = false;
        capturing = true;
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(mode == "settings" ? 1500 : 2400) };
        bool step2 = false;
        Loaded += delegate
        {
            if (mode == "settings") ShowSettings(true);
            if (mode == "hover") foreach (var c in cards) if (c.Game.Id == "BO4") c.Hover(true);
            if (mode == "update") { MagKill k; var all = MagUpdate.Fetch(5000, out k); if (k != null) killState = k; if (all != null) manifest = all; RefreshAvailability(); MagStats.Send("launcher"); UpdateOnline(); }
            if (mode == "toast") Toast(L.T("L'injecteur GSC BO2 arrive bientôt  •  suis le Discord pour sa sortie", "The BO2 GSC injector is coming soon  •  follow the Discord for its release"), games[1].Accent);
            if (mode.StartsWith("filtre")) { int f; if (int.TryParse(mode.Substring(6), out f)) { filter = f; filterBar.Select(f, false); ApplyFilter(false); foreach (var c in shown) c.Opacity = 1; } }
            t.Start();
        };
        t.Tick += delegate
        {
            if (!step2 && mode == "settings") { step2 = true; return; }
            t.Stop();
            var rtb = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(path)) enc.Save(fs);
            Close();
        };
    }

    [STAThread]
    static void Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        int li = Array.IndexOf(args, "--lang"); if (li >= 0 && li + 1 < args.Length) LangArg = args[li + 1];
        if (args.Length >= 2 && args[0] == "--splash-export") { MagSplash.Export(args[1], args.Length > 2 ? args[2] : null); return; } // images de l'écran de chargement
        bool capture = args.Length >= 4 && args[0] == "--capture";
        MagSplash.ReadCfg(IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "MULTI_COD_MAG_INJECTOR_GSC.cfg"));
        if (!capture && MagSplash.Enabled && Array.IndexOf(args, "--nosplash") < 0)
        {
            // écran de chargement animé, puis la fenêtre principale (créée à la fin pour ne rien lancer derrière)
            var sp = new MagSplash();
            sp.Done += delegate { var mw = new LauncherWindow(); app.MainWindow = mw; mw.Show(); mw.Activate(); };
            app.Run(sp);
            return;
        }
        var w = new LauncherWindow();
        if (capture) w.Capture(args[1], int.Parse(args[2]), int.Parse(args[3]), args.Length > 4 && !args[4].StartsWith("--") ? args[4] : "");
        app.Run(w);
    }
}
