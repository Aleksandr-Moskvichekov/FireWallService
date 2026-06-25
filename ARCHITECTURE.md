 # Архитектура FireWallService и FirewallWiewApp

## Обзор системы

Система состоит из двух основных компонентов:

```
┌─────────────────────────────────────────────────────────────┐
│                      Windows OS                             │
│                                                              │
│  ┌──────────────────────────┐   ┌─────────────────────────┐│
│  │   FireWallService        │   │   FirewallWiewApp       ││
│  │   (Background Service)   │   │   (WPF GUI)             ││
│  │                          │   │                         ││
│  │  • WinDivert Driver      │   │  • MVVM Pattern         ││
│  │  • Packet Capture        │◄──┤  • Named Pipes Client   ││
│  │  • Rule Engine           │   │  • UI Controls          ││
│  │  • Named Pipes Server    │   │  • Diagnostics Tool    ││
│  │                          │   │                         ││
│  └──────────────────────────┘   └─────────────────────────┘│
│           │                              ▲                  │
│           │                              │                  │
│           └──────Named Pipes ────────────┘                  │
│          (FireWallServicePipe)                              │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## Компоненты

### 1. FireWallService (Background Service)

**Назначение:** Фоновый сервис для перехвата и фильтрации сетевых пакетов

#### Основные модули:

| Модуль | Описание |
|--------|---------|
| **FirewallService.cs** | Главный класс BackgroundService, оркестрирует все компоненты |
| **IPC/NamedPipeServer.cs** | Сервер именованных каналов для связи с GUI |
| **IPC/IpcModels.cs** | DTOs для IPC (IpcRequest, IpcResponse, IpcNotification) |
| **PacketCapture/PacketLogger.cs** | Логирование и хранение информации о пакетах |
| **PacketCapture/PcapWriter.cs** | Запись пакетов в формат PCAP |
| **WinDivert/* | Обертка для WinDivert драйвера |

#### Процесс работы:

```
1. ExecuteAsync() запускается
2. Запускается NamedPipeServer на "FireWallServicePipe"
3. Инициализируется WinDivert для перехвата пакетов
4. Регистрируется обработчик IPC запросов: HandleIpcRequest
5. Основной цикл: Recv → ProcessPacketAsync → ApplyRules → Reinject
6. Параллельно: Прием IPC запросов → обработка → отправка ответа
```

#### Обработчик IPC запросов (HandleIpcRequest):

```csharp
switch (request.RequestType)
{
	case IpcRequestType.GetStatus → ServiceStatus с метриками
	case IpcRequestType.GetRules → Список всех правил
	case IpcRequestType.AddRule → Добавить новое правило
	case IpcRequestType.RemoveRule → Удалить правило
	case IpcRequestType.UpdateRule → Обновить существующее
	case IpcRequestType.GetPackets → Последние перехватанные пакеты
	case IpcRequestType.GetStatistics → Статистика работы
	case IpcRequestType.GetSettings → Текущие параметры
	case IpcRequestType.SetSettings → Изменить параметры
	case IpcRequestType.GetLogs → История событий
	case IpcRequestType.ClearLogs → Очистить логи
	case IpcRequestType.ClearPackets → Очистить историю пакетов
	case IpcRequestType.TestConnection → Проверка связи
	case IpcRequestType.StartCapture → Начать захват пакетов
	case IpcRequestType.StopCapture → Остановить захват
	...
}
```

### 2. FirewallWiewApp (WPF GUI)

**Назначение:** Графический интерфейс для управления FireWallService

#### Архитектура:

```
┌─ App.xaml, MainWindow.xaml
│
├─ Views/
│  ├─ FirewallRulesView.xaml
│  ├─ FirewallLogsView.xaml
│  ├─ FirewallPacketsView.xaml
│  ├─ FirewallStatisticsView.xaml
│  ├─ FirewallSettingsView.xaml
│  └─ [+ code-behind для каждого]
│
├─ ViewModels/
│  ├─ MainViewModel.cs (главная ВМ, координирует другие)
│  ├─ FirewallRulesViewModel.cs (управление правилами)
│  ├─ FirewallLogsViewModel.cs (просмотр логов)
│  ├─ FirewallPacketsViewModel.cs (просмотр пакетов)
│  ├─ FirewallStatisticsViewModel.cs (статистика)
│  ├─ FirewallSettingsViewModel.cs (параметры)
│  ├─ ObservableObject.cs (базовый класс MVVM)
│  └─ RelayCommand.cs (команда для MVVM)
│
├─ Services/
│  ├─ IpcClient.cs (клиент именованных каналов)
│  ├─ RuleService.cs (бизнес-логика правил)
│  ├─ LogService.cs (работа с логами)
│  ├─ PacketService.cs (работа с пакетами)
│  ├─ StatisticsService.cs (статистика)
│  ├─ SettingsService.cs (управление параметрами)
│  └─ ConnectionService.cs (состояние подключения)
│
├─ Resources/
│  ├─ BoolToColorConverter.cs (конвертер для UI)
│  ├─ Resources.xaml (стили и кисти)
│  └─ [другие ресурсы]
│
└─ DiagnosticsWindow.xaml (окно диагностики)
```

#### MVVM паттерн:

```
View (XAML) ← DataContext → ViewModel
	↓ (Binding)                    ↓
  UI Controls               ObservableObject (свойства)
	↓ (Event)                      ↓
  Click/Input              RelayCommand (команды)
	↓                              ↓
  Command.Execute() ────────→ Execute()
	↓                              ↓
Services (IpcClient) ─────→ SendRequestAsync()
	↓                              ↓
Named Pipes ────────────────→ FireWallService
```

#### Жизненный цикл приложения:

```
1. App.xaml запускается
2. App.xaml.cs инициализирует MainWindow
3. MainWindow.xaml загружается
4. MainViewModel создается (DataContext)
5. MainViewModel.InitializeConnectionAsync() вызывается
6. IpcClient.TestConnectionAsync() - проверка связи
7. Если успешно - загружаются данные всех ВМ
8. UI готов к взаимодействию
```

## Протокол IPC (Named Pipes)

### Формат сообщений

#### Запрос (IpcRequest):
```json
{
  "RequestType": "GetStatus",
  "Data": null
}
```

#### Ответ (IpcResponse):
```json
{
  "Success": true,
  "Data": {
	"IsRunning": true,
	"TotalRulesCount": 5,
	"PacketsProcessed": 1024,
	"PacketsBlocked": 12
  },
  "ErrorMessage": null
}
```

#### Уведомление (IpcNotification):
```
NOTIFY:
{
  "NotificationType": "PacketCaptured",
  "Data": { ... }
}
```

### Процесс коммуникации

```
GUI (FirewallWiewApp)              FireWallService (Named Pipes)
		│                                    │
		│─ Connect() ────────────────→ WaitForConnection()
		│                              [Ожидание запроса]
		│
		│─ Write(IpcRequest) ────────→ Read(IpcRequest)
		│                              HandleIpcRequest()
		│                              [Обработка]
		│
		│─ Read(IpcResponse) ←────── Write(IpcResponse)
		│
		│─ Disconnect() ────────────→ [Ожидание следующего]
		│                              клиента
```

### Обработка ошибок

```
Клиент пытается подключиться
	↓
[Timeout? 10 сек]
	├─ YES → ErrorMessage: "Connection timeout"
	└─ NO ↓
		[Успешное подключение]
		↓
		[Отправка запроса]
		↓
		[Ожидание ответа, 5 сек]
		├─ Timeout → ErrorMessage: "Response timeout"
		├─ IOException → ErrorMessage: "Connection error"
		├─ JsonException → ErrorMessage: "Invalid response"
		└─ OK → Parse + Return Response
```

## Папки и структура файлов

```
D:\Документы\MVS Programs\FireWallService\
│
├─ FireWallService/
│  ├─ IPC/
│  │  ├─ IpcModels.cs
│  │  ├─ NamedPipeServer.cs
│  │  └─ [другие IPC файлы]
│  │
│  ├─ PacketCapture/
│  │  ├─ PacketLogger.cs
│  │  ├─ PcapWriter.cs
│  │  └─ [другие классы захвата]
│  │
│  ├─ WinDivert/
│  │  └─ [обертка WinDivert]
│  │
│  ├─ FirewallService.cs (основной класс)
│  ├─ Program.cs (точка входа)
│  ├─ FirewallService.csproj
│  └─ [логи и конфиги]
│
├─ FirewallWiewApp/
│  ├─ Views/
│  │  ├─ FirewallRulesView.xaml
│  │  ├─ FirewallRulesView.xaml.cs
│  │  ├─ FirewallLogsView.xaml
│  │  ├─ FirewallLogsView.xaml.cs
│  │  └─ [другие views]
│  │
│  ├─ ViewModels/
│  │  ├─ MainViewModel.cs
│  │  ├─ FirewallRulesViewModel.cs
│  │  ├─ ObservableObject.cs
│  │  ├─ RelayCommand.cs
│  │  └─ [другие ВМ]
│  │
│  ├─ Services/
│  │  ├─ IpcClient.cs
│  │  ├─ RuleService.cs
│  │  └─ [другие сервисы]
│  │
│  ├─ Resources/
│  │  ├─ BoolToColorConverter.cs
│  │  ├─ Resources.xaml
│  │  └─ [другие ресурсы]
│  │
│  ├─ MainWindow.xaml
│  ├─ MainWindow.xaml.cs
│  ├─ App.xaml
│  ├─ App.xaml.cs
│  ├─ DiagnosticsWindow.xaml
│  ├─ DiagnosticsWindow.xaml.cs
│  ├─ FirewallWiewApp.csproj
│  └─ [конфиги]
│
├─ FireWallService.sln (Solution file)
├─ QUICKSTART.md
├─ TROUBLESHOOTING.md
├─ CONNECTION_DIAGNOSTICS.md
└─ ARCHITECTURE.md (этот файл)
```

## Технологический стек

| Компонент | Технология |
|-----------|-----------|
| **Framework** | .NET 10.0 |
| **GUI** | WPF (Windows Presentation Foundation) |
| **Background Service** | BackgroundService (.NET) |
| **IPC** | Named Pipes (Windows) |
| **Сериализация** | System.Text.Json |
| **Сетевой перехват** | WinDivert |
| **Запись трафика** | libpcap (PCAP) |
| **Логирование** | Microsoft.Extensions.Logging |

## Требования к запуску

1. **ОС:** Windows 7 или выше
2. **Runtime:** .NET 10.0 Runtime
3. **Права:** Администратор (для обоих приложений)
4. **Firewall:** Windows Firewall должен разрешить приложения или быть отключенным
5. **WinDivert:** Драйвер должен быть установлен и загружен

## Поток выполнения при запуске

### FireWallService:
```
Program.cs
  ↓
CreateApplicationBuilder()
  ↓
AddLogging() - Console + File
  ↓
AddHostedService<FirewallService>()
  ↓
host.Run()
  ↓
FirewallService.StartAsync()
  ├─ NamedPipeServer.StartAsync()
  ├─ InitializeWinDivert()
  ├─ InitializePcapWriter() (если включен)
  └─ Main Loop: Recv packets → Process → Apply rules
```

### FirewallWiewApp:
```
App.xaml
  ↓
App.xaml.cs
  ↓
MainWindow.xaml
  ↓
MainViewModel
  ↓
InitializeConnectionAsync()
  ├─ IpcClient.TestConnectionAsync()
  ├─ Load initial data
  └─ Setup event handlers
  ↓
UI Ready for user interaction
```

## Безопасность

1. **Привилегии:** Оба приложения должны работать от администратора
2. **Named Pipes ACL:** По умолчанию используются стандартные права Windows
3. **JSON:** Использует контролируемую десериализацию с явными типами
4. **Таймауты:** Предотвращают зависание при потере соединения

## Масштабируемость

- **Клиентов:** Может быть несколько GUI приложений, подключенных к одному сервису одновременно
- **Пакетов:** История хранит последние N пакетов (по умолчанию 10000)
- **Правил:** Не имеет практического лимита
- **Уведомлений:** Очередь уведомлений хранит сообщения до подключения клиента

## Будущие улучшения

1. **TCP/IP вместо Named Pipes** для удаленной работы
2. **Аутентификация** для защиты от неавторизованного доступа
3. **Шифрование** для передачи данных
4. **WEB Interface** помимо WPF
5. **Linux поддержка** (заменить WinDivert)
6. **Persistent Config** - сохранение правил на диск
7. **Real-time Notifications** - push уведомления по WebSocket

---

Архитектура разработана для обеспечения надежного разделения ответственности между сервисом (обработка пакетов) и GUI (управление и мониторинг).
