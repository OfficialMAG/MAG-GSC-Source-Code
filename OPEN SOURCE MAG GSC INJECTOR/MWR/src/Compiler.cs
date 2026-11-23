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
        string here = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "compiler", "gsc-tool-mwr.exe");
        return File.Exists(here) ? here : null;
    }

    class Native { public int Ps4 = -1; public bool Deduced; public string Name; }
    static Dictionary<int, Native> funcs, meths;
    static string[] opName;
    static void LoadTables()
    {
        if (funcs != null) return;
        funcs = new Dictionary<int, Native>(); meths = new Dictionary<int, Native>();
        foreach (var line in Encoding.ASCII.GetString(Injector.Resource("natives_mwr.txt")).Split('\n'))
        {
            var t = line.Trim().Split(' '); if (t.Length < 5) continue;
            var n = new Native { Name = t[4] };
            if (t[2] != "-") { n.Ps4 = Convert.ToInt32(t[2], 16); n.Deduced = t[3] == "d"; }
            (t[0] == "f" ? funcs : meths)[Convert.ToInt32(t[1], 16)] = n;
        }
        opName = new string[256];
        foreach (var line in Encoding.ASCII.GetString(Injector.Resource("ops_mwr.txt")).Split('\n'))
        {
            var t = line.Trim().Split(' '); if (t.Length < 2) continue;
            opName[Convert.ToInt32(t[0], 16)] = t[1];
        }
    }

    static readonly Dictionary<string, int> Size = BuildSizes();
    static Dictionary<string, int> BuildSizes()
    {
        var d = new Dictionary<string, int>();
        string[][] groups = {
            ("CastFieldObject plus GetGameRef GetThisthread greater shift_right dec bit_or equality ClearLocalVariableFieldCached0 notify PreScriptCall GetUndefined SetLocalVariableFieldCached0 GetLevel size AddArray endon shift_left EvalLocalArrayRefCached0 Return SafeSetVariableFieldCached0 GetSelfObject GetGame EvalArray GetSelf End less_equal EvalLocalVariableCached0 EvalLocalVariableCached1 EvalLocalVariableCached2 EvalLocalVariableCached3 EvalLocalVariableCached4 EvalLocalVariableCached5 ScriptMethodCallPointer checkclearparams waittillmatch2 minus greater_equal vector ClearArray DecTop CastBool EvalArrayRef GetZero wait waittill GetAnimObject mod clearparams ScriptFunctionCallPointer EmptyArray BoolComplement less BoolNot waittillFrameEnd waitframe GetLevelObject inc GetAnim SetVariableField divide multiply EvalLocalVariableRefCached0 bit_and voidCodepos inequality bit_ex_or BoolNotAfterAnd IsDefined IsTrue ClearVariableField EvalNewLocalVariableRefCached0").Split(' '),
            ("SetLocalVariableFieldCached RemoveLocalVariables waittillmatch ScriptMethodChildThreadCallPointer GetByte SafeSetWaittillVariableFieldCached CallBuiltinMethodPointer EvalLocalVariableCached ScriptChildThreadCallPointer EvalLocalVariableObjectCached GetNegByte GetAnimTree EvalLocalArrayCached ScriptMethodThreadCallPointer SafeSetVariableFieldCached EvalLocalVariableRefCached ScriptThreadCallPointer ClearLocalVariableFieldCached EvalLocalArrayRefCached CallBuiltinPointer CreateLocalVariable EvalNewLocalArrayRefCached0 SafeCreateVariableFieldCached SetNewLocalVariableFieldCached0").Split(' '),
            ("JumpOnTrue JumpOnFalseExpr jumpback JumpOnTrueExpr CallBuiltin0 CallBuiltin1 CallBuiltin2 CallBuiltin3 CallBuiltin4 CallBuiltin5 GetBuiltinFunction GetNegUnsignedShort GetBuiltinMethod GetUnsignedShort JumpOnFalse CallBuiltinMethod0 CallBuiltinMethod1 CallBuiltinMethod2 CallBuiltinMethod3 CallBuiltinMethod4 CallBuiltinMethod5 EvalSelfFieldVariableRef EvalAnimFieldVariable EvalLevelFieldVariableRef SetSelfFieldVariableField ClearFieldVariable EvalFieldVariable SetAnimFieldVariableField SetLevelFieldVariableField EvalSelfFieldVariable EvalFieldVariableRef EvalLevelFieldVariable EvalAnimFieldVariableRef").Split(' '),
            ("CallBuiltin ScriptLocalMethodCall ScriptLocalFunctionCall2 ScriptFarFunctionCall2 CallBuiltinMethod GetLocalFunction ScriptLocalFunctionCall GetFarFunction ScriptFarFunctionCall ScriptFarMethodCall").Split(' '),
            ("ScriptLocalMethodThreadCall ScriptLocalChildThreadCall ScriptLocalThreadCall GetInteger ScriptLocalMethodChildThreadCall GetFloat switch jump GetString GetIString ScriptFarThreadCall ScriptFarChildThreadCall ScriptFarMethodThreadCall ScriptFarMethodChildThreadCall").Split(' ') };
        for (int g = 0; g < groups.Length; g++) foreach (var o in groups[g]) d[o] = g + 1;
        d["GetAnimation"] = 9; d["GetVector"] = 13;
        return d;
    }

    public class Conversion { public byte[] Data; public int Natives; public List<string> Deduced = new List<string>(); }

    public static Conversion Convert2Ps4(byte[] gscbin)
    {
        LoadTables();
        string err;
        if (Injector.Parse(gscbin, out err) == null) throw new Exception(err);
        int cl = BitConverter.ToInt32(gscbin, 4), bl = BitConverter.ToInt32(gscbin, 12);
        var d = (byte[])gscbin.Clone();
        int start = 16 + cl, p = 1, n = 0;
        var missing = new List<string>(); var r = new Conversion();
        while (p < bl)
        {
            string op = opName[d[start + p]];
            if (op == null) throw new Exception(string.Format("opcode inconnu 0x{0:X2} à 0x{1:X}", d[start + p], p));
            int size;
            if (op == "FormalParams") size = 2 + d[start + p + 1];
            else if (op == "endswitch") size = 3 + 7 * BitConverter.ToUInt16(d, start + p + 1);
            else if (!Size.TryGetValue(op, out size)) throw new Exception("opcode non géré : " + op);
            int q = -1; bool meth = op.Contains("Method");
            if (op.StartsWith("CallBuiltin") && op != "CallBuiltinPointer" && op != "CallBuiltinMethodPointer") q = (op == "CallBuiltin" || op == "CallBuiltinMethod") ? p + 2 : p + 1;
            else if (op == "GetBuiltinFunction" || op == "GetBuiltinMethod") q = p + 1;
            if (q >= 0)
            {
                int pc = BitConverter.ToUInt16(d, start + q);
                Native nat;
                (meth ? meths : funcs).TryGetValue(pc, out nat);
                if (nat == null || nat.Ps4 < 0) missing.Add(nat != null ? nat.Name : (meth ? "_meth_" : "_func_") + pc.ToString("X4"));
                else
                {
                    BitConverter.GetBytes((ushort)nat.Ps4).CopyTo(d, start + q); n++;
                    if (nat.Deduced && !r.Deduced.Contains(nat.Name)) r.Deduced.Add(nat.Name);
                }
            }
            p += size;
        }
        if (p != bl) throw new Exception("fin du bytecode désalignée");
        if (missing.Count > 0) throw new Exception(L.T("fonctions du moteur sans numéro PS4 connu : ", "engine functions with no known PS4 number: ") + string.Join(", ", missing.ToArray()));
        r.Data = d; r.Natives = n;
        return r;
    }

    public class Result { public byte[] Script; public string Error; public List<string> Output = new List<string>(); }

    public static Result Compile(SortedDictionary<string, string> sources, Action<string> log)
    {
        var r = new Result();
        string tool = FindTool();
        if (tool == null) { r.Error = L.T("compilateur introuvable : le dossier « compiler » (gsc-tool-mwr.exe) doit être à côté de l'exe", "compiler not found: the \"compiler\" folder (gsc-tool-mwr.exe) must be next to the exe"); return r; }
        log("Compilateur : " + tool);
        string tmp = Path.Combine(Path.GetTempPath(), "MAG_MWR_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tmp);
        try
        {

            string inc = Path.Combine(Path.GetDirectoryName(tool), "include");
            if (Directory.Exists(inc))
                foreach (var f in Directory.GetFiles(inc, "*.gsc", SearchOption.AllDirectories))
                {
                    string dst = Path.Combine(tmp, f.Substring(inc.Length).TrimStart('\\', '/'));
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)); File.Copy(f, dst, true);
                }
            var includes = new List<string>(); var body = new StringBuilder();
            foreach (var kv in sources)
            {
                log("  fichier : " + kv.Key + " (" + kv.Value.Length + " caractères)");
                foreach (var line in kv.Value.TrimStart('﻿').Replace("\r\n", "\n").Split('\n'))
                {
                    var t = line.Trim();
                    if (t.StartsWith("#include")) { if (!includes.Contains(t)) includes.Add(t); body.Append("\n"); }
                    else body.Append(line).Append('\n');
                }
            }
            foreach (var i in includes)
            {
                var m = Regex.Match(i, @"#include\s+([\w\\/]+)\s*;");
                if (!m.Success) continue;
                string rel = m.Groups[1].Value.Replace('\\', '/') + ".gsc";
                if (!File.Exists(Path.Combine(tmp, rel))) { r.Error = string.Format(L.T("#include inconnu : {0} (fichiers disponibles dans compiler\\include)", "unknown #include: {0} (available files in compiler\\include)"), m.Groups[1].Value); return r; }
            }
            string src = string.Join("\n", includes.ToArray()) + "\n" + body.ToString();
            File.WriteAllText(Path.Combine(tmp, "mag.gsc"), src, new UTF8Encoding(false));
            var psi = new ProcessStartInfo(tool, "-m comp -g h1 -s pc mag.gsc")
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
            string outFile = Path.Combine(tmp, "compiled", "h1", "mag.gscbin");
            if (!File.Exists(outFile))
            {
                string first = null;
                foreach (var l in r.Output) if (l.Contains("ERROR")) { first = l; break; }
                r.Error = first ?? L.T("aucun fichier produit", "no file produced");
                return r;
            }
            try
            {
                var c = Convert2Ps4(File.ReadAllBytes(outFile));
                log(string.Format("Conversion PS4 : {0} appel(s) de fonctions du moteur", c.Natives));
                if (c.Deduced.Count > 0) log("  numéros PS4 déduits (jamais vus dans les scripts du jeu) : " + string.Join(", ", c.Deduced.ToArray()));
                r.Script = c.Data;
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

}
