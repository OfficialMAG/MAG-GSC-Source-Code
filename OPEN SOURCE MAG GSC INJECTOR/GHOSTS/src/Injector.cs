// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

public class Injector
{
    public enum GameMode { Multiplayer, Extinction }
    public static GameMode Mode = GameMode.Multiplayer;
    public static bool IsExtinction { get { return Mode == GameMode.Extinction; } }
    static readonly string[] MpNames = { "[masqué]", "[masqué]" };
    static readonly string[] ExtNames = { "[masqué]", "[masqué]" };
    public static string[] TargetNames { get { return IsExtinction ? ExtNames : MpNames; } }
    public static string TargetPath { get { return TargetNames[TargetNames.Length - 1]; } }
    public static string MenuName { get { return IsExtinction ? "MAG Menu Extinction v1.1" : "MAG Menu Ghosts v1.1"; } }
    public static string ModeName { get { return IsExtinction ? "Extinction" : L.T("Multijoueur", "Multiplayer"); } }
    const int ScriptType = 0x2C, EntrySize = 32, AssetSize = 0x28;
    const ulong Buckets = 0x10D0, Entries = 0x10E0;
    const uint BucketCount = 0x1010;
    const ulong CheckString = 0x10B0;
    const string CheckText = "Could not find script";

    public static readonly string[][] NearScripts = {
        new[] { "[masqué]", "[masqué]" },
        new[] { "[masqué]", "[masqué]" },
        new[] { "[masqué]", "[masqué]" },
        new[] { "[masqué]", "[masqué]" },
        new[] { "[masqué]", "[masqué]" },
        new[] { "[masqué]", "[masqué]" },
        new[] { "[masqué]" },
        new[] { "[masqué]", "[masqué]" },
        new[] { "[masqué]", "[masqué]" } };

    public class Asset { public ulong Addr, Buffer, Bytecode; public int CompLen, Len, BcLen; public string Name; }

    static int basePid = -1; static ulong moduleBase;

    readonly Core core;
    public volatile bool Cancel;
    public Injector(Core c) { core = c; }
    Ps4Debug Dbg { get { return core.Dbg; } }
    int Pid { get { return core.Pid; } }
    public bool Quiet;
    void Log(string t) { if (!Quiet) core.Trace(t); }
    static string X(ulong v) { return "0x" + v.ToString("X"); }

    static readonly Dictionary<string, byte[]> res = new Dictionary<string, byte[]>();
    static byte[] Cached(string name) { byte[] b; if (!res.TryGetValue(name, out b)) res[name] = b = Resource(name); return b; }
    public static byte[] Menu()
    {
        string n = IsExtinction ? "m_ext.dat" : "m_mp.dat";
        byte[] b; if (!res.TryGetValue(n, out b)) res[n] = b = Unlock(Resource(n));
        return b;
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

    public static byte[] TestScript() { return Cached(IsExtinction ? "mag_test_extinction.gscbin" : "mag_test_ghosts.gscbin"); }

    public static string[] RequiredFunctions
    {
        get { return IsExtinction ? new[] { "init", "onstartgametype", "onplayerconnect", "onspawnplayer", "onspawnalien" } : new[] { "init" }; }
    }
    public static byte[] Resource(string name)
    {
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        { if (s == null) throw new IOException("MAG Menu / ressource non incluse dans les sources publiques : " + name); using (var m = new MemoryStream()) { s.CopyTo(m); return m.ToArray(); } }
    }

    public class Script { public byte[] Buffer, Bytecode; public int Len; }
    public static Script Parse(byte[] d, out string error)
    {
        error = null;
        if (d == null || d.Length < 16) { error = L.T("fichier trop petit pour un script", "file too small for a script"); return null; }
        if (d[0] != 'G' || d[1] != 'S' || d[2] != 'C' || d[3] != 0) { error = L.T("ce n'est pas un script compilé .gscbin (en-tête GSC absent)", "this is not a compiled .gscbin script (no GSC header)"); return null; }
        int cl = BitConverter.ToInt32(d, 4), len = BitConverter.ToInt32(d, 8), bl = BitConverter.ToInt32(d, 12);
        if (cl <= 2 || len <= 0 || bl <= 1 || 16L + cl + bl != d.Length) { error = L.T("tailles de l'en-tête incohérentes", "inconsistent header sizes"); return null; }
        if (d[16] != 0x78) { error = L.T("buffer non compressé (zlib attendu)", "buffer not compressed (zlib expected)"); return null; }
        var s = new Script { Len = len, Buffer = new byte[cl], Bytecode = new byte[bl] };
        Array.Copy(d, 16, s.Buffer, 0, cl);
        Array.Copy(d, 16 + cl, s.Bytecode, 0, bl);
        if (s.Bytecode[0] != 0x58) { error = L.T("bytecode non PS4 (le premier opcode doit être End = 0x58) : compile-le avec l'injecteur", "not a PS4 bytecode (first opcode must be End = 0x58): compile it with the injector"); return null; }
        return s;
    }
    public static string CheckScript(byte[] d) { string e; Parse(d, out e); return e; }

    public static uint Hash(int type, string name)
    {
        uint h = (uint)type;
        foreach (char ch in name)
        {
            char c = ch == '\\' ? '/' : char.ToLowerInvariant(ch);
            h = h * 31 + (byte)c;
        }
        return h;
    }

    bool IsBase(ulong b)
    {
        try { return Encoding.ASCII.GetString(Dbg.Read(Pid, b + CheckString, CheckText.Length)) == CheckText; }
        catch { return false; }
    }

    public ulong FindBase()
    {
        if (basePid == Pid && moduleBase != 0) return moduleBase;
        var sw = Stopwatch.StartNew();
        if (IsBase(0x10A0))
        {
            basePid = Pid; moduleBase = 0x10A0;
            Log(string.Format("Base de default_mp.elf : 0x???? ({0} ms)", sw.ElapsedMilliseconds));
            return moduleBase;
        }
        var maps = Dbg.Maps(Pid);
        Log(string.Format("Cartes mémoire du jeu : {0}", maps.Count));
        var cand = new List<ulong>();
        foreach (var m in maps) if ((m.Name ?? "").IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0 && !cand.Contains(m.Start)) cand.Add(m.Start);
        foreach (var m in maps) if ((m.Name ?? "").IndexOf("eboot", StringComparison.OrdinalIgnoreCase) >= 0 && !cand.Contains(m.Start)) cand.Add(m.Start);
        foreach (var m in maps) if ((m.Prot & 4) != 0 && !cand.Contains(m.Start)) cand.Add(m.Start);
        int tried = 0;
        foreach (var b in cand)
        {
            tried++;
            if (IsBase(b))
            {
                basePid = Pid; moduleBase = b;
                Log(string.Format("Base de default_mp.elf : {0} (candidat {1}/{2}, {3} ms)", X(b), tried, cand.Count, sw.ElapsedMilliseconds));
                return b;
            }
        }
        Log("Base de default_mp.elf introuvable : ce n'est pas le multijoueur de Ghosts 1.21 ?");
        return 0;
    }
    static Exception NotGhosts()
    {
        return new Exception(L.T("ce n'est pas Call of Duty: Ghosts 1.21 en multijoueur (exécutable non reconnu)", "this is not Call of Duty: Ghosts 1.21 multiplayer (executable not recognised)"));
    }

    uint U32(ulong a) { return BitConverter.ToUInt32(Dbg.Read(Pid, a, 4), 0); }
    string CStr(ulong a, int max)
    {
        var b = Dbg.Read(Pid, a, max); int n = Array.IndexOf(b, (byte)0); if (n < 0) n = max;
        return Encoding.ASCII.GetString(b, 0, n);
    }

    public Asset Lookup(ulong b, string name)
    {
        uint h = Hash(ScriptType, name);
        uint idx = U32(b + Buckets + (h % BucketCount) * 4);
        Log(string.Format("  {0} : hash 0x{1:X8}, seau {2}, première entrée {3}", name, h, h % BucketCount, idx));
        int steps = 0;
        while (idx != 0 && steps++ < 100000)
        {
            if (Cancel) throw new Exception(L.T("annulé", "cancelled"));
            var e = Dbg.Read(Pid, b + Entries + (ulong)idx * EntrySize, EntrySize);
            if (BitConverter.ToInt32(e, 0) == ScriptType)
            {
                ulong asset = BitConverter.ToUInt64(e, 8);
                if (asset != 0)
                {
                    string nm = null;
                    try { nm = CStr(BitConverter.ToUInt64(Dbg.Read(Pid, asset, 8), 0), 64); } catch { }
                    if (nm != null && string.Equals(nm.Replace('\\', '/'), name, StringComparison.OrdinalIgnoreCase))
                    {
                        var a = Dbg.Read(Pid, asset, AssetSize);
                        var r = new Asset
                        {
                            Addr = asset, Name = nm, CompLen = BitConverter.ToInt32(a, 8), Len = BitConverter.ToInt32(a, 0xC), BcLen = BitConverter.ToInt32(a, 0x10),
                            Buffer = BitConverter.ToUInt64(a, 0x18), Bytecode = BitConverter.ToUInt64(a, 0x20)
                        };
                        Log(string.Format("  {0} : entrée {1} ({2} étape(s)), asset {3}, buffer {4} ({5} o -> {6} o), bytecode {7} ({8} o)",
                            name, idx, steps, X(asset), X(r.Buffer), r.CompLen, r.Len, X(r.Bytecode), r.BcLen));
                        if (r.Buffer == 0 || r.Bytecode == 0 || r.BcLen <= 0) { Log("  -> asset vide (script pas encore chargé)"); return null; }
                        return r;
                    }
                }
            }
            idx = BitConverter.ToUInt32(e, 0x14);
        }
        Log(string.Format("  {0} : absent de la table des assets ({1} entrée(s) parcourue(s))", name, steps));
        return null;
    }

    Asset LookupAny(ulong b, string[] names)
    {
        foreach (var n in names) { var a = Lookup(b, n); if (a != null) return a; }
        return null;
    }

    public Asset Find()
    {
        var sw = Stopwatch.StartNew();
        ulong b = FindBase();
        if (b == 0) throw NotGhosts();
        Log("Recherche du script cible dans la table des assets...");
        var a = LookupAny(b, TargetNames);
        Log(string.Format("Recherche terminée en {0} ms", sw.ElapsedMilliseconds));
        return a;
    }

    class Span { public ulong Lo = ulong.MaxValue, Hi; public int Count; public string Target; }
    Span ScriptSpan(ulong b)
    {
        var s = new Span();
        var all = new List<string[]>(); all.Add(TargetNames); all.AddRange(NearScripts);
        foreach (var names in all)
        {
            var a = LookupAny(b, names);
            if (a == null) continue;
            if (names == all[0]) s.Target = a.Name;
            s.Count++;
            if (a.Bytecode < s.Lo) s.Lo = a.Bytecode;
            if (a.Bytecode + (ulong)a.BcLen > s.Hi) s.Hi = a.Bytecode + (ulong)a.BcLen;
        }
        return s;
    }

    public const ulong PoolPtr = 0x1100;
    public const ulong PoolSize = 0x1040, LowOff = 0x18, HighOff = 0x230;

    public class Pool { public ulong Addr, Base, Low, High; }
    public Pool ReadPoolAt(ulong b)
    {
        ulong at = b + PoolPtr;
        var d = Dbg.Read(Pid, at, 0x238);
        return new Pool { Addr = at, Base = BitConverter.ToUInt64(d, 0), Low = BitConverter.ToUInt64(d, (int)LowOff), High = BitConverter.ToUInt64(d, (int)HighOff) };
    }

    const ulong HookSite = 0x1070, FindHeader = 0x1050;
    static readonly byte[] HookOriginal = { 0xE8, 0x7C, 0x7B, 0xEB, 0xFF };

    class Hooked { public int Pid; public GameMode Mode; public ulong Block, Cave, Header, Pristine; public int BcLen; }
    static Hooked hooked;

    public const int CaveOffset = 0x80, BufferOffset = 0x180, NameOffset = 0x40, FullOffset = 0x78, ClearSize = 0xA0;
    public static int ClearOffset(Script s) { return (int)Align((ulong)(BufferOffset + s.Buffer.Length), 16); }
    public static int PristineOffset(Script s) { return ClearOffset(s) + ClearSize; }

    public static byte[] BuildBlock(ulong at, ulong baseAddr, Script s, string[] names, out ulong cave, out ulong header)
    {
        header = at; ulong name = at + NameOffset; cave = at + CaveOffset;
        ulong buf = at + BufferOffset, pristine = at + (ulong)PristineOffset(s), pool = baseAddr + PoolPtr;
        var nl = new List<byte>();
        foreach (var n in names) { if (n.Length == 0) continue; nl.AddRange(Encoding.ASCII.GetBytes(n)); nl.Add(0); }
        nl.Add(0);
        var nameBytes = nl.ToArray();
        if (names.Length == 0 || nameBytes.Length > 0x38) throw new Exception("noms de script trop longs");
        var d = new byte[PristineOffset(s) + s.Bytecode.Length];
        BitConverter.GetBytes(name).CopyTo(d, 0);
        BitConverter.GetBytes(s.Buffer.Length).CopyTo(d, 8);
        BitConverter.GetBytes(s.Len).CopyTo(d, 0xC);
        BitConverter.GetBytes(s.Bytecode.Length).CopyTo(d, 0x10);
        BitConverter.GetBytes(buf).CopyTo(d, 0x18);
        BitConverter.GetBytes(pristine).CopyTo(d, 0x20);
        BitConverter.GetBytes(ulong.MaxValue).CopyTo(d, 0x30);
        BitConverter.GetBytes(ulong.MaxValue).CopyTo(d, 0x38);
        nameBytes.CopyTo(d, NameOffset);
        uint span = (uint)Align((ulong)s.Bytecode.Length, 16);
        var c = new List<byte>();
        c.AddRange(new byte[] { 0x48, 0x83, 0xEC, 0x08 });
        long rel = (long)(baseAddr + FindHeader) - (long)(cave + 4 + 5);
        if (rel < int.MinValue || rel > int.MaxValue) throw new Exception(L.T("bloc alloué trop loin du code du jeu", "allocated block too far from the game code"));
        c.Add(0xE8); c.AddRange(BitConverter.GetBytes((int)rel));
        c.AddRange(new byte[] { 0x48, 0x83, 0xC4, 0x08 });
        c.Add(0x48); c.Add(0xB9); c.AddRange(BitConverter.GetBytes(header + 0x28));
        c.AddRange(new byte[] { 0xFF, 0x01 });
        c.AddRange(new byte[] { 0x48, 0x85, 0xC0 });
        int jzFin1 = c.Count; c.AddRange(new byte[] { 0x74, 0x00 });
        c.AddRange(new byte[] { 0x48, 0x8B, 0x08 });
        c.AddRange(new byte[] { 0x48, 0x85, 0xC9 });
        int jzFin2 = c.Count; c.AddRange(new byte[] { 0x74, 0x00 });
        c.AddRange(new byte[] { 0x49, 0x89, 0xC9 });
        c.Add(0x48); c.Add(0xBA); c.AddRange(BitConverter.GetBytes(name));
        int next = c.Count;
        c.AddRange(new byte[] { 0x80, 0x3A, 0x00 });
        int jeFin3 = c.Count; c.AddRange(new byte[] { 0x74, 0x00 });
        c.AddRange(new byte[] { 0x4C, 0x89, 0xC9 });
        int loop = c.Count;
        c.AddRange(new byte[] { 0x44, 0x8A, 0x01 });
        c.AddRange(new byte[] { 0x44, 0x3A, 0x02 });
        int jneSkip = c.Count; c.AddRange(new byte[] { 0x75, 0x00 });
        c.AddRange(new byte[] { 0x45, 0x84, 0xC0 });
        int jzOk = c.Count; c.AddRange(new byte[] { 0x74, 0x00 });
        c.AddRange(new byte[] { 0x48, 0xFF, 0xC1 });
        c.AddRange(new byte[] { 0x48, 0xFF, 0xC2 });
        c.Add(0xEB); c.Add((byte)(sbyte)(loop - (c.Count + 1)));
        int skip = c.Count;
        c.AddRange(new byte[] { 0x80, 0x3A, 0x00 });
        int jeAdv = c.Count; c.AddRange(new byte[] { 0x74, 0x00 });
        c.AddRange(new byte[] { 0x48, 0xFF, 0xC2 });
        c.Add(0xEB); c.Add((byte)(sbyte)(skip - (c.Count + 1)));
        int adv = c.Count;
        c.AddRange(new byte[] { 0x48, 0xFF, 0xC2 });
        c.Add(0xEB); c.Add((byte)(sbyte)(next - (c.Count + 1)));
        int fin = c.Count;
        c.Add(0xC3);
        int ok = c.Count;
        c.Add(0x49); c.Add(0xBA); c.AddRange(BitConverter.GetBytes(header));
        c.Add(0x49); c.Add(0xB8); c.AddRange(BitConverter.GetBytes(pool));
        c.AddRange(new byte[] { 0x4D, 0x8B, 0x48, (byte)LowOff });
        c.AddRange(new byte[] { 0x4D, 0x3B, 0x4A, 0x38 });
        int jneFresh = c.Count; c.AddRange(new byte[] { 0x75, 0x00 });
        c.AddRange(new byte[] { 0x4D, 0x8B, 0x4A, 0x30 });
        int fresh = c.Count;
        c.AddRange(new byte[] { 0x49, 0x83, 0xC1, 0x0F });
        c.AddRange(new byte[] { 0x49, 0x83, 0xE1, 0xF0 });
        c.AddRange(new byte[] { 0x4D, 0x89, 0xCB });
        c.AddRange(new byte[] { 0x49, 0x81, 0xC3 }); c.AddRange(BitConverter.GetBytes(span));
        c.AddRange(new byte[] { 0x4D, 0x3B, 0x98 }); c.AddRange(BitConverter.GetBytes((uint)HighOff));
        int jaFull = c.Count; c.AddRange(new byte[] { 0x77, 0x00 });
        c.AddRange(new byte[] { 0x4D, 0x89, 0x4A, 0x30 });
        c.AddRange(new byte[] { 0x4D, 0x89, 0x5A, 0x38 });
        c.AddRange(new byte[] { 0x4D, 0x89, 0x58, (byte)LowOff });
        c.AddRange(new byte[] { 0x49, 0x8B, 0x38 });
        c.AddRange(new byte[] { 0x4C, 0x01, 0xCF });
        c.AddRange(new byte[] { 0x49, 0x89, 0x7A, 0x20 });
        c.Add(0x48); c.Add(0xBE); c.AddRange(BitConverter.GetBytes(pristine));
        c.Add(0xB9); c.AddRange(BitConverter.GetBytes(s.Bytecode.Length));
        c.AddRange(new byte[] { 0xF3, 0xA4 });
        c.AddRange(new byte[] { 0x41, 0xFF, 0x42, 0x2C });
        c.AddRange(new byte[] { 0x4C, 0x89, 0xD0 });
        c.Add(0xC3);
        int full = c.Count;
        c.AddRange(new byte[] { 0x41, 0xFF, 0x42, (byte)FullOffset });
        c.Add(0xC3);
        c[jzFin1 + 1] = (byte)(fin - (jzFin1 + 2)); c[jzFin2 + 1] = (byte)(fin - (jzFin2 + 2));
        c[jeFin3 + 1] = (byte)(fin - (jeFin3 + 2)); c[jneSkip + 1] = (byte)(skip - (jneSkip + 2));
        c[jeAdv + 1] = (byte)(adv - (jeAdv + 2)); c[jzOk + 1] = (byte)(ok - (jzOk + 2));
        c[jneFresh + 1] = (byte)(fresh - (jneFresh + 2)); c[jaFull + 1] = (byte)(full - (jaFull + 2));
        if (full - jaFull > 127) throw new Exception("code du détour trop long");
        if (c.Count > BufferOffset - CaveOffset) throw new Exception("code du détour trop long");
        c.ToArray().CopyTo(d, CaveOffset);
        s.Buffer.CopyTo(d, BufferOffset);
        ClearCode(baseAddr).CopyTo(d, ClearOffset(s));
        s.Bytecode.CopyTo(d, PristineOffset(s));
        return d;
    }

    const ulong ClearSite = 0x1060, HudElems = 0x10C0, EmptyId = 0x10F0, GetCs = 0x1090, SetCs = 0x1080;
    static readonly byte[] ClearOriginal = { 0xC3, 0x66, 0x66, 0x66, 0x66 };

    public static byte[] ClearCode(ulong b)
    {
        var c = new List<byte>();
        c.AddRange(new byte[] { 0x55, 0x48, 0x89, 0xE5, 0x53, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56 });
        c.AddRange(new byte[] { 0x89, 0xF8 });
        c.AddRange(new byte[] { 0x25, 0x00, 0x00, 0xFF, 0xFF });
        c.AddRange(new byte[] { 0x3D, 0x00, 0x00, 0x01, 0x00 });
        int j1 = c.Count; c.AddRange(new byte[] { 0x75, 0x00 });
        c.AddRange(new byte[] { 0x0F, 0xB7, 0xC7 });
        c.AddRange(new byte[] { 0x48, 0x69, 0xC0, 0xB8, 0x00, 0x00, 0x00 });
        c.Add(0x48); c.Add(0xB9); c.AddRange(BitConverter.GetBytes(b + HudElems));
        c.AddRange(new byte[] { 0x48, 0x03, 0x01 });
        c.AddRange(new byte[] { 0x8B, 0x98, 0x84, 0x00, 0x00, 0x00 });
        c.AddRange(new byte[] { 0x85, 0xDB });
        int j2 = c.Count; c.AddRange(new byte[] { 0x74, 0x00 });
        c.Add(0x49); c.Add(0xBC); c.AddRange(BitConverter.GetBytes(b + EmptyId));
        c.Add(0x49); c.Add(0xBD); c.AddRange(BitConverter.GetBytes(b + GetCs));
        c.Add(0x49); c.Add(0xBE); c.AddRange(BitConverter.GetBytes(b + SetCs));
        int loop = c.Count;
        c.AddRange(new byte[] { 0xFF, 0xC3 });
        c.AddRange(new byte[] { 0x81, 0xFB, 0xFF, 0x01, 0x00, 0x00 });
        int j3 = c.Count; c.AddRange(new byte[] { 0x73, 0x00 });
        c.AddRange(new byte[] { 0x8D, 0xBB, 0x1D, 0x02, 0x00, 0x00 });
        c.AddRange(new byte[] { 0x41, 0xFF, 0xD5 });
        c.AddRange(new byte[] { 0x41, 0x3B, 0x04, 0x24 });
        int j4 = c.Count; c.AddRange(new byte[] { 0x74, 0x00 });
        c.AddRange(new byte[] { 0x8D, 0xBB, 0x1D, 0x02, 0x00, 0x00 });
        c.AddRange(new byte[] { 0x31, 0xF6 });
        c.AddRange(new byte[] { 0x41, 0xFF, 0xD6 });
        c.Add(0xEB); c.Add((byte)(sbyte)(loop - (c.Count + 1)));
        int fin = c.Count;
        c.AddRange(new byte[] { 0x41, 0x5E, 0x41, 0x5D, 0x41, 0x5C, 0x5B, 0x5D, 0xC3 });
        c[j1 + 1] = (byte)(fin - (j1 + 2)); c[j2 + 1] = (byte)(fin - (j2 + 2));
        c[j3 + 1] = (byte)(fin - (j3 + 2)); c[j4 + 1] = (byte)(fin - (j4 + 2));
        if (c.Count > ClearSize) throw new Exception("code de clearalltextafterhudelem trop long");
        return c.ToArray();
    }

    static byte[] ClearJump(ulong b, ulong fn)
    {
        long rel = (long)fn - (long)(b + ClearSite + 5);
        if (rel < int.MinValue || rel > int.MaxValue) throw new Exception(L.T("bloc alloué trop loin du code du jeu", "allocated block too far from the game code"));
        var j = new byte[5]; j[0] = 0xE9; BitConverter.GetBytes((int)rel).CopyTo(j, 1);
        return j;
    }
    bool ClearPatched(ulong b) { var h = Dbg.Read(Pid, b + ClearSite, 5); return h[0] == 0xE9; }
    void ApplyClearPatch(ulong b, ulong fn)
    {
        var cur = Dbg.Read(Pid, b + ClearSite, 5);
        if (!Same(cur, ClearOriginal) && cur[0] != 0xE9) throw new Exception(L.T("code du jeu inattendu (clearalltextafterhudelem) à ", "unexpected game code (clearalltextafterhudelem) at ") + X(b + ClearSite) + " : " + BitConverter.ToString(cur));
        var j = ClearJump(b, fn);
        Dbg.Write(Pid, b + ClearSite, j);
        if (!Same(Dbg.Read(Pid, b + ClearSite, 5), j)) throw new Exception(L.T("le code du jeu n'a pas pu être modifié", "the game code could not be modified"));
        Log(string.Format("  textes HUD : clearalltextafterhudelem (vide sur PS4) -> {0} : les textes du menu sont libérés à chaque mise à jour", X(fn)));
    }
    void RemoveClearPatch(ulong b)
    {
        if (ClearPatched(b)) Dbg.Write(Pid, b + ClearSite, ClearOriginal);
    }

    static readonly ulong[] PatchAt = { 0x1030, 0x1020 };
    static readonly byte[][] PatchOrig = { new byte[] { 0x74, 0x27 }, new byte[] { 0x48, 0x8D, 0x3D, 0x39 } };
    static readonly byte[][] PatchNew = { new byte[] { 0x90, 0x90 }, new byte[] { 0x31, 0xDB, 0xEB, 0x54 } };

    bool TextPatched(ulong b)
    {
        for (int i = 0; i < PatchAt.Length; i++) if (!Same(Dbg.Read(Pid, b + PatchAt[i], PatchNew[i].Length), PatchNew[i])) return false;
        return ClearPatched(b);
    }
    void ApplyTextPatches(ulong b)
    {
        for (int i = 0; i < PatchAt.Length; i++)
        {
            var cur = Dbg.Read(Pid, b + PatchAt[i], PatchOrig[i].Length);
            if (Same(cur, PatchNew[i])) continue;
            if (!Same(cur, PatchOrig[i])) throw new Exception(L.T("code du jeu inattendu (textes HUD) à ", "unexpected game code (HUD texts) at ") + X(b + PatchAt[i]) + " : " + BitConverter.ToString(cur));
            Dbg.Write(Pid, b + PatchAt[i], PatchNew[i]);
            if (!Same(Dbg.Read(Pid, b + PatchAt[i], PatchNew[i].Length), PatchNew[i])) throw new Exception(L.T("le code du jeu n'a pas pu être modifié", "the game code could not be modified"));
        }
        Log("  textes HUD : création des textes permise (0x????), liste pleine = texte vide au lieu d'une erreur (0x????)");
    }
    void RemoveTextPatches(ulong b)
    {
        for (int i = 0; i < PatchAt.Length; i++)
            if (Same(Dbg.Read(Pid, b + PatchAt[i], PatchNew[i].Length), PatchNew[i])) Dbg.Write(Pid, b + PatchAt[i], PatchOrig[i]);
        RemoveClearPatch(b);
    }

    byte[] HookBytes(ulong b) { return Dbg.Read(Pid, b + HookSite, 5); }
    static bool Same(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    ulong HookTarget(ulong b, byte[] h) { return h[0] != 0xE8 ? 0 : (ulong)((long)(b + HookSite + 5) + BitConverter.ToInt32(h, 1)); }

    public class Status { public bool Found, ScriptIn, Reset, Intact, TextOk; public int BcLen, Calls = -1, Ours = -1, Full; }
    public Status GetStatus()
    {
        var s = new Status();
        ulong b = FindBase();
        if (b == 0) throw NotGhosts();
        s.Found = true;
        var h = HookBytes(b);
        if (Same(h, HookOriginal)) { Log("État : code d'origine, pas de script injecté"); return s; }
        ulong t = HookTarget(b, h);
        s.ScriptIn = true;
        s.TextOk = TextPatched(b);
        Log(string.Format("État : détour en place vers {0} ; textes HUD : {1}", X(t), s.TextOk ? "création permise" : "CODE D'ORIGINE (les textes du menu ne s'afficheront pas)"));
        try
        {
            ulong hdr = t - CaveOffset;
            var d = Dbg.Read(Pid, hdr, 0x80);
            s.Calls = BitConverter.ToInt32(d, 0x28); s.Ours = BitConverter.ToInt32(d, 0x2C); s.Full = BitConverter.ToInt32(d, FullOffset);
            Log(string.Format("  depuis la pose du détour, le jeu a chargé {0} script(s), dont {1} fois le script injecté ({2}){3}", s.Calls, s.Ours,
                CStr(hdr + NameOffset, 0x38), s.Full > 0 ? string.Format(" ; {0} fois pool de scripts plein (script du jeu gardé)", s.Full) : ""));
            var pool = ReadPoolAt(b);
            ulong st = BitConverter.ToUInt64(d, 0x30), en = BitConverter.ToUInt64(d, 0x38);
            Log(string.Format("  pool de scripts {0} : pile basse {1} Ko, pile haute à {2} Ko{3}", X(pool.Base), pool.Low / 1024, pool.High / 1024,
                st == ulong.MaxValue ? "" : string.Format(" ; menu placé à {0} ({1} o){2}", X(pool.Base + st), en - st, pool.Low < en ? " — place libérée depuis par le jeu (normal entre deux cartes)" : "")));
        }
        catch (Exception ex) { Log("  compteurs illisibles : " + ex.Message); }
        return s;
    }

    static ulong Align(ulong v, ulong a) { return (v + a - 1) & ~(a - 1); }

    public void Inject(byte[] file)
    {
        var sw = Stopwatch.StartNew();
        Cancel = false;
        Log(string.Format("=== Injection ({0} o, pid {1}) ===", file.Length, Pid));
        string err;
        var s = Parse(file, out err);
        if (s == null) throw new Exception(err);
        ulong b = FindBase();
        if (b == 0) throw NotGhosts();
        var h = HookBytes(b);
        bool orig = Same(h, HookOriginal);
        if (!orig && h[0] != 0xE8) throw new Exception(L.T("code du jeu inattendu à l'adresse du détour : ", "unexpected game code at the hook address: ") + BitConverter.ToString(h));
        Log(orig ? "Code d'origine en place : pose du détour" : "Détour déjà posé : nouveau script");

        var pool = ReadPoolAt(b);
        Log(string.Format("  pool de scripts : base {0}, pile basse {1} o, pile haute à {2} o", X(pool.Base), X(pool.Low), X(pool.High)));
        if (pool.Base == 0 || pool.High > PoolSize || pool.Low > pool.High)
            throw new Exception(L.T("pool de scripts du jeu illisible : va dans le menu Multijoueur puis réessaie", "game script pool unreadable: go to the Multiplayer menu and try again"));
        if (pool.High - pool.Low < Align((ulong)s.Bytecode.Length, 16) + 0x1000)
            Log("  ATTENTION : moins de 64 Ko libres dans le pool en plus du menu");
        var span = ScriptSpan(b);
        if (span.Count > 0)
        {
            bool inside = span.Lo >= pool.Base && span.Hi <= pool.Base + PoolSize;
            Log(string.Format("  scripts du jeu trouvés : {0}/{1}, bytecode entre {2} et {3} : {4}", span.Count, NearScripts.Length, X(span.Lo), X(span.Hi),
                inside ? "dans le pool (menu à portée)" : "HORS du pool"));
            if (!inside) throw new Exception(L.T("les scripts du jeu ne sont pas dans le pool attendu (contacte les devs avec le journal)", "the game scripts are not in the expected pool (contact the devs with the log)"));
        }
        Log("  mode : " + ModeName + ", script remplacé : " + TargetPath + " (noms acceptés : « " + string.Join(" », « ", TargetNames) + " »)");
        if (span.Target != null) Log("  trouvé dans la table des assets sous le nom « " + span.Target + " »");
        else Log(IsExtinction ? "  pas encore chargé (normal : il arrive avec la carte Extinction)" : "  absent de la table pour l'instant : le détour attend son chargement");
        string[] target = TargetNames;

        ulong tmpCave, tmpHeader;
        int size = BuildBlock(0x1110, 0x1110, s, target, out tmpCave, out tmpHeader).Length;
        ulong alloc = 0; bool published = false;
        try
        {
            alloc = Dbg.Alloc(Pid, size + 64);
            Log(string.Format("  allocation : {0} o -> {1}", size + 64, X(alloc)));
            if (alloc == 0 || alloc == ulong.MaxValue) throw new Exception(L.T("allocation mémoire refusée par ps4debug", "memory allocation refused by ps4debug"));
            ulong at = Align(alloc, 16), cave, header;
            var block = BuildBlock(at, b, s, target, out cave, out header);
            for (int o = 0; o < block.Length; o += 0x1000)
            {
                int n = Math.Min(0x1000, block.Length - o);
                var part = new byte[n]; Array.Copy(block, o, part, 0, n);
                Dbg.Write(Pid, at + (ulong)o, part);
            }
            var back = Dbg.Read(Pid, at, block.Length);
            if (!Same(back, block)) throw new Exception(L.T("vérification de l'écriture échouée à ", "write check failed at ") + X(at));
            Log(string.Format("  bloc écrit à {0} : en-tête {1}, détour {2}, bytecode d'origine {3} ({4} o) — relu OK", X(at), X(header), X(cave), X(at + (ulong)PristineOffset(s)), s.Bytecode.Length));
            Log("  à chaque chargement du script, le détour réserve sa place dans la pile basse du pool et y recopie le bytecode");
            ApplyTextPatches(b);
            ApplyClearPatch(b, at + (ulong)ClearOffset(s));

            var call = new byte[5]; call[0] = 0xE8;
            BitConverter.GetBytes((int)((long)cave - (long)(b + HookSite + 5))).CopyTo(call, 1);
            published = true;
            Dbg.Write(Pid, b + HookSite, call);
            if (!Same(HookBytes(b), call)) throw new Exception(L.T("le code du jeu n'a pas pu être modifié", "the game code could not be modified"));
            Log(string.Format("  appel {0} -> détour {1} (était {2})", X(b + HookSite), X(cave), BitConverter.ToString(h)));
            hooked = new Hooked { Pid = Pid, Mode = Mode, Block = at, Cave = cave, Header = header, Pristine = at + (ulong)PristineOffset(s), BcLen = s.Bytecode.Length };
            Log(string.Format("=== Injection terminée en {0} ms : le script sera chargé à la prochaine partie ===", sw.ElapsedMilliseconds));
        }
        catch
        {
            if (!published && alloc != 0 && alloc != ulong.MaxValue) { try { Dbg.Free(Pid, alloc, size + 64); } catch { } }
            throw;
        }
    }

    int tick, lastFull;
    public string AutoTick(byte[] script)
    {
        var procs = Dbg.ProcList();
        int pid = -1;
        foreach (var p in procs) if (p.Name == "default_mp.elf") { pid = p.Pid; break; }
        if (pid < 0) return core.Pid >= 0 ? "-" : null;
        string msg = null;
        if (pid != core.Pid)
        {
            core.Trace(string.Format("Auto : nouveau processus default_mp.elf (pid {0} -> {1})", core.Pid, pid));
            core.Pid = pid; msg = "pid";
        }
        Quiet = true;
        try
        {
            ulong b = (basePid == Pid && moduleBase != 0) ? moduleBase : (IsBase(0x10A0) ? 0x10A0UL : 0);
            if (b == 0) return msg;
            basePid = Pid; moduleBase = b;
            var h = HookBytes(b);
            if (hooked != null && hooked.Pid == Pid && hooked.Mode != Mode)
            {
                Quiet = false;
                core.Trace("Auto : mode changé (" + ModeName + ") -> nouveau script");
                Inject(script);
                return "inject";
            }
            if (Same(h, HookOriginal) || (hooked != null && hooked.Pid == Pid && tick % 10 == 9 && !TextPatched(b)))
            {
                Quiet = false;
                core.Trace("Auto : code d'origine -> pose du détour");
                Inject(script);
                return "inject";
            }
            if (hooked != null && hooked.Pid == Pid && ++tick % 10 == 0)
            {
                int full = BitConverter.ToInt32(Dbg.Read(Pid, hooked.Header + (ulong)FullOffset, 4), 0);
                if (full > lastFull)
                {
                    lastFull = full; Quiet = false;
                    core.Trace("Auto : ATTENTION, pool de scripts du jeu plein à ce chargement : la partie a démarré sans le menu");
                    Quiet = true;
                }
            }
            return msg;
        }
        finally { Quiet = false; }
    }

    public bool Remove()
    {
        Log("=== Retrait ===");
        ulong b = FindBase();
        if (b == 0) return false;
        bool text = TextPatched(b);
        RemoveTextPatches(b);
        if (Same(HookBytes(b), HookOriginal)) { Log(text ? "=== Retrait : patch des textes HUD retiré ===" : "=== Rien à retirer : code d'origine ==="); return text; }
        Dbg.Write(Pid, b + HookSite, HookOriginal);
        Log("=== Retrait terminé : appel d'origine remis (la prochaine partie charge le script du jeu) ===");
        return true;
    }
}
