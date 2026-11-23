// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

public class Core
{
    public static string AppName = "MAG GSC INJECTOR VANGUARD";
    public static string ProcessName = "eboot.bin";

    public static readonly string[] ProcessNames = { "eboot.bin" };
    public const int DebugPort = 744;

    public readonly Ps4Debug Dbg = new Ps4Debug();
    public event Action<string, bool> Log = delegate { };
    public event Action<string> TraceLine = delegate { };
    public event Action StatusChanged = delegate { };
    public string Ip = "", State = "Déconnecté";
    public int Pid = -1;
    public bool Busy;
    public volatile bool ReconnectAllowed;
    public bool Ready { get { return Dbg.Connected && Pid >= 0; } }
    public static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MAG_GSC_INJECTOR_VANGUARD.log");
    static readonly object fileLock = new object();

    readonly BlockingCollection<Action> queue = new BlockingCollection<Action>();
    volatile bool running = true;

    public Core()
    {
        var t = new Thread(Worker) { IsBackground = true, Name = "MAG Vanguard" };
        t.Start();
        Log += delegate (string text, bool err) { WriteFile((err ? "ERREUR " : "") + text); };
        string build = "?";
        try { build = File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location).ToString("dd/MM/yyyy HH:mm"); } catch { }
        WriteFile("---------- " + AppName + " démarré (" + DateTime.Now.ToString("dd/MM/yyyy") + ", exe du " + build + ") ----------");
    }
    public void Stop() { running = false; queue.Add(delegate { }); }
    public void Post(Action a) { queue.Add(a); }

    static void WriteFile(string text)
    {
        try { lock (fileLock) File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + text + Environment.NewLine); } catch { }
    }
    public void Trace(string text) { WriteFile(text); TraceLine(text); }

    void Worker()
    {
        while (running)
        {
            Action a;
            try { a = queue.Take(); } catch { return; }
            try { a(); }
            catch (Exception ex)
            {
                Log(ex.Message, true);
                if (!Dbg.Connected && Pid >= 0) { Pid = -1; State = "Connexion perdue"; StatusChanged(); }
            }
        }
    }

    public void Connect(string ip)
    {
        Post(delegate
        {
            Busy = true; Ip = ip; ReconnectAllowed = true; Pid = -1; State = "Connexion..."; StatusChanged();
            var sw = Stopwatch.StartNew();
            string stage = "TCP " + DebugPort;
            try
            {
                Dbg.Close();
                Trace("Connexion à ps4debug " + ip + ":" + DebugPort + "...");
                Dbg.Connect(ip, DebugPort);
                Trace("Port TCP ouvert (" + sw.ElapsedMilliseconds + " ms), verification de la version ps4debug...");
                stage = L.T("version ps4debug", "ps4debug version");
                string version = Dbg.Version();
                Trace("ps4debug confirme : version " + version + ". Demande de liste des processus...");
                stage = L.T("liste des processus", "process list");
                var procs = Dbg.ProcList();
                Trace(procs.Count + " processus : " + string.Join(", ", procs.ConvertAll(delegate (ProcEntry p) { return p.Name; }).ToArray()));
                Pid = -1;
                foreach (var want in ProcessNames)
                {
                    foreach (var p in procs) if (p.Name == want) { Pid = p.Pid; ProcessName = want; break; }
                    if (Pid >= 0) break;
                }
                bool sp = procs.Exists(delegate (ProcEntry p) { return p.Name == "eboot.bin"; });
                if (Pid < 0)
                {
                    State = "Jeu introuvable";
                    if (sp) Log(L.T("Connecté à ps4debug, mais Vanguard est en campagne : va dans le Multijoueur ou les Zombies puis reconnecte-toi.", "Connected to ps4debug, but Vanguard is in the campaign: go to Multiplayer or Zombies and connect again."), true);
                    else Log(L.T("Connecté à ps4debug, mais Call of Duty: Vanguard n'est pas lancé (eboot.bin introuvable).", "Connected to ps4debug, but Call of Duty: Vanguard is not running (eboot.bin not found)."), true);
                }
                else
                {
                    State = "Connecté";
                    Log(L.T("Connecté : Call of Duty: Vanguard (" + ProcessName + ", pid ", "Connected: Call of Duty: Vanguard (" + ProcessName + ", pid ") + Pid + ")", false);
                    try { Dbg.Notify(222, AppName + L.T(" connecté", " connected")); } catch { }
                }
            }
            catch (Exception ex)
            {
                Dbg.Close(); Pid = -1; State = "Échec";
                Log(L.T("Connexion impossible [", "Connection failed [") + stage + "] : " + ex.Message, true);
                if (stage.StartsWith("TCP")) Log(L.T("Le service ps4debug sur 744 n'est pas joignable. Le port 9090 sert uniquement a envoyer le payload. Verifie l'IP et la notification de demarrage ps4debug sur la PS4.", "The ps4debug service on 744 cannot be reached. Port 9090 only uploads payloads. Check the IP and the ps4debug startup notification on the PS4."), true);
                else if (stage == L.T("version ps4debug", "ps4debug version")) Log(L.T("Le port 744 s'ouvre, mais le protocole ps4debug n'a pas ete confirme. Redemarre completement la PS4 puis recharge GoldHEN et ps4debug.", "Port 744 opens, but the ps4debug protocol was not confirmed. Fully restart the PS4, then reload GoldHEN and ps4debug."), true);
                else Log(L.T("ps4debug repond a la version mais pas a la liste des processus. Conserve ce journal et indique le firmware PS4 ainsi que la notification du payload.", "ps4debug answers the version request but not the process list. Keep this log and report the PS4 firmware and payload notification."), true);
            }
            Busy = false; StatusChanged();
        });
    }

    public void RecoverAutoConnection()
    {
        if (!ReconnectAllowed) throw new OperationCanceledException("Reconnexion annulee");
        Dbg.Close(); Pid = -1; State = "Reconnexion..."; StatusChanged();
        try
        {
            Dbg.Connect(Ip, DebugPort, 3000, 15000);
            string version = Dbg.Version();
            var procs = Dbg.ProcList();
            foreach(var p in procs) if(p.Name=="eboot.bin") { Pid=p.Pid; break; }
            if(Pid<0)throw new IOException("Vanguard en attente : eboot.bin absent");
            if (!ReconnectAllowed) throw new OperationCanceledException("Reconnexion annulee");
            State="Connecté"; StatusChanged();
            Trace("Auto : nouvelle connexion ps4debug verifiee (version "+version+", pid "+Pid+"). Reprise du controle des scripts.");
        }
        catch { Dbg.Close(); Pid=-1; State="Reconnexion en attente"; StatusChanged(); throw; }
    }
    public void AutoConnectionLost()
    {
        Dbg.Close(); Pid=-1; State="Reconnexion en attente"; StatusChanged();
    }
    public void Disconnect() { ReconnectAllowed=false; Post(delegate { Dbg.Close(); Pid = -1; State = "Déconnecté"; Trace("Déconnecté"); StatusChanged(); }); }

    public static byte[] Payload() { return Injector.Resource("ps4debug.bin"); }

    public void SendPayload(string ip, int port, Action<bool, string> done)
    {
        ReconnectAllowed=false;
        Post(delegate
        {
            Busy = true; StatusChanged();
            try
            {
                Dbg.Close(); Pid = -1; State = "Deconnecte"; StatusChanged();
                string running = null;
                try { running = Ps4Debug.ProbeVersion(ip, DebugPort, 1000); } catch { }
                if (running != null)
                {
                    string message = L.T("ps4debug est deja actif (version ", "ps4debug is already running (version ") + running + L.T("). Clique sur Connecter.", "). Click Connect.");
                    Trace(message); done(true, message); return;
                }
                var p = Payload();
                Trace("Envoi de ps4debug (" + p.Length + " o) à " + ip + ":" + port + "...");
                Ps4Debug.SendPayload(ip, port, p);
                Trace("Fichier envoye au chargeur. Verification du demarrage de ps4debug sur " + DebugPort + "...");
                string last = null;
                var wait = Stopwatch.StartNew();
                while (wait.ElapsedMilliseconds < 12000)
                {
                    try
                    {
                        string version = Ps4Debug.ProbeVersion(ip, DebugPort, 1000);
                        string message = L.T("ps4debug actif et verifie (version ", "ps4debug is running and verified (version ") + version + L.T("). Clique sur Connecter.", "). Click Connect.");
                        Trace(message); done(true, message); return;
                    }
                    catch (Exception ex) { last = ex.Message; }
                    Thread.Sleep(500);
                }
                throw new IOException(L.T("Fichier envoye sur 9090, mais ps4debug ne repond pas sur 744 apres 12 s. Regarde la notification PS4 ; indique le firmware et son message exact. Dernier resultat : ", "File uploaded on 9090, but ps4debug did not respond on 744 within 12 s. Check the PS4 notification and report the firmware and exact message. Last result: ") + last);
            }
            catch (Exception ex) { Trace("Envoi de ps4debug échoué : " + ex.Message); done(false, ex.Message); }
            finally { Busy = false; StatusChanged(); }
        });
    }
}
