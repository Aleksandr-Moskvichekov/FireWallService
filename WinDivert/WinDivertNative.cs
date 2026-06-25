using System;
using System.Runtime.InteropServices;

namespace FireWallService.WinDivert
{
    /// <summary>
    /// P/Invoke wrapper for WinDivert.dll
    /// </summary>
    public static class WinDivertNative
    {
        private const string WinDivertDll = "WinDivert.dll";

        /// <summary>
        /// Opens a WinDivert handle for the given filter.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern IntPtr WinDivertOpen(
            [MarshalAs(UnmanagedType.LPStr)] string filter,
            WINDIVERT_LAYER layer,
            short priority,
            WINDIVERT_FLAGS flags);

        /// <summary>
        /// Receives a single captured packet/event.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertRecv(
            IntPtr handle,
            IntPtr pPacket,
            uint packetLen,
            out uint pRecvLen,
            out WINDIVERT_ADDRESS pAddr);

        /// <summary>
        /// Receives packets with extended options (supports overlapped I/O and batching).
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertRecvEx(
            IntPtr handle,
            IntPtr pPacket,
            uint packetLen,
            out uint pRecvLen,
            ulong flags,
            out WINDIVERT_ADDRESS pAddr,
            ref uint pAddrLen,
            IntPtr lpOverlapped);

        /// <summary>
        /// Injects a packet into the network stack.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertSend(
            IntPtr handle,
            IntPtr pPacket,
            uint packetLen,
            out uint pSendLen,
            ref WINDIVERT_ADDRESS pAddr);

        /// <summary>
        /// Sends packets with extended options.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertSendEx(
            IntPtr handle,
            IntPtr pPacket,
            uint packetLen,
            out uint pSendLen,
            ulong flags,
            ref WINDIVERT_ADDRESS pAddr,
            uint addrLen,
            IntPtr lpOverlapped);

        /// <summary>
        /// Shuts down a WinDivert handle.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertShutdown(
            IntPtr handle,
            WINDIVERT_SHUTDOWN how);

        /// <summary>
        /// Closes a WinDivert handle.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertClose(IntPtr handle);

        /// <summary>
        /// Sets a WinDivert parameter.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertSetParam(
            IntPtr handle,
            WINDIVERT_PARAM param,
            ulong value);

        /// <summary>
        /// Gets a WinDivert parameter.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertGetParam(
            IntPtr handle,
            WINDIVERT_PARAM param,
            out ulong pValue);

        // Helper API functions

        /// <summary>
        /// Parses a raw packet into headers and payloads.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertHelperParsePacket(
            IntPtr pPacket,
            uint packetLen,
            out IntPtr ppIpHdr,
            out IntPtr ppIpv6Hdr,
            out byte pProtocol,
            out IntPtr ppIcmpHdr,
            out IntPtr ppIcmpv6Hdr,
            out IntPtr ppTcpHdr,
            out IntPtr ppUdpHdr,
            out IntPtr ppData,
            out uint pDataLen,
            out IntPtr ppNext,
            out uint pNextLen);

        /// <summary>
        /// (Re)calculates checksums for a packet.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertHelperCalcChecksums(
            IntPtr pPacket,
            uint packetLen,
            ref WINDIVERT_ADDRESS pAddr,
            WINDIVERT_CHECKSUM_FLAGS flags);

        /// <summary>
        /// Compiles a filter string into object representation.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertHelperCompileFilter(
            [MarshalAs(UnmanagedType.LPStr)] string filter,
            WINDIVERT_LAYER layer,
            IntPtr pObject,
            uint objLen,
            out IntPtr errorStr,
            out uint errorPos);

        /// <summary>
        /// Evaluates a filter against a packet.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertHelperEvalFilter(
            [MarshalAs(UnmanagedType.LPStr)] string filter,
            IntPtr pPacket,
            uint packetLen,
            ref WINDIVERT_ADDRESS pAddr);

        /// <summary>
        /// Formats a filter object into a human-readable string.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertHelperFormatFilter(
            [MarshalAs(UnmanagedType.LPStr)] string filter,
            WINDIVERT_LAYER layer,
            IntPtr pBuffer,
            uint bufLen);

        /// <summary>
        /// Decrements the TTL/HopLimit field of a packet.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertHelperDecrementTTL(
            IntPtr pPacket,
            uint packetLen);

        /// <summary>
        /// Parses an IPv4 address string.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertHelperParseIPv4Address(
            [MarshalAs(UnmanagedType.LPStr)] string addrStr,
            out uint pAddr);

        /// <summary>
        /// Parses an IPv6 address string.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertHelperParseIPv6Address(
            [MarshalAs(UnmanagedType.LPStr)] string addrStr,
            IntPtr pAddr);

        /// <summary>
        /// Formats an IPv4 address.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertHelperFormatIPv4Address(
            uint addr,
            IntPtr pBuffer,
            uint bufLen);

        /// <summary>
        /// Formats an IPv6 address.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinDivertHelperFormatIPv6Address(
            IntPtr pAddr,
            IntPtr pBuffer,
            uint bufLen);

        /// <summary>
        /// Hashes a packet.
        /// </summary>
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern ulong WinDivertHelperHashPacket(
            IntPtr pPacket,
            uint packetLen,
            ulong seed);

        // Byte order conversion helpers
        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort WinDivertHelperNtohs(ushort x);

        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint WinDivertHelperNtohl(uint x);

        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong WinDivertHelperNtohll(ulong x);

        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort WinDivertHelperHtons(ushort x);

        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint WinDivertHelperHtonl(uint x);

        [DllImport(WinDivertDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong WinDivertHelperHtonll(ulong x);
    }
}
