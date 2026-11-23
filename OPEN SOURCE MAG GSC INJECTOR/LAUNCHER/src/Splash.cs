// MULTI COD MAG INJECTOR GSC - écran de chargement au lancement : hexagone MAG qui se dessine, « MAG », « MODDING AND
// GAMING », barre de chargement et « DEV'S BY UrBaaZz & ZERTY ». Toute l'animation est calculée depuis un seul temps t
// (Apply) : la même scène sert à l'écran animé du launcher et à l'export en images (--splash-export <dossier>).
// Clic ou touche = passer. Désactivable dans les paramètres du launcher (splash=0 dans le .cfg).
// Dev by UrBaaZz and Zerty.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;

class MagSplash : Window
{
    public const double W = 760, H = 440;
        const double TEnd = 3.45, TFadeOut = 3.15;       // durée totale, début du fondu de sortie
    public static bool Enabled = true;               // réglage « Écran de chargement » (splash= dans le .cfg)

    public event Action Done;
    readonly Grid scene;
    readonly Stopwatch clock = new Stopwatch();
    bool finished;

    // ---------- réglage dans le .cfg du launcher (lu avant la fenêtre principale) ----------
    public static void ReadCfg(string cfgPath)
    {
        try
        {
            if (!File.Exists(cfgPath)) return;
            foreach (var line in File.ReadAllLines(cfgPath))
            {
                int i = line.IndexOf('='); if (i < 0) continue;
                string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                if (k == "splash") Enabled = v != "0";
                if (k == "lang" && LauncherWindow.LangArg == null) L.En = v == "en";
            }
        }
        catch { }
    }

    public MagSplash()
    {
        Title = "MULTI COD MAG INJECTOR GSC";
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true; ShowInTaskbar = true;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        try { var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("mag.ico"); if (s != null) Icon = BitmapFrame.Create(s); } catch { }
        scene = new SplashScene(true);
        Content = scene;
        Cursor = Cursors.AppStarting;
        ((SplashScene)scene).Apply(0);
        MouseLeftButtonDown += delegate { Skip(); };
        KeyDown += delegate { Skip(); };
        Loaded += delegate { clock.Start(); CompositionTarget.Rendering += OnFrame; };
        Closed += delegate { CompositionTarget.Rendering -= OnFrame; };
    }

    // effets désactivés dans les paramètres : écran plus court
    static double Speed { get { return MagFx.Level == MagFx.Aucun ? 1.8 : 1.0; } }

    double skip; // secondes sautées (clic / touche)
    double Now { get { return clock.Elapsed.TotalSeconds * Speed + skip; } }
    void OnFrame(object s, EventArgs e)
    {
        double t = Now;
        ((SplashScene)scene).Apply(Math.Min(t, TEnd));
        if (t >= TEnd) Finish();
    }
    void Skip()
    {
        // saut : on file directement au fondu de sortie
        double t = Now;
        if (t < TFadeOut) skip += TFadeOut - t;
    }
    void Finish()
    {
        if (finished) return; finished = true;
        CompositionTarget.Rendering -= OnFrame;
        if (Done != null) Done();
        Close();
    }

    // ---------- export : images PNG + GIF animé (--splash-export <dossier>) ----------
    public static void Export(string dir, string debugTimes = null)
    {
        Directory.CreateDirectory(dir);
        var sc = new SplashScene(false);
        sc.Measure(new Size(W, H)); sc.Arrange(new Rect(0, 0, W, H));
        if (debugTimes != null)
        {
            // images de contrôle à des instants donnés (« 0.5,1.2,... »)
            foreach (var ts in debugTimes.Split(','))
            {
                double t = double.Parse(ts, System.Globalization.CultureInfo.InvariantCulture);
                sc.Apply(t); sc.UpdateLayout();
                Save(Render(sc, 1), System.IO.Path.Combine(dir, "t_" + ts + ".png"));
            }
            return;
        }
        const int fps = 20;
        var frames = new List<byte[]>();
        var delays = new List<int>();
        int n = (int)Math.Round(TFadeOut * fps);
        for (int i = 0; i <= n; i++)
        {
            double t = i / (double)fps;
            sc.Apply(t); sc.UpdateLayout();
            var bmp = Render(sc, 1);
            var g = new GifBitmapEncoder(); g.Frames.Add(BitmapFrame.Create(bmp));
            using (var ms = new MemoryStream()) { g.Save(ms); frames.Add(ms.ToArray()); }
            delays.Add(i == n ? 250 : 5); // dernière image tenue 2,5 s avant de reboucler (centièmes de seconde)
            if (i == n)
            {
                Save(bmp, System.IO.Path.Combine(dir, "MAG_ecran_chargement.png"));
                Save(Render(sc, 2), System.IO.Path.Combine(dir, "MAG_ecran_chargement_HD.png"));
            }
        }
        File.WriteAllBytes(System.IO.Path.Combine(dir, "MAG_ecran_chargement_anime.gif"), GifJoin(frames, delays));
    }
    static BitmapSource Render(FrameworkElement e, double scale)
    {
        var rtb = new RenderTargetBitmap((int)(W * scale), (int)(H * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        rtb.Render(e); rtb.Freeze();
        return rtb;
    }
    static void Save(BitmapSource b, string path)
    {
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(b));
        using (var fs = File.Create(path)) enc.Save(fs);
    }
    // assemble des GIF d'une image (encodés par WPF) en un GIF animé qui boucle : chaque image garde sa palette
    // (table locale) et reçoit un bloc de délai ; extension NETSCAPE2.0 = boucle infinie.
    static byte[] GifJoin(List<byte[]> gifs, List<int> delays)
    {
        using (var o = new MemoryStream())
        {
            var first = gifs[0];
            o.Write(first, 0, 6);                                   // GIF89a
            o.Write(first, 6, 4);                                   // largeur, hauteur
            o.WriteByte(0); o.WriteByte(0); o.WriteByte(0);         // pas de table globale
            o.Write(new byte[] { 0x21, 0xFF, 0x0B }, 0, 3);
            var ns = System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"); o.Write(ns, 0, ns.Length);
            o.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 }, 0, 5);
            for (int f = 0; f < gifs.Count; f++)
            {
                var g = gifs[f];
                int flags = g[10]; int p = 13; byte[] gct = null; int gctBits = 0;
                if ((flags & 0x80) != 0) { gctBits = flags & 7; int len = 3 * (1 << (gctBits + 1)); gct = new byte[len]; Array.Copy(g, p, gct, 0, len); p += len; }
                while (p < g.Length && g[p] == 0x21) { p += 2; while (g[p] != 0) p += g[p] + 1; p++; } // saute les extensions de WPF
                if (g[p] != 0x2C) throw new InvalidDataException("GIF inattendu");
                int d = delays[f];
                o.Write(new byte[] { 0x21, 0xF9, 0x04, 0x04, (byte)(d & 0xFF), (byte)(d >> 8), 0x00, 0x00 }, 0, 8);
                var desc = new byte[10]; Array.Copy(g, p, desc, 0, 10); p += 10;
                int lflags = desc[9];
                if ((lflags & 0x80) == 0 && gct != null) { desc[9] = (byte)((lflags & 0x40) | 0x80 | gctBits); o.Write(desc, 0, 10); o.Write(gct, 0, gct.Length); }
                else
                {
                    o.Write(desc, 0, 10);
                    if ((lflags & 0x80) != 0) { int len = 3 * (1 << ((lflags & 7) + 1)); o.Write(g, p, len); p += len; }
                }
                int start = p; p++;                                   // taille de code LZW
                while (g[p] != 0) p += g[p] + 1; p++;                 // sous-blocs de données
                o.Write(g, start, p - start);
            }
            o.WriteByte(0x3B);
            return o.ToArray();
        }
    }
}

// la scène elle-même (760 x 440). live = fenêtre (coins arrondis, fondu de sortie) ; sinon export (rectangle plein).
class SplashScene : Grid
{
    const double W = MagSplash.W, H = MagSplash.H, CX = W / 2, CY = 176, R = 122;
    readonly bool live;
    readonly Canvas c = new Canvas { Width = W, Height = H };
    readonly TranslateTransform gridMove = new TranslateTransform();
    readonly Ellipse halo;
    readonly RotateTransform ringRot = new RotateTransform(0, CX, CY), ring2Rot = new RotateTransform(0, CX, CY);
    readonly Ellipse ring, ring2;
    readonly Path hexStroke, hexFill, hexInner;
    readonly ScaleTransform hexScale = new ScaleTransform(1, 1, CX, CY);
    readonly double hexLen;
    readonly TextBlock[] letters = new TextBlock[3], shineLetters = new TextBlock[3];
    readonly TranslateTransform[] letterMove = new TranslateTransform[3], shineMove = new TranslateTransform[3];
    readonly StackPanel shineWord;
    readonly LinearGradientBrush shineMask;
    readonly DropShadowEffect magGlow;
    readonly TextBlock tagline; readonly RectangleGeometry taglineClip;
    readonly Rectangle lineL, lineR;
    readonly Rectangle barFill; readonly Ellipse barHead; readonly TextBlock barText;
    readonly TextBlock credits;
    readonly Canvas ribbon;
    readonly List<Particle> parts = new List<Particle>();
    class Particle { public Ellipse E; public double X, Speed, Phase, Size, Drift; }
    const double BarW = 280, BarY = 362;

    static Color C(string h) { return Ui.C(h); }

    public SplashScene(bool live)
    {
        this.live = live;
        Width = W; Height = H;
        var orange = C("#FF7A1A"); var gold = C("#FFC65C"); var green = C("#9BCB3C"); var green2 = C("#3FA35A");

        // fond bleu nuit + halo
        var bg = new RadialGradientBrush { Center = new Point(0.5, 0.40), GradientOrigin = new Point(0.5, 0.40), RadiusX = 0.75, RadiusY = 0.95 };
        bg.GradientStops.Add(new GradientStop(C("#17234D"), 0)); bg.GradientStops.Add(new GradientStop(C("#0A1230"), 0.45)); bg.GradientStops.Add(new GradientStop(C("#04070F"), 1));
        c.Children.Add(new Rectangle { Width = W, Height = H, Fill = bg });

        // grille tactique qui défile lentement
        var tile = new DrawingBrush(new GeometryDrawing(null, new Pen(new SolidColorBrush(Color.FromArgb(16, 200, 212, 255)), 1), new GeometryGroup { Children = { new LineGeometry(new Point(0, 0), new Point(32, 0)), new LineGeometry(new Point(0, 0), new Point(0, 32)) } }))
        { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 32, 32), ViewportUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 32, 32), ViewboxUnits = BrushMappingMode.Absolute };
        var gridRect = new Rectangle { Width = W, Height = H + 64, Fill = tile, RenderTransform = gridMove };
        Canvas.SetTop(gridRect, -32);
        var fade = new RadialGradientBrush { Center = new Point(0.5, 0.42), GradientOrigin = new Point(0.5, 0.42), RadiusX = 0.62, RadiusY = 0.8 };
        fade.GradientStops.Add(new GradientStop(Colors.White, 0)); fade.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
        gridRect.OpacityMask = fade;
        c.Children.Add(gridRect);

        var hb = new RadialGradientBrush();
        hb.GradientStops.Add(new GradientStop(Color.FromArgb(150, orange.R, orange.G, orange.B), 0));
        hb.GradientStops.Add(new GradientStop(Color.FromArgb(60, green2.R, green2.G, green2.B), 0.55));
        hb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
        halo = new Ellipse { Width = 520, Height = 360, Fill = hb };
        Canvas.SetLeft(halo, CX - 260); Canvas.SetTop(halo, CY - 180);
        c.Children.Add(halo);

        // particules (étincelles orange / vertes qui montent)
        var rnd = new Random(57);
        for (int i = 0; i < 54; i++)
        {
            var col = i % 3 == 0 ? green : i % 3 == 1 ? orange : gold;
            var p = new Particle { X = rnd.NextDouble() * W, Speed = 18 + rnd.NextDouble() * 46, Phase = rnd.NextDouble() * (H + 40), Size = 1.4 + rnd.NextDouble() * 2.6, Drift = rnd.NextDouble() * 6.28 };
            p.E = new Ellipse { Width = p.Size, Height = p.Size, Fill = new SolidColorBrush(col), Effect = i % 4 == 0 ? new BlurEffect { Radius = 2 } : null };
            parts.Add(p); c.Children.Add(p.E);
        }

        // anneaux pointillés qui tournent autour de l'hexagone
        ring = new Ellipse { Width = R * 2 + 56, Height = R * 2 + 56, Stroke = new SolidColorBrush(Color.FromArgb(90, gold.R, gold.G, gold.B)), StrokeThickness = 1.4, StrokeDashArray = new DoubleCollection { 2, 6 }, RenderTransform = ringRot };
        Canvas.SetLeft(ring, CX - R - 28); Canvas.SetTop(ring, CY - R - 28);
        ring2 = new Ellipse { Width = R * 2 + 84, Height = R * 2 + 84, Stroke = Ui.Grad(Color.FromArgb(120, orange.R, orange.G, orange.B), Color.FromArgb(0, green.R, green.G, green.B), 0), StrokeThickness = 2.2, StrokeDashArray = new DoubleCollection { 40, 22, 8, 22 }, RenderTransform = ring2Rot };
        Canvas.SetLeft(ring2, CX - R - 42); Canvas.SetTop(ring2, CY - R - 42);
        ringRot.CenterX = R + 28; ringRot.CenterY = R + 28; ring2Rot.CenterX = R + 42; ring2Rot.CenterY = R + 42;
        c.Children.Add(ring); c.Children.Add(ring2);

        // hexagone : verre sombre + contour dégradé qui se dessine
        var hex = HexGeo(R); var hexIn = HexGeo(R - 12);
        hexLen = 6 * R;
        var fill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        fill.GradientStops.Add(new GradientStop(C("#2A2140"), 0)); fill.GradientStops.Add(new GradientStop(C("#121A3A"), 0.5)); fill.GradientStops.Add(new GradientStop(C("#0E2A26"), 1));
        hexFill = new Path { Data = hex, Fill = fill, RenderTransform = hexScale };
        var stroke = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        stroke.GradientStops.Add(new GradientStop(gold, 0)); stroke.GradientStops.Add(new GradientStop(orange, 0.45)); stroke.GradientStops.Add(new GradientStop(green2, 1));
        hexStroke = new Path { Data = hex, Stroke = stroke, StrokeThickness = 4, StrokeLineJoin = PenLineJoin.Round, StrokeDashCap = PenLineCap.Round, RenderTransform = hexScale,
            Effect = new DropShadowEffect { Color = orange, BlurRadius = 22, ShadowDepth = 0, Opacity = 0.85 } };
        hexStroke.StrokeDashArray = new DoubleCollection { hexLen / 4, hexLen / 4 };
        hexInner = new Path { Data = hexIn, Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), StrokeThickness = 1, RenderTransform = hexScale };
        c.Children.Add(hexFill); c.Children.Add(hexInner); c.Children.Add(hexStroke);
        Children.Add(c);

        // « MAG » (lettres qui tombent) + reflet qui balaie
        var magFill = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        magFill.GradientStops.Add(new GradientStop(C("#FFE7B0"), 0)); magFill.GradientStops.Add(new GradientStop(gold, 0.35)); magFill.GradientStops.Add(new GradientStop(orange, 0.75)); magFill.GradientStops.Add(new GradientStop(C("#E8541A"), 1));
        var word = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        shineWord = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        string[] ls = { "M", "A", "G" };
        for (int i = 0; i < 3; i++)
        {
            letterMove[i] = new TranslateTransform(); shineMove[i] = new TranslateTransform();
            letters[i] = new TextBlock { Text = ls[i], FontSize = 86, FontFamily = Ui.Title, FontWeight = FontWeights.Bold, Foreground = magFill, RenderTransform = letterMove[i], Margin = new Thickness(i == 0 ? 0 : -2, 0, 0, 0) };
            shineLetters[i] = new TextBlock { Text = ls[i], FontSize = 86, FontFamily = Ui.Title, FontWeight = FontWeights.Bold, Foreground = Brushes.White, RenderTransform = shineMove[i], Margin = letters[i].Margin };
            word.Children.Add(letters[i]); shineWord.Children.Add(shineLetters[i]);
        }
        magGlow = new DropShadowEffect { Color = orange, BlurRadius = 28, ShadowDepth = 0, Opacity = 0.7 };
        word.Effect = magGlow;
        shineMask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0.35) };
        for (int i = 0; i < 3; i++) shineMask.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
        shineMask.GradientStops[1].Color = Color.FromArgb(230, 255, 255, 255);
        shineWord.OpacityMask = shineMask;
        double magTop = CY - 58;
        word.Margin = new Thickness(0, magTop, 0, 0); shineWord.Margin = word.Margin;
        Children.Add(word); Children.Add(shineWord);

        // « MODDING AND GAMING » (se dévoile depuis le centre) + filets de chaque côté
        tagline = new TextBlock { Text = "M O D D I N G   A N D   G A M I N G", FontSize = 18, FontFamily = Ui.Title, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(C("#EEF1FA")),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 312, 0, 0),
            Effect = new DropShadowEffect { Color = C("#7FA8FF"), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.45 } };
        taglineClip = new RectangleGeometry(new Rect(0, 0, 0, 40));
        tagline.Clip = taglineClip;
        Children.Add(tagline);
        lineL = new Rectangle { Height = 2, Width = 0, Fill = Ui.Grad(Color.FromArgb(0, orange.R, orange.G, orange.B), orange, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, RadiusX = 1, RadiusY = 1 };
        lineR = new Rectangle { Height = 2, Width = 0, Fill = Ui.Grad(green, Color.FromArgb(0, green.R, green.G, green.B), 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, RadiusX = 1, RadiusY = 1 };
        var lines = new Grid { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 324, 0, 0), Width = W };
        lines.ColumnDefinitions.Add(new ColumnDefinition()); lines.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) }); lines.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(lineL, 0); Grid.SetColumn(lineR, 2);
        lines.Children.Add(lineL); lines.Children.Add(lineR);
        Children.Add(lines);

        // barre de chargement
        var bar = new Canvas { Width = BarW, Height = 26, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, BarY, 0, 0) };
        bar.Children.Add(new Rectangle { Width = BarW, Height = 4, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(Color.FromArgb(34, 200, 212, 255)) });
        barFill = new Rectangle { Width = 0, Height = 4, RadiusX = 2, RadiusY = 2, Fill = Ui.Grad(orange, green, 0), Effect = new DropShadowEffect { Color = orange, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.8 } };
        bar.Children.Add(barFill);
        barHead = new Ellipse { Width = 10, Height = 10, Fill = Brushes.White, Effect = new DropShadowEffect { Color = gold, BlurRadius = 14, ShadowDepth = 0, Opacity = 1 } };
        Canvas.SetTop(barHead, -3); bar.Children.Add(barHead);
        barText = new TextBlock { FontSize = 10.5, FontFamily = Ui.Title, Foreground = Ui.TextDim, Width = BarW, TextAlignment = TextAlignment.Center };
        Canvas.SetTop(barText, 10); bar.Children.Add(barText);
        Children.Add(bar);

        // crédits
        credits = new TextBlock { FontSize = 13.5, FontFamily = Ui.Title, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 20), Foreground = Ui.TextSub };
        credits.Inlines.Add(new Run("D E V ' S   B Y    "));
        credits.Inlines.Add(new Run("UrBaaZz") { Foreground = Ui.Grad(gold, orange, 0) });
        credits.Inlines.Add(new Run("   &   "));
        credits.Inlines.Add(new Run("ZERTY") { Foreground = Ui.Grad(green, green2, 0) });
        Children.Add(credits);

        // rappel France discret : ruban bleu / blanc / rouge en bas à droite (comme les injecteurs)
        ribbon = new Canvas { IsHitTestVisible = false, Width = W, Height = H };
        var rib = new Canvas { RenderTransform = new RotateTransform(-36), Effect = new BlurEffect { Radius = 1.2 } };
        string[] fc = { "#1F4FD8", "#F4F6FB", "#E8323C" };
        for (int i = 0; i < 3; i++) { var r = new Rectangle { Width = 200, Height = 5, Fill = new SolidColorBrush(C(fc[i])) }; Canvas.SetTop(r, i * 5); rib.Children.Add(r); }
        Canvas.SetLeft(rib, W - 150); Canvas.SetTop(rib, H + 14);
        ribbon.Children.Add(rib);
        var tl = new Rectangle { Width = 70, Height = 2, Fill = Ui.Grad(C("#1F4FD8"), C("#E8323C"), 0), Opacity = 0.7 };
        Canvas.SetLeft(tl, 22); Canvas.SetTop(tl, 22); ribbon.Children.Add(tl);
        Children.Add(ribbon);

        // bord en verre
        Children.Add(new Border { BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(46, 200, 212, 255)), CornerRadius = new CornerRadius(live ? 22 : 0), IsHitTestVisible = false });
        if (live) Clip = new RectangleGeometry(new Rect(0, 0, W, H), 22, 22);
    }

    static Geometry HexGeo(double r)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            for (int k = 0; k < 6; k++)
            {
                double a = (-90 + 60 * k) * Math.PI / 180;
                var p = new Point(CX + r * Math.Cos(a), CY + r * Math.Sin(a));
                if (k == 0) ctx.BeginFigure(p, true, true); else ctx.LineTo(p, true, true);
            }
        }
        g.Freeze();
        return g;
    }

    // ---------- animation : tout dépend de t (secondes) ----------
    static double Clamp(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
    static double Seg(double t, double a, double b) { return Clamp((t - a) / (b - a)); }
    static double OutCubic(double x) { x = 1 - x; return 1 - x * x * x; }
    static double OutBack(double x) { const double c1 = 1.9, c3 = c1 + 1; double y = x - 1; return 1 + c3 * y * y * y + c1 * y * y; }
    static double InOut(double x) { return x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2; }

    public void Apply(double t)
    {
        // entrée / sortie de la fenêtre
        double appear = OutCubic(Seg(t, 0, 0.35));
        double leave = live ? 1 - Seg(t, 3.15, 3.45) : 1;
        Opacity = appear * leave;
        if (live) { var s = 0.96 + 0.04 * appear + 0.03 * (1 - leave); RenderTransformOrigin = new Point(0.5, 0.5); RenderTransform = new ScaleTransform(s, s); }

        gridMove.Y = (t * 10) % 32;
        double pulse = 0.5 + 0.5 * Math.Sin(t * 2.6);
        halo.Opacity = (0.35 + 0.25 * pulse) * OutCubic(Seg(t, 0.2, 1.2));

        foreach (var p in parts)
        {
            double y = H + 10 - ((p.Phase + t * p.Speed) % (H + 40));
            double x = p.X + Math.Sin(t * 1.3 + p.Drift) * 8;
            Canvas.SetLeft(p.E, x); Canvas.SetTop(p.E, y);
            p.E.Opacity = Clamp(y / H) * 0.85 * OutCubic(Seg(t, 0, 0.8));
        }

        ringRot.Angle = t * 28; ring2Rot.Angle = -t * 16;
        double ringsIn = OutCubic(Seg(t, 0.9, 1.6));
        ring.Opacity = 0.9 * ringsIn; ring2.Opacity = 0.8 * ringsIn;

        // hexagone : le contour se dessine, puis le verre se remplit avec un petit rebond
        double draw = InOut(Seg(t, 0.15, 1.05));
        double th = hexStroke.StrokeThickness;
        hexStroke.StrokeDashArray = new DoubleCollection { hexLen / th * draw + 0.001, hexLen / th };
        hexStroke.Opacity = Seg(t, 0.15, 0.3);
        double fillIn = Seg(t, 0.75, 1.25);
        hexFill.Opacity = 0.92 * OutCubic(fillIn);
        hexInner.Opacity = OutCubic(Seg(t, 1.0, 1.4));
        double sc = t < 0.75 ? 0.9 : 0.9 + 0.1 * OutBack(fillIn);
        sc += 0.012 * Math.Sin(t * 2.6) * Seg(t, 1.6, 2.2);
        hexScale.ScaleX = hexScale.ScaleY = sc;
        ((DropShadowEffect)hexStroke.Effect).Opacity = 0.55 + 0.35 * pulse;

        // « MAG » : chaque lettre tombe avec un léger décalage
        for (int i = 0; i < 3; i++)
        {
            double a = Seg(t, 1.0 + i * 0.12, 1.45 + i * 0.12);
            letters[i].Opacity = OutCubic(a);
            letterMove[i].Y = -36 * (1 - OutBack(a));
            shineMove[i].Y = letterMove[i].Y;
        }
        magGlow.Opacity = (0.45 + 0.3 * pulse) * Seg(t, 1.2, 1.7);
        // reflet : passe à 1,75 s puis toutes les 2,6 s
        double sh = t < 1.75 ? -1 : ((t - 1.75) % 2.6) / 0.75;
        double pos = -0.3 + 1.6 * Clamp(sh);
        shineMask.GradientStops[0].Offset = pos - 0.16; shineMask.GradientStops[1].Offset = pos; shineMask.GradientStops[2].Offset = pos + 0.16;
        shineWord.Opacity = sh >= 0 && sh <= 1 ? 1 : 0;

        // tagline + filets
        double tg = OutCubic(Seg(t, 1.45, 2.05));
        tagline.Opacity = tg;
        double tw = tagline.ActualWidth > 0 ? tagline.ActualWidth : 420;
        taglineClip.Rect = new Rect(tw / 2 * (1 - tg), 0, tw * tg, 40);
        double ln = OutCubic(Seg(t, 1.7, 2.3));
        lineL.Width = 95 * ln; lineR.Width = 95 * ln;

        // barre de chargement
        double prog = InOut(Seg(t, 0.2, 3.0));
        barFill.Width = BarW * prog;
        Canvas.SetLeft(barHead, BarW * prog - 5);
        barHead.Opacity = prog > 0.01 && prog < 0.995 ? 1 : 0.0;
        barText.Text = (prog >= 0.999 ? L.T("P R Ê T", "R E A D Y") : L.T("C H A R G E M E N T", "L O A D I N G")) + "   " + (int)Math.Round(prog * 100) + " %";
        barText.Opacity = Seg(t, 0.3, 0.7);

        credits.Opacity = OutCubic(Seg(t, 1.9, 2.5));
        credits.RenderTransform = new TranslateTransform(0, 10 * (1 - OutCubic(Seg(t, 1.9, 2.5))));
        ribbon.Opacity = 0.6 * OutCubic(Seg(t, 0.4, 1.2));
    }
}
