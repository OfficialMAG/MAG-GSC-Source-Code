// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.IO;

public partial class Injector
{

    public static uint ZombieGravityFlags(byte[] data)
    {
        if(data==null||data.Length!=0x58||BitConverter.ToUInt64(data,0x30)!=0x3C01342F08FA6256UL||data[0x45]!=1)
            throw new IOException("Dvar de gravite Zombies incompatible : aucune modification");
        uint flags=BitConverter.ToUInt32(data,0x40);
        if((flags&0xC0)!=0xC0)throw new IOException("Replication de gravite inattendue : aucune modification");
        return flags|0x100;
    }
    public void PrepareZombieOptions()
    {
        if(!IsZombies)return;
        ulong b=FindBase();ulong variable=BitConverter.ToUInt64(Dbg.Read(Pid,b+0xC8D2028,8),0);
        var maps=Dbg.Maps(Pid);
        if(variable>ulong.MaxValue-0x58||!maps.Exists(m=>(m.Prot&3)==3&&variable>=m.Start&&variable+0x58<=m.End))
            throw new IOException("Dvar de gravite Zombies inaccessible : aucune modification");
        var data=Dbg.Read(Pid,variable,0x58);uint flags=ZombieGravityFlags(data);
        if(flags!=BitConverter.ToUInt32(data,0x40))
        {
            Dbg.Write(Pid,variable+0x40,BitConverter.GetBytes(flags));
            if(BitConverter.ToUInt32(Dbg.Read(Pid,variable+0x40,4),0)!=flags)
                throw new IOException("Permission de gravite Zombies non confirmee");
        }
        core.Trace("Zombies : gravite moteur autorisee pour les scripts ; replication conservee, valeur inchangee.");
    }
}
