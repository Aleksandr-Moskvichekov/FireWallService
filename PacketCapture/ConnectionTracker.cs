using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FireWallService.IPC;
using FireWallService.WinDivert;

namespace FireWallService.PacketCapture
{
    using FirewallAction = FireWallService.FirewallAction;

    /// <summary>
    /// Отслеживание сетевых соединений (stateful): агрегирует пакеты в сессии по
    /// 5-кортежу (протокол + локальный/удалённый адрес и порт). Удалённый IP по
    /// возможности резолвится в доменное имя (обратный DNS, асинхронно, с кэшем).
    /// </summary>
    public class ConnectionTracker
    {
        private class Session
        {
            public string Protocol = "";
            public string LocalIP = "";
            public int LocalPort;
            public string RemoteIP = "";
            public int RemotePort;
            public bool Outbound;
            public long BytesSent;
            public long BytesReceived;
            public long PacketCount;
            public DateTime FirstSeen;
            public DateTime LastSeen;
            public string State = "Новое";
            public FirewallAction LastAction;
            public bool Closed;
        }

        private readonly ConcurrentDictionary<string, Session> _sessions = new();
        private readonly ConcurrentDictionary<string, string> _dnsCache = new();
        private readonly TimeSpan _idleTimeout = TimeSpan.FromSeconds(120);
        private readonly int _maxSessions;

        public ConnectionTracker(int maxSessions = 4096) => _maxSessions = maxSessions;

        public void Track(ParsedPacket parsed, WINDIVERT_ADDRESS addr, uint len, FirewallAction action)
        {
            if (!parsed.IsIPv4 || !parsed.IpHeader.HasValue) return;

            string srcIP = FormatIPv4(parsed.IpHeader.Value.SrcAddr);
            string dstIP = FormatIPv4(parsed.IpHeader.Value.DstAddr);
            int srcPort = 0, dstPort = 0;

            if (parsed.IsTCP && parsed.TcpHeader.HasValue)
            {
                srcPort = BinaryPrimitives.ReverseEndianness(parsed.TcpHeader.Value.SrcPort);
                dstPort = BinaryPrimitives.ReverseEndianness(parsed.TcpHeader.Value.DstPort);
            }
            else if (parsed.IsUDP && parsed.UdpHeader.HasValue)
            {
                srcPort = BinaryPrimitives.ReverseEndianness(parsed.UdpHeader.Value.SrcPort);
                dstPort = BinaryPrimitives.ReverseEndianness(parsed.UdpHeader.Value.DstPort);
            }

            bool outbound = addr.IsOutbound;
            string localIP = outbound ? srcIP : dstIP;
            int localPort = outbound ? srcPort : dstPort;
            string remoteIP = outbound ? dstIP : srcIP;
            int remotePort = outbound ? dstPort : srcPort;

            string proto = GetProtocolName(parsed.Protocol);
            string key = $"{proto}|{localIP}:{localPort}|{remoteIP}:{remotePort}";

            var now = DateTime.Now;
            var s = _sessions.GetOrAdd(key, _ =>
            {
                EnsureCapacity();
                StartReverseDns(remoteIP);
                return new Session
                {
                    Protocol = proto,
                    LocalIP = localIP,
                    LocalPort = localPort,
                    RemoteIP = remoteIP,
                    RemotePort = remotePort,
                    Outbound = outbound,
                    FirstSeen = now,
                    State = proto == "TCP" ? "Новое" : "Активно"
                };
            });

            lock (s)
            {
                s.PacketCount++;
                s.LastSeen = now;
                s.LastAction = action;
                if (outbound) s.BytesSent += len; else s.BytesReceived += len;

                if (proto == "TCP" && parsed.TcpHeader.HasValue)
                {
                    var tcp = parsed.TcpHeader.Value;
                    if (tcp.Rst || tcp.Fin) { s.State = "Закрыто"; s.Closed = true; }
                    else if (!s.Closed) s.State = (s.PacketCount > 1) ? "Установлено" : "Новое";
                }
            }
        }

        public List<ConnectionInfo> GetConnections()
        {
            var now = DateTime.Now;
            // Чистим простаивающие сессии
            foreach (var kv in _sessions)
                if (now - kv.Value.LastSeen > _idleTimeout)
                    _sessions.TryRemove(kv.Key, out _);

            var list = new List<ConnectionInfo>();
            foreach (var s in _sessions.Values)
            {
                lock (s)
                {
                    list.Add(new ConnectionInfo
                    {
                        Protocol = s.Protocol,
                        LocalIP = s.LocalIP,
                        LocalPort = s.LocalPort,
                        RemoteIP = s.RemoteIP,
                        RemotePort = s.RemotePort,
                        RemoteHost = _dnsCache.TryGetValue(s.RemoteIP, out var host) ? host : "",
                        Direction = s.Outbound ? "Исходящее" : "Входящее",
                        State = s.State,
                        BytesSent = s.BytesSent,
                        BytesReceived = s.BytesReceived,
                        PacketCount = s.PacketCount,
                        FirstSeen = s.FirstSeen,
                        LastSeen = s.LastSeen,
                        LastAction = s.LastAction.ToString()
                    });
                }
            }
            return list.OrderByDescending(c => c.LastSeen).ToList();
        }

        public void Clear() => _sessions.Clear();

        public int ActiveCount => _sessions.Count;

        private void EnsureCapacity()
        {
            if (_sessions.Count < _maxSessions) return;
            // Удаляем самые старые по LastSeen
            foreach (var kv in _sessions.OrderBy(k => k.Value.LastSeen).Take(_sessions.Count - _maxSessions + 1))
                _sessions.TryRemove(kv.Key, out _);
        }

        private void StartReverseDns(string ip)
        {
            if (_dnsCache.ContainsKey(ip)) return;
            _dnsCache[ip] = ""; // помечаем как «в процессе», чтобы не запускать повторно
            _ = Task.Run(() =>
            {
                try
                {
                    var entry = Dns.GetHostEntry(ip);
                    if (!string.IsNullOrWhiteSpace(entry.HostName))
                        _dnsCache[ip] = entry.HostName;
                }
                catch { /* PTR-записи часто нет — оставляем пусто */ }
            });
        }

        private static string FormatIPv4(uint addr) => new IPAddress(addr).ToString();

        private static string GetProtocolName(byte protocol) => protocol switch
        {
            1 => "ICMP",
            6 => "TCP",
            17 => "UDP",
            58 => "ICMPv6",
            _ => protocol.ToString()
        };
    }
}
