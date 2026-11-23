// MAG GSC INJECTOR V1 - bibliotheque visuelle : verre bleu nuit, couleur du jeu en accent (orange Black Ops / vert Modern Warfare :
// voir ToolInfo.cs), petit drapeau francais, controles animes, mise en page adaptative.
// Meme fichier dans tous les MAG GSC INJECTOR.
// Dev by UrBaaZz and Zerty.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
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
    public static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas");

    // accent = couleur du jeu (ToolInfo.cs)
    public static readonly Color Accent = C(ToolInfo.Accent), Accent2 = C(ToolInfo.Accent2), Brand = C(ToolInfo.Brand), Brand2 = C(ToolInfo.Brand2);
    public static readonly Color Danger = C("#FF5A6E"), Warn = C("#FFC83D");
    // bleu / blanc / rouge du petit drapeau (badge du menu)
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

    // ---------- drapeau et logo ----------
    // petit drapeau francais (coins arrondis, reflet)
    public static FrameworkElement MiniFlag(double w, double h)
    {
        var g = new Grid { Width = w, Height = h, Clip = new RectangleGeometry(new Rect(0, 0, w, h), h * 0.2, h * 0.2), IsHitTestVisible = false };
        Color[] c = { FlagBlue, FlagWhite, FlagRed };
        for (int i = 0; i < 3; i++) { g.ColumnDefinitions.Add(new ColumnDefinition()); var r = new Rectangle { Fill = B(c[i]) }; Grid.SetColumn(r, i); g.Children.Add(r); }
        var gloss = new Rectangle { Fill = Grad(C("#55FFFFFF"), C("#00FFFFFF"), 90) }; Grid.SetColumnSpan(gloss, 3); g.Children.Add(gloss);
        var shade = new Rectangle { Fill = Grad(C("#00000000"), C("#38000000"), 90) }; Grid.SetColumnSpan(shade, 3); g.Children.Add(shade);
        return g;
    }

    // logo MAG : hexagone dans la couleur du jeu, « M » blanc
    public static FrameworkElement Logo(double size, bool glow = false)
    {
        var g = new Grid { Width = 30, Height = 30 };
        var hex = Geometry.Parse("M15,1 L27.1,8 L27.1,22 L15,29 L2.9,22 L2.9,8 Z");
        g.Children.Add(new Path { Data = hex, Fill = Grad(Brand, Brand2, 45) });
        var gloss = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        gloss.GradientStops.Add(new GradientStop(C("#40FFFFFF"), 0)); gloss.GradientStops.Add(new GradientStop(C("#00FFFFFF"), 0.5)); gloss.GradientStops.Add(new GradientStop(C("#26000000"), 1));
        g.Children.Add(new Path { Data = hex, Fill = gloss });
        g.Children.Add(new Path { Data = hex, Stroke = B("#66FFFFFF"), StrokeThickness = 1 });
        var m = T("M", 14, Brushes.White, Title, FontWeights.SemiBold);
        m.HorizontalAlignment = HorizontalAlignment.Center; m.VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(m);
        var vb = new Viewbox { Width = size, Height = size, Child = g };
        if (glow)
        {
            // halo de la couleur du jeu qui respire doucement
            var fx = new DropShadowEffect { Color = Accent, BlurRadius = Math.Max(10, size * 0.4), ShadowDepth = 0, Opacity = 0.6 };
            vb.Effect = fx; LoopFx(fx, DropShadowEffect.OpacityProperty, 0.35, 0.8, 2.8, true);
        }
        return vb;
    }

    // ---------- boutons ----------
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

    // ---------- champs ----------
    public static TextBox Field(string text, double width = double.NaN, bool mono = false)
    {
        var t = new TextBox { Text = text ?? "", Width = width, FontFamily = mono ? Mono : Text, HorizontalAlignment = double.IsNaN(width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left };
        return t;
    }

    // carte en verre bleu nuit, trait de la couleur du jeu en haut a gauche
    // carte « Effets visuels » (voir Fx.cs) : Legers / Complets / Desactives, applique tout de suite
    public static Border FxCard()
    {
        var st = new StackPanel();
        var row = new WrapPanel();
        var chips = new System.Collections.Generic.List<Chip>();
        foreach (int v in new[] { MagFx.Legers, MagFx.Complets, MagFx.Aucun })
        {
            int k = v;
            var ch = new Chip(MagFx.Name(v, L.En) + (v == MagFx.Legers ? L.T(" (conseillé)", " (recommended)") : ""), Accent) { IsOn = MagFx.Level == v, Tag = v };
            ch.Changed += delegate { MagFx.Set(k); foreach (var c in chips) c.IsOn = (int)c.Tag == k; };
            chips.Add(ch); row.Children.Add(ch);
        }
        st.Children.Add(row);
        var t = T(MagFx.Help(L.En), 12.5, TextSub); t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 4, 0, 0);
        st.Children.Add(t);
        return Card(MagFx.Title(L.En), "", st);
    }
    public static Border Card(string title, string glyph, UIElement body, string subtitle = null)
    {
        var outer = new Border
        {
            CornerRadius = new CornerRadius(16), Background = Grad(C("#C70D1326"), C("#D6070B17"), 90), BorderBrush = Grad(C("#34FFFFFF"), C("#0FFFFFFF"), 90),
            BorderThickness = new Thickness(1), Padding = new Thickness(18, 14, 18, 16), Margin = new Thickness(8)
        };
        var st = new StackPanel();
        st.Children.Add(new Border { Height = 2, Width = 56, CornerRadius = new CornerRadius(1), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, -6, 0, 10), Background = Grad(Accent, A(Accent2, 0), 0) });
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, subtitle == null ? 10 : 2) };
        if (glyph != null)
        {
            var ic = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(9), Background = Grad(A(Accent, 48), A(Accent2, 18), 45), BorderBrush = B(A(Accent, 96)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0), Child = Icon(glyph, 14, B(Accent)) };
            ((TextBlock)ic.Child).HorizontalAlignment = HorizontalAlignment.Center;
            head.Children.Add(ic);
        }
        var tt = T(title.ToUpperInvariant(), 15, TextMain, Title, FontWeights.SemiBold); tt.VerticalAlignment = VerticalAlignment.Center; tt.Margin = new Thickness(0, 1, 0, 0);
        head.Children.Add(tt);
        st.Children.Add(head);
        if (subtitle != null) { var s = T(subtitle, 12.5, TextSub); s.TextWrapping = TextWrapping.Wrap; s.Margin = new Thickness(0, 4, 0, 10); st.Children.Add(s); }
        st.Children.Add(body);
        outer.Child = st;
        return outer;
    }

    // Ligne d'option : titre + description a gauche, controle a droite.
    public static Grid Row(string title, string desc, UIElement control)
    {
        var sl = control as Slider2;
        if (sl != null)
        {
            // curseur : titre au-dessus, curseur sur toute la largeur
            sl.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            var box = new Grid { Margin = new Thickness(0, 4, 0, 6), Background = Brushes.Transparent };
            var v = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(T(title, 14, TextMain, Text, FontWeights.SemiBold));
            if (!string.IsNullOrEmpty(desc)) { var d = T("   " + desc, 12, TextSub); d.VerticalAlignment = VerticalAlignment.Center; head.Children.Add(d); }
            v.Children.Add(head);
            sl.Margin = new Thickness(0, 2, 0, 0);
            v.Children.Add(sl);
            box.Children.Add(v);
            return box;
        }
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4), MinHeight = 40, Background = Brushes.Transparent };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        st.Children.Add(T(title, 14, TextMain, Text, FontWeights.SemiBold));
        if (!string.IsNullOrEmpty(desc)) { var d = T(desc, 12, TextSub); d.TextWrapping = TextWrapping.Wrap; st.Children.Add(d); }
        g.Children.Add(st);
        var fe = control as FrameworkElement;
        if (fe != null) fe.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        g.Children.Add(control);
        return g;
    }

    public static Rectangle Sep() { return new Rectangle { Height = 1, Fill = B("#16FFFFFF"), Margin = new Thickness(0, 6, 0, 6) }; }

    public static WrapPanel Wrap(params UIElement[] items)
    {
        var w = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var i in items) { var fe = i as FrameworkElement; if (fe != null) fe.Margin = new Thickness(0, 0, 8, 8); w.Children.Add(i); }
        return w;
    }

    public static StackPanel Labeled(string label, UIElement ctl)
    {
        var s = new StackPanel { Margin = new Thickness(0, 0, 10, 8) };
        s.Children.Add(T(label, 11.5, TextSub, Title));
        var fe = ctl as FrameworkElement; if (fe != null) fe.Margin = new Thickness(0, 4, 0, 0);
        s.Children.Add(ctl);
        return s;
    }

    // styles des controles standard ({A} = couleur d'accent du jeu, sans le #)
    public static string Styles { get { return StylesTpl.Replace("{A}", ToolInfo.Accent.Substring(1)); } }
    const string StylesTpl =
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
        " <Style TargetType='{x:Type TextBox}'>" +
        "  <Setter Property='Foreground' Value='#F2F4FA'/><Setter Property='CaretBrush' Value='#{A}'/><Setter Property='FontSize' Value='13.5'/>" +
        "  <Setter Property='SelectionBrush' Value='#66{A}'/><Setter Property='Padding' Value='9,4'/><Setter Property='MinHeight' Value='32'/>" +
        "  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TextBox}'>" +
        "   <Border x:Name='bd' CornerRadius='10' Background='#B3060A16' BorderBrush='#2AC8D4FF' BorderThickness='1'>" +
        "    <ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/></Border>" +
        "   <ControlTemplate.Triggers>" +
        "    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='#50C8D4FF'/></Trigger>" +
        "    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='#{A}'/><Setter TargetName='bd' Property='Background' Value='#E0080D1C'/></Trigger>" +
        "   </ControlTemplate.Triggers>" +
        "  </ControlTemplate></Setter.Value></Setter>" +
        " </Style>" +
        " <Style TargetType='{x:Type ListBox}'>" +
        "  <Setter Property='Background' Value='#0A0E1A'/><Setter Property='BorderBrush' Value='#24C8D4FF'/><Setter Property='BorderThickness' Value='1'/>" +
        "  <Setter Property='Foreground' Value='#F2F4FA'/><Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/>" +
        "  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ListBox}'>" +
        "   <Border CornerRadius='12' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='4'>" +
        "    <ScrollViewer Focusable='False'><ItemsPresenter/></ScrollViewer></Border>" +
        "  </ControlTemplate></Setter.Value></Setter>" +
        " </Style>" +
        " <Style TargetType='{x:Type ListBoxItem}'>" +
        "  <Setter Property='Foreground' Value='#DDE3F0'/><Setter Property='Padding' Value='10,6'/><Setter Property='Cursor' Value='Hand'/>" +
        "  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ListBoxItem}'>" +
        "   <Border x:Name='bd' CornerRadius='8' Background='Transparent' Padding='{TemplateBinding Padding}' Margin='0,1'><ContentPresenter/></Border>" +
        "   <ControlTemplate.Triggers>" +
        "    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Background' Value='#16FFFFFF'/></Trigger>" +
        "    <Trigger Property='IsSelected' Value='True'><Setter TargetName='bd' Property='Background' Value='#38{A}'/><Setter Property='Foreground' Value='White'/></Trigger>" +
        "   </ControlTemplate.Triggers>" +
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

// Nom du tool, credits et en-tete avec le logo (Accueil, A propos, pied de page).
static class Brand
{
    // « Dev by UrBaaZz and Zerty » : les deux noms dans la couleur du jeu
    public static TextBlock Credit(double size)
    {
        var cr = new TextBlock { FontFamily = Ui.Title, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        cr.Inlines.Add(new Run("Dev by ") { Foreground = Ui.TextSub });
        cr.Inlines.Add(new Run("UrBaaZz") { Foreground = Ui.B(Ui.Brand), FontWeight = FontWeights.SemiBold });
        cr.Inlines.Add(new Run(" and ") { Foreground = Ui.TextSub });
        cr.Inlines.Add(new Run("Zerty") { Foreground = Ui.B(Ui.Accent), FontWeight = FontWeights.SemiBold });
        return cr;
    }
    // « MAG GSC INJECTOR » en blanc argente + nom du jeu dans sa couleur
    public static TextBlock Name(double size)
    {
        var t = new TextBlock { FontFamily = Ui.Title, FontSize = size, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        t.Inlines.Add(new Run("MAG GSC INJECTOR ") { Foreground = Ui.Grad(Ui.C("#FFFFFF"), Ui.C("#B9C6E8"), 90) });
        t.Inlines.Add(new Run(ToolInfo.Game) { Foreground = Ui.Grad(Ui.Brand, Ui.Accent, 0) });
        t.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.55 };
        return t;
    }
    // texte a gauche, grand logo hexagonal a droite
    public static UIElement Hero(UIElement text, double logo)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(text);
        var lg = Ui.Logo(logo, true); lg.VerticalAlignment = VerticalAlignment.Center; lg.Margin = new Thickness(16, 0, 6, 0);
        Grid.SetColumn(lg, 1); g.Children.Add(lg);
        return g;
    }
    public static UIElement ContactCard(MainWindow w)
    {
        return Ui.Card("Contact & Discord", "", Contact.Body(delegate (string m) { w.Ok(m); }), L.T("Une question, un bug, une idée ? Rejoins le serveur Discord ou ajoute UrBaaZz et Zerty sur Discord.", "A question, a bug, an idea? Join the Discord server or add UrBaaZz and Zerty on Discord."));
    }
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

// Curseur maison (glisser ou cliquer), valeur affichee a droite.
class Slider2 : Grid
{
    public event Action<double> Committed;
    public double Min, Max, Step;
    double val;
    readonly string fmt;
    readonly Border fill, thumb;
    readonly Grid trackHost;
    readonly TextBlock label;
    public double Value { get { return val; } set { val = Clamp(value); Paint(); } }
    public Slider2(double min, double max, double value, double step, string format, double width = 200)
    {
        Min = min; Max = max; Step = step; fmt = format;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        trackHost = new Grid { Height = 26, Background = Brushes.Transparent, Cursor = Cursors.Hand };
        trackHost.Children.Add(new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = Ui.B("#20FFFFFF") });
        fill = new Border { Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Background = Ui.Grad(Ui.Accent2, Ui.Accent, 0) };
        trackHost.Children.Add(fill);
        thumb = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, Effect = new DropShadowEffect { Color = Ui.Accent, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.8 } };
        trackHost.Children.Add(thumb);
        Children.Add(trackHost);
        label = Ui.T("", 13, Ui.TextMain, Ui.Mono); label.HorizontalAlignment = HorizontalAlignment.Right; label.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(label, 1); Children.Add(label);
        val = Clamp(value);
        trackHost.SizeChanged += delegate { Paint(); };
        trackHost.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e) { trackHost.CaptureMouse(); FromMouse(e.GetPosition(trackHost).X); e.Handled = true; };
        trackHost.MouseMove += delegate (object s, MouseEventArgs e) { if (trackHost.IsMouseCaptured) FromMouse(e.GetPosition(trackHost).X); };
        trackHost.MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e) { if (trackHost.IsMouseCaptured) { trackHost.ReleaseMouseCapture(); if (Committed != null) Committed(val); } };
        Paint();
    }
    double Clamp(double v) { v = Math.Max(Min, Math.Min(Max, v)); if (Step > 0) v = Math.Round((v - Min) / Step) * Step + Min; return Math.Round(v, 4); }
    void FromMouse(double x) { double w = Math.Max(1, trackHost.ActualWidth - 18); val = Clamp(Min + (Max - Min) * Math.Max(0, Math.Min(1, (x - 9) / w))); Paint(); }
    void Paint()
    {
        double w = Math.Max(1, trackHost.ActualWidth - 18), p = Max > Min ? (val - Min) / (Max - Min) : 0;
        thumb.Margin = new Thickness(p * w, 0, 0, 0);
        fill.Width = p * w + 9;
        label.Text = val.ToString(fmt, CultureInfo.InvariantCulture);
    }
}

// Pastille cliquable (choix multiple, ex. perks).
class Chip : Border
{
    public event Action<bool> Changed;
    bool on; readonly TextBlock t; readonly Color tint;
    public bool IsOn { get { return on; } set { on = value; Paint(); } }
    public Chip(string text, Color tint)
    {
        this.tint = tint;
        t = Ui.T(text, 12, Ui.TextMain, Ui.Text, FontWeights.SemiBold);
        Child = t; CornerRadius = new CornerRadius(10); Padding = new Thickness(10, 5, 10, 6); BorderThickness = new Thickness(1); Cursor = Cursors.Hand;
        Margin = new Thickness(0, 0, 6, 6);
        MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e) { e.Handled = true; on = !on; Paint(); if (Changed != null) Changed(on); };
        Paint();
    }
    void Paint()
    {
        Background = on ? Ui.B(Ui.A(tint, 60)) : Ui.B("#0EFFFFFF");
        BorderBrush = on ? Ui.B(Ui.A(tint, 200)) : Ui.B("#20FFFFFF");
        t.Foreground = on ? Brushes.White : Ui.TextSub;
    }
}

// Selection d'une valeur dans une longue liste (enum : armes, perks, titres...).
class Picker : StackPanel
{
    public event Action<int, string> Picked;
    readonly TextBox filter; readonly ListBox list; string[] items = new string[0];
    public Picker(double height)
    {
        filter = new TextBox { Margin = new Thickness(0, 0, 0, 6), ToolTip = "Filtrer" };
        list = new ListBox { Height = height };
        Children.Add(filter); Children.Add(list);
        filter.TextChanged += delegate { Fill(); };
        list.MouseDoubleClick += delegate { Fire(); };
        list.KeyDown += delegate (object s, KeyEventArgs e) { if (e.Key == Key.Enter) Fire(); };
    }
    public void SetItems(string[] names) { items = names ?? new string[0]; Fill(); }
    public int SelectedIndex { get { var it = list.SelectedItem as ListBoxItem; return it == null ? -1 : (int)it.Tag; } }
    public string SelectedText { get { var it = list.SelectedItem as ListBoxItem; return it == null ? null : (string)it.Content; } }
    public void Fire() { int i = SelectedIndex; if (i >= 0 && Picked != null) Picked(i, items[i]); }
    void Fill()
    {
        string f = filter.Text.Trim().ToLowerInvariant();
        list.Items.Clear(); int n = 0;
        for (int i = 0; i < items.Length && n < 1500; i++)
        {
            var s = items[i]; if (string.IsNullOrEmpty(s)) continue;
            if (f.Length > 0 && !s.ToLowerInvariant().Contains(f)) continue;
            list.Items.Add(new ListBoxItem { Content = s, Tag = i }); n++;
        }
    }
}

// Disposition en colonnes facon "masonry" : 1, 2 ou 3 colonnes selon la largeur disponible.
class CardGrid : Panel
{
    public double ColumnMin = 430;
    public int MaxColumns = 3;
    int Cols(double w) { return Math.Max(1, Math.Min(MaxColumns, (int)(w / ColumnMin))); }
    protected override Size MeasureOverride(Size avail)
    {
        double w = double.IsInfinity(avail.Width) ? 900 : avail.Width;
        int cols = Cols(w); double cw = w / cols;
        var h = new double[cols];
        foreach (UIElement c in InternalChildren)
        {
            c.Measure(new Size(cw, double.PositiveInfinity));
            int k = 0; for (int i = 1; i < cols; i++) if (h[i] < h[k] - 0.5) k = i;
            h[k] += c.DesiredSize.Height;
        }
        double max = 0; foreach (var x in h) max = Math.Max(max, x);
        return new Size(w, max);
    }
    protected override Size ArrangeOverride(Size final)
    {
        int cols = Cols(final.Width); double cw = final.Width / cols;
        var h = new double[cols];
        foreach (UIElement c in InternalChildren)
        {
            int k = 0; for (int i = 1; i < cols; i++) if (h[i] < h[k] - 0.5) k = i;
            c.Arrange(new Rect(k * cw, h[k], cw, c.DesiredSize.Height));
            h[k] += c.DesiredSize.Height;
        }
        return final;
    }
}
