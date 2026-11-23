// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public static class GscCompiler
{
    public static string FindTool()
    {
        string here = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "compiler", "gsc-tool-aw.exe");
        return File.Exists(here) ? here : null;
    }

    static Dictionary<string, int> natByName;
    static HashSet<int> verifiedFunc, verifiedMeth;
    static HashSet<string> known;
    static string[] opName; static int[] opSize;
    static void LoadTables()
    {
        if (natByName != null) return;
        natByName = new Dictionary<string, int>(); verifiedFunc = new HashSet<int>(); verifiedMeth = new HashSet<int>();
        foreach (var line in Encoding.UTF8.GetString(Injector.Resource("natives_aw.txt")).Split('\n'))
        {
            var t = line.Split('#')[0].Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length < 3) continue;
            int id = Convert.ToInt32(t[1], 16);
            natByName[t[0] + ":" + t[2].ToLowerInvariant()] = id;
            (t[0] == "func" ? verifiedFunc : verifiedMeth).Add(id);
        }
        known = new HashSet<string>();
        foreach (var line in Encoding.UTF8.GetString(Injector.Resource("noms_natives_aw.txt")).Split('\n'))
            if (line.Trim().Length > 0) known.Add(line.Trim().ToLowerInvariant());
        opName = new string[256]; opSize = new int[256];
        foreach (var line in Encoding.UTF8.GetString(Injector.Resource("ops_aw.txt")).Split('\n'))
        {
            if (line.StartsWith("#")) continue;
            var t = line.Trim().Split(' '); if (t.Length < 3) continue;
            int op = Convert.ToInt32(t[0], 16); opName[op] = t[1]; opSize[op] = int.Parse(t[2]);
        }
    }

    static readonly HashSet<string> Keywords = new HashSet<string> { "return", "thread", "childthread", "wait", "if", "else", "while", "for", "notify", "endon", "waittill", "case", "not" };

    public static string Prepare(string src, List<string> errors)
    {
        LoadTables();
        var decl = new HashSet<string>();
        foreach (Match m in Regex.Matches(src, @"(?m)^([A-Za-z_]\w*)\s*\([^)]*\)\s*$")) decl.Add(m.Groups[1].Value.ToLowerInvariant());
        var tok = new Regex("\"(?:\\\\.|[^\"\\\\])*\"|/\\*[\\s\\S]*?\\*/|//[^\\n]*|[A-Za-z_]\\w*|::|\\S");
        var code = new List<Match>();
        foreach (Match m in tok.Matches(src))
            if (!(m.Value.StartsWith("\"") || m.Value.StartsWith("//") || m.Value.StartsWith("/*"))) code.Add(m);
        var sb = new StringBuilder(); int last = 0;
        for (int k = 0; k < code.Count; k++)
        {
            string t = code[k].Value;

            if ((t == "&" || t == "|") && k + 3 < code.Count && code[k + 1].Value == t && code[k + 2].Value == "!" && code[k + 3].Value != "=")
            {
                int line = 1; for (int i = 0; i < code[k].Index; i++) if (src[i] == '\n') line++;
                errors.Add(string.Format(L.T("ligne {0} : « && !... » ou « || !... » refusé sur AW PS4 : écris « && (!x) » / « || (!x) »", "line {0}: \"&& !...\" or \"|| !...\" is refused on AW PS4: write \"&& (!x)\" / \"|| (!x)\""), line));
            }
            if (!Regex.IsMatch(t, @"^[A-Za-z_]\w*$") || k + 1 >= code.Count || code[k + 1].Value != "(") continue;
            string low = t.ToLowerInvariant();
            if (decl.Contains(low) || Regex.IsMatch(low, @"^_(func|meth)_[0-9a-f]+$") || Keywords.Contains(low)) continue;
            string prev = k > 0 ? code[k - 1].Value : "";
            if (prev == "::" || prev == ".") continue;
            bool method = prev.Length > 0 && Regex.IsMatch(prev.Substring(prev.Length - 1), @"[\w\]\)]") && !Keywords.Contains(prev.ToLowerInvariant());
            string kind = method ? "meth" : "func", other = method ? "func" : "meth";
            int id;
            if (natByName.TryGetValue(kind + ":" + low, out id))
            {
                sb.Append(src, last, code[k].Index - last).Append(string.Format("_{0}_{1:X}", kind, id));
                last = code[k].Index + t.Length;
                continue;
            }
            if (known.Contains(low) || natByName.ContainsKey(other + ":" + low))
            {
                int line = 1; for (int i = 0; i < code[k].Index; i++) if (src[i] == '\n') line++;
                errors.Add(string.Format(L.T("ligne {0} : native « {1} » ({2}) sans numéro PS4 sûr sur AW", "line {0}: native \"{1}\" ({2}) has no reliable PS4 number on AW"), line, t, kind));
            }
        }
        sb.Append(src, last, src.Length - last);
        return sb.ToString();
    }

    public static int Check(byte[] data)
    {
        LoadTables();
        string err; var s = Injector.Parse(data, out err);
        if (s == null) throw new Exception(err);
        var bc = s.Bytecode; int pos = 1, n = 0; var bad = new List<string>();
        while (pos < bc.Length)
        {
            int op = bc[pos]; string nm = opName[op];
            if (nm == null) throw new Exception(string.Format(L.T("opcode inconnu 0x{0:X2} à 0x{1:X}", "unknown opcode 0x{0:X2} at 0x{1:X}"), op, pos));
            if (nm == "waittillmatch" || nm == "switch" || nm == "endswitch" || nm.StartsWith("ScriptFar") || nm == "GetFarFunction" || nm == "BoolNotAfterAnd")
                throw new Exception(string.Format(L.T("instruction {0} interdite sur AW PS4 (0x{1:X}) : pas d'appel vers un autre script, pas de switch / waittillmatch / « && !x »", "instruction {0} not allowed on AW PS4 (0x{1:X}): no call into another script, no switch / waittillmatch / \"&& !x\""), nm, pos));
            int sz = opSize[op];
            if (sz < 1 || pos + sz > bc.Length) throw new Exception(string.Format(L.T("instruction {0} tronquée à 0x{1:X}", "truncated instruction {0} at 0x{1:X}"), nm, pos));
            int q = -1;
            if (nm.StartsWith("CallBuiltin") && !nm.Contains("Pointer")) q = pos + ((nm == "CallBuiltin" || nm == "CallBuiltinMethod") ? 2 : 1);
            if (nm == "GetBuiltinFunction" || nm == "GetBuiltinMethod") q = pos + 1;
            if (q >= 0)
            {
                int id = BitConverter.ToUInt16(bc, q); bool meth = nm.Contains("Method");
                if (!(meth ? verifiedMeth : verifiedFunc).Contains(id))
                {
                    string x = (meth ? "_meth_" : "_func_") + id.ToString("X");
                    if (!bad.Contains(x)) bad.Add(x);
                }
            }
            pos += sz; n++;
        }
        if (pos != bc.Length) throw new Exception(L.T("fin du bytecode désalignée", "misaligned end of bytecode"));
        if (bad.Count > 0) throw new Exception(L.T("natives sans numéro PS4 sûr sur AW dans le bytecode : ", "natives without a reliable PS4 number on AW in the bytecode: ") + string.Join(", ", bad.ToArray())
            + L.T(" (une boucle foreach appelle des natives cachées : utilise for)", " (a foreach loop calls hidden natives: use for)"));
        return n;
    }

    public class Result { public byte[] Script; public string Error; public List<string> Output = new List<string>(); }

    public static Result Compile(SortedDictionary<string, string> sources, Action<string> log)
    {
        var r = new Result();
        var miss = MissingFunctions(sources);
        if (miss.Count > 0)
        {
            r.Error = string.Format(L.T("fonction(s) obligatoire(s) absente(s) : {0} (le jeu les appelle dans le script remplacé)",
                "required function(s) missing: {0} (the game calls them in the replaced script)"), string.Join(", ", miss.ToArray()));
            return r;
        }
        string tool = FindTool();
        if (tool == null) { r.Error = L.T("compilateur introuvable : le dossier « compiler » (gsc-tool-aw.exe) doit être à côté de l'exe", "compiler not found: the \"compiler\" folder (gsc-tool-aw.exe) must be next to the exe"); return r; }
        log("Compilateur : " + tool);
        var body = new StringBuilder();
        foreach (var kv in sources)
        {
            log("  fichier : " + kv.Key + " (" + kv.Value.Length + " caractères)");
            string t = kv.Value.TrimStart('﻿').Replace("\r\n", "\n");
            if (Regex.IsMatch(t, @"(?m)^\s*#include")) { r.Error = L.T("#include refusé : les scripts du jeu ne peuvent pas être appelés depuis le script injecté sur AW PS4 (appels liés sur 22 bits)", "#include refused: game scripts cannot be called from the injected script on AW PS4 (calls linked on 22 bits)"); return r; }
            body.Append(t).Append('\n');
        }
        var errors = new List<string>();
        string src = Prepare(body.ToString(), errors);
        if (errors.Count > 0) { foreach (var e in errors) log("  " + e); r.Error = errors[0] + (errors.Count > 1 ? string.Format(L.T(" (et {0} autre(s), voir le journal)", " (and {0} more, see the log)"), errors.Count - 1) : ""); return r; }
        string tmp = Path.Combine(Path.GetTempPath(), "MAG_AW_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tmp);
        try
        {
            File.WriteAllText(Path.Combine(tmp, "mag.gsc"), src, new UTF8Encoding(false));
            var psi = new ProcessStartInfo(tool, "-m comp -g s1 -s pc mag.gsc")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = tmp, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            var sw = Stopwatch.StartNew();
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                string so = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(120000)) { try { p.Kill(); } catch { } r.Error = L.T("le compilateur ne répond plus (2 min)", "the compiler stopped responding (2 min)"); return r; }
                foreach (var line in (so + "\n" + err.Result).Split('\n'))
                {
                    string l = line.TrimEnd('\r'); if (l.Trim().Length == 0 || l.StartsWith("GSC Tool")) continue;
                    l = l.Replace(tmp, "");
                    r.Output.Add(l); log("  gsc-tool : " + l);
                }
                log(string.Format("Compilation : code {0}, {1} ms", p.ExitCode, sw.ElapsedMilliseconds));
            }
            string outFile = Path.Combine(tmp, "compiled", "s1", "mag.gscbin");
            if (!File.Exists(outFile))
            {
                string first = null;
                foreach (var l in r.Output) if (l.Contains("ERROR")) { first = l; break; }
                r.Error = first ?? L.T("aucun fichier produit", "no file produced");
                return r;
            }
            try
            {
                var data = File.ReadAllBytes(outFile);
                int n = Check(data);
                log(string.Format("Contrôle PS4 : {0} instructions, natives toutes vérifiées", n));
                r.Script = data;
            }
            catch (Exception ex) { r.Error = ex.Message; }
            return r;
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    public static SortedDictionary<string, string> ReadProject(string path)
    {
        var d = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(path)) { d[Path.GetFileName(path)] = File.ReadAllText(path); return d; }
        string root = path.TrimEnd('\\', '/');
        foreach (var f in Directory.GetFiles(path, "*.gsc", SearchOption.AllDirectories))
            d[f.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/')] = File.ReadAllText(f);
        return d;
    }

    public static List<string> MissingFunctions(SortedDictionary<string, string> sources)
    {
        var all = string.Join("\n", new List<string>(sources.Values).ToArray());
        var miss = new List<string>();
        foreach (var f in Injector.RequiredFunctions)
            if (!Regex.IsMatch(all, @"^\s*" + f + @"\s*\(\s*\)", RegexOptions.Multiline | RegexOptions.IgnoreCase)) miss.Add(f + "()");
        return miss;
    }
}
