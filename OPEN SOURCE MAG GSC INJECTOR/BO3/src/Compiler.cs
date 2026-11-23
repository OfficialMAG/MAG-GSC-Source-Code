// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

public static class GscCompiler
{
    public static readonly string[] BaseSymbols = { "BO3", "T7", "PS4", "__PS4", "_GSC" };

    public static string FindActs()
    {
        string here = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "compiler", "acts.exe");
        if (File.Exists(here)) return here;
        string dev = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Logiciels", "acts_3.3.0", "acts", "bin", "acts.exe");
        if (File.Exists(dev)) return dev;
        return null;
    }

    public static List<string> Symbols(string projectDir)
    {
        var l = new List<string>(BaseSymbols);
        string conf = projectDir == null ? null : Path.Combine(projectDir, "gsc.conf");
        if (conf != null && File.Exists(conf))
            foreach (var line in File.ReadAllLines(conf))
            {
                var t = line.Trim(); if (t.StartsWith("#")) continue;
                int i = t.IndexOf('='); if (i < 0 || !t.Substring(0, i).Trim().Equals("symbols", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var s in t.Substring(i + 1).Split(',')) if (s.Trim().Length > 0 && !l.Contains(s.Trim())) l.Add(s.Trim());
            }
        return l;
    }

    public static string Preprocess(string src, string file, ICollection<string> symbols)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); foreach (var s in symbols) set.Add(s);
        var lines = src.Replace("\r\n", "\n").Split('\n');
        var stack = new Stack<bool[]>();
        bool active = true;
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].Trim();
            string kw = t.Split(new[] { ' ', '\t' }, 2)[0];
            if (kw == "#ifdef" || kw == "#ifndef")
            {
                string sym = t.Length > kw.Length ? t.Substring(kw.Length).Trim().Split(' ', '\t', '/')[0] : "";
                if (sym.Length == 0) throw new Exception(string.Format(L.T("{0} ligne {1} : symbole manquant après {2}", "{0} line {1}: missing symbol after {2}"), file, i + 1, kw));
                bool cond = set.Contains(sym) == (kw == "#ifdef");
                stack.Push(new[] { active, cond, false });
                active = active && cond; lines[i] = ""; continue;
            }
            if (kw == "#else")
            {
                if (stack.Count == 0) throw new Exception(string.Format(L.T("{0} ligne {1} : #else sans #ifdef", "{0} line {1}: #else without #ifdef"), file, i + 1));
                var top = stack.Peek(); if (top[2]) throw new Exception(string.Format(L.T("{0} ligne {1} : deuxième #else", "{0} line {1}: second #else"), file, i + 1));
                top[2] = true; active = top[0] && !top[1]; lines[i] = ""; continue;
            }
            if (kw == "#endif")
            {
                if (stack.Count == 0) throw new Exception(string.Format(L.T("{0} ligne {1} : #endif sans #ifdef", "{0} line {1}: #endif without #ifdef"), file, i + 1));
                active = stack.Pop()[0]; lines[i] = ""; continue;
            }
            if (!active) lines[i] = "";
        }
        if (stack.Count > 0) throw new Exception(string.Format(L.T("{0} : #endif manquant", "{0}: missing #endif"), file));
        return string.Join("\n", lines);
    }

    public static string EmptyArrays(string src)
    {
        return System.Text.RegularExpressions.Regex.Replace(src, @"(?<=[=(,]\s*)\[\s*\](?=\s*[;),])", "getentarray(\"mag_none_x\", \"targetname\")");
    }

    public class Result { public byte[] Script; public string Error; public List<string> Output = new List<string>(); }

    public static Result Compile(SortedDictionary<string, string> sources, string projectDir, Action<string> log)
    {
        var r = new Result();
        string acts = FindActs();
        if (acts == null) { r.Error = L.T("compilateur introuvable : le dossier « compiler » doit être à côté de l'exe", "compiler not found: the \"compiler\" folder must be next to the exe"); return r; }
        var syms = Symbols(projectDir);
        log("Compilateur : " + acts);
        log("Symboles : " + string.Join(", ", syms.ToArray()));
        string tmp = Path.Combine(Path.GetTempPath(), "MAG_BO3_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tmp);
        try
        {
            var args = new StringBuilder("gscc -g bo3 -p ps --name [masqué] -o \"" + Path.Combine(tmp, "out") + "\"");
            foreach (var kv in sources)
            {
                string p = Path.Combine(tmp, "src", kv.Key.Replace('/', '\\'));
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                string pre;
                try { pre = EmptyArrays(Preprocess(kv.Value.TrimStart('﻿'), kv.Key, syms)); }
                catch (Exception ex) { r.Error = ex.Message; log("  " + ex.Message); return r; }
                File.WriteAllText(p, pre, new UTF8Encoding(false));
                args.Append(" \"" + p + "\"");
                log("  fichier : " + kv.Key + " (" + kv.Value.Length + " caractères)");
            }
            var psi = new ProcessStartInfo(acts, args.ToString())
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(acts), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            var sw = Stopwatch.StartNew();
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                string so = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(120000)) { try { p.Kill(); } catch { } r.Error = L.T("le compilateur ne répond plus (2 min)", "the compiler stopped responding (2 min)"); return r; }
                foreach (var line in (so + "\n" + err.Result).Split('\n'))
                {
                    string l = line.TrimEnd('\r'); if (l.Trim().Length == 0 || l.StartsWith("Atian tools") || l.StartsWith("Can't use updater")) continue;
                    l = l.Replace(Path.Combine(tmp, "src") + "\\", "").Replace(tmp, "");
                    r.Output.Add(l); log("  acts : " + l);
                }
                log(string.Format("Compilation : code {0}, {1} ms", p.ExitCode, sw.ElapsedMilliseconds));
            }
            string outFile = Path.Combine(tmp, "out.gscc");
            if (!File.Exists(outFile))
            {
                string first = null;
                foreach (var l in r.Output) if (l.Contains("#") && l.Contains(":")) { first = l; break; }
                if (first == null && r.Output.Count > 0) first = r.Output[r.Output.Count - 1];
                r.Error = first ?? L.T("aucun fichier produit", "no file produced");
                return r;
            }
            r.Script = File.ReadAllBytes(outFile);
            string bad = Injector.CheckScript(r.Script);
            if (bad != null) { r.Error = bad; r.Script = null; }
            return r;
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    public static SortedDictionary<string, string> ReadProject(string dir)
    {
        var d = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string root = dir.TrimEnd('\\', '/');
        foreach (var f in Directory.GetFiles(dir, "*.gsc", SearchOption.AllDirectories))
            d[f.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/')] = File.ReadAllText(f);
        return d;
    }
}
