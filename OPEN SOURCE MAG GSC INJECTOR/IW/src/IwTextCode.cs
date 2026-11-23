// Dev By UrBaaZz & ZERTY — sources publiques (commentaires techniques et cible d'injection masqués).
using System;
using System.Collections.Generic;

public static class IwTextCode
{
    public const ulong SetSite = 0x7A4710, ClearSite = 0xBFC240;
    public const ulong HudElems = 0x60F6300, ConfigRoot = 0x76DDD88;
    public const ulong EmptyStringId = 0x61D8980, SetConfigString = 0xD9D1E0;
    public static readonly byte[] SetOriginal = { 0x55, 0x48, 0x89, 0xE5, 0x53 };
    public static readonly byte[] ClearOriginal = { 0xC3, 0x66, 0x66, 0x66, 0x66 };

    public static byte[] SetCode(ulong b)
    {
        var c = new List<byte>();
        c.AddRange(new byte[] { 0x53, 0x48, 0x89, 0xD3, 0x48, 0x89, 0xF7, 0x31, 0xC0 });

        c.AddRange(new byte[] { 0x80, 0x3F, 0x00 });
        int empty = c.Count; c.AddRange(new byte[] { 0x74, 0 });
        c.Add(0xBE); c.AddRange(BitConverter.GetBytes(0x180));
        c.Add(0xBA); c.AddRange(BitConverter.GetBytes(0x800));
        c.Add(0xB9); c.AddRange(BitConverter.GetBytes(1));
        c.AddRange(new byte[] { 0x49, 0xB8 }); c.AddRange(BitConverter.GetBytes(b + 0x135F14F));
        c.AddRange(new byte[] { 0x49, 0xBB }); c.AddRange(BitConverter.GetBytes(b + 0x79BC70));
        c.AddRange(new byte[] { 0x41, 0xFF, 0xD3 });
        int done = c.Count;
        c.AddRange(new byte[] { 0x48, 0x85, 0xDB });
        int noOut = c.Count; c.AddRange(new byte[] { 0x74, 0 });
        c.AddRange(new byte[] { 0x89, 0x03 });
        int ret = c.Count; c.AddRange(new byte[] { 0x5B, 0xC3 });
        c[empty + 1] = (byte)(done - empty - 2);
        c[noOut + 1] = (byte)(ret - noOut - 2);
        return c.ToArray();
    }

    public static byte[] ClearCode(ulong b)
    {
        var c = new List<byte>(); var exits = new List<int>();
        c.AddRange(new byte[] { 0x55, 0x48, 0x89, 0xE5, 0x53, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56 });
        c.AddRange(new byte[] { 0x89, 0xF8, 0x25, 0x00, 0x00, 0xFF, 0xFF, 0x3D, 0x00, 0x00, 0x01, 0x00 });

        exits.Add(c.Count); c.AddRange(new byte[] { 0x0F, 0x85, 0, 0, 0, 0 });
        c.AddRange(new byte[] { 0x0F, 0xB7, 0xC7, 0x3D }); c.AddRange(BitConverter.GetBytes(0x32A));
        exits.Add(c.Count); c.AddRange(new byte[] { 0x0F, 0x83, 0, 0, 0, 0 });
        c.AddRange(new byte[] { 0x48, 0x69, 0xC0 }); c.AddRange(BitConverter.GetBytes(0xC0));
        c.AddRange(new byte[] { 0x48, 0xB9 }); c.AddRange(BitConverter.GetBytes(b + HudElems));
        c.AddRange(new byte[] { 0x48, 0x01, 0xC8, 0x8B, 0x98 }); c.AddRange(BitConverter.GetBytes(0x94));
        c.AddRange(new byte[] { 0x85, 0xDB });
        exits.Add(c.Count); c.AddRange(new byte[] { 0x0F, 0x84, 0, 0, 0, 0 });
        c.AddRange(new byte[] { 0x49, 0xBC }); c.AddRange(BitConverter.GetBytes(b + EmptyStringId));
        c.AddRange(new byte[] { 0x49, 0xBD }); c.AddRange(BitConverter.GetBytes(b + ConfigRoot));
        c.AddRange(new byte[] { 0x4D, 0x8B, 0x6D, 0x00, 0x4D, 0x85, 0xED });
        exits.Add(c.Count); c.AddRange(new byte[] { 0x0F, 0x84, 0, 0, 0, 0 });
        c.AddRange(new byte[] { 0x4D, 0x8B, 0xAD, 0x18, 0x40, 0, 0, 0x4D, 0x85, 0xED });
        exits.Add(c.Count); c.AddRange(new byte[] { 0x0F, 0x84, 0, 0, 0, 0 });
        c.AddRange(new byte[] { 0x49, 0xBE }); c.AddRange(BitConverter.GetBytes(b + SetConfigString));
        int loop = c.Count;
        c.AddRange(new byte[] { 0xFF, 0xC3, 0x81, 0xFB }); c.AddRange(BitConverter.GetBytes(0x800));
        exits.Add(c.Count); c.AddRange(new byte[] { 0x0F, 0x83, 0, 0, 0, 0 });
        c.AddRange(new byte[] { 0x8D, 0xBB }); c.AddRange(BitConverter.GetBytes(0x180));
        c.AddRange(new byte[] { 0x41, 0x8B, 0x44, 0xBD, 0x00, 0x85, 0xC0 });
        exits.Add(c.Count); c.AddRange(new byte[] { 0x0F, 0x84, 0, 0, 0, 0 });
        c.AddRange(new byte[] { 0x41, 0x3B, 0x04, 0x24 });
        exits.Add(c.Count); c.AddRange(new byte[] { 0x0F, 0x84, 0, 0, 0, 0 });
        c.AddRange(new byte[] { 0x31, 0xF6, 0x41, 0xFF, 0xD6 });
        c.Add(0xE9); c.AddRange(BitConverter.GetBytes(loop - (c.Count + 4)));
        int end = c.Count;
        c.AddRange(new byte[] { 0x41, 0x5E, 0x41, 0x5D, 0x41, 0x5C, 0x5B, 0x5D, 0xC3 });
        foreach (int x in exits) { var v = BitConverter.GetBytes(end - x - 6); for (int k = 0; k < 4; k++) c[x + 2 + k] = v[k]; }
        if (c.Count > 0x100) throw new Exception("IW clear code exceeds its reserved space");
        return c.ToArray();
    }
}
