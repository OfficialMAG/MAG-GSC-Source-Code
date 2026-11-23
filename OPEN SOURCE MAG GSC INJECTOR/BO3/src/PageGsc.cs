// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;

class PageGsc : Page
{
    TextBlock compileInfo, stateInfo, modeInfo, logBox, followInfo;
    TextBox projectBox, outBox, gsccBox;
    ScrollViewer logScroll;
    FlatButton modeAuto, modeMp, modeZm, modeCp, scrMenu, scrFile, cancelBtn;
    StackPanel fileRow;
    readonly Injector inj;

    static bool cfgRead, useFile;
    static int mode = Injector.ModeAuto;
    static volatile bool working;
    static Injector current, follower;
    static System.Threading.Timer followTimer;
    static volatile bool followBusy;
    static string lastFollow = "";
    static PageGsc shown;

    public PageGsc(MainWindow w) : base(w, L.T("Injecteur GSC", "GSC Injector"), "", "")
    {
        inj = new Injector(Core);
        if (follower == null) follower = new Injector(Core);
        shown = this;
        Grid2.MaxColumns = 2;
        if (!cfgRead)
        {
            cfgRead = true;
            string m = W.CfgGet("mode", "auto");
            mode = m == "zm" ? Injector.ModeZm : m == "mp" ? Injector.ModeMp : m == "cp" ? Injector.ModeCp : Injector.ModeAuto;
            useFile = W.CfgGet("script", "menu") == "file";
        }

        var tuto = new StackPanel();
        string[] steps = {
            L.T("Sur la PS4 : active GoldHEN et son chargeur bin, puis lance Black Ops III (mise à jour 1.33).",
                "On the PS4: enable GoldHEN and its bin loader, then start Black Ops III (update 1.33)."),
            L.T("Page Accueil : entre l'IP de ta PS4, clique sur « Envoyer ps4debug », attends la notification, puis « Connecter ».",
                "Home page: enter your PS4's IP, click \"Send ps4debug\", wait for the notification, then \"Connect\"."),
            L.T("Attends le menu principal du jeu, puis clique sur « Injecter » : les MAG Menu Multijoueur, Zombies ET Campagne sont injectés d'un coup.",
                "Wait for the game's main menu, then click \"Inject\": the Multiplayer, Zombies AND Campaign MAG Menus are injected at once."),
            L.T("Mode « Automatique » (conseillé) : l'outil met tout seul le bon menu selon le mode du jeu. Laisse l'outil ouvert si tu changes de mode.",
                "\"Automatic\" mode (recommended): the tool picks the right menu for the game mode by itself. Keep the tool open if you switch modes."),
            L.T("Lance une partie dont tu es l'hôte (partie personnalisée en multijoueur, solo ou privée en Zombies, mission en campagne) : le menu se charge avec la carte.",
                "Start a match you host (custom game in Multiplayer, solo or private game in Zombies, a mission in Campaign): the menu loads with the map."),
            L.T("MAG Menu en jeu : R1 + Carré pour ouvrir, L2 / R2 (ou croix haut / bas) pour naviguer, croix gauche / droite pour régler, Carré pour valider, R3 pour revenir.",
                "MAG Menu in game: R1 + Square to open, L2 / R2 (or D-pad up / down) to move, D-pad left / right to adjust, Square to select, R3 to go back."),
            L.T("Ton propre script : carte Compilation (il doit contenir une fonction autoexec), puis « Mon fichier .gscc ».",
                "Your own script: Compilation card (it must contain an autoexec function), then \"My .gscc file\"."),
            L.T("En cas de souci, regarde le journal en bas de la page (aussi dans MAG_GSC_INJECTOR_BO3.log).",
                "If something goes wrong, check the log at the bottom of the page (also in MAG_GSC_INJECTOR_BO3.log).") };
        for (int i = 0; i < steps.Length; i++)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var num = new Border
            {
                Width = 24, Height = 24, CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Background = Ui.Grad(Ui.A(Ui.Accent, 70), Ui.A(Ui.Accent2, 40), 45), BorderBrush = Ui.B(Ui.A(Ui.Accent, 140)), BorderThickness = new Thickness(1)
            };
            var nt = Ui.T((i + 1).ToString(), 12, Brushes.White, Ui.Title, FontWeights.SemiBold); nt.HorizontalAlignment = HorizontalAlignment.Center; nt.VerticalAlignment = VerticalAlignment.Center;
            num.Child = nt; row.Children.Add(num);
            var tx = Ui.T(steps[i], 13, Ui.TextMain); tx.TextWrapping = TextWrapping.Wrap; tx.Margin = new Thickness(0, 3, 0, 0); Grid.SetColumn(tx, 1);
            row.Children.Add(tx);
            tuto.Children.Add(row);
        }
        Grid2.Children.Add(Ui.Card(L.T("Tutoriel", "Tutorial"), "", tuto));

        var j = new StackPanel();
        j.Children.Add(Caption(L.T("Menu à charger", "Menu to load")));
        modeAuto = Ui.Ghost(Injector.ModeName(Injector.ModeAuto), "", delegate { SetMode(Injector.ModeAuto); });
        modeMp = Ui.Ghost(Injector.ModeName(Injector.ModeMp), "", delegate { SetMode(Injector.ModeMp); });
        modeZm = Ui.Ghost(Injector.ModeName(Injector.ModeZm), "", delegate { SetMode(Injector.ModeZm); });
        modeCp = Ui.Ghost(Injector.ModeName(Injector.ModeCp), "", delegate { SetMode(Injector.ModeCp); });
        j.Children.Add(Ui.Wrap(modeAuto, modeMp, modeZm, modeCp));
        j.Children.Add(Caption(L.T("Script à injecter", "Script to inject")));
        scrMenu = Ui.Ghost(L.T("MAG Menu intégré", "Built-in MAG Menu"), "", delegate { SetScript(false); });
        scrFile = Ui.Ghost(L.T("Mon fichier .gscc", "My .gscc file"), "", delegate { SetScript(true); });
        j.Children.Add(Ui.Wrap(scrMenu, scrFile));
        gsccBox = Ui.Field(W.CfgGet("gscc_in", ""), 330);
        fileRow = new StackPanel();
        fileRow.Children.Add(Ui.Wrap(Ui.Labeled(L.T("Script compilé (.gscc) à injecter", "Compiled script (.gscc) to inject"), gsccBox),
            Bottom(Ui.Ghost(L.T("Parcourir", "Browse"), "", BrowseGscc))));
        j.Children.Add(fileRow);
        modeInfo = Message(); j.Children.Add(modeInfo);
        cancelBtn = Ui.Ghost(L.T("Annuler", "Cancel"), "", delegate { if (current != null) { current.Cancel = true; Core.Trace("Annulation demandée"); } }, Ui.Warn);
        j.Children.Add(Ui.Wrap(
            Ui.Primary(L.T("Injecter", "Inject"), "", Inject),
            Ui.Ghost(L.T("Retirer", "Remove"), "", Remove, Ui.Danger),
            Ui.Ghost(L.T("État", "Status"), "", State),
            cancelBtn));
        stateInfo = Message(); j.Children.Add(stateInfo);
        followInfo = Message(); j.Children.Add(followInfo);
        Grid2.Children.Add(Ui.Card("Injection", "", j));

        var c = new StackPanel();
        var note = Ui.T(L.T("Les MAG Menu sont déjà compilés dans l'outil : la compilation sert pour tes propres projets GSC. Ton script remplace [masqué] et doit démarrer par une fonction autoexec. « foreach » n'existe pas pour BO3 : utilise des boucles for.",
            "The MAG Menus are already compiled into the tool: compilation is for your own GSC projects. Your script replaces [masqué] and must start with an autoexec function. \"foreach\" is not available for BO3: use for loops."), 13, Ui.TextSub);
        note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(0, 0, 0, 6);
        c.Children.Add(note);
        projectBox = Ui.Field(W.CfgGet("project", ""), 330);
        c.Children.Add(Ui.Wrap(Ui.Labeled(L.T("Dossier du projet (tous les .gsc, sous-dossiers compris)", "Project folder (all .gsc files, subfolders included)"), projectBox),
            Bottom(Ui.Ghost(L.T("Parcourir", "Browse"), "", BrowseProject))));
        outBox = Ui.Field(W.CfgGet("gscc_out", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mon_script_bo3.gscc")), 330);
        c.Children.Add(Ui.Wrap(Ui.Labeled(L.T("Fichier .gscc à créer", ".gscc file to create"), outBox), Bottom(Ui.Ghost(L.T("Parcourir", "Browse"), "", BrowseOut))));
        c.Children.Add(Ui.Wrap(Ui.Primary(L.T("Compiler en .gscc", "Compile to .gscc"), "", Compile)));
        compileInfo = Message(); c.Children.Add(compileInfo);
        Grid2.Children.Add(Ui.Card(L.T("Compilation", "Compilation"), "", c));

        var lg = new StackPanel();
        logBox = Ui.T("", 12, Ui.TextSub, Ui.Mono); logBox.TextWrapping = TextWrapping.Wrap;
        logScroll = new ScrollViewer { Content = logBox, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        lg.Children.Add(logScroll);
        lg.Children.Add(Ui.Wrap(
            Ui.Ghost(L.T("Ouvrir le fichier log", "Open the log file"), "", delegate { try { System.Diagnostics.Process.Start(Core.LogPath); } catch (Exception ex) { W.Toast(ex.Message, Ui.Danger); } }),
            Ui.Ghost(L.T("Copier le journal", "Copy the log"), "", delegate { try { Clipboard.SetText(string.Join("\r\n", W.LogLines.ToArray())); W.Ok(L.T("Journal copié", "Log copied")); } catch { } })));
        Grid2.Children.Add(Ui.Card(L.T("Journal", "Log"), "", lg));
        Action showLog = delegate { var lines = W.LogLines; int s0 = Math.Max(0, lines.Count - 200); logBox.Text = string.Join("\n", lines.GetRange(s0, lines.Count - s0).ToArray()); logScroll.ScrollToEnd(); };
        W.LogChanged += showLog; showLog();

        Show();
        Busy(working);
        if (lastFollow.Length > 0) Say(followInfo, lastFollow, Ui.Accent);
    }

    static UIElement Bottom(UIElement e) { var s = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 8) }; s.Children.Add(e); return s; }
    static TextBlock Message() { var t = Ui.T("", 13.5, Ui.TextSub); t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 6, 0, 2); return t; }
    static TextBlock Caption(string text)
    {
        var t = new TextBlock { FontFamily = Ui.Title, FontSize = 13, Margin = new Thickness(0, 2, 0, 6) };
        t.Inlines.Add(new Run(text) { Foreground = Ui.TextSub });
        return t;
    }
    static void Say(TextBlock t, string text, Color c) { t.Text = text; t.Foreground = Ui.B(c); }
    static void Say(TextBlock t, string text) { t.Text = text; t.Foreground = Ui.TextSub; }

    static void Pick(FlatButton on, params FlatButton[] off)
    {
        on.Opacity = 1; on.BorderBrush = Ui.B(Ui.Accent);
        foreach (var o in off) { o.Opacity = 0.55; o.BorderBrush = Ui.B("#20FFFFFF"); }
    }

    void Show()
    {
        if (mode == Injector.ModeZm) Pick(modeZm, modeMp, modeAuto, modeCp);
        else if (mode == Injector.ModeMp) Pick(modeMp, modeZm, modeAuto, modeCp);
        else if (mode == Injector.ModeCp) Pick(modeCp, modeMp, modeZm, modeAuto);
        else Pick(modeAuto, modeMp, modeZm, modeCp);
        if (useFile) Pick(scrFile, scrMenu); else Pick(scrMenu, scrFile);
        fileRow.Visibility = useFile ? Visibility.Visible : Visibility.Collapsed;
        string t;
        if (useFile)
            t = L.T("Ton .gscc remplace [masqué] dans les trois modes (multijoueur, Zombies, campagne). Dans le jeu : menu principal ou salon, pas de partie en cours.",
                "Your .gscc replaces [masqué] in all three modes (Multiplayer, Zombies, Campaign). In the game: main menu or lobby, no match running.");
        else if (mode == Injector.ModeAuto)
            t = L.T("Les trois MAG Menu (multijoueur, Zombies, campagne) sont injectés ; l'outil met le bon selon le mode du jeu (laisse-le ouvert si tu changes de mode).",
                "The three MAG Menus (Multiplayer, Zombies, Campaign) are injected; the tool loads the right one for the game mode (keep it open if you switch modes).");
        else
            t = string.Format(L.T("{0} uniquement : ne lance que des parties {1} tant qu'il est injecté.", "{0} only: only start {1} matches while it is injected."),
                Injector.MenuNames[mode], Injector.ModeName(mode));
        Say(modeInfo, t, Ui.Accent);
    }

    void SetMode(int m)
    {
        if (working) { W.Toast(L.T("Patiente, une opération est en cours", "Please wait, an operation is running"), Ui.Warn); return; }
        mode = m; Show(); SaveCfg();
        if (follower.Active) Core.Post(delegate { try { Announce(follower.Follow(mode, true)); } catch (Exception ex) { Core.Trace("Suivi : " + ex.Message); } });
    }

    void SetScript(bool file)
    {
        if (working) { W.Toast(L.T("Patiente, une opération est en cours", "Please wait, an operation is running"), Ui.Warn); return; }
        useFile = file; Show(); SaveCfg();
    }

    void Busy(bool b) { working = b; cancelBtn.Visibility = b ? Visibility.Visible : Visibility.Collapsed; }

    void SaveCfg()
    {
        W.Cfg["project"] = projectBox.Text.Trim(); W.Cfg["gscc_out"] = outBox.Text.Trim(); W.Cfg["gscc_in"] = gsccBox.Text.Trim();
        W.Cfg["mode"] = mode == Injector.ModeZm ? "zm" : mode == Injector.ModeMp ? "mp" : mode == Injector.ModeCp ? "cp" : "auto"; W.Cfg["script"] = useFile ? "file" : "menu";
        W.SaveCfg();
    }

    void BrowseProject()
    {
        using (var d = new System.Windows.Forms.FolderBrowserDialog { Description = L.T("Dossier du projet GSC", "GSC project folder"), ShowNewFolderButton = false })
        {
            if (Directory.Exists(projectBox.Text.Trim())) d.SelectedPath = projectBox.Text.Trim();
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) { projectBox.Text = d.SelectedPath; SaveCfg(); }
        }
    }
    void BrowseOut()
    {
        var d = new SaveFileDialog { Filter = L.T("Script GSC compilé (*.gscc)|*.gscc", "Compiled GSC script (*.gscc)|*.gscc"), FileName = Path.GetFileName(outBox.Text.Trim()) };
        if (d.ShowDialog(W) == true) { outBox.Text = d.FileName; SaveCfg(); }
    }
    void BrowseGscc()
    {
        var d = new OpenFileDialog { Filter = L.T("Script GSC compilé (*.gscc)|*.gscc", "Compiled GSC script (*.gscc)|*.gscc") };
        if (d.ShowDialog(W) == true) { gsccBox.Text = d.FileName; SaveCfg(); }
    }

    void Compile()
    {
        if (working) { W.Toast(L.T("Patiente, une opération est en cours", "Please wait, an operation is running"), Ui.Warn); return; }
        SaveCfg();
        string outPath = outBox.Text.Trim(), dir = projectBox.Text.Trim();
        if (dir.Length == 0) { Say(compileInfo, L.T("Choisis le dossier de ton projet GSC.", "Choose your GSC project folder."), Ui.Warn); return; }
        if (outPath.Length == 0) { Say(compileInfo, L.T("Choisis le fichier .gscc à créer.", "Choose the .gscc file to create."), Ui.Warn); return; }
        SortedDictionary<string, string> sources;
        try
        {
            if (!Directory.Exists(dir)) { Say(compileInfo, L.T("Le dossier du projet n'existe pas.", "The project folder does not exist."), Ui.Danger); return; }
            sources = GscCompiler.ReadProject(dir);
            if (sources.Count == 0) { Say(compileInfo, L.T("Aucun fichier .gsc dans ce dossier.", "No .gsc file in this folder."), Ui.Danger); return; }
        }
        catch (Exception ex) { Say(compileInfo, ex.Message, Ui.Danger); return; }
        Busy(true);
        Say(compileInfo, L.T("Compilation en cours...", "Compiling..."), Ui.Warn);
        Core.Trace("=== Compilation : " + dir + " -> " + outPath + " ===");
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            GscCompiler.Result r;
            try { r = GscCompiler.Compile(sources, dir, Core.Trace); }
            catch (Exception ex) { r = new GscCompiler.Result { Error = ex.Message }; }
            if (r.Script != null)
            {
                try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))); File.WriteAllBytes(outPath, r.Script); Core.Trace("Fichier écrit : " + outPath + " (" + r.Script.Length + " o)"); }
                catch (Exception ex) { r.Error = L.T("écriture impossible : ", "cannot write: ") + ex.Message; r.Script = null; }
            }
            if (r.Error != null) Core.Trace("Compilation échouée : " + r.Error);
            Ui.Run(delegate
            {
                Busy(false);
                if (r.Script != null)
                {
                    Say(compileInfo, string.Format(L.T("Compilation terminée : {0} ({1:0.0} Ko). Il est sélectionné pour l'injection.", "Compilation complete: {0} ({1:0.0} KB). It is selected for injection."), Path.GetFileName(outPath), r.Script.Length / 1024.0), Ui.Accent);
                    gsccBox.Text = outPath; useFile = true; Show(); SaveCfg();
                    W.Ok(L.T("Compilation terminée", "Compilation complete"));
                }
                else { Say(compileInfo, L.T("Compilation échouée : ", "Compilation failed: ") + r.Error, Ui.Danger); W.Toast(L.T("Compilation échouée", "Compilation failed"), Ui.Danger); }
            });
        });
    }

    void Run(string start, Action work)
    {
        if (!NeedReady()) return;
        if (working) { W.Toast(L.T("Patiente, une opération est en cours", "Please wait, an operation is running"), Ui.Warn); return; }
        Busy(true); current = inj; inj.Cancel = false;
        Say(stateInfo, start, Ui.Warn);
        Core.Post(delegate
        {
            try { work(); }
            catch (Exception ex)
            {
                Core.Trace("ERREUR : " + ex.Message);
                Ui.Run(delegate { Say(stateInfo, ex.Message, Ui.Danger); W.Toast(ex.Message, Ui.Danger); });
            }
            finally { Ui.Run(delegate { Busy(false); current = null; }); }
        });
    }

    void Inject()
    {
        SaveCfg();
        byte[] mp, zm, cp; string name;
        string path = gsccBox.Text.Trim();
        try
        {
            if (!useFile) { mp = Injector.Menu(Injector.ModeMp); zm = Injector.Menu(Injector.ModeZm); cp = Injector.Menu(Injector.ModeCp); name = "MAG Menu BO3"; }
            else if (path.Length == 0) { Say(stateInfo, L.T("Choisis le fichier .gscc à injecter.", "Choose the .gscc file to inject."), Ui.Warn); return; }
            else { mp = File.ReadAllBytes(path); zm = mp; cp = mp; name = Path.GetFileName(path); }
        }
        catch (Exception ex) { Say(stateInfo, L.T("Lecture du script impossible : ", "Cannot read the script: ") + ex.Message, Ui.Danger); return; }
        string bad = Injector.CheckScript(mp);
        if (bad != null) { Say(stateInfo, L.T("Script refusé : ", "Script rejected: ") + bad, Ui.Danger); return; }
        int m = mode;
        Run(L.T("Injection en cours...", "Injecting..."), delegate
        {
            Core.Trace("Script : " + name + " (" + Injector.ModeName(m) + ")");
            inj.Inject(mp, zm, cp, m);
            follower = inj;
            int gm = inj.GameMode();
            try { Core.Dbg.Notify(222, name + L.T(" injecté", " injected")); } catch { }
            StartFollow();
            Ui.Run(delegate
            {
                Say(stateInfo, string.Format(L.T("Injection terminée ({0}). Jeu en mode {1}. Lance une partie : le menu se charge avec la carte.",
                    "Injection complete ({0}). Game in {1} mode. Start a match: the menu loads with the map."), name, Injector.GameModeName(gm)), Ui.Accent);
                W.Ok(L.T("Injection terminée", "Injection complete"));
            });
        });
    }

    void StartFollow()
    {
        if (followTimer != null) return;
        followTimer = new System.Threading.Timer(delegate
        {
            if (followBusy || working || !Core.Ready || follower == null || !follower.Active) return;
            followBusy = true;
            Core.Post(delegate
            {
                try { Announce(follower.Follow(mode, false)); }
                catch (Exception ex) { Core.Trace("Suivi du mode : " + ex.Message); }
                finally { followBusy = false; }
            });
        }, null, 700, 700);
    }

    static void Announce(string msg)
    {
        if (msg == null) return;
        Core c = shown != null ? shown.Core : null;
        if (c != null) { c.Trace("Suivi : " + msg); try { c.Dbg.Notify(222, msg); } catch { } }
        lastFollow = msg;
        Ui.Run(delegate { if (shown != null) { Say(shown.followInfo, msg, Ui.Accent); shown.W.Ok(msg); } });
    }

    void Remove()
    {
        Run(L.T("Retrait en cours...", "Removing..."), delegate
        {
            bool had = inj.Remove();
            if (follower != null && follower != inj) follower.Remove();
            lastFollow = "";
            Ui.Run(delegate
            {
                Say(followInfo, "");
                if (had) { Say(stateInfo, L.T("Script retiré. La prochaine carte se chargera sans lui.", "Script removed. The next map will load without it."), Ui.Accent); W.Ok(L.T("Script retiré", "Script removed")); }
                else Say(stateInfo, L.T("Aucun script à retirer.", "No script to remove."));
            });
        });
    }

    void State()
    {
        Run(L.T("Vérification...", "Checking..."), delegate
        {
            var s = inj.GetStatus();
            string t; Color c = Ui.C("#9BA4BA");
            string gm = Injector.GameModeName(s.GameMode);
            if (!s.Found) t = L.T("Scripts du jeu introuvables : attends d'être au menu principal du jeu.", "Game scripts not found: wait until you are in the game's main menu.");
            else if (s.Injected && s.Menu > 0) { t = string.Format(L.T("{0} en place (jeu en mode {1}) : il se charge avec la prochaine carte.", "{0} loaded (game in {1} mode): it starts with the next map."), Injector.MenuNames[s.Menu], gm); c = Ui.Accent; }
            else if (s.Injected) { t = string.Format(L.T("Script MAG en place (jeu en mode {0}).", "MAG script loaded (game in {0} mode)."), gm); c = Ui.Accent; }
            else t = string.Format(L.T("Aucun script en place (jeu en mode {0}). Prêt pour l'injection.", "No script loaded (game in {0} mode). Ready to inject."), gm);
            Ui.Run(delegate { Say(stateInfo, t, c); });
        });
    }
}
