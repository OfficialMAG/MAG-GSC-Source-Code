// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

class PageHome : Page
{
    TextBox ip, port; StackPanel steps; TextBlock logBox, payloadInfo; FlatButton connectBtn;
    public PageHome(MainWindow w) : base(w, L.T("Accueil", "Home"), "", "")
    {
        var hero = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hero.Children.Add(Brand.Name(36));
        var cr = Brand.Credit(14); cr.Margin = new Thickness(0, 10, 0, 4); cr.HorizontalAlignment = HorizontalAlignment.Left; hero.Children.Add(cr);
        hero.Children.Add(Ui.Wrap(Ui.Primary(L.T("Aller à l'injecteur", "Go to the injector"), "", delegate { W.ShowPage<PageGsc>(); })));
        Grid2.Children.Add(Ui.Card(L.T("Bienvenue", "Welcome"), "", Brand.Hero(hero, 104)));

        var c = new StackPanel();
        ip = Ui.Field(W.CfgGet("ip", "192.168.1.100"), 200);
        port = Ui.Field(W.CfgGet("payload_port", "9090"), 90);
        var row = new WrapPanel();
        row.Children.Add(Ui.Labeled(L.T("IP de la PS4", "PS4 IP address"), ip));
        row.Children.Add(Ui.Labeled(L.T("Port du payload", "Payload port"), port));
        c.Children.Add(row);
        connectBtn = Ui.Primary(L.T("Connecter", "Connect"), "", delegate { Save(); Core.Connect(ip.Text.Trim()); });
        c.Children.Add(Ui.Wrap(
            Ui.Ghost(L.T("Envoyer ps4debug", "Send ps4debug"), "", SendPayload),
            connectBtn,
            Ui.Ghost(L.T("Déconnecter", "Disconnect"), "", delegate { Core.Disconnect(); })));
        payloadInfo = Ui.T("", 13, Ui.TextSub); payloadInfo.TextWrapping = TextWrapping.Wrap; payloadInfo.Margin = new Thickness(0, 4, 0, 2);
        c.Children.Add(payloadInfo);
        steps = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        c.Children.Add(steps);
        Grid2.Children.Add(Ui.Card(L.T("Connexion PS4", "PS4 connection"), "", c));

        Grid2.Children.Add(Brand.ContactCard(W));

        logBox = Ui.T("", 12, Ui.TextSub, Ui.Mono); logBox.TextWrapping = TextWrapping.Wrap;
        var logScroll = new ScrollViewer { Content = logBox, MaxHeight = 240, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid2.Children.Add(Ui.Card(L.T("Journal", "Log"), "", logScroll));
        Action showLog = delegate { var lines = W.LogLines; int s0 = Math.Max(0, lines.Count - 60); logBox.Text = string.Join("\n", lines.GetRange(s0, lines.Count - s0).ToArray()); logScroll.ScrollToEnd(); };
        W.LogChanged += showLog; showLog();
        OnStatus();
    }

    void Save() { W.Cfg["ip"] = ip.Text.Trim(); W.Cfg["payload_port"] = port.Text.Trim(); W.SaveCfg(); }

    void SendPayload()
    {
        Save();
        int p;
        if (!int.TryParse(port.Text.Trim(), out p) || p < 1 || p > 65535) { W.Toast(L.T("Port invalide", "Invalid port"), Ui.Danger); return; }
        string a = ip.Text.Trim();
        payloadInfo.Text = L.T("Envoi de ps4debug...", "Sending ps4debug..."); payloadInfo.Foreground = Ui.B(Ui.Warn);
        Core.SendPayload(a, p, delegate (bool ok, string err)
        {
            Ui.Run(delegate
            {
                if (ok)
                {
                    payloadInfo.Text = L.T("ps4debug envoyé. Attends la notification sur la PS4, puis clique sur « Connecter ».", "ps4debug sent. Wait for the notification on the PS4, then click \"Connect\".");
                    payloadInfo.Foreground = Ui.B(Ui.Accent); W.Ok(L.T("ps4debug envoyé", "ps4debug sent"));
                }
                else { payloadInfo.Text = L.T("Envoi échoué : ", "Sending failed: ") + err; payloadInfo.Foreground = Ui.B(Ui.Danger); }
            });
        });
    }

    void Step(string label, string value, bool ok, bool pending)
    {
        var r = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var ic = Ui.Icon(ok ? "" : pending ? "" : "", 13, ok ? Ui.B(Ui.Accent) : pending ? Ui.B(Ui.Warn) : Ui.TextDim);
        r.Children.Add(ic);
        var l = Ui.T(label, 13, Ui.TextSub); Grid.SetColumn(l, 1); r.Children.Add(l);
        var v = Ui.T(value, 13, ok ? Ui.TextMain : Ui.TextDim, Ui.Mono); Grid.SetColumn(v, 2); v.TextTrimming = TextTrimming.CharacterEllipsis; r.Children.Add(v);
        steps.Children.Add(r);
    }

    public override void OnStatus()
    {
        steps.Children.Clear();
        bool con = Core.Dbg.Connected;
        Step("ps4debug", con ? Core.Ip + " : " + Core.DebugPort : L.T("non connecté", "not connected"), con, Core.Busy);
        Step("Black Ops 4", con && Core.Pid >= 0 ? Core.ProcessName + " (pid " + Core.Pid + ")" : "-", con && Core.Pid >= 0, Core.Busy);
        connectBtn.Enabled = !Core.Busy; connectBtn.Opacity = Core.Busy ? 0.5 : 1;
    }
}

class PageAbout : Page
{
    public PageAbout(MainWindow w) : base(w, L.T("À propos", "About"), "", "")
    {
        var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        st.Children.Add(Brand.Name(30));
        var cr = Brand.Credit(18); cr.Margin = new Thickness(0, 6, 0, 10); cr.HorizontalAlignment = HorizontalAlignment.Left; st.Children.Add(cr);
        var game = Ui.T("Call of Duty : Black Ops 4  •  PS4  •  " + L.T("mise à jour 1.26", "update 1.26") + "  •  " + L.T("Multijoueur et Zombies", "Multiplayer and Zombies"), 13, Ui.TextSub, Ui.Title);
        game.TextWrapping = TextWrapping.Wrap; st.Children.Add(game);
        Grid2.Children.Add(Ui.Card("MAG GSC INJECTOR", "", Brand.Hero(st, 84)));
        Grid2.Children.Add(Brand.ContactCard(W));
        Grid2.Children.Add(Ui.FxCard());

        var keys = new StackPanel();
        string[,] k = {
            { L.T("R1 + Carré", "R1 + Square"), L.T("ouvrir le menu", "open the menu") },
            { "L2 / R2", L.T("monter / descendre", "move up / down") },
            { L.T("Croix haut / bas", "D-pad up / down"), L.T("monter / descendre", "move up / down") },
            { L.T("Carré", "Square"), L.T("valider", "select") },
            { L.T("Rond", "Circle"), L.T("revenir / fermer", "back / close") } };
        for (int i = 0; i < k.GetLength(0); i++)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.Children.Add(Ui.T(k[i, 0], 13.5, Ui.B(Ui.Accent), Ui.Title, FontWeights.SemiBold));
            var v = Ui.T(k[i, 1], 13.5, Ui.TextMain); Grid.SetColumn(v, 1); g.Children.Add(v);
            keys.Children.Add(g);
        }
        Grid2.Children.Add(Ui.Card(L.T("Commandes du MAG Menu", "MAG Menu controls"), "", keys));
    }
}
