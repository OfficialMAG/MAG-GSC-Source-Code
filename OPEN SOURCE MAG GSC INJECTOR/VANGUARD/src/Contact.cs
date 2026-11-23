// Contact et serveur Discord de UrBaaZz et Zerty : meme fichier dans tous les tools RTM, les injecteurs GSC et le launcher.
// Dev by UrBaaZz and Zerty.
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

static class Contact
{
    public const string DiscordUrl = "https://discord.gg/m7e6S2agGU";
    public const string DiscordShort = "discord.gg/m7e6S2agGU";
    // { nom, pseudo Discord, couleur }
    public static readonly string[,] People = { { "UrBaaZz", "urbaazz", "#A78BFA" }, { "Zerty", "jlzerty", "#22D3EE" } };
    public static readonly Color Blurple = (Color)ColorConverter.ConvertFromString("#5865F2"), Blurple2 = (Color)ColorConverter.ConvertFromString("#8891F7");

    // logo Discord (Simple Icons, 24 x 24), converti en chemin WPF
    const string LogoPath = "F1 M20.317,4.3698 a19.7913,19.7913 0 0 0 -4.8851,-1.5152 a.0741,.0741 0 0 0 -.0785,.0371 c-.211,.3753 -.4447,.8648 -.6083,1.2695 c-1.8447,-.2762 -3.68,-.2762 -5.4868,0 c-.1636,-.3933 -.4058,-.8742 -.6177,-1.2695 a.077,.077 0 0 0 -.0785,-.037 a19.7363,19.7363 0 0 0 -4.8852,1.515 a.0699,.0699 0 0 0 -.0321,.0277 C.5334,9.0458 -.319,13.5799 .0992,18.0578 a.0824,.0824 0 0 0 .0312,.0561 c2.0528,1.5076 4.0413,2.4228 5.9929,3.0294 a.0777,.0777 0 0 0 .0842,-.0276 c.4616,-.6304 .8731,-1.2952 1.226,-1.9942 a.076,.076 0 0 0 -.0416,-.1057 c-.6528,-.2476 -1.2743,-.5495 -1.8722,-.8923 a.077,.077 0 0 1 -.0076,-.1277 c.1258,-.0943 .2517,-.1923 .3718,-.2914 a.0743,.0743 0 0 1 .0776,-.0105 c3.9278,1.7933 8.18,1.7933 12.0614,0 a.0739,.0739 0 0 1 .0785,.0095 c.1202,.099 .246,.1981 .3728,.2924 a.077,.077 0 0 1 -.0066,.1276 a12.2986,12.2986 0 0 1 -1.873,.8914 a.0766,.0766 0 0 0 -.0407,.1067 c.3604,.698 .7719,1.3628 1.225,1.9932 a.076,.076 0 0 0 .0842,.0286 c1.961,-.6067 3.9495,-1.5219 6.0023,-3.0294 a.077,.077 0 0 0 .0313,-.0552 c.5004,-5.177 -.8382,-9.6739 -3.5485,-13.6604 a.061,.061 0 0 0 -.0312,-.0286 Z M8.02,15.3312 c-1.1825,0 -2.1569,-1.0857 -2.1569,-2.419 c0,-1.3332 .9555,-2.4189 2.157,-2.4189 c1.2108,0 2.1757,1.0952 2.1568,2.419 c0,1.3332 -.9555,2.4189 -2.1569,2.4189 Z m7.9748,0 c-1.1825,0 -2.1569,-1.0857 -2.1569,-2.419 c0,-1.3332 .9554,-2.4189 2.1569,-2.4189 c1.2108,0 2.1757,1.0952 2.1568,2.419 c0,1.3332 -.946,2.4189 -2.1568,2.4189 Z";
    static Geometry logo;
    public static Geometry Logo { get { if (logo == null) { logo = Geometry.Parse(LogoPath); logo.Freeze(); } return logo; } }

    public static FrameworkElement LogoIcon(double size, Brush fill)
    {
        var p = new Path { Data = Logo, Fill = fill, Stretch = Stretch.Uniform, Width = size, Height = size * 0.76, VerticalAlignment = VerticalAlignment.Center };
        return p;
    }

    public static void OpenDiscord(Action<string> notify)
    {
        try { Process.Start(new ProcessStartInfo(DiscordUrl) { UseShellExecute = true }); if (notify != null) notify(L.T("Ouverture du serveur Discord…", "Opening the Discord server…")); }
        catch (Exception ex) { if (notify != null) notify(L.T("Impossible d'ouvrir le lien (", "Could not open the link (") + ex.Message + L.T(") : ", "): ") + DiscordUrl); }
    }
    public static void Copy(string what, string text, Action<string> notify)
    {
        try { Clipboard.SetText(text); if (notify != null) notify(what + L.T(" copié : ", " copied: ") + text); }
        catch { if (notify != null) notify(what + " : " + text); }
    }

    static Brush B(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    static LinearGradientBrush G(Color a, Color b) { return new LinearGradientBrush(a, b, 0); }
    static TextBlock T(string text, double size, Brush fg, bool bold)
    {
        return new TextBlock { Text = text, FontSize = size, Foreground = fg, FontFamily = new FontFamily(bold ? "Bahnschrift SemiBold, Segoe UI" : "Segoe UI Variable Text, Segoe UI"), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
    }

    // Gros bouton « Rejoindre le serveur Discord » (couleurs de Discord)
    public static FlatButton JoinButton(Action<string> notify)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(LogoIcon(20, Brushes.White));
        var t = T(L.T("Rejoindre le serveur Discord", "Join the Discord server"), 14, Brushes.White, true); t.Margin = new Thickness(10, 0, 0, 1); row.Children.Add(t);
        var b = new FlatButton(row, Blurple, Blurple2, 12, new Thickness(18, 10, 20, 10)) { HorizontalAlignment = HorizontalAlignment.Left, ToolTip = DiscordUrl };
        b.Click += delegate { OpenDiscord(notify); };
        return b;
    }

    // Petite pastille cliquable pour le pied de page
    public static FlatButton FooterPill(Action<string> notify)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(LogoIcon(14, Brushes.White));
        var t = T("Discord", 12, Brushes.White, true); t.Margin = new Thickness(7, 0, 0, 1); row.Children.Add(t);
        var b = new FlatButton(row, Color.FromArgb(200, Blurple.R, Blurple.G, Blurple.B), Blurple2, 9, new Thickness(10, 4, 11, 5)) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0), ToolTip = L.T("Rejoindre le serveur Discord : ", "Join the Discord server: ") + DiscordShort };
        b.Click += delegate { OpenDiscord(notify); };
        return b;
    }

    // Lien souligne cliquable
    public static TextBlock Link(Action<string> notify)
    {
        var t = T(DiscordUrl, 13, B("#8891F7"), false);
        t.TextDecorations = TextDecorations.Underline; t.Cursor = Cursors.Hand; t.ToolTip = L.T("Ouvrir le serveur Discord", "Open the Discord server");
        t.MouseEnter += delegate { t.Foreground = Brushes.White; };
        t.MouseLeave += delegate { t.Foreground = B("#8891F7"); };
        t.MouseLeftButtonUp += delegate { OpenDiscord(notify); };
        return t;
    }

    // Contenu complet : bouton, lien, et les pseudos Discord des deux devs (bouton Copier)
    public static StackPanel Body(Action<string> notify)
    {
        var st = new StackPanel();
        st.Children.Add(JoinButton(notify));
        var l = Link(notify); l.Margin = new Thickness(2, 10, 0, 12); st.Children.Add(l);
        for (int i = 0; i < People.GetLength(0); i++)
        {
            string name = People[i, 0], user = People[i, 1]; Color col = (Color)ColorConverter.ConvertFromString(People[i, 2]);
            var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var av = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), HorizontalAlignment = HorizontalAlignment.Left, Background = G(Color.FromArgb(230, col.R, col.G, col.B), Color.FromArgb(120, Blurple.R, Blurple.G, Blurple.B)) };
            var ini = T(name.Substring(0, 1), 16, Brushes.White, true); ini.HorizontalAlignment = HorizontalAlignment.Center; av.Child = ini;
            g.Children.Add(av);
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(info, 1);
            info.Children.Add(T(name, 15, new SolidColorBrush(col), true));
            var d = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            d.Children.Add(LogoIcon(13, B("#8891F7")));
            var ut = T("Discord : " + user, 13, B("#C9CEDA"), false); ut.Margin = new Thickness(6, 0, 0, 0); d.Children.Add(ut);
            info.Children.Add(d);
            g.Children.Add(info);
            var ct = T(L.T("Copier", "Copy"), 12.5, Brushes.White, true);
            var cb = new FlatButton(ct, Color.FromArgb(30, 255, 255, 255), Color.FromArgb(60, Blurple.R, Blurple.G, Blurple.B), 9, new Thickness(12, 5, 12, 6)) { VerticalAlignment = VerticalAlignment.Center, ToolTip = L.T("Copier le pseudo Discord de " + name, "Copy " + name + "'s Discord username") };
            cb.Click += delegate { Copy(L.T("Pseudo Discord de " + name, name + "'s Discord username"), user, notify); };
            Grid.SetColumn(cb, 2); g.Children.Add(cb);
            st.Children.Add(g);
        }
        return st;
    }
}
