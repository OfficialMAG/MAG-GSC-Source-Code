// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
public partial class Injector
{
    public enum GameMode { Multiplayer, Zombies }
    public static GameMode Mode;
    public static bool IsZombies { get { return false; } }
    public static string TargetPath { get { return IsZombies?"[masqué] (CP FX)":"[masqué]"; } }
    public static string[] TargetNames { get { return IsZombies?new[]{"[masqué]"}:new[]{"[masqué]",TargetPath,TargetPath+".gsc"}; } }
    public static string[] CallerNames { get { return IsZombies?new[]{"[masqué]"}:new[]{"[masqué]","[masqué]","[masqué]"}; } }
    public class ScriptsNotLoadedException : Exception { public ScriptsNotLoadedException(string message):base(message){} }
    public static string ModeName { get { return IsZombies?"Zombies":L.T("Multijoueur","Multiplayer"); } }
    public static string MenuName { get { return IsZombies?"MAG Menu MW2019 Zombies v0.1":"MAG Menu MW2019 MP v0.1"; } }
    public static string[] RequiredFunctions { get { return new[]{"main"}; } }
    public const int MaxBytecode=0x1080,CaveOffset=0x200,BufferOffset=0x400,NameOffset=0x50;

    public const ulong CallRange=0x10D0;
    public const ulong HookSite=0x10F0,FindHeader=0x10C0,Resume=0x1100;
    public static readonly byte[] HookOriginal={0xE8,0x51,0x95,0xDE,0xFF,0xBF,0x33,0x00,0x00,0x00,0x48,0x89,0xDE,0x49,0x89,0xC7};
    static readonly byte[] Magic=Encoding.ASCII.GetBytes("MAGMW167");
    readonly Core core;
    public volatile bool Cancel,RefreshRequested;
    public bool Quiet;
    public Injector(Core c){core=c;}
    Ps4Debug Dbg { get { return core.Dbg; } }
    int Pid { get { return core.Pid; } }
    void Log(string t){if(!Quiet)core.Trace(t);}
    static bool Same(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    static ulong Align(ulong n){return(n+15)&~15UL;}
    static ulong PageAlign(ulong n){return(n+0x1040)&~0x1040UL;}
    public static byte[] Resource(string name)
    {
        using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {if(s==null)throw new IOException("Ressource absente : "+name);using(var m=new MemoryStream()){s.CopyTo(m);return m.ToArray();}}
    }
    public static byte[] Menu(){return Unlock(Resource("m_mp.dat"));}
    public static byte[] TestScript(){return Resource(IsZombies?"mag_test_zm.gscbin":"mag_test_mp.gscbin");}

    static readonly byte[] KA = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    static readonly byte[] KB = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    static byte[] Unlock(byte[] d)
    {
        var k = new byte[32]; for (int i = 0; i < 32; i++) k[i] = (byte)(KA[i] ^ KB[i]);
        var iv = new byte[16]; Array.Copy(d, iv, 16);
        using (var a = System.Security.Cryptography.Aes.Create())
        using (var t = a.CreateDecryptor(k, iv)) return t.TransformFinalBlock(d, 16, d.Length - 16);
    }
    public class Script{public byte[] Buffer,Bytecode;public int Len;}
    public static Script Parse(byte[] d,out string error)
    {
        error=null;
        if(d==null||d.Length<18||d[0]!='G'||d[1]!='S'||d[2]!='C'||d[3]!=0){error="En-tete GSC absent";return null;}
        int cl=BitConverter.ToInt32(d,4),ln=BitConverter.ToInt32(d,8),bl=BitConverter.ToInt32(d,12);
        if(cl<2||ln<=0||ln>0x1120||bl<2||bl>MaxBytecode||16L+cl+bl!=d.Length){error="Tailles GSC incoherentes";return null;}
        if(d[16]!=0x78||d[16+cl]!=0x3B){error="Bytecode MW2019 IW8 attendu (End 0x3B)";return null;}
        var s=new Script{Buffer=new byte[cl],Bytecode=new byte[bl],Len=ln};Array.Copy(d,16,s.Buffer,0,cl);Array.Copy(d,16+cl,s.Bytecode,0,bl);return s;
    }
    public static string CheckScript(byte[] d){string e;Parse(d,out e);return e;}
    bool IsBase(ulong b)
    {
        try { return Same(Dbg.Read(Pid,b+FindHeader,20),new byte[]{0x55,0x48,0x89,0xE5,0x41,0x57,0x41,0x56,0x41,0x55,0x41,0x54,0x53,0x48,0x81,0xEC,0x38,0x01,0x00,0x00})
            &&Same(Dbg.Read(Pid,b+0x10E0,20),new byte[]{0x55,0x48,0x89,0xE5,0x41,0x57,0x41,0x56,0x53,0x50,0x49,0x89,0xFE,0xBF,0x33,0x00,0x00,0x00,0xBA,0x01}); } catch { return false; }
    }
    public ulong FindBase()
    {
        if(IsBase(0x10B0))return 0x10B0;
        foreach(var m in Dbg.Maps(Pid))if(m.Start!=0&&m.End>m.Start&&(m.Prot&1)!=0&&IsBase(m.Start))return m.Start;
        throw new Exception("Profil incompatible : eboot MW2019 CUSA08829 1.67 attendu. Aucune ecriture effectuee.");
    }
    public class Asset{public ulong Addr,Bytecode,Hash;public int BcLen;public string Name;}
    public static uint Hash(int type,string name){uint h=(uint)type;foreach(char ch in name)h=unchecked(h*31+(byte)(ch=='\\'?'/':char.ToLowerInvariant(ch)));return h;}
    Asset Lookup(ulong b,string name)
    {
        ulong pool=BitConverter.ToUInt64(Dbg.Read(Pid,b+0x1150,8),0);
        if(pool==0)return null;
        uint idx=BitConverter.ToUInt32(Dbg.Read(Pid,pool+0x1110+(Hash(0x33,name)%0x1090)*4UL,4),0);
        var seen=new HashSet<uint>();
        while(idx!=0&&seen.Count<10000)
        {
            if(Cancel)throw new OperationCanceledException();
            if(idx>=0x10A0||!seen.Add(idx))throw new Exception("Table assets MW2019 incoherente");
            var e=Dbg.Read(Pid,pool+idx*20UL,20);
            if(e[17]==0x33)
            {
                ulong a=BitConverter.ToUInt64(e,0);
                if(a!=0)
                {
                    var h=Dbg.Read(Pid,a,40);ulong np=BitConverter.ToUInt64(h,0);
                    if(np!=0)
                    {
                        var text=Dbg.Read(Pid,np,128);int end=Array.IndexOf(text,(byte)0);
                        string nm=end<0?null:Encoding.ASCII.GetString(text,0,end);
                        if(string.Equals(nm,name,StringComparison.OrdinalIgnoreCase))
                            return new Asset{Addr=a,Name=nm,Bytecode=BitConverter.ToUInt64(h,32),BcLen=BitConverter.ToInt32(h,16)};
                    }
                }
            }
            idx=BitConverter.ToUInt32(e,8);
        }
        return null;
    }
    Asset LookupAny(ulong b,string[] names){foreach(string n in names){var a=Lookup(b,n);if(a!=null&&a.Bytecode!=0&&a.BcLen>0)return a;}return null;}
    public static bool InRange(ulong lo,ulong hi,ulong code,int length)
    {
        if(lo>=hi||length<=0||code>ulong.MaxValue-(ulong)length)return false;
        return Math.Max(hi,code+(ulong)length)-Math.Min(lo,code)<=CallRange;
    }
    public static ulong FindSyscall(byte[] code,ulong start,int number)
    {
        var pat=new List<byte>{0x48,0xC7,0xC0};pat.AddRange(BitConverter.GetBytes(number));pat.AddRange(new byte[]{0x49,0x89,0xCA,0x0F,0x05,0x72,1,0xC3});
        ulong found=0;
        for(int i=0;i<=code.Length-pat.Count;i++)
        {
            int k=0;while(k<pat.Count&&code[i+k]==pat[k])k++;
            if(k==pat.Count){if(found!=0)throw new Exception("Wrapper systeme ambigu");found=start+(ulong)i;}
        }
        return found;
    }
    ulong rpc,mmap,munmap;int rpcPid=-1;
    void PrepareRpc()
    {
        if(rpcPid==Pid&&rpc!=0)return;
        rpc=mmap=munmap=0;
        foreach(var m in Dbg.Maps(Pid))
        {
            if((m.Name??"").IndexOf("libkernel",StringComparison.OrdinalIgnoreCase)<0||(m.Prot&4)==0||m.End<=m.Start||m.End-m.Start>=0x10B0)continue;
            var code=Dbg.Read(Pid,m.Start,(int)(m.End-m.Start));
            mmap=FindSyscall(code,m.Start,477);munmap=FindSyscall(code,m.Start,73);
            if(mmap!=0&&munmap!=0)break;
        }
        if(mmap==0||munmap==0)throw new Exception("Wrappers mmap/munmap absents de libkernel. Aucun detour installe.");
        rpc=Dbg.InstallRpc(Pid);if(rpc==0||rpc==ulong.MaxValue)throw new Exception("RPC ps4debug indisponible");rpcPid=Pid;
    }
    public static List<ulong> NearCandidates(ulong start,ulong end,ulong size,bool beforeOnly)
    {
        var candidates=new List<ulong>();
        for(ulong delta=0;delta<=CallRange;delta+=0x1050)
        {
            if(start>size+delta)candidates.Add(start-size-delta);
            if(!beforeOnly&&end<ulong.MaxValue-delta)candidates.Add(PageAlign(end)+delta);
        }
        return candidates;
    }
    ulong AllocNear(Asset target,ulong lo,ulong hi,int length)
    {
        var maps=Dbg.Maps(Pid);
        var region=maps.Find(m=>(m.Prot&1)!=0&&m.Start<=target.Bytecode&&target.Bytecode<m.End);
        if(region==null)throw new Exception("Zone du script cible introuvable");
        if(IsZombies)
        {

            var baseScript=LookupAny(FindBase(),new[]{"[masqué]","[masqué]","[masqué]"});
            if(baseScript==null)throw new Exception("Ancre des scripts communs absente : placement CP refuse.");
            region=maps.Find(m=>(m.Prot&1)!=0&&m.Start<=baseScript.Bytecode&&baseScript.Bytecode<m.End);
            if(region==null)throw new Exception("Debut des scripts communs introuvable : placement CP refuse.");
            Log("Placement CP avant les scripts communs : limite 0x"+region.Start.ToString("X")+". Zone de croissance des cartes exclue.");
        }
        ulong size=PageAlign((ulong)length);var candidates=NearCandidates(region.Start,region.End,size,true);
        PrepareRpc();
        foreach(ulong at in candidates)
        {
            if(Cancel)throw new OperationCanceledException();
            if(!InRange(lo,hi,at,length))continue;
            bool conflict=false,reserved=false;
            foreach(var m in maps)
            {
                if(m.Start==0x1050&&m.End==0x1160UL)continue;
                if(at<m.End&&at+size>m.Start)
                {
                    if(m.Prot!=0||at<m.Start||at+size>m.End){conflict=true;break;}
                    reserved=true;
                }
            }
            if(conflict||reserved)continue;
            ulong got=Dbg.Call(Pid,rpc,mmap,at,size,3,reserved?0x1020UL:0x1010UL,ulong.MaxValue,0);
            if(got==at){Log("Bytecode IW8 a portee : 0x"+at.ToString("X")+", zone 0x"+lo.ToString("X")+"..0x"+hi.ToString("X"));return at;}
            if(got!=0&&got!=ulong.MaxValue)Dbg.Call(Pid,rpc,munmap,got,size);
        }
        throw new Exception("Aucune place libre a portee des scripts IW8 (24 bits signes). Aucun detour installe.");
    }
    void FreeNear(ulong at,int length){if(at!=0&&rpcPid==Pid&&rpc!=0)try{Dbg.Call(Pid,rpc,munmap,at,PageAlign((ulong)length));}catch{}}
    static byte[] Jump(ulong target){var d=new byte[16]{0xFF,0x25,0,0,0,0,0,0,0,0,0,0,0,0,0x90,0x90};BitConverter.GetBytes(target).CopyTo(d,6);return d;}
    public static byte[] BuildBlock(ulong at,ulong b,Script s,string[] names,out ulong cave,ulong near=0,ulong originalCode=0,bool callerKnown=false)
    { return MW2019LoaderCode.Build(at,b,s,names,out cave,near,originalCode); }
    public class Status{public bool Found,ScriptIn,Reset,Intact,TextOk;public int TextLookups,TextReads,TextMisses,TextMarkerReads,TextIndexReads,BcLen,Calls=-1,Ours=-1,BlockMode=-1,CallerState,TargetSeen;}
    public Status GetStatus()
    {
        ulong b=FindBase(),at=OwnedBlock(b);var s=new Status{Found=true,Intact=true,TextOk=OwnedText(b)!=0};
        if(s.TextOk)
        {
            ulong t=OwnedText(b);var counters=Dbg.Read(Pid,t+0x24,20);
            s.TextLookups=BitConverter.ToInt32(counters,0);s.TextReads=BitConverter.ToInt32(counters,4);s.TextMisses=BitConverter.ToInt32(counters,8);s.TextMarkerReads=BitConverter.ToInt32(counters,12);s.TextIndexReads=BitConverter.ToInt32(counters,16);
            Log("Textes HUD 0.1.3 : libelles resolus="+s.TextLookups+", lectures HUD="+s.TextReads+", marqueurs="+s.TextMarkerReads+", index="+s.TextIndexReads+", demandes hors catalogue="+s.TextMisses);
        }
        try
        {
            var sdk=Dbg.Read(Pid,b+0x1130,8);
            if(at!=0 && BitConverter.ToInt32(Dbg.Read(Pid,at+0x34,4),0)>0 && sdk[4]==1)PrepareMWTrophies(b);
            LogMWTrophyStatus(b);
        }catch(Exception ex){Log("Lecture etat trophees : "+ex.Message);}
        if(at!=0){var d=Dbg.Read(Pid,at,0x88);s.ScriptIn=true;s.BcLen=BitConverter.ToInt32(d,16);s.Calls=BitConverter.ToInt32(d,48);s.Ours=BitConverter.ToInt32(d,52);s.Reset=BitConverter.ToInt32(d,0x44)>0;s.BlockMode=BitConverter.ToInt32(d,0x40);if(s.BlockMode==1){s.CallerState=BitConverter.ToInt32(d,0x80);s.TargetSeen=BitConverter.ToInt32(d,0x84);}}return s;
    }
    public void Inject(byte[] file)
    {
        List<string> labels;GscCompiler.Inspect(file,out labels);string error;var script=Parse(file,out error);if(script==null)throw new Exception(error);
        ulong b=FindBase(),control=OwnedControl(b),previous=OwnedBlock(b);var old=Dbg.Read(Pid,b+HookSite,16);
        if(!Same(old,HookOriginal)&&previous==0)throw new Exception("Chargeur modifie par un autre outil : aucun remplacement effectue.");
        var target=LookupAny(b,TargetNames);
        var anchor=target??LookupAny(b,new[]{"[masqué]","[masqué]","[masqué]"});
        if(anchor==null)throw new ScriptsNotLoadedException("Scripts communs absents : preparation automatique en attente du lobby Multijoueur.");
        var caller=LookupAny(b,CallerNames);
        ulong lo=anchor.Bytecode,hi=anchor.Bytecode+(ulong)anchor.BcLen;
        if(caller!=null){lo=Math.Min(lo,caller.Bytecode);hi=Math.Max(hi,caller.Bytecode+(ulong)caller.BcLen);}
        CheckText(b);
        ulong near=AllocNear(anchor,lo,hi,script.Bytecode.Length),at=0,newControl=0;bool published=false,hookExposed=false;
        int length=BufferOffset+script.Buffer.Length+2*script.Bytecode.Length+64;
        try
        {
            if(Cancel)throw new OperationCanceledException();
            at=Dbg.Alloc(Pid,length);if(at==0||at==ulong.MaxValue)throw new Exception("Allocation du menu impossible");
            ulong cave;var data=BuildBlock(at,b,script,TargetNames,out cave,near,target==null?0:target.Bytecode);
            if(caller!=null)SeedCaller(data,caller.Bytecode,caller.BcLen);
            Dbg.Write(Pid,at,data);Dbg.Write(Pid,near,script.Bytecode);
            if(!Same(Dbg.Read(Pid,at,data.Length),data)||!Same(Dbg.Read(Pid,near,script.Bytecode.Length),script.Bytecode))throw new Exception("Buffers du nouveau menu non confirmes");
            ApplyText(b,labels);
            if(!Same(Dbg.Read(Pid,b+HookSite,16),old))throw new Exception("Chargeur modifie pendant la preparation");
            if(Cancel)throw new OperationCanceledException();
            if(control==0)
            {
                newControl=Dbg.Alloc(Pid,0x200);if(newControl==0||newControl==ulong.MaxValue)throw new Exception("Allocation du controleur impossible");
                var bytes=MW2019LoaderCode.Control(newControl,b,at);Dbg.Write(Pid,newControl,bytes);
                if(!Same(Dbg.Read(Pid,newControl,bytes.Length),bytes))throw new Exception("Controleur non confirme");
                hookExposed=true;Dbg.Write(Pid,b+HookSite,Jump(newControl+0x100));
            }
            else
            {
                PrepareRpc();published=true;
                ulong replaced=Dbg.Call(Pid,rpc,control+0x140,control,at);
                if(replaced!=previous)throw new Exception("La generation precedente a change pendant la publication");
            }
            published=true;if(OwnedBlock(b)!=at)throw new Exception("Publication du nouveau menu non confirmee");
            RefreshRequested=false;
            Log("MW2019 0.1.4 : menu "+(previous==0?"prepare":"remplace")+" depuis le lobby, generation 0x"+at.ToString("X")+", code 0x"+near.ToString("X")+". "+(caller==null?"Appelant pas encore charge : verification native au premier lancement.":"Appelant present et portee verifiee.")+" Nouvelle version appliquee a la prochaine carte, sans redemarrer le jeu.");

            Log("Trophees : preparation facultative via Etat apres le chargement du menu.");
        }
        catch
        {

            if(hookExposed&&!published)try{Dbg.Write(Pid,b+HookSite,old);}catch{}
            if(!published&&!hookExposed)
            {
                if(at!=0&&at!=ulong.MaxValue)try{Dbg.Free(Pid,at,length);}catch{}
                if(newControl!=0&&newControl!=ulong.MaxValue)try{Dbg.Free(Pid,newControl,0x200);}catch{}
                FreeNear(near,script.Bytecode.Length);
            }
            throw;
        }
    }
    public string AutoTick(byte[] script)
    {
        ulong b=FindBase(),at=OwnedBlock(b);
        if(at==0||RefreshRequested||BitConverter.ToInt32(Dbg.Read(Pid,at+0x40,4),0)!=(int)Mode)
        {
            try{Inject(script);return "inject";}
            catch(ScriptsNotLoadedException){return "wait";}
        }
        return null;
    }
    public bool Remove()
    {
        ulong b=FindBase();if(OwnedBlock(b)==0)return false;Dbg.Write(Pid,b+HookSite,HookOriginal);
        if(!Same(Dbg.Read(Pid,b+HookSite,16),HookOriginal))throw new Exception("Retrait non confirme");
        Log("Chargeur restaure. Le menu actif reste jusqu'au changement de carte. Allocations conservees jusqu'a l'arret du jeu.");return true;
    }
}
