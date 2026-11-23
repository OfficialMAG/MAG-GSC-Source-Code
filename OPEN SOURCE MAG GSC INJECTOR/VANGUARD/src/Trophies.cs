// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.IO;
using System.Text;
using System.Threading;

public partial class Injector
{
    ulong trophyState, trophyScratch, trophyBase, trophySleep;
    uint trophyContext, trophyHandle;
    uint TrophyCall(ulong f, params ulong[] args) { return (uint)Dbg.Call(Pid,rpc,f,args); }
    ulong TrophySymbol(ulong dlsym,int module,string name)
    {
        Dbg.Write(Pid,trophyScratch,Encoding.ASCII.GetBytes(name+"\0"));
        Dbg.Write(Pid,trophyScratch+256,new byte[8]);
        if(TrophyCall(dlsym,(ulong)module,trophyScratch,trophyScratch+256)!=0)return 0;
        return BitConverter.ToUInt64(Dbg.Read(Pid,trophyScratch+256,8),0);
    }
    static bool Readable(System.Collections.Generic.List<MapEntry> maps,ulong a,ulong n)
    { return a!=0&&a<=ulong.MaxValue-n&&maps.Exists(m=>(m.Prot&1)!=0&&a>=m.Start&&a+n<=m.End); }
    uint TrophyReadState(ulong output)
    { return TrophyCall(trophyState,trophyContext,trophyHandle,output,output+32); }

    public void PrepareTrophies()
    {
        trophyBase=FindBase();PrepareRpc();var maps=Dbg.Maps(Pid);
        trophyScratch=Dbg.Alloc(Pid,0x4000);
        if(trophyScratch==0||trophyScratch==ulong.MaxValue)throw new IOException("Allocation trophees impossible");
        try
        {
            var stub=Dbg.Read(Pid,trophyBase+0x241D560,6);
            if(stub[0]!=0xFF||stub[1]!=0x25)throw new IOException("Import CreateContext incompatible");
            ulong got=(ulong)((long)(trophyBase+0x241D566)+BitConverter.ToInt32(stub,2));
            ulong create=BitConverter.ToUInt64(Dbg.Read(Pid,got,8),0);
            ulong dlsym=BitConverter.ToUInt64(Dbg.Read(Pid,trophyBase+0x2AB3828,8),0);
            if(!maps.Exists(m=>(m.Prot&4)!=0&&create>=m.Start&&create<m.End&&m.Name=="libSceNpTrophy.sprx") ||
               !maps.Exists(m=>(m.Prot&4)!=0&&dlsym>=m.Start&&dlsym<m.End&&(m.Name??"").ToLowerInvariant().Contains("libkernel")))
                throw new IOException("Imports systeme non verifies");
            trophyState=0;

            for(int pass=-2;pass<1024;pass++)
            {
                if(Cancel)throw new OperationCanceledException();int module=pass==-2?416:pass==-1?417:pass;if(pass==416||pass==417)continue;
                ulong candidate=TrophySymbol(dlsym,module,"sceNpTrophyCreateContext");
                if(candidate!=create)continue;
                trophyState=TrophySymbol(dlsym,module,"sceNpTrophyGetTrophyUnlockState");break;
            }
            if(!maps.Exists(m=>(m.Prot&4)!=0&&trophyState>=m.Start&&trophyState<m.End&&m.Name=="libSceNpTrophy.sprx"))
                throw new IOException("Lecture des trophees introuvable");
            trophySleep=TrophySymbol(dlsym,0x2001,"sceKernelUsleep");
            if(!maps.Exists(m=>(m.Prot&4)!=0&&trophySleep>=m.Start&&trophySleep<m.End&&(m.Name??"").ToLowerInvariant().Contains("libkernel")))
                throw new IOException("Temporisation du worker PS4 introuvable");
            var cache=Dbg.Read(Pid,trophyBase+0x2C0E2E0,32);
            trophyContext=BitConverter.ToUInt32(cache,0);
            trophyHandle=BitConverter.ToUInt32(Dbg.Read(Pid,trophyBase+0x5AC5D44,4),0);
            if(cache[4]!=0&&TrophyReadState(trophyScratch+512)==0)
            { InstallTrophyBridge(TrophyUnlockedCount(trophyScratch+512));core.Trace("Trophees : contexte PS4 confirme et bouton du menu relie a la file native.");return; }
            uint user=BitConverter.ToUInt32(Dbg.Read(Pid,trophyBase+0x62C3A80,4),0);
            Dbg.Write(Pid,trophyScratch+512,BitConverter.GetBytes(uint.MaxValue));
            uint result=TrophyCall(create,trophyScratch+512,user,0,0);
            if(result==0)trophyContext=BitConverter.ToUInt32(Dbg.Read(Pid,trophyScratch+512,4),0);
            else if(result==0x80551613)
            {

                var code=Dbg.Read(Pid,trophyState,160);
                byte[] prefix={0x55,0x48,0x89,0xE5,0x41,0x57,0x41,0x56};
                for(int i=0;i<prefix.Length;i++)if(code[i]!=prefix[i])throw new IOException("Disposition NpTrophy inconnue");
                ulong registry=0;
                for(int i=0;i<=code.Length-7;i++)if(code[i]==0x4C&&code[i+1]==0x8D&&code[i+2]==0x3D)
                {
                    ulong global=(ulong)((long)(trophyState+(ulong)i+7)+BitConverter.ToInt32(code,i+3));
                    if(!maps.Exists(m=>m.Name=="libSceNpTrophy.sprx"&&(m.Prot&3)==3&&global>=m.Start&&global+8<=m.End))continue;
                    registry=BitConverter.ToUInt64(Dbg.Read(Pid,global,8),0);break;
                }
                if(!Readable(maps,registry,0x360))throw new IOException("Registre des contextes inaccessible");
                var slots=Dbg.Read(Pid,registry+0x160,512);uint found=uint.MaxValue;int matches=0;
                for(int i=0;i<64;i++)
                {
                    ulong slot=BitConverter.ToUInt64(slots,i*8);if(!Readable(maps,slot,0x40))continue;
                    var row=Dbg.Read(Pid,slot,0x40);
                    if(row[0x14]==0||BitConverter.ToUInt32(row,0x34)!=user)continue;
                    found=BitConverter.ToUInt32(row,8);matches++;
                }
                if(matches!=1)throw new IOException("Contexte de l'utilisateur ambigu ou absent");trophyContext=found;
            }
            else throw new IOException("CreateContext : 0x"+result.ToString("X8"));
            result=TrophyReadState(trophyScratch+512);
            if(result==0x8055160F)
            {
                result=TrophyCall(trophyBase+0x241D450,trophyContext,trophyHandle,0);
                if(result!=0&&result!=0x80551610)throw new IOException("RegisterContext : 0x"+result.ToString("X8"));
            }
            else if(result!=0)throw new IOException("Contexte existant inaccessible : 0x"+result.ToString("X8"));
            result=TrophyReadState(trophyScratch+512);
            if(result!=0)throw new IOException("Verification des trophees : 0x"+result.ToString("X8"));
            var repaired=new byte[6];BitConverter.GetBytes(trophyContext).CopyTo(repaired,0);repaired[4]=repaired[5]=1;
            Dbg.Write(Pid,trophyBase+0x2C0E2E0,repaired);
            if(!Same(Dbg.Read(Pid,trophyBase+0x2C0E2E0,6),repaired))throw new IOException("Contexte non confirme");
            InstallTrophyBridge(TrophyUnlockedCount(trophyScratch+512));
            core.Trace("Trophees : contexte 0x"+trophyContext.ToString("X")+" repare et bouton du menu relie a la file native.");
        }
        finally { Dbg.Free(Pid,trophyScratch,0x4000);trophyScratch=0; }
    }

    public int UnlockTrophies(int first,int last,Action<int,int,string> progress)
    {
        if(first<1||last>44||first>last)throw new ArgumentOutOfRangeException("Trophy range 1..44");
        PrepareTrophies();ulong scratch=Dbg.Alloc(Pid,0x4000);int done=0;
        if(scratch==0||scratch==ulong.MaxValue)throw new IOException("Allocation verification trophees impossible");
        try
        {
            for(int id=first;id<=last;id++)
            {
                if(Cancel)throw new OperationCanceledException();
                uint r=TrophyReadState(scratch);if(r!=0)throw new IOException("GetUnlockState 0x"+r.ToString("X8"));
                byte[] state=Dbg.Read(Pid,scratch,16);
                string name=Encoding.ASCII.GetString(Dbg.Read(Pid,trophyBase+0x2688960+(ulong)id*48,32)).TrimEnd('\0');
                if((state[id/8]&(1<<(id%8)))==0)
                {
                    Dbg.Write(Pid,scratch+128,Encoding.ASCII.GetBytes(name+"\0"));
                    TrophyCall(trophyBase+0x5A2B50,0,scratch+128,0,0);
                    bool confirmed=false;
                    for(int poll=0;poll<40;poll++)
                    {
                        if(Cancel)throw new OperationCanceledException();Thread.Sleep(250);
                        r=TrophyReadState(scratch);if(r!=0)throw new IOException("GetUnlockState 0x"+r.ToString("X8"));
                        state=Dbg.Read(Pid,scratch,16);if((state[id/8]&(1<<(id%8)))!=0){confirmed=true;break;}
                    }
                    if(!confirmed)throw new IOException("Trophee "+id+" non confirme par la PS4");
                }
                done++;if(progress!=null)progress(done,last-first+1,name);
            }
            return done;
        }
        finally { Dbg.Free(Pid,scratch,0x4000); }
    }
}
