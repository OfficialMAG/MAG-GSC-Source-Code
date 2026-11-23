// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Text;
using System.Collections.Generic;
public partial class Injector
{
    public static byte[] LegacyBuildBlock(ulong at,ulong b,Script s,string[] names,out ulong cave,ulong near=0,ulong originalCode=0,bool callerKnown=false)
    {
        int pristine=(int)Align((ulong)(BufferOffset+s.Buffer.Length)),bytecode=(int)Align((ulong)(pristine+s.Bytecode.Length));
        var d=new byte[bytecode+s.Bytecode.Length];cave=at+CaveOffset;
        var nb=new List<byte>();foreach(string name in names){nb.AddRange(Encoding.ASCII.GetBytes(name));nb.Add(0);}nb.Add(0);
        if(nb.Count>CaveOffset-NameOffset)throw new ArgumentException("Liste de noms trop longue");
        BitConverter.GetBytes(at+NameOffset).CopyTo(d,0);BitConverter.GetBytes(s.Buffer.Length).CopyTo(d,8);BitConverter.GetBytes(s.Len).CopyTo(d,12);BitConverter.GetBytes(s.Bytecode.Length).CopyTo(d,16);
        BitConverter.GetBytes(at+BufferOffset).CopyTo(d,24);BitConverter.GetBytes(near==0?at+(ulong)bytecode:near).CopyTo(d,32);
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
        c.AddRange(new byte[]{0x48,0x39,0x50,0x20});int moved=c.Count;c.AddRange(new byte[]{0x75,0});
        int substitute=c.Count;c[noGuard+1]=(byte)(substitute-noGuard-2);
        c.AddRange(new byte[]{0x41,0xC7,0x42,0x44,0,0,0,0});

        c.AddRange(new byte[]{0x48,0x8B,0x08,0x49,0x89,0x0A,0x49,0x8B,0x7A,0x20,0x48,0xBE});c.AddRange(BitConverter.GetBytes(at+(ulong)pristine));
        c.Add(0xB9);c.AddRange(BitConverter.GetBytes(s.Bytecode.Length));c.AddRange(new byte[]{0xFC,0xF3,0xA4,0x41,0xFF,0x42,0x34,0x4C,0x89,0xD0});
        int substituted=c.Count;c.AddRange(new byte[]{0xEB,0});
        int rejected=c.Count;c.AddRange(new byte[]{0x41,0xFF,0x42,0x44});
        int end=c.Count;c[substituted+1]=(byte)(end-substituted-2);c[moved+1]=(byte)(rejected-moved-2);
        c.AddRange(new byte[]{0xBF,0x33,0,0,0,0x48,0x89,0xDE,0x49,0x89,0xC7,0xFF,0x25,0,0,0,0});c.AddRange(BitConverter.GetBytes(b+Resume));
        foreach(int fix in fixes){var delta=BitConverter.GetBytes(end-fix-6);for(int k=0;k<4;k++)c[fix+2+k]=delta[k];}
        c[mismatch+1]=(byte)(skip-mismatch-2);c[advance+1]=(byte)(adv-advance-2);c[match+1]=(byte)(ok-match-2);
        if(c.Count>BufferOffset-CaveOffset)throw new Exception("Detour trop long");c.ToArray().CopyTo(d,CaveOffset);s.Buffer.CopyTo(d,BufferOffset);s.Bytecode.CopyTo(d,pristine);s.Bytecode.CopyTo(d,bytecode);
        return d;
    }
}
