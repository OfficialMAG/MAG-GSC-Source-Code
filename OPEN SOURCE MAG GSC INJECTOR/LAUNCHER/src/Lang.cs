// MULTI COD MAG INJECTOR GSC - langue de l'interface (francais / anglais) et drapeaux de la barre du haut (meme fichier que les injecteurs).
// Dev by UrBaaZz and Zerty.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

static class L
{
    public static bool En;
    // texte dans la langue choisie : L.T("francais", "english")
    public static string T(string fr, string en) { return En ? en : fr; }

    // drapeaux dessines (pas d'image) : 27 x 18, coins arrondis
    public static FrameworkElement FlagFr()
    {
        var g = new Grid();
        string[] cols = { "#0055A4", "#FFFFFF", "#EF4135" };
        for (int i = 0; i < 3; i++) { g.ColumnDefinitions.Add(new ColumnDefinition()); var r = new Rectangle { Fill = Ui.B(cols[i]) }; Grid.SetColumn(r, i); g.Children.Add(r); }
        return Frame(g);
    }

    public static FrameworkElement FlagEn()
    {
        // Union Jack en 60 x 30 (comme le SVG officiel), etire dans le cadre
        var c = new Canvas { Width = 60, Height = 30, ClipToBounds = true };
        c.Children.Add(new Rectangle { Width = 60, Height = 30, Fill = Ui.B("#012169") });
        var diag = Geometry.Parse("M0,0 L60,30 M60,0 L0,30");
        c.Children.Add(new Path { Data = diag, Stroke = Brushes.White, StrokeThickness = 6 });
        c.Children.Add(new Path { Data = diag, Stroke = Ui.B("#C8102E"), StrokeThickness = 4, Clip = Geometry.Parse("M30,15 L60,15 L60,30 Z M30,15 L30,30 L0,30 Z M30,15 L0,15 L0,0 Z M30,15 L30,0 L60,0 Z") });
        var cross = Geometry.Parse("M30,0 L30,30 M0,15 L60,15");
        c.Children.Add(new Path { Data = cross, Stroke = Brushes.White, StrokeThickness = 10 });
        c.Children.Add(new Path { Data = cross, Stroke = Ui.B("#C8102E"), StrokeThickness = 6 });
        return Frame(new Viewbox { Stretch = Stretch.Fill, Child = c });
    }

    static FrameworkElement Frame(UIElement flag)
    {
        var host = new Grid { Width = 27, Height = 18, Clip = new RectangleGeometry(new Rect(0, 0, 27, 18), 3.5, 3.5) };
        host.Children.Add(flag);
        // leger reflet pour le relief
        host.Children.Add(new Rectangle { Fill = Ui.Grad(Ui.C("#28FFFFFF"), Ui.C("#00FFFFFF"), 90), IsHitTestVisible = false });
        return host;
    }
}
