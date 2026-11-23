using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

public class MapEntry { public string Name; public ulong Start, End; public ushort Prot; }
public class ProcEntry { public string Name; public int Pid; public override string ToString() { return Name + " (pid " + Pid + ")"; } }

// Protocole ps4debug (port 744) : en-tete {0xFFAABBCC, cmd, longueur} puis donnees ; reponse = statut 0x80000000.
public class Ps4Debug
{
    const uint Magic = 0xFFAABBCC, Success = 0x80000000;
    const uint CmdProcList = 0xBDAA0001, CmdProcRead = 0xBDAA0002, CmdProcWrite = 0xBDAA0003, CmdProcMaps = 0xBDAA0004;
    const uint CmdNotify = 0xBDDD0004, CmdProcAlloc = 0xBDAA000B, CmdProcFree = 0xBDAA000C;
    TcpClient tcp; NetworkStream ns;
    public readonly object Lock = new object();
    public bool Connected { get { return tcp != null && tcp.Connected; } }

    public void Connect(string ip, int port)
    {
        tcp = new TcpClient();
        var ar = tcp.BeginConnect(ip, port, null, null);
        if (!ar.AsyncWaitHandle.WaitOne(5000)) { tcp.Close(); tcp = null; throw new Exception(L.T("pas de reponse de ", "no response from ") + ip + ":" + port); }
        tcp.EndConnect(ar);
        tcp.NoDelay = true;
        tcp.ReceiveTimeout = 10000; tcp.SendTimeout = 10000;
        ns = tcp.GetStream();
    }
    public void Close() { if (tcp != null) tcp.Close(); tcp = null; }

    void Send(byte[] b) { ns.Write(b, 0, b.Length); }
    byte[] Recv(int n)
    {
        var b = new byte[n]; int o = 0;
        while (o < n) { int r = ns.Read(b, o, n - o); if (r <= 0) throw new Exception(L.T("connexion fermee", "connection closed")); o += r; }
        return b;
    }
    void Cmd(uint cmd, byte[] data)
    {
        var h = new byte[12];
        BitConverter.GetBytes(Magic).CopyTo(h, 0);
        BitConverter.GetBytes(cmd).CopyTo(h, 4);
        BitConverter.GetBytes(data == null ? 0 : data.Length).CopyTo(h, 8);
        Send(h);
        if (data != null && data.Length > 0) Send(data);
    }
    void Status()
    {
        uint s = BitConverter.ToUInt32(Recv(4), 0);
        if (s != Success) throw new Exception(string.Format(L.T("ps4debug a repondu 0x{0:X8}", "ps4debug replied 0x{0:X8}"), s));
    }
    static byte[] RwPacket(int pid, ulong addr, int len)
    {
        var p = new byte[16];
        BitConverter.GetBytes(pid).CopyTo(p, 0);
        BitConverter.GetBytes(addr).CopyTo(p, 4);
        BitConverter.GetBytes(len).CopyTo(p, 12);
        return p;
    }

    public List<ProcEntry> ProcList()
    {
        lock (Lock)
        {
            Cmd(CmdProcList, null); Status();
            int n = BitConverter.ToInt32(Recv(4), 0);
            var d = Recv(n * 36); var l = new List<ProcEntry>();
            for (int i = 0; i < n; i++)
                l.Add(new ProcEntry { Name = Encoding.ASCII.GetString(d, i * 36, 32).TrimEnd('\0'), Pid = BitConverter.ToInt32(d, i * 36 + 32) });
            return l;
        }
    }
    public List<MapEntry> Maps(int pid)
    {
        lock (Lock)
        {
            Cmd(CmdProcMaps, BitConverter.GetBytes(pid)); Status();
            int n = BitConverter.ToInt32(Recv(4), 0);
            var d = Recv(n * 58); var l = new List<MapEntry>();
            for (int i = 0; i < n; i++)
            {
                int o = i * 58;
                l.Add(new MapEntry
                {
                    Name = Encoding.ASCII.GetString(d, o, 32).TrimEnd('\0'),
                    Start = BitConverter.ToUInt64(d, o + 32),
                    End = BitConverter.ToUInt64(d, o + 40),
                    Prot = BitConverter.ToUInt16(d, o + 56)
                });
            }
            return l;
        }
    }
    public byte[] Read(int pid, ulong addr, int len)
    {
        lock (Lock) { Cmd(CmdProcRead, RwPacket(pid, addr, len)); Status(); return Recv(len); }
    }
    public void Write(int pid, ulong addr, byte[] data)
    {
        lock (Lock) { Cmd(CmdProcWrite, RwPacket(pid, addr, data.Length)); Status(); Send(data); Status(); }
    }
    // Notification systeme de la PS4 (sceSysUtilSendSystemNotificationWithText) : {type, longueur} puis le texte UTF-8 termine par 0.
    public void Notify(int type, string text)
    {
        var t = Encoding.UTF8.GetBytes(text + "\0"); var p = new byte[8];
        BitConverter.GetBytes(type).CopyTo(p, 0);
        BitConverter.GetBytes(t.Length).CopyTo(p, 4);
        lock (Lock) { Cmd(CmdNotify, p); Send(t); Status(); }
    }
    // allocation dans le processus du jeu : {pid, longueur} -> adresse 64 bits
    public ulong Alloc(int pid, int len)
    {
        var p = new byte[8];
        BitConverter.GetBytes(pid).CopyTo(p, 0);
        BitConverter.GetBytes(len).CopyTo(p, 4);
        lock (Lock) { Cmd(CmdProcAlloc, p); Status(); return BitConverter.ToUInt64(Recv(8), 0); }
    }
    public void Free(int pid, ulong addr, int len)
    {
        var p = new byte[16];
        BitConverter.GetBytes(pid).CopyTo(p, 0);
        BitConverter.GetBytes(addr).CopyTo(p, 4);
        BitConverter.GetBytes(len).CopyTo(p, 12);
        lock (Lock) { Cmd(CmdProcFree, p); Status(); }
    }
    // envoi du payload ps4debug au chargeur bin de GoldHEN (port 9090 par defaut)
    public static void SendPayload(string ip, int port, byte[] payload)
    {
        using (var c = new TcpClient())
        {
            var ar = c.BeginConnect(ip, port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(5000)) throw new Exception(L.T("pas de reponse de ", "no response from ") + ip + ":" + port + L.T(" (chargeur bin de GoldHEN active ?)", " (is the GoldHEN bin loader enabled?)"));
            c.EndConnect(ar);
            c.SendTimeout = 10000;
            var ns = c.GetStream();
            ns.Write(payload, 0, payload.Length);
            ns.Flush();
        }
    }
}
