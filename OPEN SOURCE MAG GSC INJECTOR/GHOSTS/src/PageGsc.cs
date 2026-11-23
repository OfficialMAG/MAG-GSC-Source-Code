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
    TextBlock compileInfo, stateInfo, logBox;
    TextBox projectBox, outBox, gsccBox;
    ScrollViewer logScroll;
    FlatButton srcMenu, srcProject, cancelBtn, autoBtn, modeMp, modeExt;
    StackPanel projectRow;
    readonly Injector inj;
    static bool useProject = true;
    static bool modeLoaded;
    static volatile bool working;
    static Injector current;

    public PageGsc(MainWindow w) : base(w, L.T("Injecteur GSC", "GSC Injector"), "", "")
    {
        inj = new Injector(Core);
        Grid2.MaxColumns = 2;
        if (!modeLoaded) { modeLoaded = true; Injector.Mode = W.CfgGet("mode", "mp") == "ext" ? Injector.GameMode.Extinction : Injector.GameMode.Multiplayer; }

        var tuto = new StackPanel();
        string[] steps = {
            L.T("Sur la PS4 : active GoldHEN et son chargeur bin, puis lance Call of Duty: Ghosts (mise à jour 1.21) et va dans le Multijoueur (ou Extinction).",
                "On the PS4: enable GoldHEN and its bin loader, then start Call of Duty: Ghosts (update 1.21) and go to Multiplayer (or Extinction)."),
            L.T("Page Accueil : entre l'IP de ta PS4, clique sur « Envoyer ps4debug », attends la notification, puis « Connecter ».",
                "Home page: enter your PS4's IP, click \"Send ps4debug\", wait for the notification, then \"Connect\"."),
            L.T("Carte Injection : choisis le mode de jeu, « Multijoueur » ou « Extinction ». Le MAG Menu intégré change avec le mode.",
                "Injection card: pick the game mode, \"Multiplayer\" or \"Extinction\". The built-in MAG Menu follows the mode."),
            L.T("Les 2 MAG Menus sont déjà dans l'injecteur : laisse le champ .gscbin vide. Compilation (facultatif) : seulement pour tes propres scripts GSC.",
                "Both MAG Menus are already in the injector: leave the .gscbin field empty. Compilation (optional): only for your own GSC scripts."),
            L.T("Dans le jeu, reste au menu principal du Multijoueur, puis clique sur « Injection auto : OFF » pour l'activer (si le jeu relance son programme multijoueur ou recharge ses scripts, l'injection auto la refait tout de suite). Laisse l'injecteur ouvert.",
                "In the game, stay in the Multiplayer main menu, then click \"Auto inject: OFF\" to turn it on (if the game restarts its multiplayer program or reloads its scripts, auto inject redoes it at once). Keep the injector open."),
            L.T("Lance une partie privée en tant qu'hôte (multijoueur ou Extinction selon le mode choisi) : le script se charge avec la carte. « Script de test » affiche « MAG TEST OK » à l'apparition : pratique pour vérifier que l'injection marche.",
                "Start a private match as the host (multiplayer or Extinction, as picked): the script loads with the map. \"Test script\" shows \"MAG TEST OK\" when you spawn: handy to check the injection works."),
            L.T("MAG Menu en jeu : R1 + Carré pour ouvrir, L2 / R2 (en multijoueur aussi la croix haut / bas) pour monter / descendre, Carré pour valider, Rond ou R3 pour revenir / fermer. L'hôte a le menu et peut le donner aux autres (Players).",
                "MAG Menu in game: R1 + Square to open, L2 / R2 (in multiplayer also d-pad up / down) to move up / down, Square to select, Circle or R3 to go back / close. The host has the menu and can give it to others (Players)."),
            L.T("En cas de souci, envoie le fichier MAG_GSC_INJECTOR_GHOSTS.log (à côté de l'injecteur) sur le Discord.",
                "If something goes wrong, send the MAG_GSC_INJECTOR_GHOSTS.log file (next to the injector) on the Discord.") };
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

        var c = new StackPanel();
        var mn = new TextBlock { FontFamily = Ui.Title, FontSize = 13, Margin = new Thickness(0, 0, 0, 6) };
        mn.Inlines.Add(new Run(L.T("Script à compiler", "Script to compile")) { Foreground = Ui.TextSub });
        c.Children.Add(mn);
        srcMenu = Ui.Ghost(Injector.MenuName, "", delegate { SetSource(false); });
        srcProject = Ui.Ghost(L.T("Fichier / dossier GSC", "GSC file / folder"), "", delegate { SetSource(true); });
        c.Children.Add(Ui.Wrap(srcMenu, srcProject));
        projectBox = Ui.Field(W.CfgGet("project", ""), 330);
        projectRow = new StackPanel();
        projectRow.Children.Add(Ui.Wrap(Ui.Labeled(L.T("Fichier .gsc, ou dossier (tous les .gsc mis bout à bout) — fonction init() obligatoire", ".gsc file, or folder (all .gsc files put together) — init() function required"), projectBox),
            Bottom(Ui.Ghost(L.T("Parcourir", "Browse"), "", BrowseProject))));
        c.Children.Add(projectRow);
        outBox = Ui.Field(W.CfgGet("gscc_out", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mon_script.gscbin")), 330);
        c.Children.Add(Ui.Wrap(Ui.Labeled(L.T("Fichier .gscbin à créer", ".gscbin file to create"), outBox), Bottom(Ui.Ghost(L.T("Parcourir", "Browse"), "", BrowseOut))));
        c.Children.Add(Ui.Wrap(Ui.Primary(L.T("Compiler en .gscbin", "Compile to .gscbin"), "", Compile)));
        compileInfo = Message(); c.Children.Add(compileInfo);
        Grid2.Children.Add(Ui.Card(L.T("Compilation", "Compilation"), "", c));

        var j = new StackPanel();
        var md = new TextBlock { FontFamily = Ui.Title, FontSize = 13, Margin = new Thickness(0, 0, 0, 6) };
        md.Inlines.Add(new Run(L.T("Mode de jeu", "Game mode")) { Foreground = Ui.TextSub });
        j.Children.Add(md);
        modeMp = Ui.Ghost(L.T("Multijoueur", "Multiplayer"), "", delegate { SetMode(false); });
        modeExt = Ui.Ghost("Extinction", "", delegate { SetMode(true); });
        j.Children.Add(Ui.Wrap(modeMp, modeExt));
        gsccBox = Ui.Field(W.CfgGet("gscc_in", ""), 330);
        j.Children.Add(Ui.Wrap(Ui.Labeled(L.T("Script compilé (.gscbin) à injecter — vide = MAG Menu intégré", "Compiled script (.gscbin) to inject — empty = built-in MAG Menu"), gsccBox),
            Bottom(Ui.Ghost(L.T("Parcourir", "Browse"), "", BrowseGscc))));
        cancelBtn = Ui.Ghost(L.T("Annuler", "Cancel"), "", delegate { if (current != null) { current.Cancel = true; Core.Trace("Annulation demandée"); } }, Ui.Warn);
        j.Children.Add(Ui.Wrap(
            Ui.Primary(L.T("Injecter", "Inject"), "", delegate { Inject(false); }),
            Ui.Ghost(L.T("Script de test", "Test script"), "", delegate { Inject(true); }),
            autoBtn = Ui.Ghost(AutoLabel(), "", ToggleAuto),
            Ui.Ghost(L.T("Retirer", "Remove"), "", Remove, Ui.Danger),
            Ui.Ghost(L.T("État", "Status"), "", State),
            cancelBtn));
        stateInfo = Message(); j.Children.Add(stateInfo);
        Grid2.Children.Add(Ui.Card("Injection", "", j));

        SetSource(true); srcMenu.Visibility = Visibility.Collapsed; srcProject.Visibility = Visibility.Collapsed;
        ShowMode();
        Busy(working);
    }

    static UIElement Bottom(UIElement e) { var s = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 8) }; s.Children.Add(e); return s; }
    static TextBlock Message() { var t = Ui.T("", 13.5, Ui.TextSub); t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 6, 0, 2); return t; }
    static void Say(TextBlock t, string text, Color c) { t.Text = text; t.Foreground = Ui.B(c); }
    static void Say(TextBlock t, string text) { t.Text = text; t.Foreground = Ui.TextSub; }

    void SetSource(bool project)
    {
        useProject = project;
        srcMenu.Opacity = project ? 0.55 : 1; srcProject.Opacity = project ? 1 : 0.55;
        srcMenu.BorderBrush = project ? Ui.B("#20FFFFFF") : Ui.B(Ui.Accent);
        srcProject.BorderBrush = project ? Ui.B(Ui.Accent) : Ui.B("#20FFFFFF");
        projectRow.Visibility = project ? Visibility.Visible : Visibility.Collapsed;
    }

    void ShowMode()
    {
        bool ext = Injector.IsExtinction;
        modeMp.Opacity = ext ? 0.55 : 1; modeExt.Opacity = ext ? 1 : 0.55;
        modeMp.BorderBrush = ext ? Ui.B("#20FFFFFF") : Ui.B(Ui.Accent);
        modeExt.BorderBrush = ext ? Ui.B(Ui.Accent) : Ui.B("#20FFFFFF");
        SetText(srcMenu, Injector.MenuName);
    }

    void SetMode(bool ext)
    {
        if (working) { W.Toast(L.T("Patiente, une opération est en cours", "Please wait, an operation is running"), Ui.Warn); return; }
        var m = ext ? Injector.GameMode.Extinction : Injector.GameMode.Multiplayer;
        if (Injector.Mode == m) return;
        Injector.Mode = m;
        W.Cfg["mode"] = ext ? "ext" : "mp"; W.SaveCfg();
        ShowMode();
        Core.Trace("Mode de jeu : " + Injector.ModeName + " (script remplacé : " + Injector.TargetPath + ", menu intégré : " + Injector.MenuName + ")");
        if (autoOn)
        {
            byte[] script; string name;
            if (PickScript(false, out script, out name)) { autoScript = script; autoName = name; Core.Trace("Injection automatique : nouveau script " + name); }
        }
        Say(stateInfo, "");
    }

    void Busy(bool b) { working = b; cancelBtn.Visibility = b ? Visibility.Visible : Visibility.Collapsed; }

    void SaveCfg() { W.Cfg["project"] = projectBox.Text.Trim(); W.Cfg["gscc_out"] = outBox.Text.Trim(); W.Cfg["gscc_in"] = gsccBox.Text.Trim(); W.SaveCfg(); }

    void BrowseProject()
    {

        var o = new OpenFileDialog { Filter = L.T("Script GSC (*.gsc)|*.gsc", "GSC script (*.gsc)|*.gsc"), Title = L.T("Fichier .gsc (Annuler pour choisir un dossier)", ".gsc file (Cancel to pick a folder)") };
        if (o.ShowDialog(W) == true) { projectBox.Text = o.FileName; SaveCfg(); return; }
        using (var d = new System.Windows.Forms.FolderBrowserDialog { Description = L.T("Dossier du projet GSC", "GSC project folder"), ShowNewFolderButton = false })
        {
            if (Directory.Exists(projectBox.Text.Trim())) d.SelectedPath = projectBox.Text.Trim();
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) { projectBox.Text = d.SelectedPath; SaveCfg(); }
        }
    }
    void BrowseOut()
    {
        var d = new SaveFileDialog { Filter = L.T("Script GSC compilé (*.gscbin)|*.gscbin", "Compiled GSC script (*.gscbin)|*.gscbin"), FileName = Path.GetFileName(outBox.Text.Trim()) };
        if (d.ShowDialog(W) == true) { outBox.Text = d.FileName; SaveCfg(); }
    }
    void BrowseGscc()
    {
        var d = new OpenFileDialog { Filter = L.T("Script GSC compilé (*.gscbin)|*.gscbin", "Compiled GSC script (*.gscbin)|*.gscbin") };
        if (d.ShowDialog(W) == true) { gsccBox.Text = d.FileName; SaveCfg(); }
    }

    void Compile()
    {
        if (working) { W.Toast(L.T("Patiente, une opération est en cours", "Please wait, an operation is running"), Ui.Warn); return; }
        SaveCfg();
        string outPath = outBox.Text.Trim(), dir = projectBox.Text.Trim();
        if (outPath.Length == 0) { Say(compileInfo, L.T("Choisis le fichier .gscbin à créer.", "Choose the .gscbin file to create."), Ui.Warn); return; }
        SortedDictionary<string, string> sources;
        try
        {
            if (useProject)
            {
                if (!Directory.Exists(dir) && !File.Exists(dir)) { Say(compileInfo, L.T("Le fichier / dossier du projet n'existe pas.", "The project file / folder does not exist."), Ui.Danger); return; }
                sources = GscCompiler.ReadProject(dir);
                if (sources.Count == 0) { Say(compileInfo, L.T("Aucun fichier .gsc dans ce dossier.", "No .gsc file in this folder."), Ui.Danger); return; }
            }
            else { Say(compileInfo, L.T("Choisis ton fichier / dossier GSC.", "Pick your GSC file / folder."), Ui.Warn); return; }
        }
        catch (Exception ex) { Say(compileInfo, ex.Message, Ui.Danger); return; }
        Busy(true);
        Say(compileInfo, L.T("Compilation en cours...", "Compiling..."), Ui.Warn);
        Core.Trace("=== Compilation (" + Injector.ModeName + ") : " + (useProject ? dir : Injector.MenuName) + " -> " + outPath + " ===");
        string pdir = dir;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            GscCompiler.Result r;
            try { r = GscCompiler.Compile(sources, Core.Trace); }
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
                    gsccBox.Text = outPath; SaveCfg();
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

    void Inject(bool testScript)
    {
        SaveCfg();
        byte[] script; string name;
        string path = gsccBox.Text.Trim();
        try
        {
            if (testScript) { script = Injector.TestScript(); name = L.T("script de test", "test script"); }
            else if (path.Length == 0) { script = Injector.Menu(); name = Injector.MenuName; }
            else { script = File.ReadAllBytes(path); name = Path.GetFileName(path); }
        }
        catch (Exception ex) { Say(stateInfo, L.T("Lecture du .gscbin impossible : ", "Cannot read the .gscbin: ") + ex.Message, Ui.Danger); return; }
        string bad = Injector.CheckScript(script);
        if (bad != null) { Say(stateInfo, L.T("Script refusé : ", "Script rejected: ") + bad, Ui.Danger); return; }
        Run(L.T("Injection en cours...", "Injecting..."), delegate
        {
            Core.Trace("Script : " + name);
            inj.Inject(script);
            try { Core.Dbg.Notify(222, name + L.T(" injecté", " injected")); } catch { }
            Ui.Run(delegate
            {
                Say(stateInfo, L.T("Injection terminée (", "Injection complete (") + name + ", " + Injector.ModeName + (Injector.IsExtinction
                    ? L.T("). Lance une partie Extinction : le script se charge avec la carte.", "). Start an Extinction match: the script loads with the map.")
                    : L.T("). Lance une partie : le script se charge avec la carte.", "). Start a match: the script loads with the map.")), Ui.Accent);
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
        Run(L.T("Vérification...", "Checking..."), delegate
        {
            var s = inj.GetStatus();
            string t; Color c = Ui.C("#9BA4BA");
            if (s.Reset) { t = L.T("Le jeu a rechargé ses scripts : l'injection a été effacée. Réinjecte.", "The game reloaded its scripts: the injection was erased. Inject again."); c = Ui.Warn; }
            else if (s.ScriptIn)
            {
                t = L.T("Injection en place.", "Injection in place.");
                if (!s.TextOk) t += L.T(" Réinjecte pour que les textes du menu s'affichent bien.", " Inject again so the menu texts show properly.");
                if (s.Full > 0) t += L.T(" Une partie a démarré sans le menu : redémarre le jeu puis réinjecte.", " A match started without the menu: restart the game then inject again.");
                if (s.Ours <= 0) t += L.T(" Menu pas encore chargé : lance une partie avec l'injection auto active.", " Menu not loaded yet: start a match with auto injection enabled.");
                else t += L.T(" Menu chargé dans la partie.", " Menu loaded in the match.");
                c = s.Ours > 0 ? Ui.Accent : Ui.Warn;
            }
            else if (!s.Found) t = L.T("Jeu introuvable : attends d'être au menu principal du Multijoueur.", "Game not found: wait until you are in the Multiplayer main menu.");
            else t = L.T("Aucun script en place. Prêt pour l'injection.", "No script loaded. Ready to inject.");
            Ui.Run(delegate { Say(stateInfo, t, c); });
        });
    }

    static System.Threading.Timer autoTimer;
    static volatile bool autoOn, autoPending;
    static byte[] autoScript; static string autoName;
    static Injector autoInj;
    static DateTime autoLastErr = DateTime.MinValue;

    static void SetText(DependencyObject o, string t)
    {
        TextBlock last = null;
        var stack = new System.Collections.Generic.Stack<object>(); stack.Push(o);
        while (stack.Count > 0)
        {
            var x = stack.Pop();
            var tb = x as TextBlock; if (tb != null) { if (last == null) last = tb; continue; }
            var d = x as DependencyObject; if (d == null) continue;
            foreach (var c in LogicalTreeHelper.GetChildren(d)) stack.Push(c);
        }
        if (last != null) last.Text = t;
    }

    static string AutoLabel() { return autoOn ? L.T("Injection auto : ON", "Auto inject: ON") : L.T("Injection auto : OFF", "Auto inject: OFF"); }

    void ToggleAuto()
    {
        if (autoOn)
        {
            autoOn = false; SetText(autoBtn, AutoLabel());
            Core.Trace("Injection automatique arrêtée");
            Say(stateInfo, L.T("Injection automatique arrêtée.", "Auto inject stopped."));
            return;
        }
        if (!Core.Dbg.Connected) { W.Toast(L.T("Connecte-toi d'abord à la PS4 (page Accueil)", "Connect to the PS4 first (Home page)"), Ui.Warn); return; }
        byte[] script; string name;
        if (!PickScript(false, out script, out name)) return;
        autoScript = script; autoName = name; autoOn = true; SetText(autoBtn, AutoLabel());
        if (autoInj == null) autoInj = new Injector(Core);
        var core = Core; var w = W;
        Core.Trace("Injection automatique activée : " + name);
        Say(stateInfo, L.T("Injection automatique active (", "Auto inject active (") + name + L.T(") : laisse l'injecteur ouvert et connecté, lance ta partie privée.", "): keep the injector open and connected, start your private match."), Ui.Accent);
        if (autoTimer == null)
            autoTimer = new System.Threading.Timer(delegate
            {
                if (!autoOn || autoPending || !core.Dbg.Connected || working) return;
                autoPending = true;
                core.Post(delegate
                {
                    try
                    {
                        string r = autoInj.AutoTick(autoScript);
                        if (r == "inject")
                        {
                            try { core.Dbg.Notify(222, autoName + L.T(" réinjecté", " re-injected")); } catch { }
                            Ui.Run(delegate { w.Ok(L.T("Script réinjecté automatiquement", "Script re-injected automatically")); });
                        }
                        else if (r == "-") core.Pid = -1;
                    }
                    catch (Exception ex)
                    {
                        if ((DateTime.Now - autoLastErr).TotalSeconds > 20) { autoLastErr = DateTime.Now; core.Trace("Auto : " + ex.Message + " (le jeu charge ?)"); }
                    }
                    finally { autoPending = false; }
                });
            }, null, 0, 300);
    }

    bool PickScript(bool testScript, out byte[] script, out string name)
    {
        script = null; name = null;
        string path = gsccBox.Text.Trim();
        try
        {
            if (testScript) { script = Injector.TestScript(); name = L.T("script de test", "test script"); }
            else if (path.Length == 0) { script = Injector.Menu(); name = Injector.MenuName; }
            else { script = File.ReadAllBytes(path); name = Path.GetFileName(path); }
        }
        catch (Exception ex) { Say(stateInfo, L.T("Lecture du .gscbin impossible : ", "Cannot read the .gscbin: ") + ex.Message, Ui.Danger); return false; }
        string bad = Injector.CheckScript(script);
        if (bad != null) { Say(stateInfo, L.T("Script refusé : ", "Script rejected: ") + bad, Ui.Danger); return false; }
        return true;
    }
}
