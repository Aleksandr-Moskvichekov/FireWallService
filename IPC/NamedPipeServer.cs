using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FireWallService.IPC;

namespace FireWallService.IPC
{
    /// <summary>
    /// Named Pipe сервер для связи с GUI
    /// </summary>
    public class NamedPipeServer : IDisposable
    {
        private readonly ILogger<NamedPipeServer> _logger;
        private readonly string _pipeName;
        private CancellationTokenSource? _cts;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly List<IpcNotification> _pendingNotifications = new();
        private readonly object _notificationsLock = new();
        private const int MaxPendingNotifications = 50;

        // События для связи с FirewallService
        public event Func<IpcRequest, Task<IpcResponse?>>? OnRequestReceived;
        public event Action? OnClientConnected;
        public event Action? OnClientDisconnected;

        public NamedPipeServer(ILogger<NamedPipeServer> logger, string pipeName = "FireWallServicePipe")
        {
            _logger = logger;
            _pipeName = pipeName;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        /// <summary>
        /// Запуск Named Pipe сервера
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _logger.LogInformation("Starting Named Pipe server on pipe: {PipeName}", _pipeName);

            _ = Task.Run(() => ServerLoop(_cts.Token), _cts.Token);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Остановка Named Pipe сервера
        /// </summary>
        public async Task StopAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                await Task.Delay(100);
                _cts.Dispose();
                _cts = null;
            }
            _logger.LogInformation("Named Pipe server stopped");
        }

        private async Task ServerLoop(CancellationToken ct)
        {
            _logger.LogInformation("ServerLoop started for pipe: {PipeName}", _pipeName);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("Waiting for next client connection...");
                    await HandleClientAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("ServerLoop cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Named Pipe server loop");
                    await Task.Delay(1000, ct);
                }
            }

            _logger.LogInformation("ServerLoop stopped");
        }

        private async Task HandleClientAsync(CancellationToken ct)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                _logger.LogDebug("Waiting for GUI client to connect on pipe: {PipeName}...", _pipeName);
                await server.WaitForConnectionAsync(ct);
                _logger.LogInformation("GUI client connected to pipe: {PipeName}", _pipeName);

                OnClientConnected?.Invoke();

                using var reader = new StreamReader(server, leaveOpen: true);
                using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };

                // Отправляем накопленные уведомления
                await SendPendingNotificationsAsync(writer, ct);

                try
                {
                    while (!ct.IsCancellationRequested && server.IsConnected)
                    {
                        var line = await reader.ReadLineAsync();
                        if (line == null)
                        {
                            _logger.LogDebug("Empty line read from client");
                            break;
                        }

                        _logger.LogDebug("Received request line: {LineLength} bytes", line.Length);

                        var request = JsonSerializer.Deserialize<IpcRequest>(line, _jsonOptions);
                        if (request == null)
                        {
                            _logger.LogWarning("Failed to deserialize request: {Line}", line);
                            continue;
                        }

                        _logger.LogInformation("Received request: {RequestType}", request.RequestType);

                        // Обрабатываем запрос
                        var response = OnRequestReceived != null
                            ? await OnRequestReceived.Invoke(request)
                            : null;

                        response ??= new IpcResponse
                        {
                            Success = false,
                            ErrorMessage = "Request not handled"
                        };

                        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                        _logger.LogDebug("Sending response: {Success}", response.Success);
                        await writer.WriteLineAsync(responseJson);
                        await writer.FlushAsync();
                    }
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "IO error while handling client");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Client handling cancelled");
                }
                finally
                {
                    OnClientDisconnected?.Invoke();
                    _logger.LogInformation("GUI client disconnected from pipe: {PipeName}", _pipeName);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("HandleClientAsync cancelled");
            }
            catch (IOException ex) when (ex.Message.Contains("broken") || ex.Message.Contains("closed") || ex.HResult == unchecked((int)0x800700E9))
            {
                _logger.LogDebug("Client disconnected during cleanup (pipe broken — expected)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in HandleClientAsync");
            }
            finally
            {
                server?.Dispose();
            }
        }

        /// <summary>
        /// Отправить push-уведомление подключенному клиенту
        /// </summary>
        public async Task SendNotificationAsync(IpcNotification notification, CancellationToken ct = default)
        {
            lock (_notificationsLock)
            {
                _pendingNotifications.Add(notification);
                // Жёсткий лимит: храним только последние N уведомлений.
                // Без него очередь растёт без ограничений (на политике Deny All
                // блокируется почти весь трафик) и захлёбывает клиента при подключении.
                int overflow = _pendingNotifications.Count - MaxPendingNotifications;
                if (overflow > 0)
                    _pendingNotifications.RemoveRange(0, overflow);
            }

            await Task.CompletedTask;
        }

        private async Task SendPendingNotificationsAsync(StreamWriter writer, CancellationToken ct)
        {
            List<IpcNotification> toSend;
            lock (_notificationsLock)
            {
                toSend = new List<IpcNotification>(_pendingNotifications);
                _pendingNotifications.Clear();
            }

            foreach (var notification in toSend)
            {
                var prefix = "NOTIFY:";
                var json = JsonSerializer.Serialize(notification, _jsonOptions);
                await writer.WriteLineAsync(prefix + json);
                await writer.FlushAsync();
                _logger.LogDebug("Sent notification: {Type}", notification.NotificationType);
            }
        }

        /// <summary>
        /// Отправка данных клиенту (для прямого вызова из GUI)
        /// </summary>
        public static async Task<IpcResponse?> SendRequestAsync(
            string pipeName,
            IpcRequest request,
            CancellationToken ct = default)
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(ct);

            using var reader = new StreamReader(client, leaveOpen: true);
            using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };

            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            });
            await writer.WriteLineAsync(requestJson);

            var responseLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseLine))
                return null;

            return JsonSerializer.Deserialize<IpcResponse>(responseLine, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
