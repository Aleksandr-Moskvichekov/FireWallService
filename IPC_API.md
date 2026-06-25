# FireWallService — IPC API Documentation

## Named Pipe подключение

**Pipe name:** `FireWallServicePipe`

GUI подключается через `NamedPipeClientStream` к этому pipe для общения с сервисом.

## Протокол обмена

Каждое сообщение — JSON строка, завершённая `\n`.

### Формат запроса (GUI → Service)
```json
{
  "Type": "IpcRequest",
  "MessageId": "uuid",
  "Timestamp": "datetime",
  "RequestType": "GetStatus|GetRules|AddRule|...",
  "Data": {}
}
```

### Формат ответа (Service → GUI)
```json
{
  "Type": "IpcResponse",
  "MessageId": "uuid",
  "Timestamp": "datetime",
  "Success": true,
  "ErrorMessage": "",
  "Data": {}
}
```

### Формат уведомления (Service → GUI)
```json
NOTIFY:{"Type":"IpcNotification","NotificationType":"PacketCaptured|RuleTriggered|...","Data":{}}
```
Уведомления начинаются с префикса `NOTIFY:`

---

## Доступные команды

### GetStatus
Получить статус сервиса
```json
Request: {"RequestType": "GetStatus"}
Response: {"Success": true, "Data": {"IsRunning": true, "RulesCount": 3, "WinDivertFilter": "true"}}
```

### GetRules
Получить все правила
```json
Request: {"RequestType": "GetRules"}
Response: {"Success": true, "Data": [{"Index":0, "Name":"Block HTTP", "Action":"Block", ...}]}
```

### AddRule
Добавить новое правило
```json
Request: {
  "RequestType": "AddRule",
  "Data": {
    "Name": "Block port 443",
    "Action": "Block",
    "Protocol": "6",
    "DestinationPort": 443,
    "Direction": "outbound"
  }
}
Response: {"Success": true}
```

### RemoveRule
Удалить правило по индексу
```json
Request: {"RequestType": "RemoveRule", "Data": 0}
Response: {"Success": true}
```

### GetPackets
Получить последние пакеты
```json
Request: {"RequestType": "GetPackets", "Data": 100}
Response: {"Success": true, "Data": [{"Timestamp":"...", "Protocol":"TCP", "SourceIP":"...", ...}]}
```

### GetStatistics
Получить статистику
```json
Request: {"RequestType": "GetStatistics"}
Response: {
  "Success": true, 
  "Data": {
    "TotalPacketsProcessed": 12345,
    "TotalPacketsAllowed": 10000,
    "TotalPacketsBlocked": 2000,
    "TotalPacketsLogged": 345,
    "TotalBytesProcessed": 12345678,
    "ServiceStartTime": "2024-01-01T00:00:00",
    "Uptime": "01:30:00",
    "PacketsPerSecond": 123.45,
    "TopBlockedIPs": {"192.168.1.100": 500},
    "TopProtocols": {"TCP": 8000, "UDP": 3000}
  }
}
```

### StartCapture
Начать запись в PCAP файл
```json
Request: {"RequestType": "StartCapture"}
Response: {"Success": true}
```

### StopCapture
Остановить запись
```json
Request: {"RequestType": "StopCapture"}
Response: {"Success": true}
```

### GetSettings
Получить настройки сервиса
```json
Request: {"RequestType": "GetSettings"}
Response: {"Success": true, "Data": {"Filter":"true", "Priority":0, "CaptureEnabled":true, ...}}
```

---

## Пример подключения из C# GUI

```csharp
using System.IO.Pipes;
using System.Text.Json;
using FireWallService.IPC;

// Отправка запроса
var request = new IpcRequest { RequestType = IpcRequestType.GetStatus };
var response = await NamedPipeServer.SendRequestAsync("FireWallServicePipe", request);

if (response?.Success == true)
{
    Console.WriteLine("Service is running!");
}
```

---

## Структура проекта

```
FireWallService/
├── WinDivert/
│   ├── WinDivertStructs.cs    # Структуры WinDivert
│   ├── WinDivertNative.cs     # P/Invoke обёртки
│   └── WinDivertHandle.cs     # Высокоуровневая обёртка
├── IPC/
│   ├── IpcModels.cs           # Модели для IPC
│   └── NamedPipeServer.cs     # Named Pipe сервер
├── PacketCapture/
│   ├── PcapWriter.cs          # Запись PCAP файлов
│   └── PacketLogger.cs        # Логирование пакетов
├── FirewallService.cs         # Основной сервис
├── FirewallRule.cs            # Правила фаервола
├── Program.cs                 # Точка входа
└── appsettings.json           # Конфигурация
```

---

## Пример GUI приложения

Для создания GUI приложения можно использовать:
- **WPF** + **NamedPipeClientStream**
- **WinForms** + **NamedPipeClientStream**
- **MAUI** для кроссплатформенности
- **Electron** + Node.js named pipe client

### Базовый шаблон GUI (WPF)

```csharp
// В GUI приложении
using var pipeClient = new NamedPipeClientStream(".", "FireWallServicePipe", PipeDirection.InOut);
await pipeClient.ConnectAsync();

using var reader = new StreamReader(pipeClient);
using var writer = new StreamWriter(pipeClient) { AutoFlush = true };

// Отправка запроса
var request = new IpcRequest { RequestType = IpcRequestType.GetStatistics };
await writer.WriteLineAsync(JsonSerializer.Serialize(request));

// Чтение ответа
var responseJson = await reader.ReadLineAsync();
var response = JsonSerializer.Deserialize<IpcResponse>(responseJson);
```
