using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FireWallService.WinDivert;

namespace FireWallService
{
    public enum FirewallAction { Allow, Block, Log }
    public enum RuleDirection { Any, Inbound, Outbound }

    public class FirewallRule
    {
        public string Name { get; set; } = "";
        public FirewallAction Action { get; set; } = FirewallAction.Allow;
        public byte? Protocol { get; set; }
        public string? SourceIP { get; set; }
        public string? DestinationIP { get; set; }
        public ushort? SourcePort { get; set; }
        public ushort? DestinationPort { get; set; }
        public RuleDirection Direction { get; set; } = RuleDirection.Any;
        public bool IsEnabled { get; set; } = true;
        public string Description { get; set; } = "";

        /// <summary>Доменное имя (например, example.com). Служба резолвит его в IP-адреса.</summary>
        public string? Domain { get; set; }

        [JsonIgnore]
        public bool HasDomain => !string.IsNullOrWhiteSpace(Domain);

        // Адреса, в которые резолвится Domain (сетевой порядок байт, как SrcAddr/DstAddr).
        // Обновляется фоновым резолвером службы; читается на потоке пакетов.
        // Замена ссылки атомарна, поэтому блокировка не нужна.
        [JsonIgnore]
        private volatile HashSet<uint> _resolvedAddresses = new();

        public void SetResolvedAddresses(HashSet<uint> set) => _resolvedAddresses = set;

        public bool Matches(ParsedPacket parsed, WINDIVERT_ADDRESS addr)
        {
            if (!IsEnabled) return false;

            if (Direction == RuleDirection.Outbound && !addr.IsOutbound) return false;
            if (Direction == RuleDirection.Inbound && addr.IsOutbound) return false;

            if (Protocol.HasValue && parsed.Protocol != Protocol.Value) return false;

            if (!string.IsNullOrEmpty(SourceIP) && parsed.IsIPv4 && parsed.IpHeader.HasValue)
            {
                if (WinDivertNative.WinDivertHelperParseIPv4Address(SourceIP, out var ruleAddr))
                    if (parsed.IpHeader.Value.SrcAddr != ruleAddr) return false;
            }

            if (!string.IsNullOrEmpty(DestinationIP) && parsed.IsIPv4 && parsed.IpHeader.HasValue)
            {
                if (WinDivertNative.WinDivertHelperParseIPv4Address(DestinationIP, out var ruleAddr))
                    if (parsed.IpHeader.Value.DstAddr != ruleAddr) return false;
            }

            // Правило по домену: пакет должен быть к одному из резолвленных адресов
            // (источник ИЛИ назначение — покрывает обе стороны обмена).
            if (HasDomain)
            {
                var resolved = _resolvedAddresses; // атомарный снимок
                if (resolved.Count == 0) return false; // ещё не резолвилось / не удалось
                if (!parsed.IsIPv4 || !parsed.IpHeader.HasValue) return false;
                if (!resolved.Contains(parsed.IpHeader.Value.SrcAddr) &&
                    !resolved.Contains(parsed.IpHeader.Value.DstAddr))
                    return false;
            }

            // Packet port fields are in network byte order; rule ports are in host byte order
            if (SourcePort.HasValue)
            {
                ushort? pktSrc = parsed.TcpHeader.HasValue
                    ? BinaryPrimitives.ReverseEndianness(parsed.TcpHeader.Value.SrcPort)
                    : parsed.UdpHeader.HasValue
                    ? BinaryPrimitives.ReverseEndianness(parsed.UdpHeader.Value.SrcPort)
                    : null;
                if (!pktSrc.HasValue || pktSrc.Value != SourcePort.Value) return false;
            }

            if (DestinationPort.HasValue)
            {
                ushort? pktDst = parsed.TcpHeader.HasValue
                    ? BinaryPrimitives.ReverseEndianness(parsed.TcpHeader.Value.DstPort)
                    : parsed.UdpHeader.HasValue
                    ? BinaryPrimitives.ReverseEndianness(parsed.UdpHeader.Value.DstPort)
                    : null;
                if (!pktDst.HasValue || pktDst.Value != DestinationPort.Value) return false;
            }

            return true;
        }
    }
}
