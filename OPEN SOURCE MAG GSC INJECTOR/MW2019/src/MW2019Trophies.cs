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
        var j=Dbg.Read(Pid,b+MW2019TrophyCode.Site,16);
        if(j[0]!=0xff||j[1]!=0x25||BitConverter.ToUInt32(j,2)!=0)return;
        ulong entry=BitConverter.ToUInt64(j,6);if(entry<0x100)return;
        ulong at=entry-0x100;if(!Same(Dbg.Read(Pid,at,8),MW2019TrophyCode.Magic))return;
        var d=Dbg.Read(Pid,at+0x20,16);
        Log("Trophees MW2019 : etat="+BitConverter.ToUInt32(d,0)+", confirmes="+BitConverter.ToUInt32(d,4)+"/28, erreur SDK=0x"+BitConverter.ToUInt32(d,8).ToString("X8"));
    }
    void PrepareMWTrophies(ulong b)
    {
        var old=Dbg.Read(Pid,b+MW2019TrophyCode.Site,16);
        if(!Same(old,MW2019TrophyCode.Original))
        {
            if(old[0]==0xff&&old[1]==0x25&&BitConverter.ToUInt32(old,2)==0)
            {
                ulong entry=BitConverter.ToUInt64(old,6);
                if(entry>=0x100&&Same(Dbg.Read(Pid,entry-0x100,8),MW2019TrophyCode.Magic)&&BitConverter.ToUInt64(Dbg.Read(Pid,entry-0xf8,8),0)==b)
                {
                    ulong owned=entry-0x100;var d=Dbg.Read(Pid,owned,0x2000);
                    var expected=MW2019TrophyCode.Build(owned,b,BitConverter.ToUInt32(d,0x40),BitConverter.ToUInt32(d,0x44),BitConverter.ToUInt64(d,0x48),BitConverter.ToUInt64(d,0x50),BitConverter.ToUInt64(d,0x58));
                    for(int k=0x100;k<expected.Length;k++)if(d[k]!=expected[k])throw new IOException("Worker trophees modifie : relancer MW2019");
                    Log("Trophees MW2019 : worker 28 trophees deja installe et relu.");return;
                }
            }
            throw new IOException("Native trophees deja modifiee : relancer MW2019");
        }
        PrepareRpc();var maps=Dbg.Maps(Pid);
        ulong create=BitConverter.ToUInt64(Dbg.Read(Pid,b+0x1EB95C8,8),0);
        ulong unlock=BitConverter.ToUInt64(Dbg.Read(Pid,b+0x1EB95F0,8),0);
        ulong dlsym=BitConverter.ToUInt64(Dbg.Read(Pid,b+0x1EB99F8,8),0);
        if(!TrophyExec(maps,create,"libscenptrophy")||!TrophyExec(maps,unlock,"libscenptrophy")||!TrophyExec(maps,dlsym,"libkernel"))throw new IOException("Imports SDK trophees non verifies");
        ulong scratch=Dbg.Alloc(Pid,0x4000),block=0,destroy=0;bool started=false,exposed=false;uint dedicated=uint.MaxValue;
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

            var cache=Dbg.Read(Pid,b+0x230DB40,8);
            if(cache[4]!=1)throw new IOException("Contexte SDK non initialise par le jeu : preparation differee");
            uint context=BitConverter.ToUInt32(cache,0);
            uint handle=BitConverter.ToUInt32(Dbg.Read(Pid,b+0xB6EE0B4,4),0);
            if(cache[4]!=1||(uint)Dbg.Call(Pid,rpc,state,context,handle,scratch+512,scratch+544)!=0||BitConverter.ToUInt32(Dbg.Read(Pid,scratch+544,4),0)!=28)
                throw new IOException("Contexte local ou liste de 28 trophees non confirme par le SDK");

            ulong makeHandle=TrophyResolve(dlsym,trophyModule,"sceNpTrophyCreateHandle",scratch);
            destroy=TrophyResolve(dlsym,trophyModule,"sceNpTrophyDestroyHandle",scratch);
            if(!TrophyExec(maps,makeHandle,"libscenptrophy")||!TrophyExec(maps,destroy,"libscenptrophy"))throw new IOException("Handle SDK prive indisponible");
            if((uint)Dbg.Call(Pid,rpc,makeHandle,scratch+2048)!=0)throw new IOException("Creation handle trophees impossible");
            dedicated=BitConverter.ToUInt32(Dbg.Read(Pid,scratch+2048,4),0);handle=dedicated;
            if((uint)Dbg.Call(Pid,rpc,state,context,handle,scratch+512,scratch+544)!=0)throw new IOException("Handle prive non confirme");
            block=Dbg.Alloc(Pid,0x2000);if(block==0||block==ulong.MaxValue)throw new IOException("Allocation worker trophees impossible");
            var bytes=MW2019TrophyCode.Build(block,b,context,handle,unlock,state,sleep);
            Dbg.Write(Pid,block,bytes);if(!Same(Dbg.Read(Pid,block,bytes.Length),bytes))throw new IOException("Worker trophees non relu");
            if(!Same(Dbg.Read(Pid,b+MW2019TrophyCode.Site,16),old))throw new IOException("Native trophees modifiee pendant la preparation");
            uint result=(uint)Dbg.Call(Pid,rpc,pthread,scratch+1024,0,block+0x800,block,block+0x1800);
            if(result!=0)throw new IOException("Creation worker trophees : 0x"+result.ToString("X8"));
            started=true;
            exposed=true;var jump=MW2019TrophyCode.Jump(block+0x100);Dbg.Write(Pid,b+MW2019TrophyCode.Site,jump);
            if(!Same(Dbg.Read(Pid,b+MW2019TrophyCode.Site,16),jump))throw new IOException("Pont trophees non confirme");
            Log("Trophees MW2019 : bouton local relie au SDK, 28 entrees confirmees. Aucun deblocage avant selection dans Account Options.");
        }
        catch
        {
            if(exposed)try{Dbg.Write(Pid,b+MW2019TrophyCode.Site,old);}catch{}
            if(!started&&block!=0&&block!=ulong.MaxValue)try{Dbg.Free(Pid,block,0x2000);}catch{}
            throw;
        }
        finally { if(!started&&dedicated!=uint.MaxValue&&destroy!=0)try{Dbg.Call(Pid,rpc,destroy,dedicated);}catch{} Dbg.Free(Pid,scratch,0x4000); }
    }
}
