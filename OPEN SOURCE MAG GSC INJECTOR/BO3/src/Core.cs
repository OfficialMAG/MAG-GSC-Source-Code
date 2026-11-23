// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

public class Core
{
    public static string AppName = "MAG GSC INJECTOR BO3";
    public const string ProcessName = "eboot.bin";
    public const int DebugPort = 744;

    public readonly Ps4Debug Dbg = new Ps4Debug();
    public event Action<string, bool> Log = delegate { };
    public event Action<string> TraceLine = delegate { };
    public event Action StatusChanged = delegate { };
    public string Ip = "", State = "Déconnecté";
    public int Pid = -1;
    public bool Busy;
    public bool Ready { get { return Dbg.Connected && Pid >= 0; } }
    public static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MAG_GSC_INJECTOR_BO3.log");
    static readonly object fileLock = new object();

    readonly BlockingCollection<Action> queue = new BlockingCollection<Action>();
    volatile bool running = true;

    public Core()
    {
        var t = new Thread(Worker) { IsBackground = true, Name = "MAG BO3" };
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
            Busy = true; Ip = ip; Pid = -1; State = "Connexion..."; StatusChanged();
            var sw = Stopwatch.StartNew();
            try
            {
                Dbg.Close();
                Trace("Connexion à ps4debug " + ip + ":" + DebugPort + "...");
                Dbg.Connect(ip, DebugPort);
                Trace("ps4debug connecté (" + sw.ElapsedMilliseconds + " ms), liste des processus...");
                var procs = Dbg.ProcList();
                Trace(procs.Count + " processus : " + string.Join(", ", procs.ConvertAll(delegate (ProcEntry p) { return p.Name; }).ToArray()));
                Pid = -1;
                foreach (var p in procs) if (p.Name == ProcessName) Pid = p.Pid;
                if (Pid < 0)
                {
                    State = "Jeu introuvable";
                    Log(L.T("Connecté à ps4debug, mais Black Ops III n'est pas lancé (" + ProcessName + " introuvable).", "Connected to ps4debug, but Black Ops III is not running (" + ProcessName + " not found)."), true);
                }
                else
                {
                    State = "Connecté";
                    Log(L.T("Connecté : Black Ops III (pid ", "Connected: Black Ops III (pid ") + Pid + ")", false);
                    try { Dbg.Notify(222, AppName + L.T(" connecté", " connected")); } catch { }
                }
            }
            catch (Exception ex)
            {
                Dbg.Close(); Pid = -1; State = "Échec";
                Log(L.T("Connexion impossible : ", "Connection failed: ") + ex.Message, true);
            }
            Busy = false; StatusChanged();
        });
    }

    public void Disconnect() { Post(delegate { Dbg.Close(); Pid = -1; State = "Déconnecté"; Trace("Déconnecté"); StatusChanged(); }); }

    public static byte[] Payload() { return Injector.Resource("ps4debug.bin"); }

    public void SendPayload(string ip, int port, Action<bool, string> done)
    {
        Post(delegate
        {
            try
            {
                var p = Payload();
                Trace("Envoi de ps4debug (" + p.Length + " o) à " + ip + ":" + port + "...");
                Ps4Debug.SendPayload(ip, port, p);
                Trace("ps4debug envoyé");
                done(true, null);
            }
            catch (Exception ex) { Trace("Envoi de ps4debug échoué : " + ex.Message); done(false, ex.Message); }
        });
    }
}
