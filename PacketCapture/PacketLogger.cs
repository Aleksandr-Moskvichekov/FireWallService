using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging;
using FireWallService.IPC;
using FireWallService.WinDivert;

namespace FireWallService.PacketCapture
{
    // Forward declaration
    using FirewallAction = FireWallService.FirewallAction;
    /// <summary>
    /// Логирование пакетов в консоль и хранение в памяти для GUI
    /// </summary>
    public class PacketLogger : IDisposable
    {
        private readonly ILogger<PacketLogger> _logger;
        private readonly ConcurrentQueue<PacketInfo> _recentPackets = new();
        private readonly int _maxStoredPackets;
        private int _totalProcessed;
        private int _totalAllowed;
        private int _totalBlocked;
        private int _totalLogged;
        private long _totalBytes;

        // Топ заблокированных IP (поток-безопасный)
        private readonly ConcurrentDictionary<string, int> _blockedIPs = new();
        private readonly ConcurrentDictionary<string, int> _protocolCounts = new();

        // Файловое логирование пакетов (чтобы старые пакеты не терялись при вытеснении из памяти)
        private readonly bool _logToFile;
        private readonly string? _logFilePath;
        private StreamWriter? _fileWriter;
        private readonly object _fileLock = new();
        private int _sinceFlush;

        public PacketLogger(ILogger<PacketLogger> logger, int maxStoredPackets = 10000,
            bool logToFile = false, string? logDirectory = null)
        {
            _logger = logger;
            _maxStoredPackets = maxStoredPackets;
            _logToFile = logToFile;

            if (_logToFile)
            {
                try
                {
                    var dir = string.IsNullOrWhiteSpace(logDirectory) ? "logs" : logDirectory;
                    Directory.CreateDirectory(dir);
                    _logFilePath = Path.Combine(dir, $"packets_{DateTime.Now:yyyyMMdd}.log");
                    _fileWriter = new StreamWriter(_logFilePath, append: true) { AutoFlush = false };
                    _fileWriter.WriteLine($"# === Сессия запущена {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                    _fileWriter.WriteLine("# Время\tДействие\tНаправл.\tПротокол\tИсточник\tНазначение\tБайт\tПравило");
                    _fileWriter.Flush();
                    _logger.LogInformation("Packet file logging enabled: {Path}", _logFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось открыть файл лога пакетов — файловое логирование отключено");
                    _logToFile = false;
                }
            }
        }

        /// <summary>
        /// Логировать пакет (консоль + хранение)
        /// </summary>
        public void LogPacket(
            ParsedPacket parsed,
            WINDIVERT_ADDRESS addr,
            uint packetLen,
            FirewallAction action,
            string ruleMatched = "")
        {
            var packetInfo = CreatePacketInfo(parsed, addr, packetLen, action, ruleMatched);

            // Сохраняем в очередь
            _recentPackets.Enqueue(packetInfo);
            TrimPackets();

            // Обновляем статистику
            Interlocked.Increment(ref _totalProcessed);
            Interlocked.Add(ref _totalBytes, packetLen);

            switch (action)
            {
                case FirewallAction.Allow:
                    Interlocked.Increment(ref _totalAllowed);
                    break;
                case FirewallAction.Block:
                    Interlocked.Increment(ref _totalBlocked);
                    _blockedIPs.AddOrUpdate(packetInfo.SourceIP, 1, (_, v) => v + 1);
                    break;
                case FirewallAction.Log:
                    Interlocked.Increment(ref _totalLogged);
                    break;
            }

            // Обновляем протоколы
            _protocolCounts.AddOrUpdate(packetInfo.Protocol, 1, (_, v) => v + 1);

            // Запись в файл (если включено)
            if (_logToFile) WriteToFile(packetInfo, action);

            // Вывод в консоль
            LogToConsole(packetInfo, action);
        }

        /// <summary>
        /// Дописать пакет в файл-лог (буферизованно, с периодическим сбросом на диск)
        /// </summary>
        private void WriteToFile(PacketInfo p, FirewallAction action)
        {
            lock (_fileLock)
            {
                if (_fileWriter == null) return;
                try
                {
                    var dir = p.IsOutbound ? "OUT" : "IN";
                    _fileWriter.WriteLine(
                        $"{p.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t{action}\t{dir}\t{p.Protocol}\t" +
                        $"{p.SourceIP}:{p.SourcePort}\t{p.DestinationIP}:{p.DestinationPort}\t" +
                        $"{p.PacketLength}\t{p.RuleMatched}");

                    // Сбрасываем на диск пачками, чтобы не терять производительность
                    if (++_sinceFlush >= 50)
                    {
                        _fileWriter.Flush();
                        _sinceFlush = 0;
                    }
                }
                catch { /* запись лога не должна ронять обработку пакетов */ }
            }
        }

        public void Dispose()
        {
            lock (_fileLock)
            {
                try { _fileWriter?.Flush(); _fileWriter?.Dispose(); }
                catch { }
                _fileWriter = null;
            }
        }

        /// <summary>
        /// Создать PacketInfo из данных пакета
        /// </summary>
        private PacketInfo CreatePacketInfo(
            ParsedPacket parsed,
            WINDIVERT_ADDRESS addr,
            uint packetLen,
            FirewallAction action,
            string ruleMatched)
        {
            var protocolName = GetProtocolName(parsed.Protocol);
            var srcIP = "";
            var dstIP = "";
            var srcPort = 0;
            var dstPort = 0;

            if (parsed.IsIPv4 && parsed.IpHeader.HasValue)
            {
                srcIP = FormatIPv4(parsed.IpHeader.Value.SrcAddr);
                dstIP = FormatIPv4(parsed.IpHeader.Value.DstAddr);
            }
            else if (parsed.IsIPv6 && parsed.Ipv6Header.HasValue)
            {
                srcIP = FormatIPv6(parsed.Ipv6Header.Value.SrcAddr);
                dstIP = FormatIPv6(parsed.Ipv6Header.Value.DstAddr);
            }

            // Порты в заголовке — в сетевом порядке байт; приводим к хостовому для отображения
            if (parsed.IsTCP && parsed.TcpHeader.HasValue)
            {
                srcPort = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(parsed.TcpHeader.Value.SrcPort);
                dstPort = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(parsed.TcpHeader.Value.DstPort);
            }
            else if (parsed.IsUDP && parsed.UdpHeader.HasValue)
            {
                srcPort = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(parsed.UdpHeader.Value.SrcPort);
                dstPort = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(parsed.UdpHeader.Value.DstPort);
            }

            return new PacketInfo
            {
                Timestamp = DateTime.Now,
                Protocol = protocolName,
                SourceIP = srcIP,
                DestinationIP = dstIP,
                SourcePort = srcPort,
                DestinationPort = dstPort,
                PacketLength = (int)packetLen,
                IsOutbound = addr.IsOutbound,
                IsLoopback = addr.IsLoopback,
                Action = action,
                RuleMatched = ruleMatched
            };
        }

        /// <summary>
        /// Вывод пакета в консоль (цветное логирование)
        /// </summary>
        private void LogToConsole(PacketInfo packet, FirewallAction action)
        {
            var direction = packet.IsOutbound ? "OUT" : "IN ";
            var actionStr = action switch
            {
                FirewallAction.Allow => "ALLOW",
                FirewallAction.Block => "BLOCK",
                FirewallAction.Log => " LOG ",
                _ => "?????"
            };

            var portInfo = "";
            if (packet.SourcePort > 0 || packet.DestinationPort > 0)
            {
                portInfo = $":{packet.SourcePort} -> {packet.DestinationIP}:{packet.DestinationPort}";
            }
            else
            {
                portInfo = $" -> {packet.DestinationIP}";
            }

            var logMsg = $"[{actionStr}] {direction} {packet.Protocol} {packet.SourceIP}{portInfo} ({packet.PacketLength}B)";

            if (!string.IsNullOrEmpty(packet.RuleMatched))
            {
                logMsg += $" [Rule: {packet.RuleMatched}]";
            }

            switch (action)
            {
                case FirewallAction.Block:
                    _logger.LogWarning(logMsg);
                    break;
                case FirewallAction.Log:
                    _logger.LogInformation(logMsg);
                    break;
                default:
                    _logger.LogDebug(logMsg);
                    break;
            }
        }

        /// <summary>
        /// Получить последние N пакетов
        /// </summary>
        public List<PacketInfo> GetRecentPackets(int count = 100)
        {
            return _recentPackets.TakeLast(count).ToList();
        }

        /// <summary>
        /// Получить всю статистику
        /// </summary>
        public FirewallStatistics GetStatistics()
        {
            return new FirewallStatistics
            {
                TotalPacketsProcessed = _totalProcessed,
                TotalPacketsAllowed = _totalAllowed,
                TotalPacketsBlocked = _totalBlocked,
                TotalPacketsLogged = _totalLogged,
                TotalBytesProcessed = _totalBytes,
                ServiceStartTime = _serviceStartTime,
                TopBlockedIPs = _blockedIPs.OrderByDescending(x => x.Value).Take(10).ToDictionary(x => x.Key, x => (long)x.Value),
                TopProtocols = _protocolCounts.OrderByDescending(x => x.Value).Take(10).ToDictionary(x => x.Key, x => (long)x.Value)
            };
        }

        /// <summary>
        /// Сбросить статистику
        /// </summary>
        public void ResetStatistics()
        {
            _totalProcessed = 0;
            _totalAllowed = 0;
            _totalBlocked = 0;
            _totalLogged = 0;
            _totalBytes = 0;
            _blockedIPs.Clear();
            _protocolCounts.Clear();
        }

        /// <summary>
        /// Очистить очередь пакетов
        /// </summary>
        public void ClearPacketHistory()
        {
            while (_recentPackets.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Очистить пакеты (алиас для ClearPacketHistory)
        /// </summary>
        public void ClearPackets()
        {
            ClearPacketHistory();
        }

        /// <summary>
        /// Удалить старые пакеты из очереди
        /// </summary>
        private void TrimPackets()
        {
            while (_recentPackets.Count > _maxStoredPackets)
            {
                _recentPackets.TryDequeue(out _);
            }
        }

        private string GetProtocolName(byte protocol)
        {
            return protocol switch
            {
                1 => "ICMP",
                6 => "TCP",
                17 => "UDP",
                41 => "IPv6",
                50 => "ESP",
                51 => "AH",
                58 => "ICMPv6",
                89 => "OSPF",
                132 => "SCTP",
                _ => protocol.ToString()
            };
        }

        private string FormatIPv4(uint addr)
        {
            // SrcAddr/DstAddr приходят в сетевом порядке байт. На little-endian машине
            // new IPAddress(uint) трактует байты в памяти как октеты напрямую — это и есть
            // правильный адрес. Дополнительный реверс ПЕРЕВОРАЧИВАЛ октеты (баг).
            return new IPAddress(addr).ToString();
        }

        private string FormatIPv6(byte[] addrBytes)
        {
            try
            {
                var addr = new IPAddress(addrBytes);
                return addr.ToString();
            }
            catch
            {
                return "IPv6";
            }
        }

        private readonly DateTime _serviceStartTime = DateTime.Now;
    }
}
