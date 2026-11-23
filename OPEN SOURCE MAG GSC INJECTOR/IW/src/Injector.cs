// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

public class Injector
{
    public enum GameMode { Multiplayer, Zombies }
    public static GameMode Mode = GameMode.Multiplayer;
    public static bool IsZombies { get { return Mode == GameMode.Zombies; } }
    public static string[] TargetNames { get { return IsZombies ? new[] { "[masqué]", "[masqué]" } : new[] { "[masqué]", "[masqué]" }; } }
    public static string TargetPath { get { return TargetNames[TargetNames.Length - 1]; } }

    public static readonly string[] ZombiesAnchorNames = { "[masqué]", "[masqué]" };
    public static string MenuName { get { return IsZombies ? "MAG Menu Zombies Spaceland v0.1" : "MAG Menu IW v0.1"; } }
    public static string ModeName { get { return IsZombies ? "Zombies Spaceland" : L.T("Multijoueur", "Multiplayer"); } }
    const int ScriptType = 0x30, EntrySize = 20, AssetSize = 0x28;
    const ulong Buckets = 0x1150, Entries = 0x1140;
    const uint BucketCount = 0x1040;
    const ulong CheckString = 0x1120, CheckGame = 0x1130;
    const string CheckText = "Could not find script", CheckGameText = "Could not find script";

    public static string[][] NearScripts { get { return IsZombies ? new[] { new[] { "[masqué]" } } : new[] { new[] { "[masqué]", "[masqué]" } }; } }
    public const ulong CallRange = 0x1070;

    public class Asset { public ulong Addr, Buffer, Bytecode; public int CompLen, Len, BcLen; public string Name; }

    static int basePid = -1; static ulong moduleBase;

    readonly Core core;
    public volatile bool Cancel;
    public volatile bool RefreshRequested;
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
        string n = IsZombies ? "m_zm.dat" : "m_mp.dat";
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

    public static byte[] TestScript() { return Cached(IsZombies ? "mag_test_zm.gscbin" : "mag_test_mp.gscbin"); }

    public static string[] RequiredFunctions { get { return new[] { "main" }; } }
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
        if (bl > MaxBytecode) { error = L.T("bytecode trop gros (512 Ko au plus)", "bytecode too big (512 KB max)"); return null; }
        var s = new Script { Len = len, Buffer = new byte[cl], Bytecode = new byte[bl] };
        Array.Copy(d, 16, s.Buffer, 0, cl);
        Array.Copy(d, 16 + cl, s.Bytecode, 0, bl);
        if (s.Bytecode[0] != 0x58) { error = L.T("bytecode non PS4 (le premier opcode doit être End = 0x58) : compile-le avec l'injecteur", "not a PS4 bytecode (first opcode must be End = 0x58): compile it with the injector"); return null; }
        return s;
    }
    public static string CheckScript(byte[] d) { string e; Parse(d, out e); return e; }
    public const int MaxBytecode = 0x1050;

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
        try
        {
            return Encoding.ASCII.GetString(Dbg.Read(Pid, b + CheckString, CheckText.Length)) == CheckText
                && Encoding.ASCII.GetString(Dbg.Read(Pid, b + CheckGame, CheckGameText.Length)) == CheckGameText;
        }
        catch { return false; }
    }

    public ulong FindBase()
    {
        if (basePid == Pid && moduleBase != 0) return moduleBase;
        var sw = Stopwatch.StartNew();
        if (IsBase(0x1080))
        {
            basePid = Pid; moduleBase = 0x1080;
            Log(string.Format("Base de default.elf : 0x???? ({0} ms)", sw.ElapsedMilliseconds));
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
                Log(string.Format("Base de default.elf : {0} (candidat {1}/{2}, {3} ms)", X(b), tried, cand.Count, sw.ElapsedMilliseconds));
                return b;
            }
        }
        Log("Base de default.elf introuvable : ce n'est pas le multijoueur d'Infinite Warfare 1.25 ?");
        return 0;
    }
    static Exception NotAw()
    {
        return new Exception(L.T("ce n'est pas Call of Duty: Infinite Warfare 1.25 en multijoueur / Zombies Spaceland (exécutable non reconnu)", "this is not Call of Duty: Infinite Warfare 1.25 multiplayer / Zombies Spaceland (executable not recognised)"));
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
            if (e[0x11] == ScriptType)
            {
                ulong asset = BitConverter.ToUInt64(e, 0);
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
            idx = BitConverter.ToUInt32(e, 8);
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
        if (b == 0) throw NotAw();
        Log("Recherche du script cible dans la table des assets...");
        var a = LookupAny(b, TargetNames);
        Log(string.Format("Recherche terminée en {0} ms", sw.ElapsedMilliseconds));
        return a;
    }

    public class Span { public ulong Lo = ulong.MaxValue, Hi; public int Count; public string Target; public ulong TargetCode; public string Anchor; public ulong AnchorCode; }
    public Span ScriptSpan(ulong b)
    {
        var s = new Span();
        var all = new List<string[]>(); all.Add(TargetNames); all.AddRange(NearScripts);
        foreach (var names in all)
        {
            var a = LookupAny(b, names);
            if (a == null) continue;
            if (names == all[0]) { s.Target = a.Name; s.TargetCode = a.Bytecode; }
            s.Count++;
            if (a.Bytecode < s.Lo) s.Lo = a.Bytecode;
            if (a.Bytecode + (ulong)a.BcLen > s.Hi) s.Hi = a.Bytecode + (ulong)a.BcLen;
        }
        if (IsZombies)
        {
            var anchor = LookupAny(b, ZombiesAnchorNames);
            if (anchor != null)
            {
                s.Anchor = anchor.Name; s.AnchorCode = anchor.Bytecode;
                s.Count++;
                s.Lo = Math.Min(s.Lo, anchor.Bytecode);
                s.Hi = Math.Max(s.Hi, anchor.Bytecode + (ulong)anchor.BcLen);
            }
        }
        return s;
    }

    public static bool CanInstall(Span span)
    {
        return IsZombies ? span.AnchorCode != 0 : span.Target != null;
    }

    public static bool InRange(Span s, ulong at, int len)
    {
        if (s.Count == 0) return true;
        ulong lo = Math.Min(s.Lo, at), hi = Math.Max(s.Hi, at + (ulong)len);
        return hi - lo <= CallRange;
    }

    ulong rpc, mmap, munmap; int rpcPid = -1;
    public static ulong FindSyscall(byte[] code, ulong start, int number)
    {
        var pat = new List<byte> { 0x48, 0xC7, 0xC0 }; pat.AddRange(BitConverter.GetBytes(number));
        pat.AddRange(new byte[] { 0x49, 0x89, 0xCA, 0x0F, 0x05, 0x72, 0x01, 0xC3 });
        ulong found = 0;
        for (int i = 0; i <= code.Length - pat.Count; i++)
        {
            int j = 0; while (j < pat.Count && code[i + j] == pat[j]) j++;
            if (j == pat.Count) { if (found != 0) throw new Exception("plusieurs wrappers système correspondants"); found = start + (ulong)i; }
        }
        return found;
    }
    void PrepareRpc()
    {
        if (rpcPid == Pid && rpc != 0) return;
        mmap = munmap = rpc = 0;
        foreach (var m in Dbg.Maps(Pid))
        {
            if ((m.Name ?? "").IndexOf("libkernel", StringComparison.OrdinalIgnoreCase) < 0 || (m.Prot & 4) == 0 || m.End - m.Start >= 0x1080) continue;
            var code = Dbg.Read(Pid, m.Start, (int)(m.End - m.Start));
            mmap = FindSyscall(code, m.Start, 477); munmap = FindSyscall(code, m.Start, 73);
            if (mmap != 0 && munmap != 0) break;
        }
        if (mmap == 0 || munmap == 0) throw new Exception(L.T("fonctions mmap / munmap introuvables dans libkernel", "mmap / munmap not found in libkernel"));
        rpc = Dbg.InstallRpc(Pid);
        if (rpc == 0 || rpc == ulong.MaxValue) throw new Exception(L.T("RPC ps4debug indisponible", "ps4debug RPC unavailable"));
        rpcPid = Pid;
        Log(string.Format("  RPC prêt : mmap {0}, munmap {1}", X(mmap), X(munmap)));
    }
    public const ulong NearWindow = 0x1050;
    ulong AllocNear(ulong scripts, int length, Span span)
    {
        PrepareRpc();
        int size = (int)Align((ulong)length, 0x1020);
        var maps = Dbg.Maps(Pid);
        var region = maps.Find(m => (m.Prot & 1) != 0 && m.Start <= scripts && scripts < m.End);
        if (region == null) throw new Exception(L.T("zone mémoire des scripts du jeu introuvable", "game script memory region not found"));
        if (IsZombies)
        {

            ulong end = region.Start + 0x1060;
            if (scripts >= end || span.Hi > end)
                throw new Exception(L.T("zone des scripts Zombies incompatible avec le profil Spaceland 1.25", "Zombies script arena does not match the Spaceland 1.25 profile"));
            span.Lo = Math.Min(span.Lo, region.Start);
            span.Hi = end;
        }
        Log(string.Format("  zone des scripts : {0}..{1} ({2} Ko)", X(region.Start), X(region.End), (region.End - region.Start) / 1024));

        var tries = new List<ulong>();
        for (ulong at = region.Start - (ulong)size; at + NearWindow >= region.Start; at -= 0x1020) tries.Add(at);
        for (ulong at = Align(region.End, 0x1020); at < region.End + NearWindow; at += 0x1020) tries.Add(at);
        foreach (ulong at in tries)
        {
            bool conflict = false, reserved = false;
            foreach (var m in maps)
            {
                if (m.Start == 0x1020 && m.End == 0x1180UL) continue;
                if (at < m.End && at + (ulong)size > m.Start)
                {
                    if (m.Prot != 0 || at < m.Start || at + (ulong)size > m.End) { conflict = true; break; }
                    reserved = true;
                }
            }
            if (conflict) continue;
            if (!InRange(span, at, length)) continue;
            ulong flags = reserved ? 0x1010UL : 0x1000UL;
            ulong got = Dbg.Call(Pid, rpc, mmap, at, (ulong)size, 3, flags, ulong.MaxValue, 0);
            if (got == at) { Log(string.Format("  bytecode à portée des scripts : {0} ({1} o)", X(got), size)); return got; }
            if (got != 0 && got != ulong.MaxValue) Dbg.Call(Pid, rpc, munmap, got, (ulong)size);
        }
        throw new Exception(L.T("aucune place libre à portée des scripts du jeu (relance le jeu puis réessaie)", "no free space within reach of the game scripts (restart the game and try again)"));
    }
    void FreeNear(ulong at, int length)
    {
        try { if (rpcPid == Pid && at != 0) Dbg.Call(Pid, rpc, munmap, at, Align((ulong)length, 0x1020)); } catch { }
    }

    const ulong HookSite = 0x1110, FindHeader = 0x1100;
    static readonly byte[] HookOriginal = { 0xE8, 0x8F, 0x81, 0xDB, 0xFF };

    class Hooked { public int Pid; public GameMode Mode; public ulong Block, Cave, Header, Pristine, Near; public int BcLen; }
    static Hooked hooked;

    public const int CaveOffset = 0x80, BufferOffset = 0x180, NameOffset = 0x40, ClearSize = 0x200;
    public static int ClearOffset(Script s) { return (int)Align((ulong)(BufferOffset + s.Buffer.Length), 16); }
    public static int SetTextOffset(Script s) { return ClearOffset(s) + 0x100; }
    public static int PristineOffset(Script s) { return ClearOffset(s) + ClearSize; }

    public static byte[] BuildBlock(ulong at, ulong baseAddr, Script s, string[] names, ulong near, out ulong cave, out ulong header)
    {
        header = at; ulong name = at + NameOffset; cave = at + CaveOffset;
        ulong buf = at + BufferOffset, pristine = at + (ulong)PristineOffset(s);
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
        BitConverter.GetBytes(near).CopyTo(d, 0x20);
        nameBytes.CopyTo(d, NameOffset);
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
        c.AddRange(new byte[] { 0x49, 0x8B, 0x7A, 0x20 });
        c.Add(0x48); c.Add(0xBE); c.AddRange(BitConverter.GetBytes(pristine));
        c.Add(0xB9); c.AddRange(BitConverter.GetBytes(s.Bytecode.Length));
        c.Add(0xFC);
        c.AddRange(new byte[] { 0xF3, 0xA4 });
        c.AddRange(new byte[] { 0x41, 0xFF, 0x42, 0x2C });
        c.AddRange(new byte[] { 0x4C, 0x89, 0xD0 });
        c.Add(0xC3);
        c[jzFin1 + 1] = (byte)(fin - (jzFin1 + 2)); c[jzFin2 + 1] = (byte)(fin - (jzFin2 + 2));
        c[jeFin3 + 1] = (byte)(fin - (jeFin3 + 2)); c[jneSkip + 1] = (byte)(skip - (jneSkip + 2));
        c[jeAdv + 1] = (byte)(adv - (jeAdv + 2)); c[jzOk + 1] = (byte)(ok - (jzOk + 2));
        if (c.Count > BufferOffset - CaveOffset) throw new Exception("code du détour trop long");
        c.ToArray().CopyTo(d, CaveOffset);
        s.Buffer.CopyTo(d, BufferOffset);
        IwTextCode.ClearCode(baseAddr).CopyTo(d, ClearOffset(s));
        IwTextCode.SetCode(baseAddr).CopyTo(d, SetTextOffset(s));
        s.Bytecode.CopyTo(d, PristineOffset(s));
        return d;
    }

    bool IsTextJump(ulong b, ulong site, byte[] expected)
    {
        try
        {
            var h = Dbg.Read(Pid, b + site, 5);
            if (h[0] != 0xE9) return false;
            ulong fn = (ulong)((long)(b + site + 5) + BitConverter.ToInt32(h, 1));
            return Same(Dbg.Read(Pid, fn, expected.Length), expected);
        }
        catch { return false; }
    }
    bool ClearPatched(ulong b) { return IsTextJump(b, IwTextCode.ClearSite, IwTextCode.ClearCode(b)); }
    bool TextPatched(ulong b) { return ClearPatched(b) && IsTextJump(b, IwTextCode.SetSite, IwTextCode.SetCode(b)); }
    void ValidateTextSite(ulong b, ulong site, byte[] original, byte[] expected)
    {
        if (!Same(Dbg.Read(Pid, b + site, 5), original) && !IsTextJump(b, site, expected))
            throw new Exception("IW : code inattendu au point de correction des textes " + X(b + site));
    }
    void WriteTextJump(ulong b, ulong site, ulong fn)
    {
        long rel = (long)fn - (long)(b + site + 5);
        if (rel < int.MinValue || rel > int.MaxValue) throw new Exception("IW : correctif texte hors de portee");
        var jump = new byte[5]; jump[0] = 0xE9; BitConverter.GetBytes((int)rel).CopyTo(jump, 1);
        Dbg.Write(Pid, b + site, jump);
        if (!Same(Dbg.Read(Pid, b + site, 5), jump)) throw new Exception("IW : ecriture du correctif texte non confirmee");
    }
    void ApplyTextPatches(ulong b, ulong at, Script s)
    {
        ValidateTextSite(b, IwTextCode.SetSite, IwTextCode.SetOriginal, IwTextCode.SetCode(b));
        ValidateTextSite(b, IwTextCode.ClearSite, IwTextCode.ClearOriginal, IwTextCode.ClearCode(b));
        WriteTextJump(b, IwTextCode.ClearSite, at + (ulong)ClearOffset(s));
        WriteTextJump(b, IwTextCode.SetSite, at + (ulong)SetTextOffset(s));
        if (!TextPatched(b)) throw new Exception("IW : verification des deux correctifs texte echouee");
        Log("Textes IW : creation des libelles en partie et nettoyage de la table actives (relus OK)");
    }
    void RemoveTextPatches(ulong b)
    {
        if (IsTextJump(b, IwTextCode.SetSite, IwTextCode.SetCode(b))) Dbg.Write(Pid, b + IwTextCode.SetSite, IwTextCode.SetOriginal);
        if (ClearPatched(b)) Dbg.Write(Pid, b + IwTextCode.ClearSite, IwTextCode.ClearOriginal);
    }
    byte[] HookBytes(ulong b) { return Dbg.Read(Pid, b + HookSite, 5); }
    static bool Same(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    ulong HookTarget(ulong b, byte[] h) { return h[0] != 0xE8 ? 0 : (ulong)((long)(b + HookSite + 5) + BitConverter.ToInt32(h, 1)); }

    bool IsOurHook(ulong b, byte[] h)
    {
        try
        {
            ulong t = HookTarget(b, h); if (t < CaveOffset) return false;
            var code = Dbg.Read(Pid, t, 9);
            if (code[0] != 0x48 || code[1] != 0x83 || code[2] != 0xEC || code[3] != 0x08 || code[4] != 0xE8) return false;
            if ((long)t + 9 + BitConverter.ToInt32(code, 5) != (long)(b + FindHeader)) return false;
            string profile = CStr(t - CaveOffset + NameOffset, 8);
            return profile == "[masqué]" || profile == "[masqué]";
        }
        catch { return false; }
    }

    public class Status { public bool Found, ScriptIn, Reset, Intact, TextOk; public int BcLen, Calls = -1, Ours = -1; }
    public Status GetStatus()
    {
        var s = new Status();
        ulong b = FindBase();
        if (b == 0) throw NotAw();
        s.Found = true;
        var h = HookBytes(b);
        if (Same(h, HookOriginal)) { Log("État : code d'origine, pas de script injecté"); return s; }
        ulong t = HookTarget(b, h);
        s.ScriptIn = true;
        s.TextOk = TextPatched(b);
        Log(string.Format("État : détour en place vers {0} ; textes HUD : {1}", X(t), s.TextOk ? "gestionnaire de textes IW" : "non reconnu"));
        try
        {
            ulong hdr = t - CaveOffset;
            var d = Dbg.Read(Pid, hdr, 0x80);
            s.Calls = BitConverter.ToInt32(d, 0x28); s.Ours = BitConverter.ToInt32(d, 0x2C);
            ulong near = BitConverter.ToUInt64(d, 0x20); s.BcLen = BitConverter.ToInt32(d, 0x10);
            Log(string.Format("  depuis la pose du détour, le jeu a chargé {0} script(s), dont {1} fois le script injecté ({2})", s.Calls, s.Ours, CStr(hdr + NameOffset, 0x38)));
            var span = ScriptSpan(b);
            Log(string.Format("  bytecode du menu à {0} ({1} o) ; scripts du jeu trouvés : {2} ; {3}", X(near), s.BcLen, span.Count,
                InRange(span, near, s.BcLen) ? "à portée des appels" : "HORS DE PORTÉE (réinjecte)"));
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
        GscCompiler.Check(file);
        ulong b = FindBase();
        if (b == 0) throw NotAw();
        var h = HookBytes(b);
        bool orig = Same(h, HookOriginal);
        if (!orig && !IsOurHook(b, h)) throw new Exception(L.T("code du jeu inattendu à l'adresse du détour : ", "unexpected game code at the hook address: ") + BitConverter.ToString(h));
        Log(orig ? "Code d'origine en place : pose du détour" : "Détour déjà posé : nouveau script");
        Log("  mode : " + ModeName + ", script remplacé : " + TargetPath + " (noms acceptés : « " + string.Join(" », « ", TargetNames) + " »)");

        var span = ScriptSpan(b);
        if (!CanInstall(span)) throw new Exception(L.T("zone des scripts absente : ouvre le pré-lobby du mode choisi puis injecte", "script arena missing: open the selected mode's pre-lobby, then inject"));
        Log(string.Format("  cible : {0} ; ancre : {1} ; scripts entre {2} et {3}", span.Target ?? "au prochain chargement de Spaceland", span.Anchor ?? span.Target, X(span.Lo), X(span.Hi)));

        ulong near = AllocNear(IsZombies ? span.AnchorCode : span.TargetCode, s.Bytecode.Length, span);
        ulong tmpCave, tmpHeader;
        int size = BuildBlock(0x1170, 0x1170, s, TargetNames, near, out tmpCave, out tmpHeader).Length;
        ulong alloc = 0; bool published = false;
        try
        {
            Dbg.Write(Pid, near, s.Bytecode);
            if (!Same(Dbg.Read(Pid, near, s.Bytecode.Length), s.Bytecode)) throw new Exception(L.T("vérification du bytecode écrit échouée", "bytecode write check failed"));
            alloc = Dbg.Alloc(Pid, size + 64);
            Log(string.Format("  allocation : {0} o -> {1}", size + 64, X(alloc)));
            if (alloc == 0 || alloc == ulong.MaxValue) throw new Exception(L.T("allocation mémoire refusée par ps4debug", "memory allocation refused by ps4debug"));
            ulong at = Align(alloc, 16), cave, header;
            var block = BuildBlock(at, b, s, TargetNames, near, out cave, out header);
            for (int o = 0; o < block.Length; o += 0x1030)
            {
                int n = Math.Min(0x1030, block.Length - o);
                var part = new byte[n]; Array.Copy(block, o, part, 0, n);
                Dbg.Write(Pid, at + (ulong)o, part);
            }
            var back = Dbg.Read(Pid, at, block.Length);
            if (!Same(back, block)) throw new Exception(L.T("vérification de l'écriture échouée à ", "write check failed at ") + X(at));
            Log(string.Format("  bloc écrit à {0} : en-tête {1}, détour {2}, bytecode d'origine {3} ({4} o) — relu OK", X(at), X(header), X(cave), X(at + (ulong)PristineOffset(s)), s.Bytecode.Length));
            Log("  à chaque chargement du script, le détour recopie le bytecode d'origine à " + X(near) + " (correctif 1)");
            published = true;
            ApplyTextPatches(b, at, s);

            var call = new byte[5]; call[0] = 0xE8;
            BitConverter.GetBytes((int)((long)cave - (long)(b + HookSite + 5))).CopyTo(call, 1);
            published = true;
            Dbg.Write(Pid, b + HookSite, call);
            if (!Same(HookBytes(b), call)) throw new Exception(L.T("le code du jeu n'a pas pu être modifié", "the game code could not be modified"));
            Log(string.Format("  appel {0} -> détour {1} (était {2})", X(b + HookSite), X(cave), BitConverter.ToString(h)));
            hooked = new Hooked { Pid = Pid, Mode = Mode, Block = at, Cave = cave, Header = header, Pristine = at + (ulong)PristineOffset(s), Near = near, BcLen = s.Bytecode.Length };
            PatchPrivateStats(b, true);
            Log(string.Format("=== Injection terminée en {0} ms : le script sera chargé à la prochaine partie ===", sw.ElapsedMilliseconds));
        }
        catch
        {
            if (!published)
            {
                FreeNear(near, s.Bytecode.Length);
                if (alloc != 0 && alloc != ulong.MaxValue) { try { Dbg.Free(Pid, alloc, size + 64); } catch { } }
            }
            throw;
        }
    }

    int tick;
    public string AutoTick(byte[] script)
    {
        var procs = Dbg.ProcList();
        int pid = -1;
        foreach (var p in procs) if (p.Name == "default.elf") { pid = p.Pid; break; }
        if (pid < 0) return core.Pid >= 0 ? "-" : null;
        string msg = null;
        if (pid != core.Pid)
        {
            core.Trace(string.Format("Auto : nouveau processus default.elf (pid {0} -> {1})", core.Pid, pid));
            core.Pid = pid; msg = "pid";
        }
        Quiet = true;
        try
        {
            ulong b = (basePid == Pid && moduleBase != 0) ? moduleBase : (IsBase(0x1080) ? 0x1080UL : 0);
            if (b == 0) return msg;
            basePid = Pid; moduleBase = b;
            var h = HookBytes(b);

            if (!CanInstall(ScriptSpan(b))) return "wait";
            if (RefreshRequested)
            {
                Quiet = false;
                core.Trace("Auto : demande explicite -> mise en place du script choisi");
                Inject(script);
                RefreshRequested = false;
                return "inject";
            }
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
                var span = ScriptSpan(b);
                if (span.Target != null && !InRange(span, hooked.Near, hooked.BcLen))
                {
                    Quiet = false;
                    core.Trace(string.Format("Auto : scripts du jeu déplacés ({0}..{1}), menu à {2} hors de portée -> réinjection", X(span.Lo), X(span.Hi), X(hooked.Near)));
                    Inject(script);
                    return "inject";
                }
            }
            return msg;
        }
        finally { Quiet = false; }
    }

    public int UnlockTrophies(int first, int last, Action<int, int, string> progress)
    { throw new NotSupportedException("Trophees IW non portes dans cette version."); }

    static readonly ulong[] PrivMaskSites = { 0x1090, 0x10A0, 0x10B0, 0x10C0, 0x10D0, 0x10E0 };
    const ulong PrivEndSite = 0x10F0;
    static readonly byte[] PrivEndOrig = { 0x0F, 0x85, 0xC0, 0xF9, 0xFF, 0xFF };

    int PrivSiteState(ulong b, ulong site)
    {

        var h = Dbg.Read(Pid, b + site, 18);
        if (h[0] != 0x48 || h[1] != 0x8B || h[7] != 0x80 || h[9] != 0x10 || h[10] != 0x00 || (h[11] != 0xBA && h[11] != 0xBE)) return -1;
        if ((long)site + 7 + BitConverter.ToInt32(h, 3) != 0x1160) return -1;
        if (h[16] == 0x75 && h[17] == 0x14) return 0;
        if (h[16] == 0x90 && h[17] == 0x90) return 1;
        return -1;
    }

    int PrivEndState(ulong b)
    {
        var h = Dbg.Read(Pid, b + PrivEndSite, 17);
        if (h[0] != 0x48 || h[1] != 0x8B || h[2] != 0x0D || (long)PrivEndSite + 7 + BitConverter.ToInt32(h, 3) != 0x1160) return -1;
        var j = new byte[6]; Array.Copy(h, 11, j, 0, 6);
        if (Same(j, PrivEndOrig)) return 0;
        bool nop = true; foreach (var x in j) if (x != 0x90) nop = false;
        return nop ? 1 : -1;
    }

    void PatchPrivateStats(ulong b, bool on)
    {
        try
        {
            int done = 0, bad = 0;
            foreach (var site in PrivMaskSites) if (PrivSiteState(b, site) < 0) bad++;
            if (PrivEndState(b) < 0) bad++;
            if (bad > 0) { Log("  stats privées : code inattendu (" + bad + " endroit(s)), rien modifié"); return; }
            foreach (var site in PrivMaskSites)
            {
                if (PrivSiteState(b, site) == (on ? 1 : 0)) continue;
                Dbg.Write(Pid, b + site + 16, on ? new byte[] { 0x90, 0x90 } : new byte[] { 0x75, 0x14 });
                done++;
            }
            if (PrivEndState(b) != (on ? 1 : 0))
            {
                Dbg.Write(Pid, b + PrivEndSite + 11, on ? new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 } : PrivEndOrig);
                done++;
            }
            if (on) Log(done > 0 ? "  stats : partie privée -> stats classées gardées (" + done + " correctif(s))" : "  stats : partie privée déjà en stats classées");
            else if (done > 0) Log("  stats privées : code d'origine remis");
        }
        catch (Exception ex) { Log("  stats privées : patch impossible (" + ex.Message + ")"); }
    }

    public bool Remove()
    {
        Log("=== Retrait ===");
        ulong b = FindBase();
        if (b == 0) return false;
        bool text = TextPatched(b) || ClearPatched(b);
        var h = HookBytes(b);
        if (Same(h, HookOriginal))
        {
            RemoveTextPatches(b);
            PatchPrivateStats(b, false);
            Log(text ? "=== Retrait : correctifs des textes HUD retirés ===" : "=== Rien à retirer : code d'origine ===");
            return text;
        }
        if (!IsOurHook(b, h)) throw new Exception(L.T("point de chargement modifié par un autre outil : retrait refusé", "load point modified by another tool: removal refused"));
        Dbg.Write(Pid, b + HookSite, HookOriginal);
        RemoveTextPatches(b);
        PatchPrivateStats(b, false);
        Log("=== Retrait terminé : appel d'origine remis (la prochaine partie charge le script du jeu) ===");
        return true;
    }
}
