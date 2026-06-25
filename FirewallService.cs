using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FireWallService.WinDivert;
using FireWallService.IPC;
using FireWallService.PacketCapture;

namespace FireWallService
{
    public class FirewallService : BackgroundService
    {
        private readonly ILogger<FirewallService> _logger;
        private readonly IConfiguration _configuration;
        private WinDivertHandle? _winDivert;
        private readonly byte[] _packetBuffer = new byte[0xFFFF];

        private readonly List<FirewallRule> _rules = new();
        private readonly object _rulesLock = new();

        private readonly PacketLogger _packetLogger;
        private readonly ConnectionTracker _connectionTracker = new();
        private PcapWriter? _pcapWriter;
        private readonly NamedPipeServer _pipeServer;
        private readonly object _pcapLock = new();

        private readonly bool _enablePacketCapture;
        private readonly bool _enablePcapRecording;
        private readonly string _pcapFilePath;
        private readonly string _rulesFilePath;
        private readonly string _policyFilePath;

        // Политика по умолчанию: действие для трафика, не попавшего ни под одно правило.
        // По ТЗ — Block (Deny All). Стартовое значение из "Firewall:DefaultAction",
        // изменяется на лету из GUI. Читается/пишется под _rulesLock.
        private FirewallAction _defaultAction;

        private static readonly JsonSerializerOptions _persistJson = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public FirewallService(
            ILogger<FirewallService> logger,
            IConfiguration configuration,
            ILogger<PacketLogger> packetLoggerLogger,
            ILogger<NamedPipeServer> pipeLogger)
        {
            _logger = logger;
            _configuration = configuration;
            _rulesFilePath = _configuration["Firewall:RulesFile"] ?? "firewall_rules.json";

            LoadFirewallRules();

            var maxStored = int.TryParse(_configuration["Firewall:Capture:MaxStoredPackets"], out var ms) ? ms : 10000;
            var logToFile = bool.TryParse(_configuration["Firewall:PacketLog:Enabled"], out var lf) && lf;
            var logDir = _configuration["Firewall:PacketLog:Directory"] ?? "logs";
            _packetLogger = new PacketLogger(packetLoggerLogger, maxStored, logToFile, logDir);
            _pipeServer = new NamedPipeServer(pipeLogger);

            _enablePacketCapture = bool.TryParse(_configuration["Firewall:Capture:Enabled"], out var ce) && ce;
            _enablePcapRecording = bool.TryParse(_configuration["Firewall:Capture:RecordToFile"], out var rec) && rec;
            _pcapFilePath = _configuration["Firewall:Capture:OutputFile"] ?? "captures/firewall_capture.pcap";

            _defaultAction = Enum.TryParse<FirewallAction>(_configuration["Firewall:DefaultAction"], true, out var da)
                ? da
                : FirewallAction.Block; // по умолчанию — запрет (Deny All)

            // Сохранённая ранее политика имеет приоритет над appsettings
            _policyFilePath = _configuration["Firewall:PolicyFile"] ?? "firewall_policy.txt";
            LoadPolicy();

            _logger.LogInformation("Firewall initialized with {Count} rules. Default policy: {Policy}",
                _rules.Count, _defaultAction);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _pipeServer.OnRequestReceived += HandleIpcRequest;
                await _pipeServer.StartAsync(stoppingToken);

                try
                {
                    InitializeWinDivert();
                    if (_enablePcapRecording) InitializePcapWriter();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WinDivert initialization failed — running in IPC-only mode (no packet filtering)");
                }

                _ = Task.Run(() => CliLoop(stoppingToken), stoppingToken);
                _ = Task.Run(() => DomainRefreshLoop(stoppingToken), stoppingToken);
                _logger.LogInformation("Firewall started. Default policy: DENY ALL. Type 'help' for commands.");

                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_winDivert == null)
                    {
                        await Task.Delay(500, stoppingToken);
                        continue;
                    }
                    try
                    {
                        if (!_winDivert.Recv(_packetBuffer, out uint recvLen, out var addr))
                        {
                            await Task.Delay(100, stoppingToken);
                            continue;
                        }
                        await ProcessPacketAsync(addr, recvLen, stoppingToken);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex) { _logger.LogError(ex, "Packet processing error"); }
                }
            }
            catch (Exception ex) { _logger.LogCritical(ex, "Fatal error in firewall service"); throw; }
            finally { Cleanup(); }
        }

        #region CLI

        private void CliLoop(CancellationToken ct)
        {
            Thread.Sleep(2000);
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("  FireWallService CLI — type 'help' for commands");
            Console.WriteLine("═══════════════════════════════════════\n");
            Console.Write("firewall> ");

            while (!ct.IsCancellationRequested)
            {
                var line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) { Console.Write("firewall> "); continue; }

                var parts = line.Trim().Split(' ', 2);
                var cmd = parts[0].ToLowerInvariant();
                var args = parts.Length > 1 ? parts[1] : "";

                try { ProcessCommand(cmd, args); }
                catch (Exception ex) { Console.WriteLine($"  Error: {ex.Message}"); }

                if (cmd is "quit" or "exit" or "q") break;
                Console.Write("\nfirewall> ");
            }
            Environment.Exit(0);
        }

        private void ProcessCommand(string cmd, string args)
        {
            switch (cmd)
            {
                case "help" or "h" or "?": ShowHelp(); break;
                case "status": ShowStatus(); break;
                case "stats" or "stat": ShowStats(); break;
                case "rules": ShowRules(); break;
                case "packets" or "pkts":
                    ShowPackets(int.TryParse(args, out var n) ? n : 20); break;
                case "top": ShowTopIPs(); break;
                case "add": AddRuleCli(args); break;
                case "del" or "remove" or "rm":
                    if (int.TryParse(args, out var idx)) RemoveRuleCli(idx);
                    else Console.WriteLine("  Usage: del <index>"); break;
                case "clear":
                    _packetLogger.ClearPacketHistory();
                    Console.WriteLine("  Packet history cleared"); break;
                case "reset-stats":
                    _packetLogger.ResetStatistics();
                    Console.WriteLine("  Statistics reset"); break;
                case "quit" or "exit" or "q":
                    Console.WriteLine("  Shutting down..."); break;
                default: Console.WriteLine($"  Unknown command: {cmd}. Type 'help'."); break;
            }
        }

        private void ShowHelp()
        {
            Console.WriteLine(@"
  status           — Service status
  stats            — Packet statistics
  top              — Top blocked IPs
  rules            — All firewall rules
  packets [N]      — Last N packets (default 20)
  add <rule>       — Add dynamic rule
  del <index>      — Remove rule by index
  clear            — Clear packet history
  reset-stats      — Reset statistics
  help             — This help
  quit/exit        — Stop service

  Add rule format:  add <action> <dir> [proto] [port]
    action: allow | block | log
    dir:    in | out | any
    proto:  tcp | udp | icmp | <number>
    port:   destination port number

  Examples:
    add block out tcp 80
    add log in udp 53
    add block any icmp");
        }

        private void ShowStatus()
        {
            var s = _packetLogger.GetStatistics();
            int ruleCount;
            lock (_rulesLock) ruleCount = _rules.Count;
            var lines = new[]
            {
                $"Service:     {(_winDivert?.IsOpen == true ? "Running" : "Stopped")}",
                $"Rules:       {ruleCount}",
                $"Policy:      DENY ALL (default)",
                $"Uptime:      {s.Uptime:hh\\:mm\\:ss}",
                $"Packets/sec: {s.PacketsPerSecond:F1}"
            };
            var max = lines.Max(l => l.Length) + 2;
            Console.WriteLine("  ┌" + new string('─', max) + "┐");
            foreach (var l in lines) Console.WriteLine($"  │ {l.PadRight(max - 1)}│");
            Console.WriteLine("  └" + new string('─', max) + "┘");
        }

        private void ShowStats()
        {
            var s = _packetLogger.GetStatistics();
            Console.WriteLine($@"
  Total processed: {s.TotalPacketsProcessed}
  Allowed:         {s.TotalPacketsAllowed}
  Blocked:         {s.TotalPacketsBlocked}
  Logged:          {s.TotalPacketsLogged}
  Data:            {FormatBytes(s.TotalBytesProcessed)}");
        }

        private void ShowRules()
        {
            List<FirewallRule> rules;
            lock (_rulesLock) rules = _rules.ToList();
            if (rules.Count == 0) { Console.WriteLine("  No rules (DENY ALL is active)"); return; }
            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                var state = r.IsEnabled ? "" : " [disabled]";
                Console.WriteLine($"  [{i}] {r.Action,-5} {r.Direction,-8} {r.Protocol?.ToString() ?? "any",-5} port:{r.DestinationPort?.ToString() ?? "any",-6} {r.Name}{state}");
            }
        }

        private void ShowPackets(int count)
        {
            var pkts = _packetLogger.GetRecentPackets(count);
            if (pkts.Count == 0) { Console.WriteLine("  No packets yet"); return; }

            Console.WriteLine($"  {"Time",-8} {"Act",-5} {"Dir",-3} {"Proto",-5} {"Source",-22} {"Dest",-22} {"Size",6}");
            Console.WriteLine("  " + new string('-', 75));
            foreach (var p in pkts)
            {
                var act = p.Action switch { FirewallAction.Allow => "ALLOW", FirewallAction.Block => "BLOCK", _ => "LOG  " };
                var dir = p.IsOutbound ? "OUT" : "IN ";
                var src = $"{p.SourceIP}:{p.SourcePort}";
                var dst = $"{p.DestinationIP}:{p.DestinationPort}";
                Console.WriteLine($"  {p.Timestamp.ToString("HH:mm:ss"),-8} {act,-5} {dir,-3} {p.Protocol,-5} {src,-22} {dst,-22} {p.PacketLength,6}");
            }
        }

        private void ShowTopIPs()
        {
            var s = _packetLogger.GetStatistics();
            if (s.TopBlockedIPs.Count == 0) { Console.WriteLine("  No blocked IPs yet"); return; }
            Console.WriteLine("  Top blocked IPs:");
            foreach (var (ip, cnt) in s.TopBlockedIPs.Take(10))
                Console.WriteLine($"    {ip,-20} {cnt,6} packets");
        }

        private void AddRuleCli(string args)
        {
            var p = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 2) { Console.WriteLine("  Usage: add <allow|block|log> <in|out|any> [tcp|udp|icmp|proto] [port]"); return; }

            var act = p[0] switch { "block" or "drop" => FirewallAction.Block, "log" => FirewallAction.Log, _ => FirewallAction.Allow };
            var dir = p[1] switch { "in" or "inbound" => RuleDirection.Inbound, "out" or "outbound" => RuleDirection.Outbound, _ => RuleDirection.Any };
            byte? proto = p.Length > 2 ? p[2] switch { "tcp" => (byte)6, "udp" => (byte)17, "icmp" => (byte)1, _ => byte.TryParse(p[2], out var v) ? v : null } : null;
            ushort? port = p.Length > 3 && ushort.TryParse(p[3], out var pt) ? pt : null;

            var rule = new FirewallRule
            {
                Name = $"CLI_{DateTime.Now:HHmmss}",
                Action = act, Protocol = proto, Direction = dir, DestinationPort = port
            };
            lock (_rulesLock) _rules.Add(rule);
            SaveRules();
            Console.WriteLine($"  Added [{_rules.Count - 1}]: {act} {dir} proto:{proto?.ToString() ?? "any"} port:{port?.ToString() ?? "any"}");
        }

        private void RemoveRuleCli(int index)
        {
            string name;
            lock (_rulesLock)
            {
                if (index < 0 || index >= _rules.Count)
                { Console.WriteLine($"  Invalid index. Valid range: 0-{_rules.Count - 1}"); return; }
                name = _rules[index].Name;
                _rules.RemoveAt(index);
            }
            SaveRules();
            Console.WriteLine($"  Removed [{index}]: {name}");
        }

        private static string FormatBytes(long b) => b switch
        {
            < 1024 => $"{b} B",
            < 1048576 => $"{b / 1024.0:F1} KB",
            < 1073741824 => $"{b / 1048576.0:F1} MB",
            _ => $"{b / 1073741824.0:F2} GB"
        };

        #endregion

        #region WinDivert & Packet Processing

        private void InitializeWinDivert()
        {
            _winDivert = new WinDivertHandle();
            string filter = _configuration["Firewall:Filter"] ?? "true";
            short priority = short.TryParse(_configuration["Firewall:Priority"], out var p) ? p : (short)0;
            _winDivert.Open(filter, WINDIVERT_LAYER.WINDIVERT_LAYER_NETWORK, priority);

            if (uint.TryParse(_configuration["Firewall:QueueLength"], out var ql))
                _winDivert.SetParam(WINDIVERT_PARAM.WINDIVERT_PARAM_QUEUE_LENGTH, ql);
            if (uint.TryParse(_configuration["Firewall:QueueTime"], out var qt))
                _winDivert.SetParam(WINDIVERT_PARAM.WINDIVERT_PARAM_QUEUE_TIME, qt);
            if (uint.TryParse(_configuration["Firewall:QueueSize"], out var qs))
                _winDivert.SetParam(WINDIVERT_PARAM.WINDIVERT_PARAM_QUEUE_SIZE, qs);
        }

        private void InitializePcapWriter()
        {
            try
            {
                _pcapWriter = new PcapWriter(_pcapFilePath, new PcapLogger(_logger));
                _logger.LogInformation("PCAP recording to: {Path}", _pcapFilePath);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "PCAP init failed"); }
        }

        private async Task ProcessPacketAsync(WINDIVERT_ADDRESS addr, uint packetLen, CancellationToken ct)
        {
            if (!WinDivertHandle.ParsePacket(_packetBuffer, out var parsed))
            {
                Reinject(addr, packetLen);
                return;
            }

            var action = EvaluateRules(parsed, addr);
            var ruleName = GetMatchedRuleName(parsed, addr);

            // Учитываем пакет в таблице соединений (stateful inspection)
            _connectionTracker.Track(parsed, addr, packetLen, action);

            if (_enablePacketCapture || action != FirewallAction.Allow)
                _packetLogger.LogPacket(parsed, addr, packetLen, action, ruleName);

            // Write only the actual packet bytes (not the entire buffer)
            if (_enablePcapRecording)
                lock (_pcapLock) _pcapWriter?.WritePacket(_packetBuffer[..(int)packetLen], DateTime.Now);

            // Примечание: пуш-уведомления по каждому пакету намеренно убраны.
            // GUI опрашивает службу периодически (GetPackets/GetStatus каждые 2 с),
            // а на политике Deny All уведомление на каждый пакет создавало лавину,
            // которая захлёбывала IPC-канал и била по производительности.

            if (action == FirewallAction.Allow || action == FirewallAction.Log)
                Reinject(addr, packetLen);
            // Block: do not reinject — packet is dropped

            await Task.CompletedTask;
        }

        private FirewallAction EvaluateRules(ParsedPacket parsed, WINDIVERT_ADDRESS addr)
        {
            lock (_rulesLock)
            {
                foreach (var r in _rules)
                    if (r.Matches(parsed, addr)) return r.Action;
                return _defaultAction; // Политика по умолчанию (по ТЗ — Block / Deny All)
            }
        }

        private string GetMatchedRuleName(ParsedPacket parsed, WINDIVERT_ADDRESS addr)
        {
            lock (_rulesLock)
            {
                foreach (var r in _rules)
                    if (r.Matches(parsed, addr)) return r.Name;
            }
            return _defaultAction == FirewallAction.Allow ? "DEFAULT_ALLOW" : "DEFAULT_DENY";
        }

        private void Reinject(WINDIVERT_ADDRESS addr, uint len)
        {
            if (!_winDivert!.Send(_packetBuffer, ref addr, out _, len))
                _logger.LogWarning("Reinject failed, error: {Err}",
                    System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }

        private void Cleanup()
        {
            _winDivert?.Dispose();
            lock (_pcapLock) _pcapWriter?.Dispose();
            _packetLogger.Dispose();
            _logger.LogInformation("Firewall stopped. Total processed: {N}", _packetLogger.GetStatistics().TotalPacketsProcessed);
        }

        public override void Dispose() { Cleanup(); _pipeServer.Dispose(); base.Dispose(); }

        #endregion

        #region Rule Persistence

        private void LoadFirewallRules()
        {
            lock (_rulesLock)
            {
                _rules.Clear();

                if (File.Exists(_rulesFilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(_rulesFilePath);
                        var loaded = JsonSerializer.Deserialize<List<FirewallRule>>(json, _persistJson);
                        if (loaded != null)
                        {
                            _rules.AddRange(loaded);
                            _logger.LogInformation("Loaded {Count} rules from {File}", _rules.Count, _rulesFilePath);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load rules.json, falling back to appsettings");
                    }
                }

                LoadRulesFromConfig();
            }
        }

        private void LoadRulesFromConfig()
        {
            var section = _configuration.GetSection("Firewall:Rules");
            if (!section.Exists()) return;
            for (int i = 0; i < 100; i++)
            {
                var rs = section.GetSection(i.ToString());
                if (!rs.Exists()) break;
                _rules.Add(new FirewallRule
                {
                    Name = rs["Name"] ?? $"Rule_{i}",
                    Action = Enum.TryParse<FirewallAction>(rs["Action"], out var a) ? a : FirewallAction.Allow,
                    Protocol = byte.TryParse(rs["Protocol"], out var pr) ? pr : null,
                    SourceIP = rs["SourceIP"],
                    DestinationIP = rs["DestinationIP"],
                    SourcePort = ushort.TryParse(rs["SourcePort"], out var sp) ? sp : null,
                    DestinationPort = ushort.TryParse(rs["DestinationPort"], out var dp) ? dp : null,
                    Direction = rs["Direction"]?.ToLower() switch
                    {
                        "inbound" or "in" => RuleDirection.Inbound,
                        "outbound" or "out" => RuleDirection.Outbound,
                        _ => RuleDirection.Any
                    },
                    IsEnabled = !bool.TryParse(rs["IsEnabled"], out var en) || en,
                    Description = rs["Description"] ?? "",
                    Domain = NullIfEmpty(rs["Domain"])
                });
            }
            _logger.LogInformation("Loaded {Count} rules from appsettings", _rules.Count);
        }

        private void SaveRules()
        {
            try
            {
                List<FirewallRule> snapshot;
                lock (_rulesLock) snapshot = _rules.ToList();

                var json = JsonSerializer.Serialize(snapshot, _persistJson);
                var tmp = _rulesFilePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, _rulesFilePath, overwrite: true);
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to save rules to {File}", _rulesFilePath); }
        }

        /// <summary>Запустить резолв доменов в фоне (не блокируя вызывающий поток).</summary>
        private void ResolveDomainsAsync() => _ = Task.Run(ResolveDomains);

        /// <summary>
        /// Резолвит доменные имена правил в IP-адреса (DNS) и обновляет наборы у правил.
        /// Вызывается при старте, при изменении правил и периодически.
        /// </summary>
        private void ResolveDomains()
        {
            List<FirewallRule> domainRules;
            lock (_rulesLock) domainRules = _rules.Where(r => r.HasDomain).ToList();
            if (domainRules.Count == 0) return;

            var cache = new Dictionary<string, HashSet<uint>>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in domainRules)
            {
                var domain = rule.Domain!;
                if (!cache.TryGetValue(domain, out var addrs))
                {
                    addrs = new HashSet<uint>();
                    try
                    {
                        foreach (var ip in System.Net.Dns.GetHostAddresses(domain))
                            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                addrs.Add(BitConverter.ToUInt32(ip.GetAddressBytes(), 0));
                    }
                    catch (Exception ex) { _logger.LogDebug(ex, "DNS resolve failed for {Domain}", domain); }
                    cache[domain] = addrs;
                }
                rule.SetResolvedAddresses(addrs);
            }

            foreach (var kv in cache)
                _logger.LogInformation("Домен '{Domain}' → {Count} адр.", kv.Key, kv.Value.Count);
        }

        private async Task DomainRefreshLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                ResolveDomains();
                try { await Task.Delay(TimeSpan.FromSeconds(60), ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        #endregion

        #region IPC Handlers

        private Task<IpcResponse?> HandleIpcRequest(IpcRequest req) => req.RequestType switch
        {
            IpcRequestType.GetStatus => HandleGetStatusRequest(),
            IpcRequestType.GetRules => HandleGetRulesRequest(),
            IpcRequestType.GetPackets => HandleGetPacketsRequest(req),
            IpcRequestType.GetConnections => HandleGetConnectionsRequest(),
            IpcRequestType.GetStatistics => HandleGetStatisticsRequest(),
            IpcRequestType.AddRule => Task.FromResult<IpcResponse?>(AddRuleIpc(req.Data)),
            IpcRequestType.RemoveRule => Task.FromResult<IpcResponse?>(RemoveRuleIpc(req.Data)),
            IpcRequestType.UpdateRule => Task.FromResult<IpcResponse?>(UpdateRuleIpc(req.Data)),
            IpcRequestType.GetSettings => HandleGetSettingsRequest(),
            IpcRequestType.GetLogs => HandleGetLogsRequest(req),
            IpcRequestType.ClearLogs => HandleClearRequest(),
            IpcRequestType.ClearPackets => HandleClearRequest(),
            IpcRequestType.StartCapture => Task.FromResult<IpcResponse?>(new IpcResponse { Success = true, Data = "Capture flag is read-only at runtime" }),
            IpcRequestType.StopCapture => Task.FromResult<IpcResponse?>(new IpcResponse { Success = true, Data = "Capture flag is read-only at runtime" }),
            IpcRequestType.TestConnection => Task.FromResult<IpcResponse?>(new IpcResponse { Success = true, Data = "Connected" }),
            IpcRequestType.SetDefaultPolicy => Task.FromResult<IpcResponse?>(SetDefaultPolicyIpc(req.Data)),
            IpcRequestType.SetSetting => Task.FromResult<IpcResponse?>(new IpcResponse { Success = true }),
            _ => Task.FromResult<IpcResponse?>(new IpcResponse { Success = false, ErrorMessage = $"Unknown request: {req.RequestType}" })
        };

        private Task<IpcResponse?> HandleGetStatusRequest()
        {
            var stats = _packetLogger.GetStatistics();
            int total, active;
            FirewallAction policy;
            lock (_rulesLock) { total = _rules.Count; active = _rules.Count(r => r.IsEnabled); policy = _defaultAction; }
            var status = new ServiceStatus
            {
                IsRunning = _winDivert?.IsOpen ?? false,
                StartTime = stats.ServiceStartTime,
                TotalPacketsProcessed = stats.TotalPacketsProcessed,
                TotalPacketsAllowed = stats.TotalPacketsAllowed,
                TotalPacketsBlocked = stats.TotalPacketsBlocked,
                CaptureActive = _enablePacketCapture,
                ActiveRulesCount = active,
                TotalRulesCount = total,
                DefaultPolicy = policy.ToString()
            };
            return Task.FromResult<IpcResponse?>(new IpcResponse { Success = true, Data = status });
        }

        private Task<IpcResponse?> HandleGetRulesRequest()
        {
            List<RuleInfo> rules;
            lock (_rulesLock)
            {
                rules = _rules.Select((r, i) => new RuleInfo
                {
                    Index = i,
                    Name = r.Name,
                    Action = r.Action.ToString(),
                    Protocol = r.Protocol.HasValue
                        ? r.Protocol.Value switch { 6 => "TCP", 17 => "UDP", 1 => "ICMP", var v => v.ToString() }
                        : null,
                    SourceIP = r.SourceIP,
                    DestinationIP = r.DestinationIP,
                    SourcePort = r.SourcePort,
                    DestinationPort = r.DestinationPort,
                    Direction = r.Direction.ToString(),
                    Enabled = r.IsEnabled,
                    Description = r.Description,
                    Domain = r.Domain
                }).ToList();
            }
            return Task.FromResult<IpcResponse?>(new IpcResponse { Success = true, Data = rules });
        }

        private Task<IpcResponse?> HandleGetPacketsRequest(IpcRequest req)
        {
            int count = ExtractInt(req.Data, 100);
            var packets = _packetLogger.GetRecentPackets(count);
            return Task.FromResult<IpcResponse?>(new IpcResponse { Success = true, Data = packets });
        }

        private Task<IpcResponse?> HandleGetStatisticsRequest()
        {
            return Task.FromResult<IpcResponse?>(new IpcResponse { Success = true, Data = _packetLogger.GetStatistics() });
        }

        private Task<IpcResponse?> HandleGetConnectionsRequest()
        {
            return Task.FromResult<IpcResponse?>(new IpcResponse { Success = true, Data = _connectionTracker.GetConnections() });
        }

        private Task<IpcResponse?> HandleGetSettingsRequest()
        {
            var settings = new FirewallSettings
            {
                CaptureEnabled = _enablePacketCapture,
                PcapRecordingEnabled = _enablePcapRecording,
                LogFilePath = _pcapFilePath,
                MaxPacketsInMemory = 10000
            };
            return Task.FromResult<IpcResponse?>(new IpcResponse { Success = true, Data = settings });
        }

        private Task<IpcResponse?> HandleGetLogsRequest(IpcRequest req)
        {
            int count = ExtractInt(req.Data, 200);
            var packets = _packetLogger.GetRecentPackets(count);
            var logs = packets.Select(p => new LogEntry
            {
                Timestamp = p.Timestamp,
                LogLevel = p.Action == FirewallAction.Block ? "Warning" : "Information",
                Message = $"[{(p.IsOutbound ? "OUT" : "IN ")}] {p.Protocol,-5} " +
                          $"{p.SourceIP}:{p.SourcePort} -> {p.DestinationIP}:{p.DestinationPort} " +
                          $"({p.PacketLength}B) [{p.Action}] {p.RuleMatched}",
                Category = p.Protocol
            }).ToList();
            return Task.FromResult<IpcResponse?>(new IpcResponse { Success = true, Data = logs });
        }

        private Task<IpcResponse?> HandleClearRequest()
        {
            _packetLogger.ClearPackets();
            return Task.FromResult<IpcResponse?>(new IpcResponse { Success = true, Data = "Cleared" });
        }

        private IpcResponse AddRuleIpc(object? data)
        {
            try
            {
                var d = Deserialize<RuleUpdateData>(data);
                if (d == null) return Fail("Invalid rule data");
                var rule = new FirewallRule
                {
                    Name = string.IsNullOrWhiteSpace(d.Name) ? $"Rule_{DateTime.Now:HHmmss}" : d.Name,
                    Action = Enum.TryParse<FirewallAction>(d.Action, out var a) ? a : FirewallAction.Allow,
                    Protocol = ParseProtocol(d.Protocol),
                    SourceIP = NullIfEmpty(d.SourceIP),
                    DestinationIP = NullIfEmpty(d.DestinationIP),
                    SourcePort = d.SourcePort.HasValue ? (ushort)d.SourcePort.Value : null,
                    DestinationPort = d.DestinationPort.HasValue ? (ushort)d.DestinationPort.Value : null,
                    Direction = Enum.TryParse<RuleDirection>(d.Direction, out var dir) ? dir : RuleDirection.Any,
                    IsEnabled = d.Enabled,
                    Description = d.Description ?? "",
                    Domain = NullIfEmpty(d.Domain)
                };
                lock (_rulesLock) _rules.Add(rule);
                ResolveDomainsAsync();
                SaveRules();
                _logger.LogInformation("Rule added via IPC: '{Name}'", rule.Name);
                return new IpcResponse { Success = true, Data = "Rule added" };
            }
            catch (Exception ex) { _logger.LogError(ex, "AddRuleIpc failed"); return Fail(ex.Message); }
        }

        private IpcResponse RemoveRuleIpc(object? data)
        {
            try
            {
                var idx = ExtractInt(data, -1);
                lock (_rulesLock)
                {
                    if (idx < 0 || idx >= _rules.Count)
                        return Fail($"Index {idx} out of range (0-{_rules.Count - 1})");
                    var name = _rules[idx].Name;
                    _rules.RemoveAt(idx);
                    _logger.LogInformation("Rule [{Index}] '{Name}' removed via IPC", idx, name);
                }
                SaveRules();
                return new IpcResponse { Success = true, Data = "Rule removed" };
            }
            catch (Exception ex) { _logger.LogError(ex, "RemoveRuleIpc failed"); return Fail(ex.Message); }
        }

        private IpcResponse UpdateRuleIpc(object? data)
        {
            try
            {
                var d = Deserialize<RuleUpdateData>(data);
                if (d?.Index == null) return Fail("Missing index in update data");
                var idx = d.Index.Value;
                lock (_rulesLock)
                {
                    if (idx < 0 || idx >= _rules.Count)
                        return Fail($"Index {idx} out of range (0-{_rules.Count - 1})");
                    var r = _rules[idx];
                    r.Name = string.IsNullOrWhiteSpace(d.Name) ? r.Name : d.Name;
                    r.Action = Enum.TryParse<FirewallAction>(d.Action, out var a) ? a : r.Action;
                    r.Direction = Enum.TryParse<RuleDirection>(d.Direction, out var dir) ? dir : r.Direction;
                    r.Protocol = d.Protocol != null ? ParseProtocol(d.Protocol) : r.Protocol;
                    r.SourceIP = NullIfEmpty(d.SourceIP);
                    r.DestinationIP = NullIfEmpty(d.DestinationIP);
                    r.SourcePort = d.SourcePort.HasValue ? (ushort)d.SourcePort.Value : null;
                    r.DestinationPort = d.DestinationPort.HasValue ? (ushort)d.DestinationPort.Value : null;
                    r.IsEnabled = d.Enabled;
                    r.Description = d.Description ?? r.Description;
                    r.Domain = NullIfEmpty(d.Domain);
                    _logger.LogInformation("Rule [{Index}] '{Name}' updated via IPC", idx, r.Name);
                }
                ResolveDomainsAsync();
                SaveRules();
                return new IpcResponse { Success = true, Data = "Rule updated" };
            }
            catch (Exception ex) { _logger.LogError(ex, "UpdateRuleIpc failed"); return Fail(ex.Message); }
        }

        private IpcResponse SetDefaultPolicyIpc(object? data)
        {
            var s = ExtractString(data);
            if (!Enum.TryParse<FirewallAction>(s, true, out var action))
                return Fail($"Недопустимая политика: '{s}'. Ожидается Allow или Block.");

            lock (_rulesLock) _defaultAction = action;
            SavePolicy();
            _logger.LogInformation("Default policy changed via IPC: {Policy}", action);
            return new IpcResponse { Success = true, Data = action.ToString() };
        }

        private void LoadPolicy()
        {
            try
            {
                if (!File.Exists(_policyFilePath)) return;
                var text = File.ReadAllText(_policyFilePath).Trim();
                if (Enum.TryParse<FirewallAction>(text, true, out var action))
                {
                    _defaultAction = action;
                    _logger.LogInformation("Default policy restored from {File}: {Policy}", _policyFilePath, action);
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to load saved policy"); }
        }

        private void SavePolicy()
        {
            try
            {
                FirewallAction action;
                lock (_rulesLock) action = _defaultAction;
                var tmp = _policyFilePath + ".tmp";
                File.WriteAllText(tmp, action.ToString());
                File.Move(tmp, _policyFilePath, overwrite: true);
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to save policy to {File}", _policyFilePath); }
        }

        private static string? ExtractString(object? data)
        {
            if (data is string s) return s;
            if (data is JsonElement el && el.ValueKind == JsonValueKind.String) return el.GetString();
            return data?.ToString();
        }

        private static T? Deserialize<T>(object? data) where T : class
        {
            if (data is T t) return t;
            try
            {
                var json = JsonSerializer.Serialize(data);
                return JsonSerializer.Deserialize<T>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } });
            }
            catch { return null; }
        }

        private static int ExtractInt(object? data, int fallback)
        {
            if (data is int i) return i;
            if (data is JsonElement el && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
            return fallback;
        }

        private static byte? ParseProtocol(string? s) => s?.Trim().ToLower() switch
        {
            null or "" or "any" => null,
            "tcp" => 6,
            "udp" => 17,
            "icmp" => 1,
            _ => byte.TryParse(s, out var v) ? v : null
        };

        private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static IpcResponse Fail(string msg) => new() { Success = false, ErrorMessage = msg };

        #endregion
    }

    internal class PcapLogger : Microsoft.Extensions.Logging.ILogger<PcapWriter>
    {
        private readonly ILogger _l;
        public PcapLogger(ILogger l) => _l = l;
        public IDisposable? BeginScope<TState>(TState s) where TState : notnull => null;
        public bool IsEnabled(LogLevel l) => _l.IsEnabled(l);
        public void Log<TState>(LogLevel l, EventId e, TState s, Exception? x, Func<TState, Exception?, string> f) => _l.Log(l, e, s, x, f);
    }
}
