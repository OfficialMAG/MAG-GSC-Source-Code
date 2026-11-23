// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.IO;
using System.Text;
using System.Threading;
public partial class Injector
{
    static bool TrophyExec(System.Collections.Generic.List<MapEntry> maps,ulong p,string module)
    { return p!=0&&maps.Exists(m=>(m.Prot&4)!=0&&p>=m.Start&&p<m.End&&(m.Name??"").ToLowerInvariant().Contains(module)); }
    ulong TrophyResolve(ulong dlsym,int module,string name,ulong scratch)
    {
        Dbg.Write(Pid,scratch,Encoding.ASCII.GetBytes(name+"\0"));Dbg.Write(Pid,scratch+256,new byte[8]);
        if((uint)Dbg.Call(Pid,rpc,dlsym,(ulong)module,scratch,scratch+256)!=0)return 0;
        return BitConverter.ToUInt64(Dbg.Read(Pid,scratch+256,8),0);
    }
    void LogMWTrophyStatus(ulong b)
    {
        var j=Dbg.Read(Pid,b+MWIITrophyCode.Site,20);
        if(j[0]!=0xff||j[1]!=0x25||BitConverter.ToUInt32(j,2)!=0)return;
        ulong entry=BitConverter.ToUInt64(j,6);if(entry<0x100)return;
        ulong at=entry-0x100;if(!Same(Dbg.Read(Pid,at,8),MWIITrophyCode.Magic))return;
        var d=Dbg.Read(Pid,at+0x20,16);
        Log("Trophees MWII : etat="+BitConverter.ToUInt32(d,0)+", confirmes="+BitConverter.ToUInt32(d,4)+"/25, erreur SDK=0x"+BitConverter.ToUInt32(d,8).ToString("X8"));
    }
    void PrepareMWTrophies(ulong b)
    {
        var old=Dbg.Read(Pid,b+MWIITrophyCode.Site,20);
        if(!Same(old,MWIITrophyCode.Original))
        {
            if(old[0]==0xff&&old[1]==0x25&&BitConverter.ToUInt32(old,2)==0)
            {
                ulong entry=BitConverter.ToUInt64(old,6);
                if(entry>=0x100&&Same(Dbg.Read(Pid,entry-0x100,8),MWIITrophyCode.Magic)&&BitConverter.ToUInt64(Dbg.Read(Pid,entry-0xf8,8),0)==b)
                {
                    ulong owned=entry-0x100;var d=Dbg.Read(Pid,owned,0x2000);
                    var expected=MWIITrophyCode.Build(owned,b,BitConverter.ToUInt32(d,0x40),BitConverter.ToUInt32(d,0x44),BitConverter.ToUInt64(d,0x48),BitConverter.ToUInt64(d,0x50),BitConverter.ToUInt64(d,0x58),BitConverter.ToUInt64(d,0x80));
                    for(int k=0x100;k<expected.Length;k++)if(d[k]!=expected[k])throw new IOException("Worker trophees modifie : relancer MWII");
                    Log("Trophees MWII : worker 25 trophees deja installe et relu.");return;
                }
            }
            throw new IOException("Native trophees deja modifiee : relancer MWII");
        }
        PrepareRpc();var maps=Dbg.Maps(Pid);
        if(!maps.Exists(m=>(m.Name??"").ToLowerInvariant().Contains("libscenptrophy")))
        {
            ulong load=BitConverter.ToUInt64(Dbg.Read(Pid,b+0x32DFD78,8),0);
            if(!TrophyExec(maps,load,"libSceSysmodule".ToLowerInvariant()))throw new IOException("Import Sysmodule non verifie");
            uint loadStatus=(uint)Dbg.Call(Pid,rpc,load,0xAD);
            if(loadStatus!=0)throw new IOException("Chargement SDK trophees : 0x"+loadStatus.ToString("X8"));
            maps=Dbg.Maps(Pid);
        }
        ulong create=BitConverter.ToUInt64(Dbg.Read(Pid,b+0x32DFEE0,8),0);
        ulong unlock=BitConverter.ToUInt64(Dbg.Read(Pid,b+0x32DFF10,8),0);
        ulong dlsym=BitConverter.ToUInt64(Dbg.Read(Pid,b+0x32E2400,8),0);
        if(!TrophyExec(maps,create,"libscenptrophy")||!TrophyExec(maps,unlock,"libscenptrophy")||!TrophyExec(maps,dlsym,"libkernel"))throw new IOException("Imports SDK trophees non verifies");
        ulong scratch=Dbg.Alloc(Pid,0x4000),block=0,destroy=0;bool started=false,exposed=false;uint dedicated=uint.MaxValue,ownContext=uint.MaxValue;ulong destroyContext=0;
        if(scratch==0||scratch==ulong.MaxValue)throw new IOException("Allocation trophees impossible");
        try
        {
            ulong state=0;int trophyModule=-1;
            for(int module=0;module<1024;module++)
            {
                if(Cancel)throw new OperationCanceledException();
                if(TrophyResolve(dlsym,module,"sceNpTrophyCreateContext",scratch)!=create)continue;
                trophyModule=module;state=TrophyResolve(dlsym,module,"sceNpTrophyGetTrophyUnlockState",scratch);break;
            }
            ulong sleep=TrophyResolve(dlsym,0x2001,"sceKernelUsleep",scratch);
            ulong pthread=TrophyResolve(dlsym,0x2001,"scePthreadCreate",scratch);
            if(!TrophyExec(maps,state,"libscenptrophy")||!TrophyExec(maps,sleep,"libkernel")||!TrophyExec(maps,pthread,"libkernel"))throw new IOException("Symboles SDK trophees/worker introuvables");

            var cache=Dbg.Read(Pid,b+0x34474B0,8);
            bool privateContext=cache[4]!=2||cache[5]!=1;
            uint context=BitConverter.ToUInt32(cache,0);ulong register=0;
            if(privateContext)
            {
                uint user=BitConverter.ToUInt32(Dbg.Read(Pid,b+0x5B24AE0,4),0);
                if(user==uint.MaxValue||(int)user<0)throw new IOException("Utilisateur PS4 local non initialise");
                destroyContext=TrophyResolve(dlsym,trophyModule,"sceNpTrophyDestroyContext",scratch);
                if(!TrophyExec(maps,destroyContext,"libscenptrophy"))throw new IOException("Destruction contexte SDK non verifiee");
                uint createStatus=(uint)Dbg.Call(Pid,rpc,create,scratch+512,user,0,0);
                if(createStatus!=0)throw new IOException("Creation contexte trophees : 0x"+createStatus.ToString("X8"));
                context=BitConverter.ToUInt32(Dbg.Read(Pid,scratch+512,4),0);ownContext=context;
                register=BitConverter.ToUInt64(Dbg.Read(Pid,b+0x32DFF78,8),0);
                if(!TrophyExec(maps,register,"libscenptrophy"))throw new IOException("Enregistrement SDK non verifie");
            }
            ulong makeHandle=TrophyResolve(dlsym,trophyModule,"sceNpTrophyCreateHandle",scratch);
            destroy=TrophyResolve(dlsym,trophyModule,"sceNpTrophyDestroyHandle",scratch);
            if(!TrophyExec(maps,makeHandle,"libscenptrophy")||!TrophyExec(maps,destroy,"libscenptrophy"))throw new IOException("Handle SDK prive indisponible");
            if((uint)Dbg.Call(Pid,rpc,makeHandle,scratch+2048)!=0)throw new IOException("Creation handle trophees impossible");
            dedicated=BitConverter.ToUInt32(Dbg.Read(Pid,scratch+2048,4),0);uint handle=dedicated;
            if(!privateContext&&((uint)Dbg.Call(Pid,rpc,state,context,handle,scratch+512,scratch+544)!=0||BitConverter.ToUInt32(Dbg.Read(Pid,scratch+544,4),0)!=25))
                throw new IOException("Liste SDK de 25 trophees non confirmee");
            block=Dbg.Alloc(Pid,0x2000);if(block==0||block==ulong.MaxValue)throw new IOException("Allocation worker trophees impossible");
            var bytes=MWIITrophyCode.Build(block,b,context,handle,unlock,state,sleep,register);
            Dbg.Write(Pid,block,bytes);if(!Same(Dbg.Read(Pid,block,bytes.Length),bytes))throw new IOException("Worker trophees non relu");
            if(!Same(Dbg.Read(Pid,b+MWIITrophyCode.Site,20),old))throw new IOException("Native trophees modifiee pendant la preparation");
            uint result=(uint)Dbg.Call(Pid,rpc,pthread,scratch+1024,0,block+0x800,block,block+0x1800);
            if(result!=0)throw new IOException("Creation worker trophees : 0x"+result.ToString("X8"));
            started=true;
            exposed=true;var jump=MWIITrophyCode.Jump(block+0x100);Dbg.Write(Pid,b+MWIITrophyCode.Site,jump);
            if(!Same(Dbg.Read(Pid,b+MWIITrophyCode.Site,20),jump))throw new IOException("Pont trophees non confirme");
            Log("Trophees MWII : bouton local relie au SDK, 25 entrees attendues, verification SDK avant deblocage. Aucun deblocage avant selection dans Account Options.");
        }
        catch
        {
            if(exposed)try{Dbg.Write(Pid,b+MWIITrophyCode.Site,old);}catch{}
            if(!started&&block!=0&&block!=ulong.MaxValue)try{Dbg.Free(Pid,block,0x2000);}catch{}
            throw;
        }
        finally { if(!started&&dedicated!=uint.MaxValue&&destroy!=0)try{Dbg.Call(Pid,rpc,destroy,dedicated);}catch{} if(!started&&ownContext!=uint.MaxValue&&destroyContext!=0)try{Dbg.Call(Pid,rpc,destroyContext,ownContext);}catch{} Dbg.Free(Pid,scratch,0x4000); }
    }
}
