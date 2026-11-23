// MagFx : effets visuels economes en GPU (fichier commun, IDENTIQUE dans tous les tools et launchers MAG).
//
// Pourquoi : une animation infinie (halos de fond qui derivent, titre qui pulse, particules, reflet...) oblige WPF
// a recomposer la fenetre a chaque image, 60 fois par seconde, meme quand le tool est cache derriere une autre
// fenetre. Avec de grands degrades et des DropShadowEffect, ca fait tourner le GPU en continu.
//
// Ce que fait MagFx :
//  - toutes les animations en boucle passent par MagFx.Loop (Ui.Loop / Ui.LoopFx) et sont enregistrees ici ;
//  - 3 niveaux, reglage commun a tous les tools MAG (RTM et GSC) : %APPDATA%\MAG_TOOL\effets.txt
//      2 = Complets  : tout est anime (limite a 30 images/s) ;
//      1 = Legers    : (par defaut) decor fixe, seuls les petits voyants clignotent (15 images/s) ;
//      0 = Desactives: aucune animation, transitions instantanees ;
//  - boucles coupees quand la fenetre est reduite ou n'est pas au premier plan, relancees au retour ;
//  - un changement de niveau s'applique tout de suite (MagFx.Set), sans redemarrer.
// Variable d'environnement MAG_FX_FILE = autre fichier (tests).
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;

static class MagFx
{
    public const int Aucun = 0, Legers = 1, Complets = 2;
    static int level = -1;
    static bool paused, capture;
    public static event Action Changed;

    class Entry
    {
        public IAnimatable T; public DependencyProperty P;
        public double From, To, Sec, Begin; public bool Rev, Heavy;
        public AnimationClock C;
    }
    static readonly List<Entry> loops = new List<Entry>();

    static string FilePath
    {
        get
        {
            string f = Environment.GetEnvironmentVariable("MAG_FX_FILE");
            if (!string.IsNullOrEmpty(f)) return f;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MAG_TOOL", "effets.txt");
        }
    }

    // niveau actuel (lu une fois dans effets.txt ; absent ou illisible = Legers)
    public static int Level
    {
        get
        {
            if (level < 0)
            {
                level = Legers;
                try
                {
                    string p = FilePath;
                    if (File.Exists(p))
                    {
                        string s = File.ReadAllText(p).Trim().ToLowerInvariant();
                        if (s.StartsWith("2") || s.StartsWith("complet") || s.StartsWith("full")) level = Complets;
                        else if (s.StartsWith("0") || s.StartsWith("aucun") || s.StartsWith("off") || s.StartsWith("desactiv") || s.StartsWith("désactiv")) level = Aucun;
                    }
                }
                catch { }
            }
            return level;
        }
    }
    // gros effets decoratifs (fond anime, particules, halos qui pulsent)
    public static bool Heavy { get { return Level >= Complets; } }
    // transitions courtes (survol, ouverture) et petits voyants
    public static bool Anims { get { return Level >= Legers; } }

    public static void Set(int v)
    {
        if (v < Aucun) v = Aucun; if (v > Complets) v = Complets;
        level = v;
        try { string p = FilePath; Directory.CreateDirectory(Path.GetDirectoryName(p)); File.WriteAllText(p, v.ToString()); } catch { }
        foreach (var e in loops) Apply(e);
        if (Changed != null) Changed();
    }

    // animation en boucle ; heavy = decor (coupe en Legers), sinon petit voyant (coupe seulement en Desactives)
    public static void Loop(IAnimatable target, DependencyProperty prop, double from, double to, double seconds, bool reverse, double beginSeconds, bool heavy)
    {
        Stop(target, prop);
        var e = new Entry { T = target, P = prop, From = from, To = to, Sec = seconds, Rev = reverse, Begin = beginSeconds, Heavy = heavy };
        loops.Add(e);
        Apply(e);
    }

    public static void Stop(IAnimatable target, DependencyProperty prop)
    {
        for (int i = loops.Count - 1; i >= 0; i--)
        {
            var e = loops[i];
            if (ReferenceEquals(e.T, target) && e.P == prop) { Detach(e); loops.RemoveAt(i); }
        }
        target.BeginAnimation(prop, null);
    }

    // a appeler avant de reconstruire toute l'interface (changement de langue...)
    public static void StopAll()
    {
        foreach (var e in loops) Detach(e);
        loops.Clear();
    }

    public static int Running
    {
        get { int n = 0; foreach (var e in loops) if (e.C != null) n++; return n; }
    }

    static void Detach(Entry e)
    {
        if (e.C == null) return;
        try { e.C.Controller.Stop(); } catch { }
        e.T.ApplyAnimationClock(e.P, null);
        e.C = null;
    }

    static void Apply(Entry e)
    {
        Detach(e);
        bool on = !paused && (e.Heavy ? Level >= Complets : Level >= Legers);
        if (!on) return;
        var a = new DoubleAnimation(e.From, e.To, TimeSpan.FromSeconds(e.Sec))
        {
            AutoReverse = e.Rev, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(e.Begin),
            EasingFunction = e.Rev ? new SineEase { EasingMode = EasingMode.EaseInOut } : null
        };
        Timeline.SetDesiredFrameRate(a, Level >= Complets ? 30 : 15);
        e.C = a.CreateClock();
        e.T.ApplyAnimationClock(e.P, e.C);
    }

    static void Pause(bool p)
    {
        if (capture) p = false;
        if (p == paused) return;
        paused = p;
        foreach (var e in loops) Apply(e);
    }

    // fenetre principale : boucles coupees quand elle est reduite ou en arriere-plan
    public static void Attach(Window w)
    {
        Action upd = delegate { var lg = Environment.GetEnvironmentVariable("MAG_FX_LOG"); if (!string.IsNullOrEmpty(lg)) File.AppendAllText(lg, DateTime.Now.ToString("HH:mm:ss.fff") + " state=" + w.WindowState + " active=" + w.IsActive + " visible=" + w.IsVisible + " loops=" + loops.Count + Environment.NewLine); Pause(w.WindowState == WindowState.Minimized || !w.IsActive || !w.IsVisible); };
        w.Activated += delegate { upd(); };
        w.Deactivated += delegate { upd(); };
        w.StateChanged += delegate { upd(); };
        w.IsVisibleChanged += delegate { upd(); };
    }

    // pour les fenetres de capture (--capture) : jamais en pause, sinon l'image serait figee au mauvais moment
    public static void NoPause() { capture = true; Pause(false); }

    // textes du reglage (FR / EN)
    public static string Name(int v, bool en)
    {
        if (v >= Complets) return en ? "Full" : "Complets";
        if (v == Legers) return en ? "Light" : "Légers";
        return en ? "Off" : "Désactivés";
    }
    public static string Title(bool en) { return en ? "Visual effects" : "Effets visuels"; }
    public static string Help(bool en)
    {
        return en
            ? "Light (recommended) = fixed background, very low GPU use. Full = animated background. Off = no animation at all. Animations always stop when the window is minimized or in the background. Shared by all MAG tools."
            : "Légers (conseillé) = décor fixe, presque pas de GPU. Complets = fond animé. Désactivés = aucune animation. Les animations s'arrêtent toujours quand la fenêtre est réduite ou en arrière-plan. Réglage commun à tous les tools MAG.";
    }
}
