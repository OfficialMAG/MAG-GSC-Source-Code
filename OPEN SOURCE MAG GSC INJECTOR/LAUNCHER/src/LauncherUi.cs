// MULTI COD MAG INJECTOR GSC - bibliotheque visuelle du launcher : meme theme que les MAG GSC INJECTOR (verre bleu nuit,
// ruban bleu / blanc / rouge discret), orange des Black Ops + vert des autres opus, controles animes.
// Dev by UrBaaZz and Zerty.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

static class Ui
{
    public static readonly FontFamily Title = new FontFamily("Bahnschrift SemiBold, Bahnschrift, Segoe UI");
    public static readonly FontFamily TitleLight = new FontFamily("Bahnschrift Light, Bahnschrift, Segoe UI");
    public static readonly FontFamily Text = new FontFamily("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily Icons = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");

    // orange Black Ops (couleur principale du launcher) et vert des autres opus
    public static readonly Color Accent = C("#FF7A1A"), Accent2 = C("#FF3D2E"), Brand = C("#FFC65C"), Brand2 = C("#FF6A1A");
    public static readonly Color Green = C("#9BCB3C"), Green2 = C("#3FA35A");
    public static readonly Color Danger = C("#FF5A6E"), Warn = C("#FFC83D"), Online = C("#3CF08A");
    public static readonly Color FlagBlue = C("#1F4FD8"), FlagWhite = C("#F4F6FB"), FlagRed = C("#E8323C");
    public static readonly Brush TextMain = B("#F2F4FA"), TextSub = B("#9BA4BA"), TextDim = B("#5F6880");

    public static Dispatcher Disp;
    public static void Run(Action a) { if (Disp == null || Disp.CheckAccess()) a(); else Disp.BeginInvoke(a); }

    public static Color C(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
    public static Color A(Color c, byte a) { return Color.FromArgb(a, c.R, c.G, c.B); }
    public static SolidColorBrush B(string hex) { var b = new SolidColorBrush(C(hex)); b.Freeze(); return b; }
    public static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    public static LinearGradientBrush Grad(Color a, Color b, double angle)
    {
        double r = angle * Math.PI / 180;
        return new LinearGradientBrush(a, b, new Point(0.5 - Math.Cos(r) / 2, 0.5 - Math.Sin(r) / 2), new Point(0.5 + Math.Cos(r) / 2, 0.5 + Math.Sin(r) / 2));
    }
    public static string Spaced(string s) { return string.Join(" ", s.ToCharArray()); }

    public static void Anim(IAnimatable target, DependencyProperty prop, double to, double ms, IEasingFunction ease = null, double beginMs = 0)
    {
        if (!MagFx.Anims) { ms = 0; beginMs = 0; }
        var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease ?? new CubicEase { EasingMode = EasingMode.EaseOut }, BeginTime = TimeSpan.FromMilliseconds(beginMs) };
        target.BeginAnimation(prop, a, HandoffBehavior.SnapshotAndReplace);
    }
    public static void Loop(IAnimatable target, DependencyProperty prop, double from, double to, double seconds, bool reverse, double beginSeconds = 0)
    {
        // petit effet (voyant qui clignote...) : coupe seulement si les effets sont desactives (voir Fx.cs)
        MagFx.Loop(target, prop, from, to, seconds, reverse, beginSeconds, false);
    }
    public static void LoopFx(IAnimatable target, DependencyProperty prop, double from, double to, double seconds, bool reverse, double beginSeconds = 0)
    {
        // gros effet decoratif (fond anime, halo qui pulse, reflet...) : seulement en effets « Complets »
        MagFx.Loop(target, prop, from, to, seconds, reverse, beginSeconds, true);
    }
    public static void StopLoop(IAnimatable target, DependencyProperty prop) { MagFx.Stop(target, prop); }
    public static void ColorTo(SolidColorBrush b, Color c, double ms) { b.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(c, TimeSpan.FromMilliseconds(ms))); }

    public static TextBlock T(string text, double size, Brush fg, FontFamily font = null, FontWeight? weight = null)
    {
        return new TextBlock { Text = text, FontSize = size, Foreground = fg, FontFamily = font ?? Text, FontWeight = weight ?? FontWeights.Normal };
    }
    public static TextBlock Icon(string glyph, double size, Brush fg)
    {
        return new TextBlock { Text = glyph, FontSize = size, Foreground = fg, FontFamily = Icons, VerticalAlignment = VerticalAlignment.Center };
    }

    // petit drapeau francais (coins arrondis, reflet)
    public static FrameworkElement MiniFlag(double w, double h)
    {
        var g = new Grid { Width = w, Height = h, Clip = new RectangleGeometry(new Rect(0, 0, w, h), h * 0.2, h * 0.2), IsHitTestVisible = false };
        Color[] c = { FlagBlue, FlagWhite, FlagRed };
        for (int i = 0; i < 3; i++) { g.ColumnDefinitions.Add(new ColumnDefinition()); var r = new Rectangle { Fill = B(c[i]) }; Grid.SetColumn(r, i); g.Children.Add(r); }
        var gloss = new Rectangle { Fill = Grad(C("#55FFFFFF"), C("#00FFFFFF"), 90) }; Grid.SetColumnSpan(gloss, 3); g.Children.Add(gloss);
        return g;
    }

    public static readonly Geometry Hex = Geometry.Parse("M15,1 L27.1,8 L27.1,22 L15,29 L2.9,22 L2.9,8 Z");

    // logo MAG du launcher : hexagone degrade orange (Black Ops) -> vert (autres opus), « M » blanc
    public static FrameworkElement Logo(double size, bool glow = false)
    {
        var g = new Grid { Width = 30, Height = 30 };
        var fill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        fill.GradientStops.Add(new GradientStop(Brand, 0)); fill.GradientStops.Add(new GradientStop(Accent, 0.48)); fill.GradientStops.Add(new GradientStop(Green2, 1));
        g.Children.Add(new Path { Data = Hex, Fill = fill });
        var gloss = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        gloss.GradientStops.Add(new GradientStop(C("#40FFFFFF"), 0)); gloss.GradientStops.Add(new GradientStop(C("#00FFFFFF"), 0.5)); gloss.GradientStops.Add(new GradientStop(C("#26000000"), 1));
        g.Children.Add(new Path { Data = Hex, Fill = gloss });
        g.Children.Add(new Path { Data = Hex, Stroke = B("#66FFFFFF"), StrokeThickness = 1 });
        var m = T("M", 14, Brushes.White, Title, FontWeights.SemiBold);
        m.HorizontalAlignment = HorizontalAlignment.Center; m.VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(m);
        var vb = new Viewbox { Width = size, Height = size, Child = g };
        if (glow)
        {
            var fx = new DropShadowEffect { Color = Accent, BlurRadius = Math.Max(10, size * 0.4), ShadowDepth = 0, Opacity = 0.6 };
            vb.Effect = fx; LoopFx(fx, DropShadowEffect.OpacityProperty, 0.35, 0.8, 2.8, true);
        }
        return vb;
    }

    // bouton principal degrade
    public static FlatButton Primary(string text, string glyph, Action click, Color? a1 = null, Color? a2 = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        if (glyph != null) { var ic = Icon(glyph, 13, Brushes.White); ic.Margin = new Thickness(0, 0, 8, 0); row.Children.Add(ic); }
        row.Children.Add(T(text, 13.5, Brushes.White, Title, FontWeights.SemiBold));
        var b = new FlatButton(row, Colors.Transparent, A(Colors.White, 34), 12, new Thickness(16, 9, 16, 10));
        b.Underlay = Grad(a1 ?? Accent, a2 ?? Accent2, 0);
        b.Effect = new DropShadowEffect { Color = a1 ?? Accent, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.38 };
        b.Click += click;
        return b;
    }
    // bouton secondaire en verre
    public static FlatButton Ghost(string text, string glyph, Action click, Color? tint = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        if (glyph != null) { var ic = Icon(glyph, 12.5, TextMain); ic.Margin = new Thickness(0, 0, 8, 0); row.Children.Add(ic); }
        row.Children.Add(T(text, 13, TextMain, Text, FontWeights.SemiBold));
        var c = tint ?? C("#C8D4FF");
        var b = new FlatButton(row, A(c, 16), A(c, 42), 12, new Thickness(14, 8, 14, 9));
        b.BorderBrush = B(A(c, 40)); b.BorderThickness = new Thickness(1);
        b.Click += click;
        return b;
    }

    // barres de defilement fines (theme bleu nuit)
    public const string Styles =
        "<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +
        " <Style TargetType='{x:Type ScrollBar}'>" +
        "  <Setter Property='Width' Value='8'/><Setter Property='MinWidth' Value='8'/>" +
        "  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'>" +
        "   <Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='{TemplateBinding Orientation}'>" +
        "    <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'>" +
        "     <Border x:Name='b' CornerRadius='4' Background='#40C8D4FF' Margin='1,2'/>" +
        "     <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#80C8D4FF'/></Trigger></ControlTemplate.Triggers>" +
        "    </ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid>" +
        "  </ControlTemplate></Setter.Value></Setter>" +
        " </Style>" +
        " <Style TargetType='{x:Type ToolTip}'>" +
        "  <Setter Property='Foreground' Value='#F2F4FA'/>" +
        "  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ToolTip}'>" +
        "   <Border CornerRadius='8' Background='#F20C1224' BorderBrush='#34C8D4FF' BorderThickness='1' Padding='10,6'><ContentPresenter/></Border>" +
        "  </ControlTemplate></Setter.Value></Setter>" +
        " </Style>" +
        "</ResourceDictionary>";
}

// Bouton plat anime (fond qui s'eclaire, petit rebond au clic).
class FlatButton : Border
{
    public event Action Click;
    readonly SolidColorBrush bg;
    Color normal, hover;
    readonly Border under;
    public Brush Underlay { set { under.Background = value; } }
    public bool Enabled = true;
    public FlatButton(UIElement content, Color normal, Color hover, double radius, Thickness padding)
    {
        this.normal = normal; this.hover = hover;
        var g = new Grid();
        under = new Border { CornerRadius = new CornerRadius(radius) };
        g.Children.Add(under);
        bg = new SolidColorBrush(normal);
        var face = new Border { CornerRadius = new CornerRadius(radius), Background = bg, Padding = padding, Child = content };
        g.Children.Add(face);
        Child = g; CornerRadius = new CornerRadius(radius); Cursor = Cursors.Hand;
        RenderTransformOrigin = new Point(0.5, 0.5); RenderTransform = new ScaleTransform(1, 1);
        MouseEnter += delegate { if (Enabled) Ui.ColorTo(bg, this.hover, 150); };
        MouseLeave += delegate { Ui.ColorTo(bg, this.normal, 220); Press(false); };
        MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e) { if (Enabled) Press(true); e.Handled = true; };
        MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e) { Press(false); e.Handled = true; if (Enabled && IsMouseOver && Click != null) Click(); };
    }
    public void SetColors(Color n, Color h) { normal = n; hover = h; Ui.ColorTo(bg, IsMouseOver ? h : n, 150); }
    void Press(bool down)
    {
        var st = (ScaleTransform)RenderTransform;
        Ui.Anim(st, ScaleTransform.ScaleXProperty, down ? 0.95 : 1, 110);
        Ui.Anim(st, ScaleTransform.ScaleYProperty, down ? 0.95 : 1, 110);
    }
}

// Interrupteur anime.
class Toggle : Border
{
    public event Action<bool> Changed;
    readonly TranslateTransform knobPos = new TranslateTransform(0, 0);
    readonly SolidColorBrush track = new SolidColorBrush(Ui.C("#22FFFFFF"));
    readonly Ellipse knob;
    readonly DropShadowEffect glow;
    bool on;
    public bool IsOn { get { return on; } set { Set(value, false); } }
    public Toggle()
    {
        Width = 46; Height = 26; CornerRadius = new CornerRadius(13); Background = track; Cursor = Cursors.Hand;
        BorderBrush = Ui.B("#30FFFFFF"); BorderThickness = new Thickness(1);
        knob = new Ellipse { Width = 18, Height = 18, Fill = Ui.B("#C9CEDA"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(3, 0, 0, 0), RenderTransform = knobPos };
        glow = new DropShadowEffect { Color = Ui.Accent, BlurRadius = 16, ShadowDepth = 0, Opacity = 0 };
        knob.Effect = glow;
        Child = knob;
        MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e) { e.Handled = true; Set(!on, true); };
    }
    public void Set(bool v, bool notify)
    {
        on = v;
        Ui.Anim(knobPos, TranslateTransform.XProperty, on ? 20 : 0, 220, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 });
        Ui.ColorTo(track, on ? Ui.Accent : Ui.C("#22FFFFFF"), 200);
        knob.Fill = on ? Brushes.White : Ui.B("#C9CEDA");
        Ui.Anim(glow, DropShadowEffect.OpacityProperty, on ? 0.9 : 0, 220);
        BorderBrush = on ? Ui.B(Ui.A(Ui.Accent, 200)) : Ui.B("#30FFFFFF");
        if (notify && Changed != null) Changed(on);
    }
}
