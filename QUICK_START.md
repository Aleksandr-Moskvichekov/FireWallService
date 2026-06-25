# ⚡ БЫСТРЫЙ СТАРТ (5 МИНУТ)

## 1️⃣ Откройте PowerShell от администратора

```powershell
# Нажмите Win+X → PowerShell (Admin)
# ИЛИ: Правой кнопкой на PowerShell → "Запустить от имени администратора"
```

## 2️⃣ Запустите сервис

```powershell
cd "D:\Документы\MVS Programs\FireWallService\FireWallService\bin\Debug\net10.0"
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

✅ **Оставьте это окно открытым!**

## 3️⃣ Откройте НОВОЕ окно PowerShell от администратора

```powershell
# Нажмите Win+X → PowerShell (Admin) - ВТОРОЕ ОКНО
```

## 4️⃣ Запустите приложение

```powershell
cd "D:\Документы\MVS Programs\FireWallService\FirewallWiewApp\bin\Debug\net10.0-windows"
.\FirewallWiewApp.exe
```

✅ **Приложение откроется**

## 5️⃣ Тестируйте подключение

1. Нажмите **"Диагностика"** (кнопка в верхней части приложения)
2. Нажмите **"Test Connection"**
3. Посмотрите результат:

### ✅ Успех:
```
[12:34:56.789] → Testing connection...
[12:34:57.234] ✓ SUCCESS: Service is running
[12:34:57.234]   Rules Count: 5
[12:34:57.234]   Packets Processed: 1024
[12:34:57.234]   Packets Blocked: 12
```
**➜ ВСЁ РАБОТАЕТ!** 🎉

### ❌ Ошибка:
```
[12:34:56.789] → Testing connection...
[12:35:07.890] ✗ FAILED: Failed to connect
[12:35:07.890]   Error: Connection timeout after 10000ms
```
**➜ См. раздел "РЕШЕНИЕ ПРОБЛЕМ"** ниже

---

## 🔧 ЕСЛИ НЕ РАБОТАЕТ

### Проблема: "Connection timeout"
```powershell
# 1. Убедитесь, что сервис запущен в первом окне PowerShell
# 2. Нажмите "Test Connection" еще раз
# 3. Если не поможет - перезагрузитесь
```

### Проблема: "Access is denied"
```powershell
# 1. Закройте оба приложения
# 2. Откройте PowerShell от администратора (обязательно!)
# 3. Запустите заново
```

### Проблема: Приложение не запускается
```powershell
# 1. Проверьте, что Visual Studio собрала проект
# 2. В Visual Studio: Build → Rebuild Solution
# 3. Убедитесь, что нет ошибок
# 4. Запустите заново
```

---

## 📚 ПОДРОБНЕЕ

- **Полное руководство:** [DIAGNOSTICS_GUIDE.md](DIAGNOSTICS_GUIDE.md)
- **Решение проблем:** [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
- **Архитектура:** [ARCHITECTURE.md](ARCHITECTURE.md)
- **Все документы:** [INDEX.md](INDEX.md)

---

## ✨ ГЛАВНОЕ ПОМНИТЬ

| Что | Где |
|-----|-----|
| **Сервис** | PowerShell окно 1 (оставить открытым) |
| **Приложение** | PowerShell окно 2 |
| **Диагностика** | Кнопка в приложении |
| **Логи** | firewall.log (в папке сервиса) |
| **Ошибки** | Смотреть в окне диагностики |

---

**Готово! Наслаждайтесь!** 🚀

