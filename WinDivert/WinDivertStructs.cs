using System.Runtime.InteropServices;

namespace FireWallService.WinDivert
{
    /// <summary>
    /// WinDivert layers
    /// </summary>
    public enum WINDIVERT_LAYER : uint
    {
        WINDIVERT_LAYER_NETWORK = 0,
        WINDIVERT_LAYER_NETWORK_FORWARD = 1,
        WINDIVERT_LAYER_FLOW = 2,
        WINDIVERT_LAYER_SOCKET = 3,
        WINDIVERT_LAYER_REFLECT = 4
    }

    /// <summary>
    /// WinDivert events
    /// </summary>
    public enum WINDIVERT_EVENT : ushort
    {
        WINDIVERT_EVENT_NETWORK_PACKET = 0,
        WINDIVERT_EVENT_FLOW_ESTABLISHED = 1,
        WINDIVERT_EVENT_FLOW_DELETED = 2,
        WINDIVERT_EVENT_SOCKET_BIND = 3,
        WINDIVERT_EVENT_SOCKET_CONNECT = 4,
        WINDIVERT_EVENT_SOCKET_LISTEN = 5,
        WINDIVERT_EVENT_SOCKET_ACCEPT = 6,
        WINDIVERT_EVENT_SOCKET_CLOSE = 7,
        WINDIVERT_EVENT_REFLECT_OPEN = 8,
        WINDIVERT_EVENT_REFLECT_CLOSE = 9
    }

    /// <summary>
    /// WinDivert shutdown modes
    /// </summary>
    public enum WINDIVERT_SHUTDOWN : uint
    {
        WINDIVERT_SHUTDOWN_RECV = 0x1,
        WINDIVERT_SHUTDOWN_SEND = 0x2,
        WINDIVERT_SHUTDOWN_BOTH = 0x3
    }

    /// <summary>
    /// WinDivert flags for WinDivertOpen
    /// </summary>
    [Flags]
    public enum WINDIVERT_FLAGS : ulong
    {
        None = 0,
        WINDIVERT_FLAG_SNIFF = 0x0001,
        WINDIVERT_FLAG_DROP = 0x0002,
        WINDIVERT_FLAG_RECV_ONLY = 0x0003,
        WINDIVERT_FLAG_READ_ONLY = WINDIVERT_FLAG_RECV_ONLY,
        WINDIVERT_FLAG_SEND_ONLY = 0x0004,
        WINDIVERT_FLAG_WRITE_ONLY = WINDIVERT_FLAG_SEND_ONLY,
        WINDIVERT_FLAG_NO_INSTALL = 0x0008,
        WINDIVERT_FLAG_FRAGMENTS = 0x0010
    }

    /// <summary>
    /// WinDivert parameters
    /// </summary>
    public enum WINDIVERT_PARAM : uint
    {
        WINDIVERT_PARAM_QUEUE_LENGTH = 0,
        WINDIVERT_PARAM_QUEUE_TIME = 1,
        WINDIVERT_PARAM_QUEUE_SIZE = 2,
        WINDIVERT_PARAM_VERSION_MAJOR = 3,
        WINDIVERT_PARAM_VERSION_MINOR = 4
    }

    /// <summary>
    /// Network data for WINDIVERT_LAYER_NETWORK and WINDIVERT_LAYER_NETWORK_FORWARD
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDIVERT_DATA_NETWORK
    {
        public uint IfIdx;
        public uint SubIfIdx;
    }

    /// <summary>
    /// Flow data for WINDIVERT_LAYER_FLOW
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDIVERT_DATA_FLOW
    {
        public ulong Endpoint;
        public ulong ParentEndpoint;
        public uint ProcessId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] LocalAddr;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] RemoteAddr;
        public ushort LocalPort;
        public ushort RemotePort;
        public byte Protocol;
    }

    /// <summary>
    /// Socket data for WINDIVERT_LAYER_SOCKET
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDIVERT_DATA_SOCKET
    {
        public ulong Endpoint;
        public ulong ParentEndpoint;
        public uint ProcessId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] LocalAddr;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] RemoteAddr;
        public ushort LocalPort;
        public ushort RemotePort;
        public byte Protocol;
    }

    /// <summary>
    /// Reflect data for WINDIVERT_LAYER_REFLECT
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDIVERT_DATA_REFLECT
    {
        public long Timestamp;
        public uint ProcessId;
        public WINDIVERT_LAYER Layer;
        public ulong Flags;
        public short Priority;
    }

    /// <summary>
    /// WinDivert address structure - represents the "address" of a captured packet
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDIVERT_ADDRESS
    {
        public long Timestamp;
        public ulong Layer;       // 8 bits
        public ulong Event;       // 8 bits
        public ulong Sniffed;     // 1 bit
        public ulong Outbound;    // 1 bit
        public ulong Loopback;    // 1 bit
        public ulong Impostor;    // 1 bit
        public ulong IPv6;        // 1 bit
        public ulong IPChecksum;  // 1 bit
        public ulong TCPChecksum; // 1 bit
        public ulong UDPChecksum; // 1 bit
        public ulong Padding1;
        public ulong Padding2;
        public ulong Data0;
        public ulong Data1;
        public ulong Data2;
        public ulong Data3;

        /// <summary>
        /// Get Network data from the union
        /// </summary>
        public WINDIVERT_DATA_NETWORK Network
        {
            get
            {
                var data = new WINDIVERT_DATA_NETWORK();
                unsafe
                {
                    fixed (ulong* p = &Data0)
                    {
                        data.IfIdx = ((uint*)p)[0];
                        data.SubIfIdx = ((uint*)p)[1];
                    }
                }
                return data;
            }
        }

        public bool IsOutbound => Outbound != 0;
        public bool IsLoopback => Loopback != 0;
        public bool IsIPv6 => IPv6 != 0;
        public bool HasIPChecksum => IPChecksum != 0;
        public bool HasTCPChecksum => TCPChecksum != 0;
        public bool HasUDPChecksum => UDPChecksum != 0;
    }

    /// <summary>
    /// IPv4 header
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WINDIVERT_IPHDR
    {
        public byte HdrLengthAndVersion;  // 4 bits HdrLength, 4 bits Version
        public byte TOS;
        public ushort Length;
        public ushort Id;
        public ushort FragOffAndFlags;
        public byte TTL;
        public byte Protocol;
        public ushort Checksum;
        public uint SrcAddr;
        public uint DstAddr;

        public byte HeaderLength => (byte)(HdrLengthAndVersion & 0x0F);
        public byte Version => (byte)((HdrLengthAndVersion >> 4) & 0x0F);
        
        public ushort FragmentOffset => (ushort)(FragOffAndFlags & 0x1FFF);
        public bool MF => (FragOffAndFlags & 0x2000) != 0;
        public bool DF => (FragOffAndFlags & 0x4000) != 0;
    }

    /// <summary>
    /// IPv6 header
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WINDIVERT_IPV6HDR
    {
        public uint VersionTrafficFlow1;
        public byte TrafficFlow2;
        public ushort FlowLabel;
        public ushort Length;
        public byte NextHdr;
        public byte HopLimit;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] SrcAddr;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] DstAddr;

        public byte Version => (byte)((VersionTrafficFlow1 >> 28) & 0x0F);
        public byte TrafficClass => (byte)(((VersionTrafficFlow1 >> 20) & 0xFF) | ((VersionTrafficFlow1 >> 16) & 0x0F));
    }

    /// <summary>
    /// ICMP header
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WINDIVERT_ICMPHDR
    {
        public byte Type;
        public byte Code;
        public ushort Checksum;
        public uint Body;
    }

    /// <summary>
    /// ICMPv6 header
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WINDIVERT_ICMPV6HDR
    {
        public byte Type;
        public byte Code;
        public ushort Checksum;
        public uint Body;
    }

    /// <summary>
    /// TCP header
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WINDIVERT_TCPHDR
    {
        public ushort SrcPort;
        public ushort DstPort;
        public uint SeqNum;
        public uint AckNum;
        public ushort ReservedAndHdrLength;  // 4 bits Reserved1, 4 bits HdrLength
        public ushort Flags;                  // Fin, Syn, Rst, Psh, Ack, Urg, Reserved2
        public ushort Window;
        public ushort Checksum;
        public ushort UrgPtr;

        public byte HeaderLength => (byte)((ReservedAndHdrLength >> 12) & 0x0F);
        public bool Fin => (Flags & 0x0001) != 0;
        public bool Syn => (Flags & 0x0002) != 0;
        public bool Rst => (Flags & 0x0004) != 0;
        public bool Psh => (Flags & 0x0008) != 0;
        public bool Ack => (Flags & 0x0010) != 0;
        public bool Urg => (Flags & 0x0020) != 0;
    }

    /// <summary>
    /// UDP header
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WINDIVERT_UDPHDR
    {
        public ushort SrcPort;
        public ushort DstPort;
        public ushort Length;
        public ushort Checksum;
    }

    /// <summary>
    /// WinDivert checksum flags
    /// </summary>
    [Flags]
    public enum WINDIVERT_CHECKSUM_FLAGS : ulong
    {
        None = 0,
        WINDIVERT_HELPER_NO_IP_CHECKSUM = 0x0001,
        WINDIVERT_HELPER_NO_ICMP_CHECKSUM = 0x0002,
        WINDIVERT_HELPER_NO_ICMPV6_CHECKSUM = 0x0004,
        WINDIVERT_HELPER_NO_TCP_CHECKSUM = 0x0008,
        WINDIVERT_HELPER_NO_UDP_CHECKSUM = 0x0010
    }
}
