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
            var d=Dbg.Read(Pid,at,0x200);if(!Same(d.Sub(0,8),MWIILoaderCode.ControlMagic)||BitConverter.ToUInt64(d,8)!=b)return 0;
            var expected=MWIILoaderCode.Control(at,b,BitConverter.ToUInt64(d,0x10));
            if(!Same(d.Sub(0x100,0x100),expected.Sub(0x100,0x100)))throw new IOException("Code du controleur MAG modifie");return at;
        }
        catch(IOException){throw;}catch{return 0;}
    }
    ulong OwnedBlock(ulong b)
    {
        ulong c=OwnedControl(b);if(c==0)return 0;
        ulong at=BitConverter.ToUInt64(Dbg.Read(Pid,c+0x10,8),0);
        if(at==0||!Same(Dbg.Read(Pid,at+0x38,8),MWIILoaderCode.Magic))throw new IOException("Generation MWII incoherente");
        return at;
    }
}
