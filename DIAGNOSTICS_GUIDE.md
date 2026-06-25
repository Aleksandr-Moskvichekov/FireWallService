# 🔧 ПОШАГОВАЯ ДИАГНОСТИКА ПРОБЛЕМЫ ПОДКЛЮЧЕНИЯ

## Шаг 1️⃣: Убедитесь, что запустили от администратора

```powershell
# Проверить привилегии PowerShell
$currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Write-Host "Администратор: $isAdmin"
# Должно быть: Администратор: True
```

## Шаг 2️⃣: Запустить сервис

```powershell
# PowerShell от администратора
cd "D:\Документы\MVS Programs\FireWallService\FireWallService\bin\Debug\net10.0"

# Запустить сервис
.\FireWallService.exe
```

**Ожидаемый вывод:**
```
╔════════════════════════════════════════════════════════╗
║   FireWallService Started                              ║
║   Named Pipe: FireWallServicePipe                      ║
║   Waiting for GUI connections...                       ║
╚════════════════════════════════════════════════════════╝
```

✅ **Если видите это** - сервис готов к подключению

❌ **Если получили ошибку** - проверьте:
- Нужны ли административные права (да!)
- Нет ли другого процесса на этом порту
- Не забыли ли собрать проект (Visual Studio → Build → Rebuild Solution)

---

## Шаг 3️⃣: В другом окне PowerShell запустить приложение

```powershell
# НОВОЕ окно PowerShell от администратора
cd "D:\Документы\MVS Programs\FireWallService\FirewallWiewApp\bin\Debug\net10.0-windows"

# Запустить приложение
.\FirewallWiewApp.exe
```

**Ожидаемый результат:**
- Откроется окно приложения
- Верхнее меню активно

---

## Шаг 4️⃣: Открыть диагностику

1. В приложении нажмите на кнопку **"Диагностика"** (синяя кнопка в верхней части)
2. Откроется окно диагностики

---

## Шаг 5️⃣: Тестировать подключение

### Вариант A: Одноразовый тест
1. Нажмите **"Test Connection"**
2. Ждите результата (макс 15 сек)

**Результат успеха:**
```
[12:34:56.789] → Testing connection...
[12:34:57.234] ✓ SUCCESS: Service is running
[12:34:57.234]   Rules Count: 5
[12:34:57.234]   Packets Processed: 1024
[12:34:57.234]   Packets Blocked: 12
[12:34:57.234] Connection established successfully!
Successes: 1, Failures: 0
```

**Результат ошибки - примеры:**
```
[12:34:56.789] → Testing connection...
[12:35:07.890] ✗ FAILED: Failed to connect
[12:35:07.890]   Error: Connection timeout after 10000ms
[12:35:07.890]   Connection not established
Successes: 0, Failures: 1
```

### Вариант B: Непрерывное тестирование
1. Нажмите **"Auto-Test"**
2. Автоматически тестирует каждые 3 сек
3. Смотрите в реальном времени, обновляется ли статистика
4. Нажмите **"Auto-Test"** еще раз для остановки

---

## 🎯 Интерпретация ошибок

### ✅ SUCCESS
```
✓ SUCCESS: Service is running
  Rules Count: N
  Packets Processed: N
  Packets Blocked: N
```
**Статус:** ✅ **ВСЁ РАБОТАЕТ!** Подключение успешно.

---

### ❌ Connection timeout after 10000ms
```
✗ FAILED: Failed to connect
  Error: Connection timeout after 10000ms
```
**Причина:** Сервис не ответил за 10 секунд

**Решение:**
1. Проверьте, запущен ли FireWallService в другом окне
2. Проверьте, видите ли вы сообщение "Waiting for GUI connections..."
3. Если нет - перезагрузите сервис

---

### ❌ Access is denied (0x5)
```
✗ FAILED: Failed to connect
  Error: Access is denied (0x5)
```
**Причина:** Недостаточно прав доступа

**Решение:**
1. Закройте оба приложения
2. Запустите PowerShell от администратора (правой кнопкой)
3. Запустите сервис и приложение снова

---

### ❌ All pipe instances are busy (0x231)
```
✗ FAILED: Failed to connect
  Error: All pipe instances are busy (0x231)
```
**Причина:** Канал занят другим процессом

**Решение:**
1. Закройте оба приложения
2. Перезагрузитесь
3. Запустите снова

---

### ❌ The system cannot find the file specified (0x2)
```
✗ FAILED: Failed to connect
  Error: The system cannot find the file specified (0x2)
```
**Причина:** Неверный путь к каналу или сервис не запущен

**Решение:**
1. Убедитесь, что сервис запущен
2. Проверьте, что имя канала совпадает (FireWallServicePipe)

---

### ❌ Empty response from service
```
✗ FAILED: Failed to connect
  Error: Empty response from service
```
**Причина:** Сервис подключился, но не отправил ответ

**Решение:**
1. Проверьте firewall.log
2. Перезагрузите оба приложения
3. Проверьте, не заблокирована ли попытка подключения брандмауэром Windows

---

## 📋 Проверочный лист диагностики

```
[ ] 1. Запущен PowerShell от администратора
[ ] 2. FireWallService запущен и выводит "Waiting for GUI connections..."
[ ] 3. FirewallWiewApp запущен
[ ] 4. Нажата кнопка "Диагностика"
[ ] 5. Нажата кнопка "Test Connection"
[ ] 6. Результат успешен (✓ SUCCESS)
[ ] 7. Статистика обновляется (Rules, Packets, etc)
[ ] 8. Auto-Test показывает постоянное подключение
```

✅ **Если все пункты отмечены** - система работает нормально!

---

## 📊 Мониторинг логов

### Лог сервиса (firewall.log)
```powershell
# Просмотреть в реальном времени
Get-Content "firewall.log" -Wait -Tail 20
```

**Ищите ошибки:**
```
[ERR] Unable to start Named Pipe Server
[ERR] Client connection failed
[ERR] Deserialization error
```

### Событие успешного подключения:
```
[INF] ServerLoop started
[INF] Waiting for client connection...
[INF] Client connected on pipe FireWallServicePipe
[INF] Received IPC request: GetStatus
[INF] Sending response...
```

---

## 🚀 Быстрое восстановление при проблеме

```powershell
# 1. Закройте оба приложения

# 2. Найдите процессы
Get-Process | Where-Object { $_.ProcessName -like "*FireWall*" }

# 3. Если есть - завершите
Stop-Process -Name "FireWallService" -Force
Stop-Process -Name "FirewallWiewApp" -Force

# 4. Очистите (при необходимости)
Remove-Item "firewall.log" -ErrorAction SilentlyContinue

# 5. Запустите заново
```

---

## 💡 Продвинутая диагностика

### Проверить порт/канал
```powershell
# Проверить все названные каналы
$pipes = [System.IO.Directory]::GetFiles("\\.\\pipe\\")
$pipes | Where-Object { $_ -like "*FireWall*" }
```

### Проверить брандмауэр Windows
```powershell
# Может ли приложение подключаться?
Get-NetFirewallProfile | Select-Object Name, Enabled

# Выключить для теста (осторожно!)
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled $false

# Включить обратно
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled $true
```

### Детальный лог подключения
Добавьте в файл `IpcClient.cs` перед `SendRequestAsync`:
```csharp
System.Diagnostics.Debug.WriteLine($"[IPC] Attempting to connect to {pipeName}");
```

---

## ✨ Результаты

Если вы видите:
- ✅ "SUCCESS" в диагностике
- ✅ Счетчик "Successes" увеличивается
- ✅ Статистика (Rules, Packets) обновляется

**То подключение работает идеально!** 🎉

---

**Нужна помощь?** Обратитесь к [TROUBLESHOOTING.md](TROUBLESHOOTING.md)

