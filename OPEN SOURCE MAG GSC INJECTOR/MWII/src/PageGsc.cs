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
    FlatButton srcMenu, srcProject, cancelBtn, autoBtn, modeMp, modeZm;
    StackPanel projectRow;
    readonly Injector inj;
    static bool useProject = true;
    static bool modeLoaded;
    static volatile bool working;
    static Injector current;

    public PageGsc(MainWindow w) : base(w, L.T("Injecteur GSC", "GSC Injector"), "", L.T("Version de test - MWII 1.08 - Multijoueur", "Test build - MWII 1.08 - Multiplayer"))
    {
        inj = new Injector(Core);
        Grid2.MaxColumns = 2;
        if (!modeLoaded) { modeLoaded = true; Injector.Mode = Injector.GameMode.Multiplayer; }

        var tuto = new StackPanel();
        string[] steps = {
            L.T("Sur la PS4 : active GoldHEN et ps4debug, puis lance MWII 1.08. Choisis Multijoueur, en partie privee locale.",
                "On PS4: enable GoldHEN and ps4debug, then start MWII 1.08. Choose Multiplayer in both the game and injector."),
            L.T("Page Accueil : entre l'IP de ta PS4, clique sur « Envoyer ps4debug », attends la notification, puis « Connecter ».",
                "Home page: enter your PS4's IP, click \"Send ps4debug\", wait for the notification, then \"Connect\"."),
            L.T("Carte Injection : cette version contient le menu multijoueur MWII et un script de test pour verifier son chargement.",
                "Injection card: MWII multiplayer menu and a diagnostic test script."),
            L.T("Le MAG Menu est déjà dans l'injecteur : laisse le champ .gscbin vide. Compilation (facultatif) : seulement pour tes propres scripts GSC.",
                "The MAG Menu is already in the injector: leave the .gscbin field empty. Compilation (optional): only for your own GSC scripts."),
            L.T("Clique Injecter depuis le lobby Multijoueur, meme avant la premiere partie. Le menu sera charge au lancement de la carte.",
                "From the selected mode’s pre-lobby, click Inject, then start a private match as host. The menu loads with the first map launch."),
            L.T("Le bouton Script de test permet de vérifier le chargement : injecte depuis le pré-lobby puis lance la partie. « MAG MWII TEST OK » doit apparaître au spawn. Pour changer de script, compile tes modifications et reinjecte. La nouvelle version sera chargee sur la prochaine carte, sans redemarrer le jeu.",
                "Start a private match as the host (multiplayer): the script loads with the map. \"Test script\" shows \"MAG MWII TEST OK\" when you spawn: handy to check the injection works."),
            L.T("MAG Menu en jeu : R1 + Carré pour ouvrir, L2 / R2 pour monter / descendre, Carré pour valider, Rond ou R3 pour revenir / fermer. L'hôte a le menu et peut le donner aux autres joueurs (All Players).",
                "MAG Menu in game: R1 + Square to open, L2 / R2 to move up / down, Square to select, Circle or R3 to go back / close. The host has the menu and can give it to the other players (All Players)."),
            L.T("En cas de souci, envoie le fichier MAG_GSC_INJECTOR_MWII.log (à côté de l'injecteur) sur le Discord.",
                "If something goes wrong, send the MAG_GSC_INJECTOR_MWII.log file (next to the injector) on the Discord.") };
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
        projectRow.Children.Add(Ui.Wrap(Ui.Labeled(L.T("Fichier .gsc, ou dossier (tous les .gsc mis bout à bout) — fonction main() obligatoire", ".gsc file, or folder (all .gsc files put together) — main() function required"), projectBox),
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
        modeZm = Ui.Ghost("Zombies", "", delegate { SetMode(true); });
        j.Children.Add(Ui.Wrap(modeMp));
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
        bool ext = Injector.IsZombies;
        modeMp.Opacity = ext ? 0.55 : 1; modeZm.Opacity = ext ? 1 : 0.55;
        modeMp.BorderBrush = ext ? Ui.B("#20FFFFFF") : Ui.B(Ui.Accent);
        modeZm.BorderBrush = ext ? Ui.B(Ui.Accent) : Ui.B("#20FFFFFF");
        SetText(srcMenu, Injector.MenuName);
    }

    void SetMode(bool ext)
    {
        if (working || autoPending) { W.Toast(L.T("Patiente, une opération est en cours", "Please wait, an operation is running"), Ui.Warn); return; }
        var m = ext ? Injector.GameMode.Zombies : Injector.GameMode.Multiplayer;
        if (Injector.Mode == m) return;
        Injector.Mode = m;
        W.Cfg["mode"] = ext ? "zm" : "mp"; W.SaveCfg();
        ShowMode();
        Core.Trace("Mode de jeu : " + Injector.ModeName + " (menu intégré : " + Injector.MenuName + ", script remplacé : " + Injector.TargetPath + ")");
        string t = ext
            ? L.T("Mode Zombies : injecte depuis le pré-lobby Zombies, puis lance la carte Zombies en hôte.", "Zombies mode: inject from the Zombies pre-lobby, then start the Zombies map as host.")
            : L.T("Mode multijoueur : MAG Menu multijoueur. Injecte puis lance une partie multijoueur.", "Multiplayer mode: multiplayer MAG Menu. Inject then start a multiplayer match.");
        if (gsccBox.Text.Trim().Length > 0) t += L.T(" Attention : un .gscbin perso est choisi, vérifie qu'il est fait pour ce mode (vide = MAG Menu intégré).", " Note: a custom .gscbin is selected, check it is made for this mode (empty = built-in MAG Menu).");
        if (autoOn)
        {
            byte[] script; string name;
            if (PickScript(false, out script, out name)) { autoScript = script; autoName = name; if (autoInj != null) autoInj.RefreshRequested = true; Core.Trace("Injection automatique : nouveau script " + name); t += L.T(" L'injection auto met le nouveau script en place.", " Auto inject puts the new script in place."); }
        }
        Say(stateInfo, t, Ui.Accent);
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
            try { inj.Inject(script); }
            catch (Injector.ScriptsNotLoadedException)
            {

                Core.Trace("Preparation automatique en attente des scripts communs du lobby.");
                Ui.Run(delegate { StartAuto(script, name); });
                return;
            }
            try { Core.Dbg.Notify(222, name + L.T(" injecté", " injected")); } catch { }
            Ui.Run(delegate
            {
                if (autoOn) { autoScript = script; autoName = name; if (autoInj != null) autoInj.RefreshRequested = false; }
                Say(stateInfo, Injector.IsZombies ? L.T("Point CP [masqué] installé. Lance la carte Zombies : l'appelant [masqué] et sa portée seront vérifiés au chargement. Le bouton État indique si le script a été remplacé.", "CP point [masqué] installed. Start the Zombies map: caller [masqué] and its reach will be checked during loading. Status shows whether the script was replaced.") : L.T("Injection terminée (", "Injection complete (") + name + ", " + Injector.ModeName + L.T("). Nouvelle version preparee. Lance la prochaine carte pour l'appliquer ; aucun redemarrage du jeu necessaire.", "). From the pre-lobby, start the match. If already in a match, restart the map."), Ui.Accent);
                W.Ok(L.T("Injection terminée", "Injection complete"));
            });
        });
    }

    void Remove()
    {
        autoOn = false; SetText(autoBtn, AutoLabel());
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
            if(s.BlockMode==1 && s.CallerState==2) { t=L.T("Chargement CP refuse : l'appelant [masqué] ne correspond pas au profil ou se trouve hors portee IW9. Le script du jeu est conserve. Garde le journal.", "CP loading refused: caller [masqué] differs from the profile or is outside S4 reach. The game script is preserved. Keep the log."); c=Ui.Danger; }
            else if(s.BlockMode==1 && s.CallerState==0) { t=L.T("Point CP [masqué] arme ; attente du chargement de l'initialisation [masqué]. Lance la carte Zombies depuis ce lobby. Aucune substitution avant verification.", "CP point [masqué] armed; waiting for init [masqué]. Start the Zombies map from this lobby. No substitution until validation."); c=Ui.Warn; }
            else if (s.Reset) { t = L.T("La portee du script appelant charge depasse la limite IW9 : cette carte conserve son script original. Reinjecte depuis le lobby puis relance la carte.", "The target script moved: substitution was refused to protect S4 call range. Return to the pre-lobby and inject again."); c = Ui.Warn; }
            else if (s.ScriptIn)
            {
                t = L.T("Détour en place.", "Hook in place.") + (s.Calls >= 0 ? string.Format(L.T(" Le jeu a chargé {0} script(s) depuis, dont {1} fois le script injecté.", " The game loaded {0} script(s) since, {1} of them the injected one."), s.Calls, s.Ours) : "");
                t += s.TextOk ? L.T(" Correctif texte IW9 actif.", " IW9 text hook active.") : L.T(" Correctif texte absent : reinjecte avec la nouvelle version.", " Text fix missing: inject with the new version.");
                if(s.CallerState==0)t += L.T(" Appelant en attente : verification au premier chargement de carte.", " Caller pending: validation at first map load.");
                if(s.TextOk)t += string.Format(L.T(" Textes resolus : {0} ; lectures HUD : {1}.", " Labels resolved: {0}; HUD draw reads: {1}."),s.TextLookups,s.TextReads);
                if (s.Ours == 0) t += L.T(" Menu pas encore chargé : lance une nouvelle carte avec l'injection auto active.", " Menu not loaded yet: start a new map with auto injection enabled.");
                else t += L.T(" L'affichage en jeu reste à vérifier.", " In-game display still needs to be checked.");
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
    static DateTime autoRetryAfter = DateTime.MinValue;

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
        StartAuto(script, name);
    }

    void StartAuto(byte[] script, string name)
    {
        autoScript = script; autoName = name; autoOn = true; SetText(autoBtn, AutoLabel());
        if (autoInj == null) autoInj = new Injector(Core);
        autoInj.RefreshRequested = true;
        var core = Core; var w = W;
        Core.Trace("Injection automatique activée : " + name);
        Say(stateInfo, L.T("En attente du chargement (", "Waiting for map scripts (") + name + L.T(") : lance ta partie privée et attends la confirmation. Si le menu manque, lance ensuite une nouvelle partie.", "): start your private match and wait for confirmation. If the menu is missing, then start a new match."), Ui.Warn);
        if (autoTimer == null)
            autoTimer = new System.Threading.Timer(delegate
            {
                if (!autoOn || autoPending || !core.ReconnectAllowed || working || DateTime.UtcNow < autoRetryAfter) return;
                autoPending = true;
                core.Post(delegate
                {
                    try
                    {
                        if (!autoOn || !core.ReconnectAllowed) return;
                        if (!core.Dbg.Connected)
                        {
                            core.RecoverAutoConnection();
                            autoInj = new Injector(core); autoInj.RefreshRequested = true;
                        }
                        string r = autoInj.AutoTick(autoScript);
                        if (r == "inject")
                        {
                            try { core.Dbg.Notify(222, autoName + L.T(" réinjecté", " re-injected")); } catch { }
                            Ui.Run(delegate
                            {
                                Say(stateInfo, L.T("Menu prepare : lance la prochaine carte pour appliquer cette version, sans redemarrer le jeu.", "Injection confirmed: start the match from the pre-lobby. If already in a match, restart the map."), Ui.Accent);
                                w.Ok(L.T("Script réinjecté automatiquement", "Script re-injected automatically"));
                            });
                        }
                        else if (r == "wait") Ui.Run(delegate { Say(stateInfo, Injector.IsZombies ? L.T("Zombies en attente : lance la partie privée, reste connecté et attends la confirmation. Si le menu manque ensuite, lance une nouvelle partie.", "Waiting for Zombies: start the private match, stay connected and wait for confirmation. If the menu is then missing, start a new match.") : L.T("Scripts indisponibles : ouvre le pré-lobby du mode choisi et reste connecté.", "Scripts unavailable: open the selected mode’s pre-lobby and stay connected."), Ui.Warn); });
                        else if (r == "-") core.Pid = -1;
                    }
                    catch (Exception ex)
                    {
                        autoRetryAfter=DateTime.UtcNow.AddSeconds(5);
                        if(!core.Dbg.Connected)
                        {
                            core.AutoConnectionLost();
                            Ui.Run(delegate { Say(stateInfo, L.T("ps4debug interrompu pendant le chargement : reconnexion automatique en cours. Garde l'injecteur ouvert.", "ps4debug interrupted during loading: reconnecting automatically. Keep the injector open."), Ui.Warn); });
                        }
                        if ((DateTime.Now - autoLastErr).TotalSeconds > 20) { autoLastErr = DateTime.Now; core.Trace("Auto : " + ex.Message + " (le jeu charge ?)"); }
                    }
                    finally { autoPending = false; }
                });
            }, null, 0, 1000);
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
