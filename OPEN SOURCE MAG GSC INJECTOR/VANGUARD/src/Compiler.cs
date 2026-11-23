// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
public static class GscCompiler
{
    static Dictionary<string,int> natives;
    static readonly HashSet<string> keywords=new HashSet<string>{"return","thread","childthread","wait","if","else","while","for","notify","endon","waittill","case","not","isdefined"};
    static void Load()
    {
        if(natives!=null)return;natives=new Dictionary<string,int>();
        foreach(string l in Encoding.UTF8.GetString(Injector.Resource("natives_vanguard.txt")).Split('\n'))
        {var p=l.Split('#')[0].Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);if(p.Length>=3)natives[p[0]+":"+p[2]]=Convert.ToInt32(p[1],16);}
    }
    public static string FindTool(){string p=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"compiler","gsc-tool-vanguard.exe");return File.Exists(p)?p:null;}
    public static string Prepare(string src,List<string> errors)
    {
        Load();var decl=new HashSet<string>();foreach(Match m in Regex.Matches(src,@"(?m)^(\w+)\([^\n]*\)\s*\{"))decl.Add(m.Groups[1].Value.ToLowerInvariant());
        var tokens=new List<Match>();foreach(Match m in Regex.Matches(src,@"""(?:\\.|[^""\\])*""|/\*[\s\S]*?\*/|//[^\n]*|[A-Za-z_]\w*|::|\S"))
            if(!m.Value.StartsWith("\"")&&!m.Value.StartsWith("//")&&!m.Value.StartsWith("/*"))tokens.Add(m);
        var output=new StringBuilder();int last=0;
        for(int k=0;k+1<tokens.Count;k++)
        {
            var t=tokens[k];string name=t.Value.ToLowerInvariant();if(!Regex.IsMatch(name,@"^[a-z_]\w*$")||tokens[k+1].Value!="(")continue;
            if(decl.Contains(name)||keywords.Contains(name)||Regex.IsMatch(name,@"^_(func|meth)_[0-9a-f]+$"))continue;
            string prev=k==0?"":tokens[k-1].Value;if(prev=="::"||prev==".")continue;
            string kind=Regex.IsMatch(prev,@"[\w\]\)]$")&&!keywords.Contains(prev)?"meth":"func";
            int id;if(!natives.TryGetValue(kind+":"+name,out id)){errors.Add("Native Vanguard non verifiee : "+kind+" "+name);continue;}
            output.Append(src,last,t.Index-last);output.Append("_"+kind+"_"+id.ToString("X"));last=t.Index+t.Length;
        }
        output.Append(src,last,src.Length-last);return output.ToString();
    }
    static string Run(string tool,string args,string cwd)
    {
        var p=new Process{StartInfo=new ProcessStartInfo(tool,args){WorkingDirectory=cwd,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        using(p){p.Start();var so=p.StandardOutput.ReadToEndAsync();var se=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(60000)){p.Kill();throw new Exception("Delai de compilation depasse");}string text=so.Result+se.Result;if(p.ExitCode!=0)throw new Exception(text);return text;}
    }
    static string Temp()
    {
        string p=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"compiler","temp",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;
    }
    public static int Check(byte[] data)
    {
        string err;var s=Injector.Parse(data,out err);if(s==null)throw new Exception(err);
        string tool=FindTool();if(tool==null)throw new Exception("Dossier compiler absent : extrais le ZIP complet.");
        Load();string tmp=Temp();
        try
        {
            File.WriteAllBytes(Path.Combine(tmp,"mag.gscbin"),data);Run(tool,"-m disasm -g s4 -s ps4 mag.gscbin",tmp);
            string asm=Path.Combine(tmp,"disassembled","s4","mag.gscasm");if(!File.Exists(asm))throw new Exception("Desassemblage S4 impossible");
            string text=File.ReadAllText(asm);
            if(!text.Contains("sub:main"))throw new Exception("main() absent du script compile");
            if(Regex.IsMatch(text,@"OP_(ScriptFar|GetFarFunction|NativeFar)"))throw new Exception("Appels vers un autre script non pris en charge");
            foreach(Match m in Regex.Matches(text,@"OP_(CallBuiltin\w*|GetBuiltinFunction|GetBuiltinMethod)\s+([^\r\n]+)"))
            {
                if(m.Groups[1].Value.Contains("Pointer"))continue;
                string kind=m.Groups[1].Value.Contains("Method")?"meth":"func";string tail=m.Groups[2].Value.Trim();
                var parts=tail.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);string native=parts[0];
                if(!natives.ContainsKey(kind+":"+native))throw new Exception("Native non verifiee dans le bytecode : "+native);
            }
            return Regex.Matches(text,@"(?m)^\s*OP_").Count;
        }
        finally{try{Directory.Delete(tmp,true);}catch{}}
    }
    public class Result{public byte[] Script;public string Error;public List<string> Output=new List<string>();}
    public static string WrapCpSource(string source)
    {
        if(source.Contains("// MAG_CP_BOOTSTRAP:"))return source;
        string renamed=Regex.Replace(source,@"(?m)^(\s*)main\s*\(","${1}mag_cp_menu_main(");
        string template=Encoding.UTF8.GetString(Injector.Resource("cp_bootstrap.gsc"));
        return template.Replace("// MAG_CP_USER_SOURCE",renamed);
    }
    public static void CheckCpBootstrap(byte[] file)
    {
        string temp=Temp();try
        {
            File.WriteAllBytes(Path.Combine(temp,"cp.gscbin"),file);
            Run(FindTool(),"-m disasm -g s4 -s ps4 cp.gscbin",temp);
            string code=File.ReadAllText(Path.Combine(temp,"disassembled","s4","cp.gscasm"));
            if(!code.Contains("sub:mag_cp_menu_main") || !code.Contains("sub:_id_7F8E"))throw new Exception("Script Zombies sans initialisation CP [masqué] : recompile le .gsc en mode Zombies avec cette version, ou laisse le champ vide pour le menu integre.");
        }finally{try{Directory.Delete(temp,true);}catch{}}
    }
    public static Result Compile(SortedDictionary<string,string> sources,Action<string> log)
    {
        var r=new Result();string tmp=null;
        try
        {
            var missing=MissingFunctions(sources);if(missing.Count>0)throw new Exception("Fonction obligatoire absente : "+string.Join(", ",missing));
            string tool=FindTool();if(tool==null)throw new Exception("Dossier compiler absent");
            var sb=new StringBuilder();foreach(var pair in sources)sb.AppendLine(pair.Value);string src=sb.ToString();
            if(Regex.IsMatch(src,@"(?m)^\s*#include"))throw new Exception("#include non pris en charge : un seul script autonome est remplace");
            if(Injector.IsZombies)src=WrapCpSource(src);
            var errors=new List<string>();string prep=Prepare(src,errors);if(errors.Count>0)throw new Exception(string.Join("\n",errors));
            tmp=Temp();File.WriteAllText(Path.Combine(tmp,"mag.gsc"),prep,new UTF8Encoding(false));
            string output=Run(tool,"-m comp -g s4 -s ps4 mag.gsc",tmp);r.Output.Add(output);log(output.Trim());
            string f=Path.Combine(tmp,"compiled","s4","mag.gscbin");if(!File.Exists(f))throw new Exception("Aucun script produit : "+output);
            var data=File.ReadAllBytes(f);int count=Check(data);log("Vanguard : "+count+" instructions controlees par desassemblage S4");r.Script=data;
        }
        catch(Exception ex){r.Error=ex.Message;}
        finally{if(tmp!=null)try{Directory.Delete(tmp,true);}catch{}}
        return r;
    }
    public static SortedDictionary<string,string> ReadProject(string path)
    {
        var r=new SortedDictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        if(File.Exists(path))r[path]=File.ReadAllText(path);
        else foreach(string f in Directory.GetFiles(path,"*.gsc",SearchOption.AllDirectories))r[f]=File.ReadAllText(f);return r;
    }
    public static List<string> MissingFunctions(SortedDictionary<string,string> sources)
    {
        string src=string.Join("\n",sources.Values);var r=new List<string>();foreach(string f in Injector.RequiredFunctions)if(!Regex.IsMatch(src,@"(?m)^\s*"+f+@"\s*\("))r.Add(f);return r;
    }
}
