// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.IO;

public partial class Injector
{
    int TrophyUnlockedCount(ulong buffer)
    {
        var data=Dbg.Read(Pid,buffer,36);
        if(BitConverter.ToUInt32(data,32)!=45)throw new IOException("Liste PS4 inattendue : Vanguard attend 45 trophees");
        int count=0;for(int i=0;i<45;i++)if((data[i/8]&(1<<(i%8)))!=0)count++;
        return count;
    }
    void InstallTrophyBridge(int count)
    {
        ulong site=trophyBase+TrophyBridgeCode.Site,import=trophyBase+0x2AB1400;
        var oldSite=Dbg.Read(Pid,site,18);ulong owned=0;
        string protocol=null;
        if(!Same(oldSite,TrophyBridgeCode.Original))
        {
            if(oldSite[0]!=0xFF||oldSite[1]!=0x25||BitConverter.ToUInt32(oldSite,2)!=0)
                throw new IOException("Native giveachievement deja modifiee par un autre outil");
            ulong entry=BitConverter.ToUInt64(oldSite,6);if(entry<0x100)throw new IOException("Pont trophees incoherent");
            owned=entry-0x100;protocol=System.Text.Encoding.ASCII.GetString(Dbg.Read(Pid,owned,8));
            if((protocol!="MAGTR021"&&protocol!="MAGTR023")||BitConverter.ToUInt64(Dbg.Read(Pid,owned+8,8),0)!=trophyBase ||
               !Same(Dbg.Read(Pid,owned+0x80,18),TrophyBridgeCode.Original))
                throw new IOException("Pont trophees non reconnu : relance Vanguard");
        }
        ulong oldImport=BitConverter.ToUInt64(Dbg.Read(Pid,import,8),0),originalUnlock=oldImport;
        if(protocol=="MAGTR023"&&oldImport==owned+0x400)originalUnlock=BitConverter.ToUInt64(Dbg.Read(Pid,owned+0x10,8),0);
        var maps=Dbg.Maps(Pid);
        if(!maps.Exists(m=>m.Name=="libSceNpTrophy.sprx"&&(m.Prot&4)!=0&&originalUnlock>=m.Start&&originalUnlock<m.End))
            throw new IOException("Import UnlockTrophy non verifie : aucun remplacement effectue");
        ulong mask=BitConverter.ToUInt64(Dbg.Read(Pid,trophyScratch+512,8),0)&0x1FFFFFFFFFFFUL;
        if(protocol=="MAGTR023"&&oldImport==owned+0x400&&BitConverter.ToUInt32(Dbg.Read(Pid,owned+0x38,4),0)==trophyContext)
        {
            var expected=TrophyBridgeCode.Build(owned,trophyBase,count,mask,trophyContext,originalUnlock,trophyState,trophySleep);
            var code=new byte[0x829];Array.Copy(expected,0x100,code,0,code.Length);
            if(!Same(Dbg.Read(Pid,owned+0x100,code.Length),code))throw new IOException("Code du worker trophees modifie : relance Vanguard");
            Dbg.Call(Pid,rpc,owned+0x900,mask);
            core.Trace("Trophees : worker natif deja installe, progression PS4 actualisee ; un seul lot global.");return;
        }
        ulong block=Dbg.Alloc(Pid,0x4000);
        if(block==0||block==ulong.MaxValue)throw new IOException("Allocation du worker trophees impossible");
        bool exposed=false;
        try
        {
            var bytes=TrophyBridgeCode.Build(block,trophyBase,count,mask,trophyContext,originalUnlock,trophyState,trophySleep);Dbg.Write(Pid,block,bytes);
            if(!Same(Dbg.Read(Pid,block,bytes.Length),bytes))throw new IOException("Worker trophees non relu");
            if(!Same(Dbg.Read(Pid,site,18),oldSite)||BitConverter.ToUInt64(Dbg.Read(Pid,import,8),0)!=oldImport)
                throw new IOException("Natives trophees modifiees pendant la preparation");
            var jump=TrophyBridgeCode.Jump(block+0x100);exposed=true;
            Dbg.Write(Pid,import,BitConverter.GetBytes(block+0x400));Dbg.Write(Pid,site,jump);
            if(!Same(Dbg.Read(Pid,site,18),jump)||BitConverter.ToUInt64(Dbg.Read(Pid,import,8),0)!=block+0x400)
                throw new IOException("Worker trophees non confirme");
            core.Trace("UNLOCK ALL TROPHY : lot natif unique et suivi SDK installes et relus ; PS4 "+count+"/45. Pas d'attente GSC entre les trophees.");
        }
        catch
        {
            if(exposed){try{Dbg.Write(Pid,site,oldSite);Dbg.Write(Pid,import,BitConverter.GetBytes(oldImport));}catch{}}
            else try{Dbg.Free(Pid,block,0x4000);}catch{}
            throw;
        }
    }
}
