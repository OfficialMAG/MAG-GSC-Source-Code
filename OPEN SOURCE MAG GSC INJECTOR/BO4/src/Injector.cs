// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

public class Injector
{
    public const int ModeMp = 0, ModeZm = 1;

    public static readonly ulong[] HashBbs = { 0x10B0, 0x10D0 };
    public static readonly string[] NameBbs = { "[masqué]", "[masqué]" };
    public static readonly string[] MenuFiles = { "m_mp.dat", "m_zm.dat" };
    public static readonly string[] MenuNames = { "MAG Menu v1.5", "MAG Menu Zombies v1.2" };
    public static string ModeName(int mode) { return mode == ModeZm ? "Zombies" : L.T("Multijoueur", "Multiplayer"); }
    public const ulong HashTarget = 0x10C0;
    public const string NameTarget = "[masqué]";
    static readonly byte[] Magic = { 0x80, 0x47, 0x53, 0x43, 0x0D, 0x0A, 0x00, 0x36 };
    const int HeaderSize = 0x60, AssetSize = 0x20, Chunk = 4 << 20;
    const int OriginalTargetSize = 0x2D8;

    const int SptType = 48;
    const ulong TypeNames = 0x1050, SptNameString = 0x1040;
    const ulong Buckets = 0x1060, Entries = 0x1070, NextTable = 0x1090, Overrides = 0x1080;
    const int BucketCount = 0x1000;

    public class Entry { public ulong Addr, Buffer; public int Size; }

    public class Found { public Entry Bb, Other, Target; }

    class Saved
    {
        public int Pid; public ulong TargetEntry, TargetBuf; public int TargetSize;
        public ulong[] BbEntry = new ulong[2], BbBuf = new ulong[2], BbCopy = new ulong[2]; public int[] BbSize = new int[2];
    }
    static Saved saved;
    static int basePid = -1; static ulong moduleBase;

    readonly Core core;
    public volatile bool Cancel;
    public int Mode = ModeMp;
    public Injector(Core c) { core = c; }
    int OtherMode { get { return 1 - Mode; } }
    Ps4Debug Dbg { get { return core.Dbg; } }
    int Pid { get { return core.Pid; } }
    void Log(string t) { core.Trace(t); }
    static string X(ulong v) { return "0x" + v.ToString("X"); }

    static readonly byte[][] menus = new byte[2][];
    public static byte[] Menu(int mode)
    {
        if (menus[mode] == null) menus[mode] = Unlock(Resource(MenuFiles[mode]));
        return menus[mode];
    }
    public static byte[] Resource(string name)
    {
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        { if (s == null) throw new IOException("MAG Menu / ressource non incluse dans les sources publiques : " + name); using (var m = new MemoryStream()) { s.CopyTo(m); return m.ToArray(); } }
    }

    static readonly byte[] KA = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    static readonly byte[] KB = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    static byte[] Unlock(byte[] d)
    {
        var k = new byte[32]; for (int i = 0; i < 32; i++) k[i] = (byte)(KA[i] ^ KB[i]);
        var iv = new byte[16]; Array.Copy(d, iv, 16);
        using (var a = System.Security.Cryptography.Aes.Create())
        using (var t = a.CreateDecryptor(k, iv)) return t.TransformFinalBlock(d, 16, d.Length - 16);
    }
    public static bool ValidHeader(byte[] h, ulong name)
    {
        if (h == null || h.Length < HeaderSize) return false;
        for (int i = 0; i < Magic.Length; i++) if (h[i] != Magic[i]) return false;
        return BitConverter.ToUInt64(h, 0x10) == name;
    }

    public static string CheckScript(byte[] s)
    {
        if (s == null || s.Length < HeaderSize) return L.T("fichier trop petit pour un script GSC", "file too small for a GSC script");
        for (int i = 0; i < 7; i++) if (s[i] != Magic[i]) return L.T("ce n'est pas un script GSC compilé (.gscc)", "this is not a compiled GSC script (.gscc)");
        if (s[7] != 0x36) return string.Format(L.T("script pour une autre version du moteur (VM 0x{0:X2}, BO4 = 0x36)", "script for another engine version (VM 0x{0:X2}, BO4 = 0x36)"), s[7]);
        if (BitConverter.ToUInt64(s, 0x10) != HashTarget) return L.T("le script doit être compilé sous le nom [masqué]", "the script must be compiled as [masqué]");
        if (BitConverter.ToInt32(s, 0x48) > s.Length) return L.T("taille indiquée dans l'en-tête incohérente", "inconsistent size in the header");
        return null;
    }

    public static int PatchTrophies(byte[] s)
    {
        if (s == null || s.Length < HeaderSize) return 0;
        int cnt = BitConverter.ToUInt16(s, 0x1C), off = BitConverter.ToInt32(s, 0x24), done = 0;
        for (int i = 0; i < cnt && off + 8 <= s.Length; i++)
        {
            int so = BitConverter.ToInt32(s, off), n = s[off + 4];
            off += 8 + n * 4;
            if (so < 0 || so + 4 > s.Length || s[so] != (byte)'M' || s[so + 1] != (byte)'T' || !char.IsDigit((char)s[so + 2]) || !char.IsDigit((char)s[so + 3])) continue;
            int e = so; while (e < s.Length && s[e] != 0) e++;
            int idx = (s[so + 2] - '0') * 10 + (s[so + 3] - '0');
            if (idx >= TrophyBlobs.Hex.Length) continue;
            byte[] blob = FromHex(TrophyBlobs.Hex[idx]);
            if (blob.Length != e - so + 1) continue;
            Array.Copy(blob, 0, s, so, blob.Length);
            done++;
        }
        return done;
    }
    static byte[] FromHex(string h)
    {
        var b = new byte[h.Length / 2];
        for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
        return b;
    }

    public static ulong Hash(string name)
    {
        ulong h = 14695981039346656037;
        foreach (char ch in name.ToLowerInvariant().Replace('\\', '/')) { h ^= (byte)ch; h *= 0x10A0; }
        return h & 0x10E0;
    }

    bool IsBase(ulong b)
    {
        try
        {
            ulong p = BitConverter.ToUInt64(Dbg.Read(Pid, b + TypeNames + SptType * 8, 8), 0);
            if (p != b + SptNameString) return false;
            var s = Dbg.Read(Pid, p, 16);
            return System.Text.Encoding.ASCII.GetString(s, 0, 15) == "scriptparsetree";
        }
        catch { return false; }
    }

    public ulong FindBase()
    {
        if (basePid == Pid && moduleBase != 0) return moduleBase;
        var sw = Stopwatch.StartNew();

        if (IsBase(0x1020))
        {
            basePid = Pid; moduleBase = 0x1020;
            Log(string.Format("Base de eboot.bin : 0x???? ({0} ms)", sw.ElapsedMilliseconds));
            return moduleBase;
        }
        var maps = Dbg.Maps(Pid);
        Log(string.Format("Cartes mémoire du jeu : {0}", maps.Count));
        var cand = new List<ulong>();
        foreach (var m in maps) if ((m.Name ?? "").IndexOf("eboot", StringComparison.OrdinalIgnoreCase) >= 0 && !cand.Contains(m.Start)) cand.Add(m.Start);
        foreach (var m in maps) if ((m.Prot & 4) != 0 && !cand.Contains(m.Start)) cand.Add(m.Start);
        foreach (var m in maps) if (!cand.Contains(m.Start)) cand.Add(m.Start);
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
        Log("Base de eboot.bin introuvable : ce n'est pas Black Ops 4 1.26 ?");
        return 0;
    }

    uint U32(ulong a) { return BitConverter.ToUInt32(Dbg.Read(Pid, a, 4), 0); }
    ulong U64(ulong a) { return BitConverter.ToUInt64(Dbg.Read(Pid, a, 8), 0); }

    Entry Lookup(ulong b, string name)
    {
        ulong h = Hash(name);
        uint idx = U32(b + Buckets + (h & (BucketCount - 1)) * 4);
        int steps = 0;
        while (idx != 0 && steps++ < 100000)
        {
            ulong ent = b + Entries + (ulong)idx * 16;
            var e = Dbg.Read(Pid, ent, 16);
            if (e[0] == SptType)
            {
                ulong asset = BitConverter.ToUInt64(e, 8), nm = 0;
                try { if (asset != 0) nm = U64(asset) & 0x10E0; }
                catch { Log(string.Format("  entrée {0} : asset {1} illisible, ignorée", idx, X(asset))); }
                if (asset != 0 && nm == h)
                {
                    uint final = idx, o; int ov = 0;
                    while ((o = U32(b + Overrides + (ulong)final * 4)) != 0 && ov++ < 64) final = o;
                    if (final != idx) { asset = U64(b + Entries + (ulong)final * 16 + 8); Log(string.Format("  {0} : entrée {1} remplacée par {2}", name, idx, final)); }
                    var a = Dbg.Read(Pid, asset, AssetSize);
                    var r = new Entry { Addr = asset, Buffer = BitConverter.ToUInt64(a, 0x10), Size = BitConverter.ToInt32(a, 0x18) };
                    Log(string.Format("  {0} : entrée {1} ({2} étape(s)), asset {3}, buffer {4}, {5} o", name, final, steps, X(asset), X(r.Buffer), r.Size));
                    if (r.Buffer == 0 || r.Size <= HeaderSize || r.Size > (64 << 20)) { Log("  -> buffer vide ou taille invalide (script pas encore chargé)"); return null; }
                    if (!ValidHeader(Dbg.Read(Pid, r.Buffer, HeaderSize), h)) { Log("  -> en-tête GSC invalide à " + X(r.Buffer)); return null; }
                    return r;
                }
            }
            idx = U32(b + NextTable + (ulong)idx * 4);
        }
        Log(string.Format("  {0} : absent de la table des assets ({1} entrée(s) parcourue(s))", name, steps));
        return null;
    }

    public Found Find(Action<string> progress)
    {
        var sw = Stopwatch.StartNew();
        var f = new Found();
        ulong b = FindBase();
        if (b != 0)
        {
            Log("Recherche des scripts dans la table des assets (mode " + ModeName(Mode) + ")...");
            f.Target = Lookup(b, NameTarget);
            f.Bb = Lookup(b, NameBbs[Mode]);
            f.Other = Lookup(b, NameBbs[OtherMode]);
            Log(string.Format("Recherche terminée en {0} ms", sw.ElapsedMilliseconds));
            return f;
        }
        return Scan(progress);
    }

    Entry Check(ulong addr, ulong name)
    {
        try
        {
            var e = Dbg.Read(Pid, addr, AssetSize);
            if (BitConverter.ToUInt64(e, 0) != name) return null;
            ulong buf = BitConverter.ToUInt64(e, 0x10); int size = BitConverter.ToInt32(e, 0x18);
            if (buf == 0 || size <= HeaderSize || size > (64 << 20)) return null;
            if (!ValidHeader(Dbg.Read(Pid, buf, HeaderSize), name)) return null;
            return new Entry { Addr = addr, Buffer = buf, Size = size };
        }
        catch { return null; }
    }

    Found Scan(Action<string> progress)
    {
        var f = new Found();
        var sw = Stopwatch.StartNew();
        var maps = Dbg.Maps(Pid).FindAll(delegate (MapEntry m) { return (m.Prot & 3) == 3 && m.End > m.Start; });
        ulong total = 0, done = 0; foreach (var m in maps) total += m.End - m.Start;
        Log(string.Format("Balayage de secours : {0} zone(s), {1:0} Mo", maps.Count, total / 1048576.0));
        int lastPct = -1;
        foreach (var m in maps)
        {
            for (ulong a = m.Start; a < m.End; a += Chunk)
            {
                if (Cancel) { Log("Balayage annulé"); throw new Exception(L.T("annulé", "cancelled")); }
                int len = (int)Math.Min((ulong)Chunk + AssetSize, m.End - a);
                byte[] d;
                try { d = Dbg.Read(Pid, a, len); } catch (Exception ex) { Log("  lecture impossible à " + X(a) + " : " + ex.Message); break; }
                ulong hashBb = HashBbs[Mode];
                for (int i = 0; i + AssetSize <= d.Length; i += 8)
                {
                    ulong v = BitConverter.ToUInt64(d, i);
                    if (v == hashBb && f.Bb == null) { f.Bb = Check(a + (ulong)i, hashBb); if (f.Bb != null) Log("  " + NameBbs[Mode] + " trouvé : asset " + X(f.Bb.Addr)); }
                    else if (v == HashTarget && f.Target == null) { f.Target = Check(a + (ulong)i, HashTarget); if (f.Target != null) Log("  [masqué] trouvé : asset " + X(f.Target.Addr)); }
                    if (f.Bb != null && f.Target != null) { Log(string.Format("Balayage terminé en {0:0.0} s", sw.Elapsed.TotalSeconds)); return f; }
                }
                done += (ulong)Math.Min(Chunk, len);
                int pct = (int)(done * 100 / Math.Max(1UL, total));
                if (pct != lastPct) { lastPct = pct; if (progress != null) progress(pct + "%"); if (pct % 10 == 0) Log(string.Format("  {0}% ({1:0} Mo lus, {2:0} s)", pct, done / 1048576.0, sw.Elapsed.TotalSeconds)); }
            }
        }
        Log(string.Format("Balayage terminé en {0:0.0} s, scripts non trouvés", sw.Elapsed.TotalSeconds));
        return f;
    }

    List<ulong> Includes(Entry bb, out int offset, out int count)
    {
        var h = Dbg.Read(Pid, bb.Buffer, HeaderSize);
        offset = BitConverter.ToInt32(h, 0x18); count = BitConverter.ToUInt16(h, 0x58);
        var l = new List<ulong>();
        if (offset <= 0 || offset >= bb.Size + 0x1010 || count > 1024) throw new Exception(L.T("table d'includes de bb.gsc invalide", "invalid bb.gsc include table"));
        if (count > 0)
        {
            var t = Dbg.Read(Pid, bb.Buffer + (ulong)offset, count * 8);
            for (int i = 0; i < count; i++) l.Add(BitConverter.ToUInt64(t, i * 8));
        }
        return l;
    }

    bool IsOriginal(Entry t) { return t.Size == OriginalTargetSize || (saved != null && saved.Pid == Pid && t.Buffer == saved.TargetBuf); }
    bool IsHooked(Entry bb) { int o, n; return bb != null && Includes(bb, out o, out n).Contains(HashTarget); }

    public class Status { public bool BbLoaded, OtherLoaded, CoreLoaded, ScriptIn, Hooked; }
    public Status GetStatus()
    {
        var s = new Status();
        var f = Find(null);
        s.BbLoaded = f.Bb != null; s.OtherLoaded = f.Other != null; s.CoreLoaded = f.Target != null;
        if (f.Target != null) s.ScriptIn = !IsOriginal(f.Target);
        s.Hooked = IsHooked(f.Bb);
        Log(string.Format("État : mode {0} {1}, mode {2} {3}, script {4}, accroché {5}", ModeName(Mode), s.BbLoaded ? "chargé" : "non chargé",
            ModeName(OtherMode), s.OtherLoaded ? "chargé" : "non chargé", s.ScriptIn ? "en place" : "absent", s.Hooked ? "oui" : "non"));
        return s;
    }

    static ulong Align(ulong v, ulong a) { return (v + a - 1) & ~(a - 1); }

    ulong WriteBlock(string what, byte[] data)
    {
        int allocLen = data.Length + 16;
        ulong alloc = Dbg.Alloc(Pid, allocLen);
        Log(string.Format("  allocation {0} : {1} o -> {2}", what, allocLen, X(alloc)));
        if (alloc == 0) throw new Exception(L.T("allocation mémoire refusée par ps4debug", "memory allocation refused by ps4debug"));
        ulong at = Align(alloc, 16);
        Dbg.Write(Pid, at, data);
        var back = Dbg.Read(Pid, at, Math.Min(data.Length, HeaderSize));
        for (int i = 0; i < back.Length; i++) if (back[i] != data[i]) throw new Exception(L.T("vérification de l'écriture échouée à ", "write check failed at ") + X(at));
        Log(string.Format("  écriture {0} : {1} o à {2} (relu OK)", what, data.Length, X(at)));
        return at;
    }

    void SetAsset(string what, ulong asset, ulong buf, int size)
    {
        var e = new byte[16];
        BitConverter.GetBytes(buf).CopyTo(e, 0);
        BitConverter.GetBytes(size).CopyTo(e, 8);
        Dbg.Write(Pid, asset + 0x10, e);
        Log(string.Format("  asset {0} ({1}) -> buffer {2}, {3} o", what, X(asset), X(buf), size));
    }

    public void Inject(byte[] m, Action<string> progress)
    {
        var sw = Stopwatch.StartNew();
        Cancel = false;
        Log(string.Format("=== Injection {0} ({1} o, pid {2}) ===", ModeName(Mode), m.Length, Pid));
        string bad = CheckScript(m);
        if (bad != null) throw new Exception(bad);
        var f = Find(progress);
        if (f.Target == null) throw new Exception(L.T("[masqué] introuvable : attends d'être au menu principal du jeu.", "[masqué] not found: wait until you are in the game's main menu."));
        if (f.Bb == null)
        {
            if (f.Other != null)
                throw new Exception(string.Format(L.T("le jeu est en mode {0} : choisis « {0} » dans l'outil, ou va dans le menu {1} du jeu.",
                    "the game is in {0} mode: pick \"{0}\" in the tool, or go to the game's {1} menu."), ModeName(OtherMode), ModeName(Mode)));
            throw new Exception(string.Format(L.T("{0} introuvable : va dans le menu {1} du jeu (pas de partie en cours).",
                "{0} not found: go to the game's {1} menu (no match running)."), NameBbs[Mode], ModeName(Mode)));
        }
        if (saved != null && saved.Pid != Pid) saved = null;
        if (saved == null) saved = new Saved { Pid = Pid };

        if (f.Other != null && IsHooked(f.Other)) { Log("  " + NameBbs[OtherMode] + " accroché par une injection précédente : décroché"); Unhook(OtherMode, f.Other); }

        bool original = IsOriginal(f.Target);
        if (original && f.Target.Buffer != saved.TargetBuf) { saved.TargetEntry = f.Target.Addr; saved.TargetBuf = f.Target.Buffer; saved.TargetSize = f.Target.Size; }
        Log(original ? "1/2 [masqué] d'origine : remplacement" : "1/2 [masqué] déjà remplacé : nouveau script");
        var script = (byte[])m.Clone();
        int tro = PatchTrophies(script);
        if (tro > 0) Log(string.Format("  noms de trophées du jeu recopiés : {0}", tro));
        ulong crcFrom = !original && saved.TargetBuf != 0 ? saved.TargetBuf : f.Target.Buffer;
        Array.Copy(Dbg.Read(Pid, crcFrom, 0x10), 0x08, script, 0x08, 8);
        Log(string.Format("  crc d'origine recopié : 0x{0:X8}", BitConverter.ToUInt32(script, 8)));
        ulong at = WriteBlock("script", script);
        SetAsset("[masqué]", f.Target.Addr, at, script.Length);

        int incOff, incCount;
        var inc = Includes(f.Bb, out incOff, out incCount);
        Log(string.Format("2/2 {0} : {1} include(s) à +0x{2:X}", NameBbs[Mode], incCount, incOff));
        if (inc.Contains(HashTarget)) Log("  [masqué] déjà inclus : rien à faire");
        else
        {
            var bb = Dbg.Read(Pid, f.Bb.Buffer, f.Bb.Size);
            int newOff = (int)Align((ulong)bb.Length, 8);
            var nb = new byte[newOff + (inc.Count + 1) * 8];
            Array.Copy(bb, nb, bb.Length);
            for (int i = 0; i < inc.Count; i++) BitConverter.GetBytes(inc[i]).CopyTo(nb, newOff + i * 8);
            BitConverter.GetBytes(HashTarget).CopyTo(nb, newOff + inc.Count * 8);
            BitConverter.GetBytes(newOff).CopyTo(nb, 0x18);
            BitConverter.GetBytes(nb.Length).CopyTo(nb, 0x48);
            BitConverter.GetBytes((ushort)(inc.Count + 1)).CopyTo(nb, 0x58);
            ulong bAt = WriteBlock("copie de bb.gsc", nb);
            saved.BbEntry[Mode] = f.Bb.Addr; saved.BbBuf[Mode] = f.Bb.Buffer; saved.BbSize[Mode] = f.Bb.Size; saved.BbCopy[Mode] = bAt;
            SetAsset(NameBbs[Mode], f.Bb.Addr, bAt, nb.Length);
        }
        PatchStatsLocation();
        Log(string.Format("=== Injection terminée en {0} ms ===", sw.ElapsedMilliseconds));
    }

    const ulong StatsLocJump = 0x1030;
    void PatchStatsLocation()
    {
        try
        {
            ulong b = FindBase();
            if (b == 0) return;
            var h = Dbg.Read(Pid, b + StatsLocJump - 3, 5);
            if (h[0] == 0x83 && h[1] == 0xFE && h[2] == 0x01 && h[4] == 0x2D)
            {
                if (h[3] == 0x75) { Dbg.Write(Pid, b + StatsLocJump, new byte[] { 0xEB }); Log("  stats : partie privée -> stats publiques (MP Public / ZM Online)"); }
                else if (h[3] == 0xEB) Log("  stats : déjà en stats publiques");
                else Log("  stats : code inattendu, rien modifié (" + BitConverter.ToString(h) + ")");
            }
            else Log("  stats : code inattendu, rien modifié (" + BitConverter.ToString(h) + ")");
        }
        catch (Exception ex) { Log("  stats : patch impossible (" + ex.Message + ")"); }
    }

    bool Unhook(int mode, Entry bb)
    {
        if (saved != null && saved.Pid == Pid && saved.BbCopy[mode] != 0 && saved.BbEntry[mode] == bb.Addr && bb.Buffer == saved.BbCopy[mode])
        {
            SetAsset(NameBbs[mode], bb.Addr, saved.BbBuf[mode], saved.BbSize[mode]);
            saved.BbCopy[mode] = 0;
            return true;
        }
        int off, n; var inc = Includes(bb, out off, out n);
        int k = inc.IndexOf(HashTarget);
        if (k < 0) { Log("  " + NameBbs[mode] + " n'inclut pas [masqué]"); return false; }
        inc.RemoveAt(k);
        var t = new byte[(inc.Count + 1) * 8];
        for (int i = 0; i < inc.Count; i++) BitConverter.GetBytes(inc[i]).CopyTo(t, i * 8);
        Dbg.Write(Pid, bb.Buffer + (ulong)off, t);
        Dbg.Write(Pid, bb.Buffer + 0x58, BitConverter.GetBytes((ushort)inc.Count));
        Log("  include retiré de " + NameBbs[mode] + " (" + inc.Count + " restant(s))");
        return true;
    }

    public bool Remove()
    {
        Log("=== Retrait ===");
        var f = Find(null);
        bool did = false;
        if (f.Bb != null && Unhook(Mode, f.Bb)) did = true;
        if (f.Other != null && Unhook(OtherMode, f.Other)) did = true;
        if (f.Target != null && saved != null && saved.Pid == Pid && saved.TargetBuf != 0 && saved.TargetEntry == f.Target.Addr && f.Target.Buffer != saved.TargetBuf)
        {
            SetAsset("[masqué]", f.Target.Addr, saved.TargetBuf, saved.TargetSize);
            did = true;
        }
        Log(did ? "=== Retrait terminé ===" : "=== Rien à retirer ===");
        return did;
    }
}
