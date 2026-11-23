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
    TextBlock compileInfo, stateInfo, modeInfo, logBox;
    TextBox projectBox, outBox, gsccBox;
    ScrollViewer logScroll;
    FlatButton modeMp, modeZm, scrMenu, scrFile, cancelBtn;
    StackPanel fileRow;
    readonly Injector inj;

    static bool cfgRead, useFile;
    static int mode = Injector.ModeMp;
    static volatile bool working;
    static Injector current;

    public PageGsc(MainWindow w) : base(w, L.T("Injecteur GSC", "GSC Injector"), "\uE945", "")
    {
        inj = new Injector(Core);
        Grid2.MaxColumns = 2;
        if (!cfgRead)
        {
            cfgRead = true;
            mode = W.CfgGet("mode", "mp") == "zm" ? Injector.ModeZm : Injector.ModeMp;
            useFile = W.CfgGet("script", "menu") == "file";
        }

        var tuto = new StackPanel();
        string[] steps = {
            L.T("Sur la PS4 : active GoldHEN et son chargeur bin, puis lance Black Ops 4 (mise à jour 1.26).",
                "On the PS4: enable GoldHEN and its bin loader, then start Black Ops 4 (update 1.26)."),
            L.T("Page Accueil : entre l'IP de ta PS4, clique sur « Envoyer ps4debug », attends la notification, puis « Connecter ».",
                "Home page: enter your PS4's IP, click \"Send ps4debug\", wait for the notification, then \"Connect\"."),
            L.T("Carte Injection : choisis le mode, Multijoueur ou Zombies. Le MAG Menu du mode est déjà dans l'outil (ou choisis ton propre .gscc).",
                "Injection card: pick the mode, Multiplayer or Zombies. The MAG Menu for that mode is built into the tool (or pick your own .gscc)."),
            L.T("Dans le jeu, va dans le menu du même mode (Multijoueur ou Zombies, pas de partie en cours), puis clique sur « Injecter ».",
                "In the game, go to the menu of the same mode (Multiplayer or Zombies, no match running), then click \"Inject\"."),
            L.T("Lance une partie dont tu es l'hôte (partie personnalisée en multijoueur, partie solo ou privée en Zombies) : le script se charge avec la carte.",
                "Start a match you host (custom game in Multiplayer, solo or private game in Zombies): the script loads with the map."),
            L.T("MAG Menu en jeu : R1 + Carré pour ouvrir, L2 / R2 (ou croix haut / bas) pour monter / descendre, Carré pour valider, Rond pour revenir.",
                "MAG Menu in game: R1 + Square to open, L2 / R2 (or D-pad up / down) to move up / down, Square to select, Circle to go back."),
            L.T("Ton propre script : carte Compilation, choisis le dossier de ton projet GSC puis « Compiler en .gscc ».",
                "Your own script: Compilation card, pick your GSC project folder, then \"Compile to .gscc\"."),
            L.T("En cas de souci, regarde le journal en bas de la page (aussi dans MAG_GSC_INJECTOR_BO4.log).",
                "If something goes wrong, check the log at the bottom of the page (also in MAG_GSC_INJECTOR_BO4.log).") };
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
        Grid2.Children.Add(Ui.Card(L.T("Tutoriel", "Tutorial"), "\uE82D", tuto));

        var j = new StackPanel();
        j.Children.Add(Caption(L.T("Mode de jeu", "Game mode")));
        modeMp = Ui.Ghost(Injector.ModeName(Injector.ModeMp), "\uE716", delegate { SetMode(Injector.ModeMp); });
        modeZm = Ui.Ghost(Injector.ModeName(Injector.ModeZm), "\uEBE8", delegate { SetMode(Injector.ModeZm); });
        j.Children.Add(Ui.Wrap(modeMp, modeZm));
        j.Children.Add(Caption(L.T("Script à injecter", "Script to inject")));
        scrMenu = Ui.Ghost(L.T("MAG Menu intégré", "Built-in MAG Menu"), "\uE7FC", delegate { SetScript(false); });
        scrFile = Ui.Ghost(L.T("Mon fichier .gscc", "My .gscc file"), "\uE7C3", delegate { SetScript(true); });
        j.Children.Add(Ui.Wrap(scrMenu, scrFile));
        gsccBox = Ui.Field(W.CfgGet("gscc_in", ""), 330);
        fileRow = new StackPanel();
        fileRow.Children.Add(Ui.Wrap(Ui.Labeled(L.T("Script compilé (.gscc) à injecter", "Compiled script (.gscc) to inject"), gsccBox),
            Bottom(Ui.Ghost(L.T("Parcourir", "Browse"), "\uE838", BrowseGscc))));
        j.Children.Add(fileRow);
        modeInfo = Message(); j.Children.Add(modeInfo);
        cancelBtn = Ui.Ghost(L.T("Annuler", "Cancel"), "\uE711", delegate { if (current != null) { current.Cancel = true; Core.Trace("Annulation demandée"); } }, Ui.Warn);
        j.Children.Add(Ui.Wrap(
            Ui.Primary(L.T("Injecter", "Inject"), "\uE768", Inject),
            Ui.Ghost(L.T("Retirer", "Remove"), "\uE74D", Remove, Ui.Danger),
            Ui.Ghost(L.T("État", "Status"), "\uE9D9", State),
            cancelBtn));
        stateInfo = Message(); j.Children.Add(stateInfo);
        Grid2.Children.Add(Ui.Card("Injection", "\uE945", j));

        var c = new StackPanel();
        var note = Ui.T(L.T("Les MAG Menu sont déjà compilés dans l'outil : la compilation sert pour tes propres projets GSC (multijoueur ou Zombies).",
            "The MAG Menus are already compiled into the tool: compilation is for your own GSC projects (Multiplayer or Zombies)."), 13, Ui.TextSub);
        note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(0, 0, 0, 6);
        c.Children.Add(note);
        projectBox = Ui.Field(W.CfgGet("project", ""), 330);
        c.Children.Add(Ui.Wrap(Ui.Labeled(L.T("Dossier du projet (tous les .gsc, sous-dossiers compris)", "Project folder (all .gsc files, subfolders included)"), projectBox),
            Bottom(Ui.Ghost(L.T("Parcourir", "Browse"), "\uE838", BrowseProject))));
        outBox = Ui.Field(W.CfgGet("gscc_out", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mon_script_bo4.gscc")), 330);
        c.Children.Add(Ui.Wrap(Ui.Labeled(L.T("Fichier .gscc à créer", ".gscc file to create"), outBox), Bottom(Ui.Ghost(L.T("Parcourir", "Browse"), "\uE838", BrowseOut))));
        c.Children.Add(Ui.Wrap(Ui.Primary(L.T("Compiler en .gscc", "Compile to .gscc"), "\uE943", Compile)));
        compileInfo = Message(); c.Children.Add(compileInfo);
        Grid2.Children.Add(Ui.Card(L.T("Compilation", "Compilation"), "\uE943", c));

        var lg = new StackPanel();
        logBox = Ui.T("", 12, Ui.TextSub, Ui.Mono); logBox.TextWrapping = TextWrapping.Wrap;
        logScroll = new ScrollViewer { Content = logBox, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        lg.Children.Add(logScroll);
        lg.Children.Add(Ui.Wrap(
            Ui.Ghost(L.T("Ouvrir le fichier log", "Open the log file"), "\uE8E5", delegate { try { System.Diagnostics.Process.Start(Core.LogPath); } catch (Exception ex) { W.Toast(ex.Message, Ui.Danger); } }),
            Ui.Ghost(L.T("Copier le journal", "Copy the log"), "\uE8C8", delegate { try { Clipboard.SetText(string.Join("\r\n", W.LogLines.ToArray())); W.Ok(L.T("Journal copié", "Log copied")); } catch { } })));
        Grid2.Children.Add(Ui.Card(L.T("Journal", "Log"), "\uE81C", lg));
        Action showLog = delegate { var lines = W.LogLines; int s0 = Math.Max(0, lines.Count - 200); logBox.Text = string.Join("\n", lines.GetRange(s0, lines.Count - s0).ToArray()); logScroll.ScrollToEnd(); };
        W.LogChanged += showLog; showLog();

        Show();
        Busy(working);
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

    static void Pick(FlatButton on, FlatButton off)
    {
        on.Opacity = 1; on.BorderBrush = Ui.B(Ui.Accent);
        off.Opacity = 0.55; off.BorderBrush = Ui.B("#20FFFFFF");
    }

    void Show()
    {
        inj.Mode = mode;
        if (mode == Injector.ModeZm) Pick(modeZm, modeMp); else Pick(modeMp, modeZm);
        if (useFile) Pick(scrFile, scrMenu); else Pick(scrMenu, scrFile);
        fileRow.Visibility = useFile ? Visibility.Visible : Visibility.Collapsed;
        string m = Injector.ModeName(mode);
        Say(modeInfo, useFile
            ? string.Format(L.T("Ton .gscc sera chargé avec les cartes {0}. Dans le jeu : menu {0}, pas de partie en cours.",
                "Your .gscc will load with {0} maps. In the game: {0} menu, no match running."), m)
            : string.Format(L.T("{0} (intégré à l'outil). Dans le jeu : menu {1}, pas de partie en cours.",
                "{0} (built into the tool). In the game: {1} menu, no match running."), Injector.MenuNames[mode], m), Ui.Accent);
    }

    void SetMode(int m)
    {
        if (working) { W.Toast(L.T("Patiente, une opération est en cours", "Please wait, an operation is running"), Ui.Warn); return; }
        mode = m; Show(); SaveCfg();
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
        W.Cfg["mode"] = mode == Injector.ModeZm ? "zm" : "mp"; W.Cfg["script"] = useFile ? "file" : "menu";
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
        Busy(true); current = inj; inj.Cancel = false; inj.Mode = mode;
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
        byte[] script; string name;
        string path = gsccBox.Text.Trim();
        try
        {
            if (!useFile) { script = Injector.Menu(mode); name = Injector.MenuNames[mode]; }
            else if (path.Length == 0) { Say(stateInfo, L.T("Choisis le fichier .gscc à injecter.", "Choose the .gscc file to inject."), Ui.Warn); return; }
            else { script = File.ReadAllBytes(path); name = Path.GetFileName(path); }
        }
        catch (Exception ex) { Say(stateInfo, L.T("Lecture du .gscc impossible : ", "Cannot read the .gscc: ") + ex.Message, Ui.Danger); return; }
        string bad = Injector.CheckScript(script);
        if (bad != null) { Say(stateInfo, L.T("Script refusé : ", "Script rejected: ") + bad, Ui.Danger); return; }
        string m = Injector.ModeName(mode);
        Run(L.T("Injection en cours...", "Injecting..."), delegate
        {
            Core.Trace("Script : " + name + " (" + m + ")");
            inj.Inject(script, delegate (string pct) { Ui.Run(delegate { Say(stateInfo, L.T("Recherche des scripts du jeu... ", "Looking for the game scripts... ") + pct, Ui.Warn); }); });
            try { Core.Dbg.Notify(222, name + L.T(" injecté", " injected")); } catch { }
            Ui.Run(delegate
            {
                Say(stateInfo, string.Format(L.T("Injection terminée ({0}, {1}). Lance une partie : le script se charge avec la carte.",
                    "Injection complete ({0}, {1}). Start a match: the script loads with the map."), name, m), Ui.Accent);
                W.Ok(L.T("Injection terminée", "Injection complete"));
            });
        });
    }

    void Remove()
    {
        Run(L.T("Retrait en cours...", "Removing..."), delegate
        {
            bool had = inj.Remove();
            Ui.Run(delegate
            {
                if (had) { Say(stateInfo, L.T("Script retiré. La prochaine carte se chargera sans lui.", "Script removed. The next map will load without it."), Ui.Accent); W.Ok(L.T("Script retiré", "Script removed")); }
                else Say(stateInfo, L.T("Aucun script à retirer.", "No script to remove."));
            });
        });
    }

    void State()
    {
        string m = Injector.ModeName(mode), other = Injector.ModeName(1 - mode);
        Run(L.T("Vérification...", "Checking..."), delegate
        {
            var s = inj.GetStatus();
            string t; Color c = Ui.C("#9BA4BA");
            if (s.ScriptIn && s.Hooked) { t = string.Format(L.T("Script en place : il se charge à chaque carte {0}.", "Script loaded: it starts on every {0} map."), m); c = Ui.Accent; }
            else if (!s.CoreLoaded) t = L.T("Scripts du jeu introuvables : attends d'être au menu principal du jeu.", "Game scripts not found: wait until you are in the game's main menu.");
            else if (!s.BbLoaded && s.OtherLoaded) { t = string.Format(L.T("Le jeu est en mode {0} : choisis « {0} » dans l'outil, ou va dans le menu {1} du jeu.", "The game is in {0} mode: pick \"{0}\" in the tool, or go to the game's {1} menu."), other, m); c = Ui.Warn; }
            else if (!s.BbLoaded) t = string.Format(L.T("Menu {0} non chargé : va dans le menu {0} du jeu.", "{0} menu not loaded: go to the game's {0} menu."), m);
            else if (s.ScriptIn) t = L.T("Script présent mais pas accroché : clique sur « Injecter ».", "Script present but not hooked: click \"Inject\".");
            else t = string.Format(L.T("Aucun script en place. Prêt pour l'injection ({0}).", "No script loaded. Ready to inject ({0})."), m);
            Ui.Run(delegate { Say(stateInfo, t, c); });
        });
    }
}
