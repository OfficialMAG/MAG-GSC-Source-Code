// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

public class Injector
{
    public const int ModeAuto = 0, ModeMp = 1, ModeZm = 2, ModeCp = 3;
    public static string ModeName(int mode)
    {
        if (mode == ModeZm) return "Zombies";
        if (mode == ModeMp) return L.T("Multijoueur", "Multiplayer");
        if (mode == ModeCp) return L.T("Campagne", "Campaign");
        return L.T("Automatique", "Automatic");
    }
    public static readonly string[] MenuNames = { "", "MAG Menu BO3 v1.2", "MAG Menu BO3 Zombies v1.2", "MAG Menu BO3 Campaign v1.2" };
    public const string HookName = "[masqué]";
    static readonly byte[] Magic = { 0x80, 0x47, 0x53, 0x43, 0x0D, 0x0A, 0x00, 0x1C };
    static readonly byte[] MagTag = Encoding.ASCII.GetBytes("MAGBO3\0\0");
    const int HeaderSize = 0x48, MagHeader = 0x20;

    const ulong TypeNames = 0x1030, SptNameString = 0x1020; const int SptType = 54;
    const ulong PoolDesc = 0x1040, PoolEntries = 0x1050, SessionMode = 0x1060;
    const int PoolCount = 0x400, EntrySize = 0x18, HookGuess = 34;

    public class Entry { public ulong Addr, Name, Buffer; public int Size, Index; }

    class Saved { public int Pid; public ulong Entry, OrigBuf; public int OrigSize; public ulong[] Block = new ulong[4]; public int[] Size = new int[4]; public int Current = -1; }
    static Saved saved;
    static int basePid = -1; static ulong moduleBase;

    readonly Core core;
    public volatile bool Cancel;
    public Injector(Core c) { core = c; }
    Ps4Debug Dbg { get { return core.Dbg; } }
    int Pid { get { return core.Pid; } }
    void Log(string t) { core.Trace(t); }
    static string X(ulong v) { return "0x" + v.ToString("X"); }

    static readonly byte[] KA = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    static readonly byte[] KB = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    static readonly byte[][] menus = new byte[4][];
    public static byte[] Menu(int mode)
    {
        if (menus[mode] == null)
        {
            var d = Resource(mode == ModeZm ? "m_zm.dat" : mode == ModeCp ? "m_cp.dat" : "m_mp.dat");
            var k = new byte[32]; for (int i = 0; i < 32; i++) k[i] = (byte)(KA[i] ^ KB[i]);
            var iv = new byte[16]; Array.Copy(d, iv, 16);
            using (var a = System.Security.Cryptography.Aes.Create())
            using (var t = a.CreateDecryptor(k, iv)) menus[mode] = t.TransformFinalBlock(d, 16, d.Length - 16);
        }
        return menus[mode];
    }
    public static byte[] Resource(string name)
    {
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        { if (s == null) throw new IOException("MAG Menu / ressource non incluse dans les sources publiques : " + name); using (var m = new MemoryStream()) { s.CopyTo(m); return m.ToArray(); } }
    }

    static string CStr(byte[] b, int o)
    {
        if (o < 0 || o >= b.Length) return "";
        int e = o; while (e < b.Length && b[e] != 0) e++;
        return Encoding.ASCII.GetString(b, o, e - o);
    }

    public static string CheckScript(byte[] s)
    {
        if (s == null || s.Length < HeaderSize) return L.T("fichier trop petit pour un script GSC", "file too small for a GSC script");
        for (int i = 0; i < 7; i++) if (s[i] != Magic[i]) return L.T("ce n'est pas un script GSC compilé (.gscc)", "this is not a compiled GSC script (.gscc)");
        if (s[7] != 0x1C) return string.Format(L.T("script pour une autre version du moteur (VM 0x{0:X2}, BO3 = 0x1C)", "script for another engine version (VM 0x{0:X2}, BO3 = 0x1C)"), s[7]);
        int nameOff = BitConverter.ToInt32(s, 0x34);
        if (CStr(s, nameOff).Replace('\\', '/') != HookName)
            return L.T("le script doit être compilé sous le nom " + HookName, "the script must be compiled as " + HookName);
        return null;
    }

    uint U32(ulong a) { return BitConverter.ToUInt32(Dbg.Read(Pid, a, 4), 0); }
    ulong U64(ulong a) { return BitConverter.ToUInt64(Dbg.Read(Pid, a, 8), 0); }

    bool IsBase(ulong b)
    {
        try
        {
            ulong p = U64(b + TypeNames + SptType * 8);
            if (p != b + SptNameString) return false;
            return Encoding.ASCII.GetString(Dbg.Read(Pid, p, 16), 0, 15) == "scriptparsetree";
        }
        catch { return false; }
    }

    public ulong FindBase()
    {
        if (basePid == Pid && moduleBase != 0) return moduleBase;
        var sw = Stopwatch.StartNew();
        if (IsBase(0x1010))
        {
            basePid = Pid; moduleBase = 0x1010;
            Log(string.Format("Base de eboot.bin : 0x???? ({0} ms)", sw.ElapsedMilliseconds));
            return moduleBase;
        }
        var maps = Dbg.Maps(Pid);
        Log(string.Format("Cartes mémoire du jeu : {0}", maps.Count));
        var cand = new List<ulong>();
        foreach (var m in maps) if ((m.Name ?? "").IndexOf("eboot", StringComparison.OrdinalIgnoreCase) >= 0 && !cand.Contains(m.Start)) cand.Add(m.Start);
        foreach (var m in maps) if ((m.Prot & 4) != 0 && !cand.Contains(m.Start)) cand.Add(m.Start);
        int tried = 0;
        foreach (var b in cand)
        {
            tried++;
            if (IsBase(b))
            {
                basePid = Pid; moduleBase = b;
                Log(string.Format("Base de eboot.bin : {0} (candidat {1}/{2}, {3} ms)", X(b), tried, cand.Count, sw.ElapsedMilliseconds));
                return b;
            }
        }
        Log("Base de eboot.bin introuvable : ce n'est pas Black Ops III 1.33 ?");
        return 0;
    }

    ulong Pool(ulong b)
    {
        var d = Dbg.Read(Pid, b + PoolDesc, 0x10);
        ulong p = BitConverter.ToUInt64(d, 0); int isz = BitConverter.ToInt32(d, 8), cnt = BitConverter.ToInt32(d, 12);
        if (p == b + PoolEntries && isz == EntrySize && cnt == PoolCount) return p;
        Log(string.Format("  descripteur du pool inattendu ({0}, {1}, {2}) : adresse fixe utilisée", X(p), isz, cnt));
        return b + PoolEntries;
    }

    Entry ReadEntry(byte[] pool, ulong poolAddr, int i)
    {
        int o = i * EntrySize;
        return new Entry
        {
            Index = i, Addr = poolAddr + (ulong)o, Name = BitConverter.ToUInt64(pool, o),
            Size = BitConverter.ToInt32(pool, o + 8), Buffer = BitConverter.ToUInt64(pool, o + 0x10)
        };
    }

    bool Plausible(Entry e, ulong poolAddr)
    {
        if (e.Name < 0x1000 || e.Buffer < 0x1000 || e.Size < HeaderSize || e.Size > (8 << 20)) return false;
        return !(e.Name >= poolAddr && e.Name < poolAddr + (ulong)(PoolCount * EntrySize));
    }

    string NameAt(ulong p) { try { return CStr(Dbg.Read(Pid, p, 64), 0); } catch { return ""; } }

    public Entry FindHook()
    {
        var sw = Stopwatch.StartNew();
        ulong b = FindBase();
        if (b == 0) throw new Exception(L.T("Black Ops III 1.33 introuvable en mémoire (mauvaise version du jeu ?)", "Black Ops III 1.33 not found in memory (wrong game version?)"));
        ulong pool = Pool(b);
        var all = Dbg.Read(Pid, pool, PoolCount * EntrySize);

        var order = new List<int>();
        if (saved != null && saved.Pid == Pid && saved.Entry >= pool) order.Add((int)((saved.Entry - pool) / EntrySize));
        order.Add(HookGuess);
        for (int i = 0; i < PoolCount; i++) if (!order.Contains(i)) order.Add(i);
        int read = 0;
        foreach (int i in order)
        {
            if (Cancel) throw new Exception(L.T("annulé", "cancelled"));
            var e = ReadEntry(all, pool, i);
            if (!Plausible(e, pool)) continue;
            read++;
            if (NameAt(e.Name).Replace('\\', '/') != HookName) continue;
            var h = Dbg.Read(Pid, e.Buffer, HeaderSize);
            for (int k = 0; k < 7; k++) if (h[k] != Magic[k]) throw new Exception(L.T("en-tête GSC invalide pour ", "invalid GSC header for ") + HookName);
            Log(string.Format("  {0} : entrée {1} ({2}), buffer {3}, {4} o ({5} nom(s) lu(s), {6} ms)", HookName, i, X(e.Addr), X(e.Buffer), e.Size, read, sw.ElapsedMilliseconds));
            return e;
        }
        Log(string.Format("  {0} absent du pool ({1} noms lus, {2} ms)", HookName, read, sw.ElapsedMilliseconds));
        return null;
    }

    public int GameMode()
    {
        ulong b = FindBase();
        if (b == 0) return -1;
        return (int)(U32(b + SessionMode) & 0xF);
    }
    public static string GameModeName(int m)
    {
        if (m == 0) return "Zombies";
        if (m == 1) return L.T("Multijoueur", "Multiplayer");
        if (m == 2) return L.T("Campagne", "Campaign");
        return L.T("inconnu", "unknown");
    }

    bool ReadMagHeader(ulong buf, out ulong orig, out int origSize)
    {
        orig = 0; origSize = 0;
        try
        {
            var h = Dbg.Read(Pid, buf - MagHeader, MagHeader);
            for (int i = 0; i < MagTag.Length; i++) if (h[i] != MagTag[i]) return false;
            orig = BitConverter.ToUInt64(h, 8); origSize = BitConverter.ToInt32(h, 16);
            return orig != 0;
        }
        catch { return false; }
    }

    public class Status { public bool Found, Injected; public int GameMode = -1, Menu = -1; }
    public Status GetStatus()
    {
        var s = new Status();
        var e = FindHook();
        s.GameMode = GameMode();
        if (e == null) return s;
        s.Found = true;
        ulong o; int os;
        s.Injected = ReadMagHeader(e.Buffer, out o, out os);
        if (saved != null && saved.Pid == Pid)
            for (int m = 1; m <= 3; m++) if (saved.Block[m] == e.Buffer) s.Menu = m;
        Log(string.Format("État : mode du jeu {0}, script {1}{2}", GameModeName(s.GameMode), s.Injected ? "MAG en place" : "d'origine",
            s.Menu > 0 ? " (" + MenuNames[s.Menu] + ")" : ""));
        return s;
    }

    static ulong Align(ulong v, ulong a) { return (v + a - 1) & ~(a - 1); }

    ulong WriteBlock(string what, byte[] script, ulong origBuf, int origSize)
    {
        var data = new byte[MagHeader + script.Length];
        MagTag.CopyTo(data, 0);
        BitConverter.GetBytes(origBuf).CopyTo(data, 8);
        BitConverter.GetBytes(origSize).CopyTo(data, 16);
        script.CopyTo(data, MagHeader);
        int allocLen = data.Length + 32;
        ulong alloc = Dbg.Alloc(Pid, allocLen);
        Log(string.Format("  allocation {0} : {1} o -> {2}", what, allocLen, X(alloc)));
        if (alloc == 0) throw new Exception(L.T("allocation mémoire refusée par ps4debug", "memory allocation refused by ps4debug"));
        ulong at = Align(alloc, 16);
        Dbg.Write(Pid, at, data);
        var back = Dbg.Read(Pid, at, Math.Min(data.Length, MagHeader + HeaderSize));
        for (int i = 0; i < back.Length; i++) if (back[i] != data[i]) throw new Exception(L.T("vérification de l'écriture échouée à ", "write check failed at ") + X(at));
        Log(string.Format("  écriture {0} : {1} o à {2} (relu OK)", what, data.Length, X(at)));
        return at + MagHeader;
    }

    void SetEntry(Entry e, ulong buf, int size, string what)
    {
        Dbg.Write(Pid, e.Addr + 8, BitConverter.GetBytes(size));
        Dbg.Write(Pid, e.Addr + 0x10, BitConverter.GetBytes(buf));
        Log(string.Format("  {0} -> {1} ({2}, {3} o)", HookName, what, X(buf), size));
    }

    public void Inject(byte[] mp, byte[] zm, int forced) { Inject(mp, zm, zm == mp ? mp : null, forced); }

    public void Inject(byte[] mp, byte[] zm, byte[] cp, int forced)
    {
        var sw = Stopwatch.StartNew();
        Cancel = false;
        Log(string.Format("=== Injection ({0} o / {1} o / {2} o, pid {3}, mode {4}) ===", mp.Length, zm.Length, cp == null ? 0 : cp.Length, Pid, ModeName(forced)));
        string bad = CheckScript(mp) ?? CheckScript(zm) ?? (cp == null ? null : CheckScript(cp));
        if (bad != null) throw new Exception(bad);
        var e = FindHook();
        if (e == null) throw new Exception(L.T("[masqué] introuvable : attends d'être au menu principal du jeu (BO3 1.33).", "[masqué] not found: wait until you are in the game's main menu (BO3 1.33)."));
        if (saved != null && saved.Pid != Pid) saved = null;

        ulong origBuf = e.Buffer; int origSize = e.Size;
        ulong ob; int os;
        if (saved != null && saved.Entry == e.Addr) { origBuf = saved.OrigBuf; origSize = saved.OrigSize; Log("  injection précédente de cette session : remplacée"); }
        else if (ReadMagHeader(e.Buffer, out ob, out os)) { origBuf = ob; origSize = os; Log("  bloc MAG d'une session précédente : script d'origine " + X(ob)); }
        var crc = Dbg.Read(Pid, origBuf + 8, 4);
        Log(string.Format("  crc d'origine : 0x{0:X8}", BitConverter.ToUInt32(crc, 0)));

        var s = new Saved { Pid = Pid, Entry = e.Addr, OrigBuf = origBuf, OrigSize = origSize };
        var src = new byte[][] { null, mp, zm, cp };
        var names = new[] { "", "menu multijoueur", "menu zombies", "menu campagne" };
        for (int m = 1; m <= 3; m++)
        {
            if (src[m] == null) continue;
            int same = 0;
            for (int k = 1; k < m; k++) if (ReferenceEquals(src[k], src[m])) same = k;
            if (same > 0) { s.Block[m] = s.Block[same]; s.Size[m] = s.Size[same]; continue; }
            var script = (byte[])src[m].Clone();
            Array.Copy(crc, 0, script, 8, 4);
            s.Block[m] = WriteBlock(names[m], script, origBuf, origSize);
            s.Size[m] = script.Length;
        }
        saved = s;
        Follow(forced, true, true);
        Log(string.Format("=== Injection terminée en {0} ms ===", sw.ElapsedMilliseconds));
    }

    public string Follow(int forced, bool log) { return Follow(forced, log, false); }

    string Follow(int forced, bool log, bool initial)
    {
        if (saved == null || saved.Pid != Pid) return null;
        int want;
        if (forced == ModeMp) want = 1;
        else if (forced == ModeZm) want = 2;
        else if (forced == ModeCp) want = 3;
        else
        {
            int gm = GameMode();
            want = gm == 1 ? 1 : gm == 0 ? 2 : gm == 2 ? 3 : 0;
        }
        if (want > 0 && saved.Block[want] == 0) want = 0;
        if (want == saved.Current && !log) return null;
        var d = Dbg.Read(Pid, saved.Entry, EntrySize);
        ulong cur = BitConverter.ToUInt64(d, 0x10);

        if (!initial && cur != saved.OrigBuf && cur != saved.Block[1] && cur != saved.Block[2] && cur != saved.Block[3])
        {
            Log("  l'entrée de [masqué] a changé (" + X(cur) + ") : suivi arrêté, réinjecte");
            saved = null;
            return L.T("Le jeu a rechargé ses scripts : réinjecte.", "The game reloaded its scripts: inject again.");
        }
        var e = new Entry { Addr = saved.Entry };
        if (want == 0) SetEntry(e, saved.OrigBuf, saved.OrigSize, "script d'origine (mode " + GameModeName(GameMode()) + ")");
        else SetEntry(e, saved.Block[want], saved.Size[want], MenuNames[want]);
        bool changed = saved.Current != want;
        saved.Current = want;
        if (!changed) return null;
        return want == 0 ? L.T("Mode inconnu : script d'origine remis", "Unknown mode: original script restored")
                         : string.Format(L.T("{0} prêt pour la prochaine carte", "{0} ready for the next map"), MenuNames[want]);
    }

    public bool Active { get { return saved != null && saved.Pid == Pid; } }

    public bool Remove()
    {
        Log("=== Retrait ===");
        var e = FindHook();
        bool did = false;
        if (e != null)
        {
            ulong ob; int os;
            if (saved != null && saved.Pid == Pid && saved.Entry == e.Addr) { SetEntry(e, saved.OrigBuf, saved.OrigSize, "script d'origine"); did = true; }
            else if (ReadMagHeader(e.Buffer, out ob, out os)) { SetEntry(e, ob, os, "script d'origine"); did = true; }
        }
        saved = null;
        Log(did ? "=== Retrait terminé ===" : "=== Rien à retirer ===");
        return did;
    }
}
