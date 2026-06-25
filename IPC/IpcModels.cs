using System;
using System.Collections.Generic;

namespace FireWallService.IPC
{
    // Forward declaration
    using FirewallAction = FireWallService.FirewallAction;
    /// <summary>
    /// Базовый класс для всех IPC сообщений
    /// </summary>
    public abstract class IpcMessage
    {
        public string Type { get; set; } = "";
        public Guid MessageId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Запрос от GUI к сервису
    /// </summary>
    public class IpcRequest : IpcMessage
    {
        public IpcRequestType RequestType { get; set; }
        public object? Data { get; set; }
    }

    /// <summary>
    /// Ответ от сервиса к GUI
    /// </summary>
    public class IpcResponse : IpcMessage
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public object? Data { get; set; }
    }

    /// <summary>
    /// Push-уведомление от сервиса к GUI (без запроса)
    /// </summary>
    public class IpcNotification : IpcMessage
    {
        public IpcNotificationType NotificationType { get; set; }
        public object? Data { get; set; }
    }

    /// <summary>
    /// Типы запросов от GUI
    /// </summary>
    public enum IpcRequestType
    {
        GetStatus,
        GetRules,
        AddRule,
        RemoveRule,
        UpdateRule,
        GetPackets,
        GetStatistics,
        StartCapture,
        StopCapture,
        SetSetting,
        GetSettings,
        GetLogs,
        ClearLogs,
        ClearPackets,
        SaveConfiguration,
        LoadConfiguration,
        TestConnection,
        SetDefaultPolicy,
        GetConnections
    }

    /// <summary>
    /// Типы уведомлений от сервиса
    /// </summary>
    public enum IpcNotificationType
    {
        PacketCaptured,
        RuleTriggered,
        ServiceStatusChanged,
        StatisticsUpdated,
        ErrorOccurred,
        LogUpdated,
        RuleAdded,
        RuleRemoved,
        RuleUpdated,
        SettingChanged,
        CaptureStarted,
        CaptureStopped
    }

    /// <summary>
    /// Информация о пакете для GUI
    /// </summary>
    public class PacketInfo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; }
        public string Protocol { get; set; } = "";
        public string SourceIP { get; set; } = "";
        public string DestinationIP { get; set; } = "";
        public int SourcePort { get; set; }
        public int DestinationPort { get; set; }
        public int PacketLength { get; set; }
        public bool IsOutbound { get; set; }
        public bool IsLoopback { get; set; }
        public FirewallAction Action { get; set; }
        public string RuleMatched { get; set; } = "";
        public int? ProcessId { get; set; }
    }

    /// <summary>
    /// Информация о правиле для GUI
    /// </summary>
    public class RuleInfo
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public string Action { get; set; } = "Allow";
        public string? Protocol { get; set; }
        public string? SourceIP { get; set; }
        public string? DestinationIP { get; set; }
        public int? SourcePort { get; set; }
        public int? DestinationPort { get; set; }
        public string Direction { get; set; } = "Any";
        public bool Enabled { get; set; } = true;
        public string Description { get; set; } = "";
        public string? Domain { get; set; }
    }

    /// <summary>
    /// Статистика фаервола
    /// </summary>
    public class FirewallStatistics
    {
        public long TotalPacketsProcessed { get; set; }
        public long TotalPacketsAllowed { get; set; }
        public long TotalPacketsBlocked { get; set; }
        public long TotalPacketsLogged { get; set; }
        public long TotalBytesProcessed { get; set; }
        public DateTime ServiceStartTime { get; set; }
        public TimeSpan Uptime => DateTime.Now - ServiceStartTime;
        public double PacketsPerSecond => Uptime.TotalSeconds > 0 ? TotalPacketsProcessed / Uptime.TotalSeconds : 0;
        public Dictionary<string, long> TopBlockedIPs { get; set; } = new();
        public Dictionary<string, long> TopProtocols { get; set; } = new();
    }

    /// <summary>
    /// Информация о подключении GUI
    /// </summary>
    public class ClientInfo
    {
        public string ClientName { get; set; } = "";
        public DateTime ConnectedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Настройки сервиса
    /// </summary>
    public class FirewallSettings
    {
        public bool CaptureEnabled { get; set; } = false;
        public bool PcapRecordingEnabled { get; set; } = false;
        public int MaxPacketsInMemory { get; set; } = 10000;
        public bool LogBlockedPackets { get; set; } = true;
        public bool LogAllowedPackets { get; set; } = false;
        public string LogFilePath { get; set; } = "firewall.log";
        public Dictionary<string, string> CustomSettings { get; set; } = new();
    }

    /// <summary>
    /// Статус сервиса
    /// </summary>
    public class ServiceStatus
    {
        public bool IsRunning { get; set; }
        public DateTime StartTime { get; set; }
        public long TotalPacketsProcessed { get; set; }
        public long TotalPacketsAllowed { get; set; }
        public long TotalPacketsBlocked { get; set; }
        public bool CaptureActive { get; set; }
        public string? LastError { get; set; }
        public int ActiveRulesCount { get; set; }
        public int TotalRulesCount { get; set; }
        public string DefaultPolicy { get; set; } = "Block";
    }

    /// <summary>
    /// Информация о сетевом соединении (сессии) для GUI
    /// </summary>
    public class ConnectionInfo
    {
        public string Protocol { get; set; } = "";
        public string LocalIP { get; set; } = "";
        public int LocalPort { get; set; }
        public string RemoteIP { get; set; } = "";
        public int RemotePort { get; set; }
        public string RemoteHost { get; set; } = "";   // обратный DNS / домен
        public string Direction { get; set; } = "";     // кто инициировал
        public string State { get; set; } = "";          // Новое / Установлено / Закрыто / Активно
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public long PacketCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public string LastAction { get; set; } = "";     // Allow / Block
    }

    /// <summary>
    /// Запись лога
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string LogLevel { get; set; } = "";
        public string Message { get; set; } = "";
        public string? Category { get; set; }
    }

    /// <summary>
    /// Параметры для добавления/обновления правила
    /// </summary>
    public class RuleUpdateData
    {
        public int? Index { get; set; }
        public string Name { get; set; } = "";
        public string Action { get; set; } = "Allow";
        public string? Protocol { get; set; }
        public string? SourceIP { get; set; }
        public string? DestinationIP { get; set; }
        public int? SourcePort { get; set; }
        public int? DestinationPort { get; set; }
        public string Direction { get; set; } = "Any";
        public bool Enabled { get; set; } = true;
        public string? Description { get; set; }
        public string? Domain { get; set; }
    }
}
