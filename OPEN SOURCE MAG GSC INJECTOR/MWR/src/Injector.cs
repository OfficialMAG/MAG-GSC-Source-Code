// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

public class Injector
{
    public const string TargetName = "[masqué]";
    public const string TargetPath = "[masqué]";
    const int ScriptType = 0x30, EntrySize = 32, AssetSize = 0x28;
    const ulong Buckets = 0x10A0, Entries = 0x10B0;
    const uint BucketCount = 0x1040;
    const ulong CheckString = 0x1090;
    const string CheckText = "Could not find script";

    public class Asset { public ulong Addr, Buffer, Bytecode; public int CompLen, Len, BcLen; }

        static int basePid = -1; static ulong moduleBase;

    readonly Core core;
    public volatile bool Cancel;
    public Injector(Core c) { core = c; }
    Ps4Debug Dbg { get { return core.Dbg; } }
    int Pid { get { return core.Pid; } }
    public bool Quiet;
    void Log(string t) { if (!Quiet) core.Trace(t); }
    static string X(ulong v) { return "0x" + v.ToString("X"); }

    static byte[] menu, test;
    public static byte[] Menu()
    {
        if (menu == null) menu = Unlock(Resource("m.dat"));
        return menu;
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

    public static byte[] TestScript()
    {
        if (test == null) test = Resource("mag_test_mwr.gscbin");
        return test;
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
        if (IsBase(0x1060))
        {
            basePid = Pid; moduleBase = 0x1060;
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
        Log("Base de default_mp.elf introuvable : ce n'est pas le multijoueur de MWR 1.15 ?");
        return 0;
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
                    if (nm != null && string.Equals(nm, name, StringComparison.OrdinalIgnoreCase))
                    {
                        var a = Dbg.Read(Pid, asset, AssetSize);
                        var r = new Asset
                        {
                            Addr = asset, CompLen = BitConverter.ToInt32(a, 8), Len = BitConverter.ToInt32(a, 0xC), BcLen = BitConverter.ToInt32(a, 0x10),
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

    public Asset Find()
    {
        var sw = Stopwatch.StartNew();
        ulong b = FindBase();
        if (b == 0) throw new Exception(L.T("ce n'est pas Modern Warfare Remastered 1.15 en multijoueur (exécutable non reconnu)", "this is not Modern Warfare Remastered 1.15 multiplayer (executable not recognised)"));
        Log("Recherche du script cible dans la table des assets...");
        var a = Lookup(b, TargetName);
        Log(string.Format("Recherche terminée en {0} ms", sw.ElapsedMilliseconds));
        return a;
    }

    const ulong HookSite = 0x1080, FindHeader = 0x1070;
    static readonly byte[] HookOriginal = { 0xE8, 0x9B, 0xB6, 0xDD, 0xFF };

    class Hooked { public int Pid; public ulong Block, Cave, Header, Bytecode; public byte[] BcHead; }
    static Hooked hooked;

    public static byte[] BuildBlock(ulong at, ulong baseAddr, Script s, ulong bytecode, out ulong cave, out ulong header)
    {
        header = at; ulong name = at + 0x28; cave = at + 0x38;
        ulong buf = at + 0x98;
        var d = new byte[0x98 + s.Buffer.Length];
        BitConverter.GetBytes(name).CopyTo(d, 0);
        BitConverter.GetBytes(s.Buffer.Length).CopyTo(d, 8);
        BitConverter.GetBytes(s.Len).CopyTo(d, 0xC);
        BitConverter.GetBytes(s.Bytecode.Length).CopyTo(d, 0x10);
        BitConverter.GetBytes(buf).CopyTo(d, 0x18);
        BitConverter.GetBytes(bytecode).CopyTo(d, 0x20);
        Encoding.ASCII.GetBytes("[masqué]").CopyTo(d, 0x28);
        var c = new List<byte>();
        c.AddRange(new byte[] { 0x48, 0x83, 0xEC, 0x08 });
        long rel = (long)(baseAddr + FindHeader) - (long)(cave + 4 + 5);
        if (rel < int.MinValue || rel > int.MaxValue) throw new Exception(L.T("bloc alloué trop loin du code du jeu", "allocated block too far from the game code"));
        c.Add(0xE8); c.AddRange(BitConverter.GetBytes((int)rel));
        c.AddRange(new byte[] { 0x48, 0x83, 0xC4, 0x08 });
        c.Add(0x48); c.Add(0xB9); c.AddRange(BitConverter.GetBytes(header + 0x30));
        c.AddRange(new byte[] { 0xFF, 0x01 });
        c.AddRange(new byte[] { 0x48, 0x85, 0xC0 });
        c.AddRange(new byte[] { 0x74, 0x3D });
        c.AddRange(new byte[] { 0x48, 0x8B, 0x08 });
        c.AddRange(new byte[] { 0x48, 0x85, 0xC9 });
        c.AddRange(new byte[] { 0x74, 0x35 });
        c.AddRange(new byte[] { 0x48, 0x8B, 0x11 });
        c.AddRange(new byte[] { 0x49, 0xB8, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00 });
        c.AddRange(new byte[] { 0x4C, 0x21, 0xC2 });
        c.AddRange(new byte[] { 0x49, 0xB8, 0x34, 0x32, 0x39, 0x32, 0x38, 0x00, 0x00, 0x00 });
        c.AddRange(new byte[] { 0x4C, 0x39, 0xC2 });
        c.AddRange(new byte[] { 0x75, 0x16 });
        c.Add(0x48); c.Add(0xB9); c.AddRange(BitConverter.GetBytes(header + 0x34));
        c.AddRange(new byte[] { 0xFF, 0x01 });
        c.Add(0x48); c.Add(0xB8); c.AddRange(BitConverter.GetBytes(header));
        c.Add(0xC3);
        if (c.Count > 0x60) throw new Exception("code du détour trop long");
        c.ToArray().CopyTo(d, 0x38);
        s.Buffer.CopyTo(d, (int)(buf - at));
        return d;
    }

    const ulong ScriptArena = 0x10E0, NearWindow = 0x1050;
    int rpcPid = -1; ulong rpc, mmap, munmap;

    public static ulong FindSyscall(byte[] code, ulong start, int number)
    {
        var sig = new List<byte>(new byte[] { 0x48, 0xC7, 0xC0 });
        sig.AddRange(BitConverter.GetBytes(number));
        sig.AddRange(new byte[] { 0x49, 0x89, 0xCA, 0x0F, 0x05, 0x72, 0x01, 0xC3 });
        ulong found = 0;
        for (int i = 0; i <= code.Length - sig.Count; i++)
        {
            int j = 0; while (j < sig.Count && code[i + j] == sig[j]) j++;
            if (j != sig.Count) continue;
            if (found != 0) throw new Exception("Plusieurs wrappers système correspondants");
            found = start + (ulong)i;
        }
        return found;
    }

    ulong AllocBytecode(int length)
    {
        if (length > 0x1030) throw new Exception("Bytecode trop grand pour la zone proche MWR 1.15 (128 Ko maximum)");
        int size = (int)Align((ulong)length, 0x1010);
        if (rpcPid != Pid)
        {
            mmap = munmap = rpc = 0;
            foreach (var m in Dbg.Maps(Pid))
            {
                if ((m.Name ?? "").IndexOf("libkernel", StringComparison.OrdinalIgnoreCase) < 0 || (m.Prot & 4) == 0) continue;
                if (m.End <= m.Start || m.End - m.Start > 0x1060) continue;
                var code = Dbg.Read(Pid, m.Start, (int)(m.End - m.Start));
                mmap = FindSyscall(code, m.Start, 477); munmap = FindSyscall(code, m.Start, 73);
                if (mmap != 0 && munmap != 0) break;
            }
            if (mmap == 0 || munmap == 0) throw new Exception("Wrappers mmap/munmap de libkernel introuvables : injection annulée");
            rpc = Dbg.InstallRpc(Pid);
            if (rpc == 0 || rpc == ulong.MaxValue) throw new Exception("Installation RPC refusée");
            rpcPid = Pid;
        }
        var maps = Dbg.Maps(Pid);
        for (ulong hint = ScriptArena - (ulong)size; hint >= ScriptArena - NearWindow; hint -= 0x1010)
        {
            bool occupied = false;
            foreach (var m in maps)
            {

                if (m.Start == 0x1010 && m.End == 0x10F0) continue;
                if (hint < m.End && hint + (ulong)size > m.Start) { occupied = true; break; }
            }
            if (occupied) continue;
            ulong addr = Dbg.Call(Pid, rpc, mmap, hint, (ulong)size, 3, 0x1000, ulong.MaxValue, 0);
            if (addr == 0 || addr == ulong.MaxValue) continue;
            if (addr != hint)
            {
                if (Dbg.Call(Pid, rpc, munmap, addr, (ulong)size) != 0) throw new Exception("Impossible de libérer une allocation hors portée");
                continue;
            }
            Log(string.Format("  bytecode proche des scripts : {0} o -> {1} (appels relatifs 22 bits)", size, X(addr)));
            return addr;
        }
        throw new Exception("Aucune mémoire proche disponible : redémarre complètement MWR puis réinjecte depuis le menu multijoueur");
    }

    byte[] HookBytes(ulong b) { return Dbg.Read(Pid, b + HookSite, 5); }
    static bool Same(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    ulong HookTarget(ulong b, byte[] h) { return h[0] != 0xE8 ? 0 : (ulong)((long)(b + HookSite + 5) + BitConverter.ToInt32(h, 1)); }

    public class Status { public bool Found, ScriptIn, Reset, Intact; public int BcLen, Calls = -1, Ours = -1; }
    public Status GetStatus()
    {
        var s = new Status();
        ulong b = FindBase();
        if (b == 0) throw new Exception(L.T("ce n'est pas Modern Warfare Remastered 1.15 en multijoueur (exécutable non reconnu)", "this is not Modern Warfare Remastered 1.15 multiplayer (executable not recognised)"));
        s.Found = true;
        var h = HookBytes(b);
        if (Same(h, HookOriginal)) { Log("État : code d'origine, pas de script injecté"); return s; }
        ulong t = HookTarget(b, h);
        s.ScriptIn = true;
        Log(string.Format("État : détour en place vers {0}", X(t)));
        try
        {
            var cnt = Dbg.Read(Pid, t - 0x38 + 0x30, 8);
            s.Calls = BitConverter.ToInt32(cnt, 0); s.Ours = BitConverter.ToInt32(cnt, 4);
            Log(string.Format("  depuis la pose du détour, le jeu a chargé {0} script(s), dont {1} fois le script injecté ([masqué])", s.Calls, s.Ours));
        }
        catch (Exception ex) { Log("  compteurs illisibles : " + ex.Message); }
        if (hooked != null && hooked.Pid == Pid && hooked.Cave == t)
        {
            var now = Dbg.Read(Pid, hooked.Bytecode, hooked.BcHead.Length);
            s.Intact = Same(now, hooked.BcHead);
            Log(s.Intact ? "  bytecode brut inchangé (chargement pas encore confirmé)" : "  bytecode réécrit depuis l'injection (les liaisons du chargeur modifient normalement les références ; ceci ne confirme pas l'exécution)");
        }
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
        if (b == 0) throw new Exception(L.T("ce n'est pas Modern Warfare Remastered 1.15 en multijoueur (exécutable non reconnu)", "this is not Modern Warfare Remastered 1.15 multiplayer (executable not recognised)"));
        var h = HookBytes(b);
        bool orig = Same(h, HookOriginal);
        if (!orig && h[0] != 0xE8) throw new Exception(L.T("code du jeu inattendu à l'adresse du détour : ", "unexpected game code at the hook address: ") + BitConverter.ToString(h));
        Log(orig ? "Code d'origine en place : pose du détour" : "Détour déjà posé : nouveau script");

        ulong tmpCave, tmpHeader;
        int size = BuildBlock(0x10C0, 0x10C0, s, 0x10D0, out tmpCave, out tmpHeader).Length;
        ulong bytecode = AllocBytecode(s.Bytecode.Length);
        ulong alloc = 0; bool published = false;
        try
        {
        alloc = Dbg.Alloc(Pid, size + 64);
        Log(string.Format("  allocation : {0} o -> {1}", size + 64, X(alloc)));
        if (alloc == 0 || alloc == ulong.MaxValue) throw new Exception(L.T("allocation mémoire refusée par ps4debug", "memory allocation refused by ps4debug"));
        ulong at = Align(alloc, 16), cave, header;
        var block = BuildBlock(at, b, s, bytecode, out cave, out header);
        Dbg.Write(Pid, bytecode, s.Bytecode);
        if (!Same(Dbg.Read(Pid, bytecode, s.Bytecode.Length), s.Bytecode)) throw new Exception("Vérification du bytecode proche échouée");
        for (int o = 0; o < block.Length; o += 0x1020)
        {
            int n = Math.Min(0x1020, block.Length - o);
            var part = new byte[n]; Array.Copy(block, o, part, 0, n);
            Dbg.Write(Pid, at + (ulong)o, part);
        }
        var back = Dbg.Read(Pid, at, Math.Min(block.Length, 0x200));
        for (int i = 0; i < back.Length; i++) if (back[i] != block[i]) throw new Exception(L.T("vérification de l'écriture échouée à ", "write check failed at ") + X(at));
        Log(string.Format("  bloc écrit à {0} : en-tête {1}, détour {2}, bytecode {3} ({4} o) — relu OK", X(at), X(header), X(cave), X(bytecode), s.Bytecode.Length));

        var call = new byte[5]; call[0] = 0xE8;
        BitConverter.GetBytes((int)((long)cave - (long)(b + HookSite + 5))).CopyTo(call, 1);
        published = true;
        Dbg.Write(Pid, b + HookSite, call);
        if (!Same(HookBytes(b), call)) throw new Exception(L.T("le code du jeu n'a pas pu être modifié", "the game code could not be modified"));
        Log(string.Format("  appel {0} -> détour {1} (était {2})", X(b + HookSite), X(cave), BitConverter.ToString(h)));
        hooked = new Hooked { Pid = Pid, Block = at, Cave = cave, Header = header, Bytecode = bytecode, BcHead = new byte[Math.Min(256, s.Bytecode.Length)] };
        Array.Copy(s.Bytecode, hooked.BcHead, hooked.BcHead.Length);
        Log(string.Format("=== Injection terminée en {0} ms : le script sera chargé à la prochaine carte ===", sw.ElapsedMilliseconds));
        }
        catch
        {
            if (!published)
            {
                try { Dbg.Call(Pid, rpc, munmap, bytecode, Align((ulong)s.Bytecode.Length, 0x1010)); } catch { }
                if (alloc != 0 && alloc != ulong.MaxValue) { try { Dbg.Free(Pid, alloc, size + 64); } catch { } }
            }
            throw;
        }
    }

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
            ulong b = (basePid == Pid && moduleBase != 0) ? moduleBase : (IsBase(0x1060) ? 0x1060UL : 0);
            if (b == 0) return msg;
            basePid = Pid; moduleBase = b;
            var h = HookBytes(b);
            if (!Same(h, HookOriginal)) return msg;
            Quiet = false;
            core.Trace("Auto : code d'origine -> pose du détour");
            Inject(script);
            return "inject";
        }
        finally { Quiet = false; }
    }

    public bool Remove()
    {
        Log("=== Retrait ===");
        ulong b = FindBase();
        if (b == 0) return false;
        if (Same(HookBytes(b), HookOriginal)) { Log("=== Rien à retirer : code d'origine ==="); return false; }
        Dbg.Write(Pid, b + HookSite, HookOriginal);
        Log("=== Retrait terminé : appel d'origine remis (la prochaine carte charge le script du jeu) ===");
        return true;
    }
}
