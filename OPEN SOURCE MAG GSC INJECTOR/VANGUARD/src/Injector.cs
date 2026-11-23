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
    public static bool IsZombies { get { return Mode==GameMode.Zombies; } }
    public static string TargetPath { get { return IsZombies?"[masqué] (CP FX)":"[masqué]"; } }
    public static string[] TargetNames { get { return IsZombies?new[]{"[masqué]"}:new[]{"[masqué]",TargetPath,TargetPath+".gsc"}; } }
    public static string[] CallerNames { get { return IsZombies?new[]{"[masqué]"}:new[]{"[masqué]","[masqué]","[masqué]"}; } }
    public class ScriptsNotLoadedException : Exception { public ScriptsNotLoadedException(string message):base(message){} }
    public static string ModeName { get { return IsZombies?"Zombies":L.T("Multijoueur","Multiplayer"); } }
    public static string MenuName { get { return IsZombies?"MAG Menu Vanguard Zombies v0.1":"MAG Menu Vanguard MP v0.1"; } }
    public static string[] RequiredFunctions { get { return new[]{"main"}; } }
    public const int MaxBytecode=0x1050,CaveOffset=0x200,BufferOffset=0x400,NameOffset=0x50;

    public const ulong CallRange=0x1090;
    public const ulong HookSite=0x10B0,FindHeader=0x1070,Resume=0x10C0;
    public static readonly byte[] HookOriginal={0xE8,0xD0,0x02,0x2C,0xFF,0xBF,0x35,0,0,0,0x48,0x89,0xDE,0x49,0x89,0xC6};
    static readonly byte[] Magic=Encoding.ASCII.GetBytes("MAGVG126");
    readonly Core core;
    public volatile bool Cancel,RefreshRequested;
    public bool Quiet;
    public Injector(Core c){core=c;}
    Ps4Debug Dbg { get { return core.Dbg; } }
    int Pid { get { return core.Pid; } }
    void Log(string t){if(!Quiet)core.Trace(t);}
    static bool Same(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    static ulong Align(ulong n){return(n+15)&~15UL;}
    static ulong PageAlign(ulong n){return(n+0x1020)&~0x1020UL;}
    public static byte[] Resource(string name)
    {
        using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {if(s==null)throw new IOException("Ressource absente : "+name);using(var m=new MemoryStream()){s.CopyTo(m);return m.ToArray();}}
    }
    public static byte[] Menu(){return Unlock(Resource(IsZombies?"m_zm.dat":"m_mp.dat"));}
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
        if(cl<2||ln<=0||ln>0x10D0||bl<2||bl>MaxBytecode||16L+cl+bl!=d.Length){error="Tailles GSC incoherentes";return null;}
        if(d[16]!=0x78||d[16+cl]!=0x3B){error="Bytecode Vanguard S4 attendu (End 0x3B)";return null;}
        var s=new Script{Buffer=new byte[cl],Bytecode=new byte[bl],Len=ln};Array.Copy(d,16,s.Buffer,0,cl);Array.Copy(d,16+cl,s.Bytecode,0,bl);return s;
    }
    public static string CheckScript(byte[] d){string e;Parse(d,out e);return e;}
    bool IsBase(ulong b)
    {
        try{return Same(Dbg.Read(Pid,b+FindHeader,20),new byte[]{0x55,0x48,0x89,0xE5,0x41,0x57,0x41,0x56,0x41,0x55,0x41,0x54,0x53,0x48,0x81,0xEC,0x58,1,0,0})
            &&Same(Dbg.Read(Pid,b+0x10A0,20),new byte[]{0x55,0x48,0x89,0xE5,0x41,0x57,0x41,0x56,0x41,0x55,0x41,0x54,0x53,0x48,0x81,0xEC,0x98,4,0,0});}catch{return false;}
    }
    public ulong FindBase()
    {
        if(IsBase(0x1080))return 0x1080;
        foreach(var m in Dbg.Maps(Pid))if(m.Start!=0&&m.End>m.Start&&(m.Prot&1)!=0&&IsBase(m.Start))return m.Start;
        throw new Exception("Profil incompatible : eboot Vanguard CUSA24041 1.26 attendu. Aucune ecriture effectuee.");
    }
    public class Asset{public ulong Addr,Bytecode,Hash;public int BcLen;public string Name;}
    public static uint Hash(int type,string name){uint h=(uint)type;foreach(char ch in name)h=unchecked(h*31+(byte)(ch=='\\'?'/':char.ToLowerInvariant(ch)));return h;}
    Asset Lookup(ulong b,string name)
    {
        uint idx=BitConverter.ToUInt32(Dbg.Read(Pid,b+0x10F0+(Hash(0x35,name)%0x1040)*4UL,4),0);
        var seen=new HashSet<uint>();
        while(idx!=0&&seen.Count<10000)
        {
            if(Cancel)throw new OperationCanceledException();
            if(idx>=0x1060||!seen.Add(idx))throw new Exception("Table assets Vanguard incoherente");
            var e=Dbg.Read(Pid,b+0x10E0+idx*20UL,20);
            if(e[17]==0x35)
            {
                ulong a=BitConverter.ToUInt64(e,0);
                if(a!=0)
                {
                    var h=Dbg.Read(Pid,a,48);ulong np=BitConverter.ToUInt64(h,0);
                    if(np!=0)
                    {
                        var text=Dbg.Read(Pid,np,128);int end=Array.IndexOf(text,(byte)0);
                        string nm=end<0?null:Encoding.ASCII.GetString(text,0,end);
                        if(string.Equals(nm,name,StringComparison.OrdinalIgnoreCase))
                            return new Asset{Addr=a,Name=nm,Hash=BitConverter.ToUInt64(h,8),Bytecode=BitConverter.ToUInt64(h,40),BcLen=BitConverter.ToInt32(h,24)};
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
            if((m.Name??"").IndexOf("libkernel",StringComparison.OrdinalIgnoreCase)<0||(m.Prot&4)==0||m.End<=m.Start||m.End-m.Start>=0x1080)continue;
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
        for(ulong delta=0;delta<=0x1060;delta+=0x1030)
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
        ulong size=PageAlign((ulong)length);var candidates=NearCandidates(region.Start,region.End,size,IsZombies);
        PrepareRpc();
        foreach(ulong at in candidates)
        {
            if(Cancel)throw new OperationCanceledException();
            if(!InRange(lo,hi,at,length))continue;
            bool conflict=false,reserved=false;
            foreach(var m in maps)
            {
                if(m.Start==0x1030&&m.End==0x1110UL)continue;
                if(at<m.End&&at+size>m.Start)
                {
                    if(m.Prot!=0||at<m.Start||at+size>m.End){conflict=true;break;}
                    reserved=true;
                }
            }
            if(conflict||(IsZombies&&reserved))continue;
            ulong got=Dbg.Call(Pid,rpc,mmap,at,size,3,reserved?0x1010UL:0x1000UL,ulong.MaxValue,0);
            if(got==at){Log("Bytecode S4 a portee : 0x"+at.ToString("X")+", zone 0x"+lo.ToString("X")+"..0x"+hi.ToString("X"));return at;}
            if(got!=0&&got!=ulong.MaxValue)Dbg.Call(Pid,rpc,munmap,got,size);
        }
        throw new Exception("Aucune place libre a portee des scripts S4 (24 bits signes). Aucun detour installe.");
    }
    void FreeNear(ulong at,int length){if(at!=0&&rpcPid==Pid&&rpc!=0)try{Dbg.Call(Pid,rpc,munmap,at,PageAlign((ulong)length));}catch{}}
    static byte[] Jump(ulong target){var d=new byte[16]{0xFF,0x25,0,0,0,0,0,0,0,0,0,0,0,0,0x90,0x90};BitConverter.GetBytes(target).CopyTo(d,6);return d;}
    ulong OwnedBlock(ulong b)
    {
        var h=Dbg.Read(Pid,b+HookSite,16);
        if(h[0]!=0xFF||h[1]!=0x25||BitConverter.ToUInt32(h,2)!=0)return 0;
        ulong at=BitConverter.ToUInt64(h,6);if(at<CaveOffset)return 0;at-=CaveOffset;
        try{return Same(Dbg.Read(Pid,at+0x38,8),Magic)?at:0;}catch{return 0;}
    }
    public static byte[] BuildBlock(ulong at,ulong b,Script s,string[] names,out ulong cave,ulong near=0,ulong originalCode=0,bool callerKnown=false)
    {
        int pristine=(int)Align((ulong)(BufferOffset+s.Buffer.Length)),bytecode=(int)Align((ulong)(pristine+s.Bytecode.Length));
        var d=new byte[bytecode+s.Bytecode.Length];cave=at+CaveOffset;
        var nb=new List<byte>();foreach(string name in names){nb.AddRange(Encoding.ASCII.GetBytes(name));nb.Add(0);}nb.Add(0);
        if(nb.Count>CaveOffset-NameOffset)throw new ArgumentException("Liste de noms trop longue");
        BitConverter.GetBytes(at+NameOffset).CopyTo(d,0);BitConverter.GetBytes(s.Buffer.Length).CopyTo(d,16);BitConverter.GetBytes(s.Len).CopyTo(d,20);BitConverter.GetBytes(s.Bytecode.Length).CopyTo(d,24);
        BitConverter.GetBytes(at+BufferOffset).CopyTo(d,32);BitConverter.GetBytes(near==0?at+(ulong)bytecode:near).CopyTo(d,40);
        BitConverter.GetBytes(originalCode).CopyTo(d,0x48);
        Magic.CopyTo(d,0x38);BitConverter.GetBytes((int)Mode).CopyTo(d,0x40);nb.ToArray().CopyTo(d,NameOffset);
        var c=new List<byte>();var fixes=new List<int>();

        c.AddRange(new byte[]{0x49,0xBB});c.AddRange(BitConverter.GetBytes(b+FindHeader));c.AddRange(new byte[]{0x41,0xFF,0xD3});
        c.AddRange(new byte[]{0x49,0xBA});c.AddRange(BitConverter.GetBytes(at));c.AddRange(new byte[]{0x41,0xFF,0x42,0x30,0x48,0x85,0xC0});fixes.Add(c.Count);c.AddRange(new byte[]{0x0F,0x84,0,0,0,0});
        c.AddRange(new byte[]{0x48,0x8B,0x08,0x48,0x85,0xC9});fixes.Add(c.Count);c.AddRange(new byte[]{0x0F,0x84,0,0,0,0});
        c.AddRange(new byte[]{0x49,0x89,0xC9,0x48,0xBA});c.AddRange(BitConverter.GetBytes(at+NameOffset));
        int next=c.Count;c.AddRange(new byte[]{0x80,0x3A,0});fixes.Add(c.Count);c.AddRange(new byte[]{0x0F,0x84,0,0,0,0});c.AddRange(new byte[]{0x4C,0x89,0xC9});
        int loop=c.Count;c.AddRange(new byte[]{0x44,0x8A,0x01,0x44,0x3A,0x02});int mismatch=c.Count;c.AddRange(new byte[]{0x75,0});
        c.AddRange(new byte[]{0x45,0x84,0xC0});int match=c.Count;c.AddRange(new byte[]{0x74,0});
        c.AddRange(new byte[]{0x48,0xFF,0xC1,0x48,0xFF,0xC2,0xEB});c.Add((byte)(sbyte)(loop-c.Count-1));
        int skip=c.Count;c.AddRange(new byte[]{0x80,0x3A,0});int advance=c.Count;c.AddRange(new byte[]{0x74,0});c.AddRange(new byte[]{0x48,0xFF,0xC2,0xEB});c.Add((byte)(sbyte)(skip-c.Count-1));
        int adv=c.Count;c.AddRange(new byte[]{0x48,0xFF,0xC2,0xEB});c.Add((byte)(sbyte)(next-c.Count-1));
        int ok=c.Count;

        c.AddRange(new byte[]{0x49,0x8B,0x52,0x48,0x48,0x85,0xD2});int noGuard=c.Count;c.AddRange(new byte[]{0x74,0});
        c.AddRange(new byte[]{0x48,0x39,0x50,0x28});int moved=c.Count;c.AddRange(new byte[]{0x75,0});
        int substitute=c.Count;c[noGuard+1]=(byte)(substitute-noGuard-2);
        c.AddRange(new byte[]{0x41,0xC7,0x42,0x44,0,0,0,0});

        c.AddRange(new byte[]{0x48,0x8B,0x08,0x49,0x89,0x0A,0x48,0x8B,0x48,0x08,0x49,0x89,0x4A,0x08,0x49,0x8B,0x7A,0x28,0x48,0xBE});c.AddRange(BitConverter.GetBytes(at+(ulong)pristine));
        c.Add(0xB9);c.AddRange(BitConverter.GetBytes(s.Bytecode.Length));c.AddRange(new byte[]{0xFC,0xF3,0xA4,0x41,0xFF,0x42,0x34,0x4C,0x89,0xD0});
        int substituted=c.Count;c.AddRange(new byte[]{0xEB,0});
        int rejected=c.Count;c.AddRange(new byte[]{0x41,0xFF,0x42,0x44});
        int end=c.Count;c[substituted+1]=(byte)(end-substituted-2);c[moved+1]=(byte)(rejected-moved-2);
        c.AddRange(new byte[]{0xBF,0x35,0,0,0,0x48,0x89,0xDE,0x49,0x89,0xC6,0xFF,0x25,0,0,0,0});c.AddRange(BitConverter.GetBytes(b+Resume));
        foreach(int fix in fixes){var delta=BitConverter.GetBytes(end-fix-6);for(int k=0;k<4;k++)c[fix+2+k]=delta[k];}
        c[mismatch+1]=(byte)(skip-mismatch-2);c[advance+1]=(byte)(adv-advance-2);c[match+1]=(byte)(ok-match-2);
        if(c.Count>BufferOffset-CaveOffset)throw new Exception("Detour trop long");c.ToArray().CopyTo(d,CaveOffset);s.Buffer.CopyTo(d,BufferOffset);s.Bytecode.CopyTo(d,pristine);s.Bytecode.CopyTo(d,bytecode);
        if(IsZombies)ZombiesHookCode.Write(d,at,b,pristine,s.Bytecode.Length,callerKnown);
        return d;
    }
    ulong OwnedText(ulong b)
    {
        var jump=Dbg.Read(Pid,b+VanguardTextCode.IndexSite,16);
        if(jump[0]!=0xFF||jump[1]!=0x25||BitConverter.ToInt32(jump,2)!=0)return 0;
        ulong entry=BitConverter.ToUInt64(jump,6);if(entry<0x100)return 0;
        ulong at=entry-0x100;
        if(!Same(Dbg.Read(Pid,at,8),VanguardTextCode.Magic))return 0;
        var expected=VanguardTextCode.Build(at,b);
        if(!Same(Dbg.Read(Pid,b+VanguardTextCode.SetSite,16),Jump(at+0x400)))return 0;
        foreach(int off in new[]{0x100,0x400,0x800})
        {
            var code=new byte[0x300];Array.Copy(expected,off,code,0,code.Length);
            if(!Same(Dbg.Read(Pid,at+(ulong)off,code.Length),code))return 0;
        }
        return at;
    }
    void CheckText(ulong b)
    {
        if(OwnedText(b)!=0)return;
        if(!Same(Dbg.Read(Pid,b+VanguardTextCode.IndexSite,16),VanguardTextCode.IndexOriginal)||
           !Same(Dbg.Read(Pid,b+VanguardTextCode.SetSite,16),VanguardTextCode.SetOriginal))
            throw new Exception("Fonctions texte modifiees ou incompatibles. Relance completement Vanguard.");
    }
    void ApplyText(ulong b)
    {
        CheckText(b);if(OwnedText(b)!=0)return;
        ulong at=Dbg.Alloc(Pid,VanguardTextCode.Size);
        if(at==0||at==ulong.MaxValue)throw new Exception("Allocation des textes impossible");
        bool exposed=false;
        try
        {
            var data=VanguardTextCode.Build(at,b);Dbg.Write(Pid,at,data);
            if(!Same(Dbg.Read(Pid,at,data.Length),data))throw new Exception("Table des textes non confirmee");
            CheckText(b);
            exposed=true;Dbg.Write(Pid,b+VanguardTextCode.IndexSite,Jump(at+0x100));
            Dbg.Write(Pid,b+VanguardTextCode.SetSite,Jump(at+0x400));
            if(OwnedText(b)!=at)throw new Exception("Correctif texte non confirme");
            Log("Vanguard : textes MP adaptes, table fixe et lecteurs du jeu conserves.");
        }
        catch
        {
            if(exposed)
            {
                try{if(Same(Dbg.Read(Pid,b+VanguardTextCode.IndexSite,16),Jump(at+0x100)))Dbg.Write(Pid,b+VanguardTextCode.IndexSite,VanguardTextCode.IndexOriginal);}catch{}
                try{if(Same(Dbg.Read(Pid,b+VanguardTextCode.SetSite,16),Jump(at+0x400)))Dbg.Write(Pid,b+VanguardTextCode.SetSite,VanguardTextCode.SetOriginal);}catch{}
            }
            else try{Dbg.Free(Pid,at,VanguardTextCode.Size);}catch{}
            throw;
        }
    }
    public class Status{public bool Found,ScriptIn,Reset,Intact,TextOk;public int BcLen,Calls=-1,Ours=-1,BlockMode=-1,CallerState,TargetSeen;}
    public Status GetStatus()
    {
        ulong b=FindBase(),at=OwnedBlock(b);var s=new Status{Found=true,Intact=true,TextOk=OwnedText(b)!=0};
        if(s.TextOk){ulong t=OwnedText(b);if(BitConverter.ToInt32(Dbg.Read(Pid,t+0x20,4),0)!=0)throw new Exception("Table des textes pleine pour cette carte : aucun index hors capacite reseau n'a ete ajoute.");}
        if(at!=0){var d=Dbg.Read(Pid,at,0x88);s.ScriptIn=true;s.BcLen=BitConverter.ToInt32(d,24);s.Calls=BitConverter.ToInt32(d,48);s.Ours=BitConverter.ToInt32(d,52);s.Reset=BitConverter.ToInt32(d,0x44)>0;s.BlockMode=BitConverter.ToInt32(d,0x40);if(s.BlockMode==1){s.CallerState=BitConverter.ToInt32(d,0x80);s.TargetSeen=BitConverter.ToInt32(d,0x84);}}return s;
    }
    public void Inject(byte[] file)
    {
        GscCompiler.Check(file);if(IsZombies)GscCompiler.CheckCpBootstrap(file);string err;var s=Parse(file,out err);if(s==null)throw new Exception(err);
        ulong b=FindBase();var old=Dbg.Read(Pid,b+HookSite,16);
        if(!Same(old,HookOriginal)&&OwnedBlock(b)==0)throw new Exception("Chargeur deja modifie par un autre outil. Relance Vanguard.");
        if(Cancel)throw new OperationCanceledException();
        var target=LookupAny(b,TargetNames);
        if(target==null&&!IsZombies)throw new ScriptsNotLoadedException("Script cible pas encore charge : ouvre le pre-lobby du mode choisi. Aucun detour installe.");
        if(IsZombies&&target!=null&&(target.Hash!=0x1170UL||target.BcLen!=330))throw new Exception("Le script CP FX [masqué] ne correspond pas au PKG 1.26 analyse. Aucun detour installe.");
        var placement=target;
        if(placement==null)placement=LookupAny(b,new[]{"[masqué]","[masqué]","[masqué]"});
        if(placement==null)throw new ScriptsNotLoadedException("Ancre commune absente : ouvre le lobby du jeu avant d'armer le point CP.");
        ulong lo=placement.Bytecode,hi=placement.Bytecode+(ulong)placement.BcLen;
        var caller=LookupAny(b,CallerNames);
        if(IsZombies&&caller!=null&&(caller.Hash!=0x1160UL||caller.BcLen!=16119))throw new Exception("Initialisation CP [masqué] incompatible avec Zombies 1.26. Aucun detour installe.");
        if(caller==null&&!IsZombies)throw new ScriptsNotLoadedException("Script appelant "+ModeName+" absent : chargement en attente. Aucun detour installe.");
        if(caller!=null){lo=Math.Min(lo,caller.Bytecode);hi=Math.Max(hi,caller.Bytecode+(ulong)caller.BcLen);}
        CheckText(b);
        ulong near=AllocNear(placement,lo,hi,s.Bytecode.Length);
        int length=BufferOffset+s.Buffer.Length+2*s.Bytecode.Length+64;ulong at=0;bool installed=false;
        try
        {
            at=Dbg.Alloc(Pid,length);if(at==0||at==ulong.MaxValue)throw new Exception("Allocation PS4 impossible");
            ulong originalCode=target==null?0:target.Bytecode;
            ulong cave;var block=BuildBlock(at,b,s,TargetNames,out cave,near,originalCode,caller!=null);Dbg.Write(Pid,at,block);
            Dbg.Write(Pid,near,s.Bytecode);
            if(!Same(Dbg.Read(Pid,near,s.Bytecode.Length),s.Bytecode))throw new Exception("Bytecode a portee non confirme");
            if(!Same(Dbg.Read(Pid,at,block.Length),block))throw new Exception("Bloc non confirme en memoire");
            if(!Same(Dbg.Read(Pid,b+HookSite,16),old))throw new Exception("Le chargeur a change pendant la preparation");
            if(Cancel)throw new OperationCanceledException();
            PrepareZombieOptions();
            ApplyText(b);
            var jump=Jump(cave);installed=true;Dbg.Write(Pid,b+HookSite,jump);
            if(!Same(Dbg.Read(Pid,b+HookSite,16),jump))throw new Exception("Detour non confirme en memoire");
            try { PrepareTrophies(); } catch(Exception ex) { core.Trace("Trophees indisponibles : "+ex.Message); }
            RefreshRequested=false;Log("Vanguard 1.26 : detour installe et relu. Profil "+ModeName+", cible "+(target==null?"[masqué] (validation differee)":target.Name)+", original 0x"+originalCode.ToString("X")+", menu 0x"+near.ToString("X")+", bloc 0x"+at.ToString("X")+". Lance une nouvelle carte.");
        }
        catch
        {

            if(installed){try{if(OwnedBlock(b)==at)Dbg.Write(Pid,b+HookSite,old);}catch{}}
            else{if(at!=0&&at!=ulong.MaxValue)try{Dbg.Free(Pid,at,length);}catch{}FreeNear(near,s.Bytecode.Length);}throw;
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
