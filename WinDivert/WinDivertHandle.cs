using System;
using System.Runtime.InteropServices;

namespace FireWallService.WinDivert
{
    /// <summary>
    /// High-level wrapper for WinDivert handle management
    /// </summary>
    public class WinDivertHandle : IDisposable
    {
        private IntPtr _handle = IntPtr.Zero;
        private bool _disposed = false;

        public bool IsOpen => _handle != IntPtr.Zero && _handle != new IntPtr(-1);

        /// <summary>
        /// Opens a WinDivert handle with the specified filter
        /// </summary>
        public bool Open(string filter, WINDIVERT_LAYER layer = WINDIVERT_LAYER.WINDIVERT_LAYER_NETWORK, 
                        short priority = 0, WINDIVERT_FLAGS flags = WINDIVERT_FLAGS.None)
        {
            Close();

            _handle = WinDivertNative.WinDivertOpen(filter, layer, priority, flags);

            if (_handle == IntPtr.Zero || _handle == new IntPtr(-1))
            {
                int error = Marshal.GetLastWin32Error();
                throw new WinDivertException(error, $"Failed to open WinDivert handle. Error code: {error}");
            }

            return true;
        }

        /// <summary>
        /// Receives a single packet
        /// </summary>
        public bool Recv(byte[] packet, out uint recvLen, out WINDIVERT_ADDRESS addr, uint packetLen = 0)
        {
            if (!IsOpen)
                throw new InvalidOperationException("WinDivert handle is not open");

            uint len = packetLen > 0 ? packetLen : (uint)packet.Length;

            unsafe
            {
                fixed (byte* pPacket = packet)
                {
                    bool result = WinDivertNative.WinDivertRecv(
                        _handle,
                        (IntPtr)pPacket,
                        len,
                        out recvLen,
                        out addr);

                    return result;
                }
            }
        }

        /// <summary>
        /// Sends/injects a packet
        /// </summary>
        public bool Send(byte[] packet, ref WINDIVERT_ADDRESS addr, out uint sendLen, uint packetLen = 0)
        {
            if (!IsOpen)
                throw new InvalidOperationException("WinDivert handle is not open");

            uint len = packetLen > 0 ? packetLen : (uint)packet.Length;

            unsafe
            {
                fixed (byte* pPacket = packet)
                {
                    bool result = WinDivertNative.WinDivertSend(
                        _handle,
                        (IntPtr)pPacket,
                        len,
                        out sendLen,
                        ref addr);

                    return result;
                }
            }
        }

        /// <summary>
        /// Shuts down the handle (recv, send, or both)
        /// </summary>
        public bool Shutdown(WINDIVERT_SHUTDOWN how)
        {
            if (!IsOpen)
                return false;

            return WinDivertNative.WinDivertShutdown(_handle, how);
        }

        /// <summary>
        /// Sets a WinDivert parameter
        /// </summary>
        public bool SetParam(WINDIVERT_PARAM param, ulong value)
        {
            if (!IsOpen)
                return false;

            return WinDivertNative.WinDivertSetParam(_handle, param, value);
        }

        /// <summary>
        /// Gets a WinDivert parameter
        /// </summary>
        public bool GetParam(WINDIVERT_PARAM param, out ulong value)
        {
            if (!IsOpen)
            {
                value = 0;
                return false;
            }

            return WinDivertNative.WinDivertGetParam(_handle, param, out value);
        }

        /// <summary>
        /// Calculates checksums for a packet
        /// </summary>
        public static bool CalcChecksums(byte[] packet, ref WINDIVERT_ADDRESS addr, 
                                         WINDIVERT_CHECKSUM_FLAGS flags = WINDIVERT_CHECKSUM_FLAGS.None)
        {
            unsafe
            {
                fixed (byte* pPacket = packet)
                {
                    return WinDivertNative.WinDivertHelperCalcChecksums(
                        (IntPtr)pPacket,
                        (uint)packet.Length,
                        ref addr,
                        flags);
                }
            }
        }

        /// <summary>
        /// Parses a packet into headers
        /// </summary>
        public static bool ParsePacket(byte[] packet, out ParsedPacket parsed)
        {
            parsed = default;

            unsafe
            {
                fixed (byte* pPacket = packet)
                {
                    bool result = WinDivertNative.WinDivertHelperParsePacket(
                        (IntPtr)pPacket,
                        (uint)packet.Length,
                        out IntPtr ppIpHdr,
                        out IntPtr ppIpv6Hdr,
                        out byte protocol,
                        out IntPtr ppIcmpHdr,
                        out IntPtr ppIcmpv6Hdr,
                        out IntPtr ppTcpHdr,
                        out IntPtr ppUdpHdr,
                        out IntPtr ppData,
                        out uint pDataLen,
                        out IntPtr ppNext,
                        out uint pNextLen);

                    if (!result)
                        return false;

                    parsed.IpHeader = ppIpHdr != IntPtr.Zero ? Marshal.PtrToStructure<WINDIVERT_IPHDR>(ppIpHdr) : (WINDIVERT_IPHDR?)null;
                    parsed.Ipv6Header = ppIpv6Hdr != IntPtr.Zero ? Marshal.PtrToStructure<WINDIVERT_IPV6HDR>(ppIpv6Hdr) : (WINDIVERT_IPV6HDR?)null;
                    parsed.TcpHeader = ppTcpHdr != IntPtr.Zero ? Marshal.PtrToStructure<WINDIVERT_TCPHDR>(ppTcpHdr) : (WINDIVERT_TCPHDR?)null;
                    parsed.UdpHeader = ppUdpHdr != IntPtr.Zero ? Marshal.PtrToStructure<WINDIVERT_UDPHDR>(ppUdpHdr) : (WINDIVERT_UDPHDR?)null;
                    parsed.IcmpHeader = ppIcmpHdr != IntPtr.Zero ? Marshal.PtrToStructure<WINDIVERT_ICMPHDR>(ppIcmpHdr) : (WINDIVERT_ICMPHDR?)null;
                    parsed.Icmpv6Header = ppIcmpv6Hdr != IntPtr.Zero ? Marshal.PtrToStructure<WINDIVERT_ICMPV6HDR>(ppIcmpv6Hdr) : (WINDIVERT_ICMPV6HDR?)null;
                    parsed.Protocol = protocol;
                    parsed.DataLength = pDataLen;

                    return true;
                }
            }
        }

        /// <summary>
        /// Closes the WinDivert handle
        /// </summary>
        public void Close()
        {
            if (IsOpen)
            {
                WinDivertNative.WinDivertClose(_handle);
                _handle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Close();
                _disposed = true;
            }
        }

        ~WinDivertHandle()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Parsed packet structure
    /// </summary>
    public struct ParsedPacket
    {
        public WINDIVERT_IPHDR? IpHeader;
        public WINDIVERT_IPV6HDR? Ipv6Header;
        public WINDIVERT_TCPHDR? TcpHeader;
        public WINDIVERT_UDPHDR? UdpHeader;
        public WINDIVERT_ICMPHDR? IcmpHeader;
        public WINDIVERT_ICMPV6HDR? Icmpv6Header;
        public byte Protocol;
        public uint DataLength;

        public bool IsIPv4 => IpHeader.HasValue;
        public bool IsIPv6 => Ipv6Header.HasValue;
        public bool IsTCP => TcpHeader.HasValue;
        public bool IsUDP => UdpHeader.HasValue;
        public bool IsICMP => IcmpHeader.HasValue;
        public bool IsICMPv6 => Icmpv6Header.HasValue;
    }

    /// <summary>
    /// WinDivert exception
    /// </summary>
    public class WinDivertException : Exception
    {
        public int ErrorCode { get; }

        public WinDivertException(int errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }

        public WinDivertException(string message) : base(message)
        {
            ErrorCode = -1;
        }
    }
}
