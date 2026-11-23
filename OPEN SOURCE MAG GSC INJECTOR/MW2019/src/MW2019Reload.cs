// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
public partial class Injector
{
    public static void SeedCaller(byte[] d,ulong code,int length)
    {
        BitConverter.GetBytes(1).CopyTo(d,0x80);BitConverter.GetBytes(code).CopyTo(d,0x90);BitConverter.GetBytes(code+(ulong)length).CopyTo(d,0x98);
    }
    ulong OwnedControl(ulong b)
    {
        var j=Dbg.Read(Pid,b+HookSite,16);if(j[0]!=0xff||j[1]!=0x25||BitConverter.ToUInt32(j,2)!=0)return 0;
        ulong entry=BitConverter.ToUInt64(j,6);if(entry<0x100)return 0;ulong at=entry-0x100;
        try
        {
            var d=Dbg.Read(Pid,at,0x200);if(!Same(d.Sub(0,8),MW2019LoaderCode.ControlMagic)||BitConverter.ToUInt64(d,8)!=b)return 0;
            var expected=MW2019LoaderCode.Control(at,b,BitConverter.ToUInt64(d,0x10));
            if(!Same(d.Sub(0x100,0x100),expected.Sub(0x100,0x100)))throw new IOException("Code du controleur MAG modifie");return at;
        }
        catch(IOException){throw;}catch{return 0;}
    }
    ulong OwnedBlock(ulong b)
    {
        ulong control=OwnedControl(b);
        if(control!=0)
        {
            ulong at=BitConverter.ToUInt64(Dbg.Read(Pid,control+0x10,8),0);
            if(at==0||!Same(Dbg.Read(Pid,at+0x38,8),MW2019LoaderCode.Magic))throw new IOException("Generation MAG incoherente");return at;
        }
        var j=Dbg.Read(Pid,b+HookSite,16);if(j[0]!=0xff||j[1]!=0x25||BitConverter.ToUInt32(j,2)!=0)return 0;
        ulong entry=BitConverter.ToUInt64(j,6);if(entry<CaveOffset)return 0;ulong legacy=entry-CaveOffset;
        var h=Dbg.Read(Pid,legacy,0x50);if(!Same(h.Sub(0x38,8),Magic))return 0;
        int cl=BitConverter.ToInt32(h,8),ln=BitConverter.ToInt32(h,12),bl=BitConverter.ToInt32(h,16);
        if(cl<2||cl>0x1120||ln<1||bl<2||bl>MaxBytecode)return 0;
        ulong cave;var expectedOld=LegacyBuildBlock(legacy,b,new Script{Buffer=new byte[cl],Bytecode=new byte[bl],Len=ln},TargetNames,out cave,BitConverter.ToUInt64(h,32),BitConverter.ToUInt64(h,0x48));
        if(!Same(Dbg.Read(Pid,entry,BufferOffset-CaveOffset),expectedOld.Sub(CaveOffset,BufferOffset-CaveOffset)))throw new IOException("Ancien chargeur MAG modifie");
        return legacy;
    }
    class TextOwner { public ulong At,ReadSite;public byte[] Expected;public List<string> Labels;public string Version; }
    static List<string> ReadTextLabels(byte[] data,ulong at)
    {
        uint count=BitConverter.ToUInt32(data,0x74);if(count>1023)throw new IOException("Catalogue des textes incoherent");
        var labels=new List<string>();var utf8=new UTF8Encoding(false,true);
        for(int n=0;n<count;n++)
        {
            ulong ptr=BitConverter.ToUInt64(data,0x1000+n*8);
            if(ptr<at+0x1030||ptr>=at+0x1070)throw new IOException("Pointeur texte MAG incoherent");
            int p=(int)(ptr-at),end=Array.IndexOf(data,(byte)0,p);if(end<0)throw new IOException("Libelle MAG non termine");
            labels.Add(utf8.GetString(data,p,end-p));
        }
        return labels;
    }
    TextOwner TextState(ulong b)
    {
        var index=Dbg.Read(Pid,b+MW2019TextCode.IndexSite,16);
        if(Same(index,MW2019TextCode.IndexOriginal))
        {
            if(!Same(Dbg.Read(Pid,b+MW2019TextCode.SetSite,16),MW2019TextCode.SetOriginal)||!Same(Dbg.Read(Pid,b+MW2019TextCode.ReadSite,16),MW2019TextCode.ReadOriginal)||!Same(Dbg.Read(Pid,b+MW2019Text011.ReadSite,16),MW2019Text011.ReadOriginal))throw new IOException("Fonction texte modifiee par un autre outil");return null;
        }
        if(index[0]!=0xff||index[1]!=0x25||BitConverter.ToUInt32(index,2)!=0)throw new IOException("Detour texte inconnu");
        ulong entry=BitConverter.ToUInt64(index,6);if(entry<0x100)throw new IOException("Pointeur texte invalide");ulong at=entry-0x100;
        string magic=Encoding.ASCII.GetString(Dbg.Read(Pid,at,8));
        var data=Dbg.Read(Pid,at,magic=="MWTXT018"?MW2019TextCode.Size:0x1070);byte[] expected;ulong read=0;
        if(magic=="MWTXT018") { expected=MW2019TextCode.Build(at,b);read=MW2019TextCode.ReadSite; }
        else if(magic=="MWTXT014") { expected=MW2019Text014.Build(at,b);read=MW2019TextCode.ReadSite; }
        else if(magic=="MWTXT012"||magic=="MWTXT013") { expected=MW2019Text013.Build(at,b);read=MW2019TextCode.ReadSite; }
        else if(magic=="MWTXT011") { expected=MW2019Text011.Build(at,b);read=MW2019Text011.ReadSite; }
        else if(magic=="MAGMW001")expected=MW2019Text010.Build(at,b);
        else throw new IOException("Signature de textes inconnue : aucun detour etranger remplace");
        foreach(int offset in new[]{0x100,0x400,0x800})if(!Same(data.Sub(offset,0x300),expected.Sub(offset,0x300)))throw new IOException("Code texte MAG modifie");
        if(read!=0&&!Same(data.Sub(0xc00,0x300),expected.Sub(0xc00,0x300)))throw new IOException("Lecteur texte MAG modifie");
        if(!Same(Dbg.Read(Pid,b+MW2019TextCode.SetSite,16),Jump(at+0x400)))throw new IOException("Detour SetText incoherent");
        if(read!=0&&!Same(Dbg.Read(Pid,b+read,16),Jump(at+0xc00)))throw new IOException("Detour lecteur texte incoherent");
        if(read!=MW2019TextCode.ReadSite&&!Same(Dbg.Read(Pid,b+MW2019TextCode.ReadSite,16),MW2019TextCode.ReadOriginal))throw new IOException("Dessin HUD modifie par un autre outil");
        if(read!=MW2019Text011.ReadSite&&!Same(Dbg.Read(Pid,b+MW2019Text011.ReadSite,16),MW2019Text011.ReadOriginal))throw new IOException("Lecteur NCS modifie par un autre outil");
        return new TextOwner{At=at,ReadSite=read,Expected=expected,Labels=ReadTextLabels(data,at),Version=magic};
    }
    ulong OwnedText(ulong b) { var state=TextState(b);return state!=null&&state.Version=="MWTXT018"?state.At:0; }
    void CheckText(ulong b)
    {
        var state=TextState(b);if(state!=null)return;
        ushort count=BitConverter.ToUInt16(Dbg.Read(Pid,b+0x1140,2),0);if(count>0x1060)throw new IOException("Plage d'index texte deja utilisee");
    }
    void ApplyText(ulong b,List<string> additions)
    {
        var owner=TextState(b);var labels=owner==null?MW2019TextCode.DefaultLabels():new List<string>(owner.Labels);
        var seen=new HashSet<string>(labels,StringComparer.Ordinal);
        foreach(string s in MW2019TextCode.DefaultLabels())if(seen.Add(s))labels.Add(s);
        foreach(string s in additions)if(s.Length>0&&s.IndexOf('\0')<0){string v="\x1f"+s.TrimStart('\x1f');if(seen.Add(v))labels.Add(v);}
        if(owner!=null&&owner.Version=="MWTXT018")
        {
            var before=Dbg.Read(Pid,owner.At,MW2019TextCode.Size);var next=MW2019TextCode.Build(owner.At,b,labels);
            int oldCount=owner.Labels.Count;
            for(int i=0;i<oldCount;i++)if(BitConverter.ToUInt64(before,0x1000+i*8)!=BitConverter.ToUInt64(next,0x1000+i*8))throw new IOException("Catalogue MAG non contigu : modification refusee");
            int end=0x1030;for(int i=0;i<oldCount;i++)end+=Encoding.UTF8.GetByteCount(owner.Labels[i])+1;

            if(labels.Count>oldCount)
            {
                Dbg.Write(Pid,owner.At+(ulong)end,next.Sub(end,MW2019TextCode.FixedSize-end));
                Dbg.Write(Pid,owner.At+0x1000+(ulong)oldCount*8,next.Sub(0x1000+oldCount*8,(labels.Count-oldCount+1)*8));
                if(!Same(Dbg.Read(Pid,owner.At+(ulong)end,MW2019TextCode.FixedSize-end),next.Sub(end,MW2019TextCode.FixedSize-end))||!Same(Dbg.Read(Pid,owner.At+0x1000,labels.Count*8),next.Sub(0x1000,labels.Count*8)))throw new IOException("Catalogue ajoute non confirme : compteur conserve");
                Dbg.Write(Pid,owner.At+0x74,BitConverter.GetBytes(labels.Count));
            }
            if(!Same(Dbg.Read(Pid,owner.At+0x1000,labels.Count*8),next.Sub(0x1000,labels.Count*8)))throw new IOException("Catalogue ajoute non confirme");
            Log("Textes 0.1.8 : catalogue reutilise, "+oldCount+" -> "+labels.Count+" libelles ; anciens index conserves.");return;
        }
        ulong at=Dbg.Alloc(Pid,MW2019TextCode.Size);if(at==0||at==ulong.MaxValue)throw new IOException("Allocation des textes impossible");
        var sites=new[]{MW2019TextCode.IndexSite,MW2019TextCode.SetSite,MW2019TextCode.ReadSite,MW2019Text011.ReadSite};var saved=new List<byte[]>();foreach(ulong site in sites)saved.Add(Dbg.Read(Pid,b+site,16));bool exposed=false;
        try
        {
            var data=MW2019TextCode.Build(at,b,labels);Dbg.Write(Pid,at,data);if(!Same(Dbg.Read(Pid,at,data.Length),data))throw new IOException("Catalogue texte non relu");
            TextState(b);exposed=true;
            Dbg.Write(Pid,b+MW2019TextCode.ReadSite,Jump(at+0xc00));
            Dbg.Write(Pid,b+MW2019TextCode.SetSite,Jump(at+0x400));Dbg.Write(Pid,b+MW2019TextCode.IndexSite,Jump(at+0x100));
            if(owner!=null&&owner.ReadSite==MW2019Text011.ReadSite)Dbg.Write(Pid,b+MW2019Text011.ReadSite,MW2019Text011.ReadOriginal);
            if(OwnedText(b)!=at)throw new IOException("Migration texte non confirmee");
            Log("Textes 0.1.8 : "+(owner==null?"installation":"migration depuis "+owner.Version)+", "+labels.Count+" libelles. Anciennes allocations conservees.");
        }
        catch
        {
            if(exposed){for(int i=0;i<sites.Length;i++)try{Dbg.Write(Pid,b+sites[i],saved[i]);}catch{}}
            else try{Dbg.Free(Pid,at,MW2019TextCode.Size);}catch{}
            throw;
        }
    }
}
static class MWBytes
{
    public static byte[] Sub(this byte[] d,int start,int length){var r=new byte[length];Array.Copy(d,start,r,0,length);return r;}
}
