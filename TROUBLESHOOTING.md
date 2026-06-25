# Решение проблем подключения IPC

## Проблема

Приложение **FirewallWiewApp** не может подключиться к сервису **FireWallService** через именованные каналы (Named Pipes).

## Основные причины и быстрые решения

### ✓ Первое: Запустите оба приложения от администратора

Это самая частая причина проблем!

**Для PowerShell:**
```powershell
# Откройте новый PowerShell от администратора
Start-Process powershell -Verb RunAs

# Затем запустите приложения
cd "D:\Документы\MVS Programs\FireWallService\FireWallService\bin\Debug\net10.0"
.\FireWallService.exe
```

**В другом окне PowerShell (также от администратора):**
```powershell
cd "D:\Документы\MVS Programs\FireWallService\FirewallWiewApp\bin\Debug\net10.0-windows"
.\FirewallWiewApp.exe
```

### ✓ Второе: Проверьте, что сервис запущен

**В PowerShell от администратора:**
```powershell
Get-Process | Where-Object {$_.Name -match "FireWall"}
```

Вы должны увидеть хотя бы процесс `FireWallService`.

### ✓ Третье: Используйте встроенное диагностическое окно

1. Запустите FirewallWiewApp
2. Нажмите кнопку **"Диагностика"** в правом верхнем углу
3. Нажмите **"Test Connection"**
4. Проверьте результат в логах:
   - ✓ Если видите `SUCCESS` - подключение работает
   - ✗ Если видите ошибку - смотрите ниже

## Расширенная диагностика

### Ошибка: "Connection timeout to FireWallServicePipe"

**Причина:** Сервис не запущен или не слушает канал.

**Решение:**
1. Проверьте, запущен ли FireWallService
2. Убедитесь, что он работает от администратора
3. Посмотрите консоль сервиса - там должно быть:
   ```
   ╔════════════════════════════════════════════════════════╗
   ║   FireWallService Started                              ║
   ║   Named Pipe: FireWallServicePipe                      ║
   ║   Waiting for GUI connections...                       ║
   ╚════════════════════════════════════════════════════════╝
   ```

### Ошибка: "Access is denied"

**Причина:** Недостаточно прав доступа.

**Решение:**
```powershell
# Убедитесь, что оба процесса работают от администратора
# Откройте диспетчер задач (Ctrl+Shift+Esc)
# Посмотрите в столбце "User" - там должно быть "SYSTEM" или ваше имя

# Если запущено не от администратора - перезагрузите от администратора
Start-Process powershell -Verb RunAs
```

### Ошибка: "Connection reset by peer"

**Причина:** Сервис закрыл соединение неожиданно.

**Решение:**
1. Посмотрите firewall.log:
   ```powershell
   Get-Content firewall.log -Tail 50
   ```
2. Поищите ошибки (ERROR, Exception, Fatal)
3. Перезагрузите сервис

### Ошибка: "Empty response from service"

**Причина:** Сервис подключился, но не отправил ответ.

**Решение:**
1. Проверьте, что HandleIpcRequest правильно обрабатывает запросы
2. Посмотрите firewall.log для деталей
3. Попробуйте использовать GetStatus (это простой запрос)

## Продвинутые методы диагностики

### Проверка сетевых соединений

```powershell
# Посмотрите все сетевые соединения
Get-NetTCPConnection | Where-Object {$_.LocalPort -like "*Name*"}

# Или посмотрите процессы, занимающие порты
netstat -ano | findstr LISTEN
```

### Просмотр полных логов

```powershell
# Логи сервиса
notepad "D:\Документы\MVS Programs\FireWallService\firewall.log"

# Для непрерывного просмотра:
Get-Content "D:\Документы\MVS Programs\FireWallService\firewall.log" -Wait -Tail 20
```

### Отключение Windows Firewall (временно для тестирования)

```powershell
# Отключить все профили
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled $False

# Включить обратно
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled $True

# Проверить статус
Get-NetFirewallProfile
```

### Проверка прав доступа на именованные каналы

```powershell
# Посмотрите ACL (права доступа) для каналов
icacls "\\.\pipe\" | findstr /i "users"
```

## Пошаговая диагностика

### Шаг 1: Проверьте, запущен ли сервис
```powershell
$proc = Get-Process FireWallService -ErrorAction SilentlyContinue
if ($proc) {
	Write-Host "✓ FireWallService запущен (PID: $($proc.Id))"
} else {
	Write-Host "✗ FireWallService не запущен"
}
```

### Шаг 2: Проверьте права администратора
```powershell
# Эта команда работает только для администратора
try {
	[System.IO.File]::WriteAllText("$env:TEMP\admin_test.txt", "test")
	Remove-Item "$env:TEMP\admin_test.txt" -Force
	Write-Host "✓ Текущий пользователь - администратор"
} catch {
	Write-Host "✗ Недостаточно прав администратора"
}
```

### Шаг 3: Запустите диагностику в приложении
```
1. Откройте Диагностическое окно
2. Нажмите "Start Auto-Test"
3. Смотрите результаты каждые 3 секунды
4. Сохраните логи для анализа
```

## Если всё ещё не работает

1. **Сохраните логи:**
   - firewall.log (сервис)
   - Выводы Diagnostics Window (приложение)

2. **Проверьте версии .NET:**
   ```powershell
   dotnet --list-runtimes
   ```
   Должен быть установлен `.NET 10.0.x`

3. **Перестройте решение:**
   ```powershell
   dotnet clean
   dotnet build
   ```

4. **Перезагрузитесь:**
   Иногда помогает обычная перезагрузка компьютера

## Настройки подключения (можно отредактировать)

В файле `IpcClient.cs`:
- **Timeout для подключения:** 10 секунд (строка 56)
- **Timeout для ответа:** 5 секунд (строка 68)

Если сеть медленная, увеличьте эти значения:

```csharp
// Измените эти значения:
await client.ConnectAsync(10000, cancellationToken); // 10000 = 10 сек
cts.CancelAfter(TimeSpan.FromSeconds(5)); // 5 сек
```

На более высокие значения (20000 = 20 сек, 10 сек соответственно).

---

## Чеклист для проверки

- [ ] Оба приложения запущены от администратора
- [ ] FireWallService показывает "Waiting for GUI connections..."
- [ ] Windows Firewall не блокирует приложения (или отключен)
- [ ] Нет других приложений на том же Named Pipe
- [ ] Версия .NET совпадает (10.0)
- [ ] Никаких других окон PowerShell не запущено на том же порте
- [ ] Диагностическое окно показывает SUCCESS при тестировании

Если все пункты выполнены, но подключение всё ещё не работает - отправьте логи и описание проблемы.
