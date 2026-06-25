# 📚 Индекс документации - Быстрая навигация

Добро пожаловать! Выберите, что вам нужно:

## 🚀 Новый пользователь? Начните отсюда:

1. **[START_HERE.md](START_HERE.md)** 📌 **НАЧНИТЕ ЗДЕСЬ**
   - Пошаговые инструкции по запуску
   - Решение типичных ошибок
   - 5 минут для полного старта

2. **[README.md](README.md)** - Обзор проекта
   - Описание функционала
   - Требования к системе
   - Основные команды

## 📖 Документация для разных ситуаций

### 🔴 Проблемы и решения
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - Решение проблем подключения
  - Ошибка: Connection timeout
  - Ошибка: Access is denied
  - Ошибка: Empty response
  - Пошаговая диагностика
  - PowerShell команды для проверки

- **[CONNECTION_DIAGNOSTICS.md](CONNECTION_DIAGNOSTICS.md)** - Диагностика IPC
  - Причины проблем
  - Способы диагностики
  - Встроенное диагностическое окно
  - Авто-тестирование

### 📚 Глубокое изучение
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Техническая архитектура
  - Диаграммы компонентов
  - Описание модулей
  - MVVM паттерн
  - Протокол IPC
  - Технологический стек

- **[IMPROVEMENTS.md](IMPROVEMENTS.md)** - Что было улучшено
  - Логирование IpcClient
  - Диагностическое окно
  - Консольный вывод
  - Полный список улучшений

- **[SOLUTION_SUMMARY.md](SOLUTION_SUMMARY.md)** - Резюме решения
  - Проблема и решение
  - Что было сделано
  - Результаты
  - Как использовать

## 🎯 По функциям приложения
- **[QUICKSTART.md](QUICKSTART.md)** - Функции и быстрый старт
  - Управление правилами
  - Просмотр статистики
  - Захват пакетов
  - Логирование
  - Параметры

## ✓ Проверка
- **[CHECKLIST.md](CHECKLIST.md)** - Контрольный список
  - Что было сделано
  - Функциональность
  - Результаты

## 📋 Быстрая справка по типам проблем

| Ваша проблема | Что читать |
|--------|-----------|
| Не знаю как запустить | [START_HERE.md](START_HERE.md) |
| Connection timeout | [TROUBLESHOOTING.md](TROUBLESHOOTING.md#ошибка-connection-timeout) |
| Access is denied | [TROUBLESHOOTING.md](TROUBLESHOOTING.md#ошибка-access-is-denied) |
| Хочу понять архитектуру | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Хочу узнать что улучшено | [IMPROVEMENTS.md](IMPROVEMENTS.md) |
| Нужен общий обзор | [README.md](README.md) |
| Нужна пошаговая инструкция | [QUICKSTART.md](QUICKSTART.md) |

## 🔍 Поиск по ключевым словам

### Диагностика и отладка
- **Встроенное тестирование:** [START_HERE.md](START_HERE.md) → Шаг 5
- **Auto-Test режим:** [TROUBLESHOOTING.md](TROUBLESHOOTING.md#расширенная-диагностика)
- **Просмотр логов:** [TROUBLESHOOTING.md](TROUBLESHOOTING.md#просмотр-полных-логов)
- **Команды PowerShell:** [TROUBLESHOOTING.md](TROUBLESHOOTING.md#пошаговая-диагностика)

### Управление приложением
- **Запуск:** [START_HERE.md](START_HERE.md)
- **Функции:** [QUICKSTART.md](QUICKSTART.md)
- **Командная строка:** [QUICKSTART.md](QUICKSTART.md#команды-консоли-firewall-service)
- **Конфигурация:** [QUICKSTART.md](QUICKSTART.md#файлы-конфигурации)

### Техническая информация
- **Архитектура:** [ARCHITECTURE.md](ARCHITECTURE.md)
- **Протокол IPC:** [ARCHITECTURE.md](ARCHITECTURE.md#протокол-ipc-named-pipes)
- **Папки и структура:** [ARCHITECTURE.md](ARCHITECTURE.md#папки-и-структура-файлов)
- **Технологический стек:** [ARCHITECTURE.md](ARCHITECTURE.md#технологический-стек)

## 💡 Советы по использованию документации

### Если вы спешите (5 минут)
1. Откройте [START_HERE.md](START_HERE.md)
2. Выполните шаги 1-5
3. Готово!

### Если у вас проблема (15 минут)
1. Найдите вашу ошибку в [START_HERE.md](START_HERE.md) → "Решение типичных ошибок"
2. Если не помогло → [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
3. Используйте "Пошаговая диагностика"
4. Готово!

### Если вы хотите все понять (30 минут)
1. Прочитайте [README.md](README.md)
2. Изучите [ARCHITECTURE.md](ARCHITECTURE.md)
3. Посмотрите диаграммы компонентов
4. Готово!

### Если вы разработчик (60+ минут)
1. Начните с [ARCHITECTURE.md](ARCHITECTURE.md)
2. Изучите [IMPROVEMENTS.md](IMPROVEMENTS.md)
3. Посмотрите исходный код
4. Экспериментируйте!

## 🆘 Если ничего не помогло

1. **Проверьте все требования:**
   - Windows 7+, .NET 10.0, права администратора
   - См. [QUICKSTART.md](QUICKSTART.md#требования)

2. **Используйте встроенную диагностику:**
   - Нажмите кнопку "Диагностика" в приложении
   - Запустите "Auto-Test" 
   - Смотрите точное сообщение об ошибке

3. **Просмотрите логи:**
   ```powershell
   Get-Content firewall.log -Tail 50
   ```
   См. [TROUBLESHOOTING.md](TROUBLESHOOTING.md#просмотр-полных-логов)

4. **Прочитайте CONNECTION_DIAGNOSTICS.md:**
   - Там подробно описаны все причины

## 📞 Контактная информация для поддержки

При обращении в поддержку приложите:
1. Скриншот из Diagnostics Window (кнопка "Диагностика")
2. Содержимое файла `firewall.log`
3. Вывод команды: `dotnet --list-runtimes`
4. Ваша версия Windows

Это поможет быстро найти причину проблемы.

## 🎓 Обучающие ресурсы

- **WPF и MVVM:** [ARCHITECTURE.md](ARCHITECTURE.md#mvvm-паттерн)
- **Named Pipes:** [ARCHITECTURE.md](ARCHITECTURE.md#протокол-ipc-named-pipes)
- **WinDivert:** [QUICKSTART.md](QUICKSTART.md#лицензия-и-лицензионное-соглашение)
- **.NET Background Services:** [ARCHITECTURE.md](ARCHITECTURE.md#firewall-service-background-service)

## 📈 Структура документации

```
📁 Документация
├─ 🚀 START_HERE.md          ← Начните отсюда!
├─ 📖 README.md               ← Основная документация
├─ ⚡ QUICKSTART.md           ← Быстрый старт
├─ 🔴 TROUBLESHOOTING.md      ← Решение проблем
├─ 🔍 CONNECTION_DIAGNOSTICS.md ← Диагностика IPC
├─ 🏗️ ARCHITECTURE.md         ← Архитектура
├─ ✨ IMPROVEMENTS.md         ← Сделанные улучшения
├─ 📝 SOLUTION_SUMMARY.md     ← Резюме решения
├─ ✓ CHECKLIST.md            ← Контрольный список
└─ 📚 INDEX.md               ← Этот файл (навигация)
```

## 🔗 Связь между документами

```
START_HERE.md
	↓
	├→ Проблема? → TROUBLESHOOTING.md
	│               ↓
	│               → CONNECTION_DIAGNOSTICS.md
	│
	├→ Нужны функции? → QUICKSTART.md
	│
	└→ Интересует архитектура? → ARCHITECTURE.md
									↓
									→ IMPROVEMENTS.md
									→ SOLUTION_SUMMARY.md
```

## ✨ Новичкам

Если вы впервые пользуетесь этим приложением:

1. **День 1:** Прочитайте [START_HERE.md](START_HERE.md) (10 мин)
2. **День 2:** Прочитайте [QUICKSTART.md](QUICKSTART.md) (15 мин)
3. **День 3:** Изучите [ARCHITECTURE.md](ARCHITECTURE.md) (30 мин)
4. **День 4+:** Экспериментируйте с приложением!

---

## 📊 Статистика документации

| Документ | Размер | Время чтения | Сложность |
|----------|--------|-------------|-----------|
| START_HERE.md | ~4 KB | 5 мин | ⭐ Легко |
| README.md | ~8 KB | 10 мин | ⭐ Легко |
| QUICKSTART.md | ~10 KB | 15 мин | ⭐ Легко |
| TROUBLESHOOTING.md | ~12 KB | 20 мин | ⭐⭐ Средне |
| CONNECTION_DIAGNOSTICS.md | ~8 KB | 15 мин | ⭐⭐ Средне |
| ARCHITECTURE.md | ~20 KB | 40 мин | ⭐⭐⭐ Сложно |
| IMPROVEMENTS.md | ~8 KB | 15 мин | ⭐⭐ Средне |
| SOLUTION_SUMMARY.md | ~6 KB | 10 мин | ⭐⭐ Средне |
| **ВСЕГО** | **76 KB** | **2 часа** | **Всё уровни** |

---

**Выберите нужный документ и начните работу!** 📖

**Рекомендуем начать с [START_HERE.md](START_HERE.md)** 👈
