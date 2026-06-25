using System;
using System.Buffers.Binary;
using System.IO;
using Microsoft.Extensions.Logging;

namespace FireWallService.PacketCapture
{
    /// <summary>
    /// PCAP файл для записи пакетов (совместим с Wireshark)
    /// </summary>
    public class PcapWriter : IDisposable
    {
        private FileStream _fileStream;
        private BinaryWriter _writer;
        private readonly ILogger<PcapWriter> _logger;
        private bool _disposed = false;
        private int _packetCount = 0;

        // PCAP Global Header (24 bytes)
        // Magic: 0xA1B2C3D4
        // Version: 2.4
        // Link type: 103 (Raw IPv4/IPv6)
        private static readonly byte[] PcapGlobalHeader = new byte[]
        {
            0xD4, 0xC3, 0xB2, 0xA1,  // Magic number (little-endian: 0xA1B2C3D4)
            0x02, 0x00,              // Major version: 2
            0x04, 0x00,              // Minor version: 4
            0x00, 0x00, 0x00, 0x00,  // Time zone: 0
            0x00, 0x00, 0x00, 0x00,  // Timestamp accuracy: 0
            0xFF, 0xFF, 0x00, 0x00,  // Max packet length: 65535
            0x67, 0x00, 0x00, 0x00   // Link type: 103 (LINKTYPE_RAW)
        };

        public string FilePath { get; }
        public int PacketCount => _packetCount;

        public PcapWriter(string filePath, ILogger<PcapWriter> logger)
        {
            FilePath = filePath;
            _logger = logger;

            // Создаём директорию если нужно
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_fileStream);

            // Записываем PCAP Global Header
            _writer.Write(PcapGlobalHeader);
            _fileStream.Flush();

            _logger.LogInformation("PCAP file created: {FilePath}", filePath);
        }

        /// <summary>
        /// Записать пакет в PCAP файл
        /// </summary>
        /// <param name="packet">Данные пакета</param>
        /// <param name="timestamp">Время захвата</param>
        public void WritePacket(byte[] packet, DateTime timestamp)
        {
            if (_disposed)
                return;

            if (packet == null || packet.Length == 0)
                return;

            // PCAP Packet Header (16 bytes):
            // Timestamp seconds (4)
            // Timestamp microseconds (4)
            // Captured length (4)
            // Original length (4)

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var utcTimestamp = timestamp.ToUniversalTime();
            var totalSeconds = (utcTimestamp - epoch).TotalSeconds;
            var seconds = (uint)totalSeconds;
            var microseconds = (uint)((totalSeconds - seconds) * 1_000_000);

            // Записываем Packet Header
            _writer.Write(seconds);
            _writer.Write(microseconds);
            _writer.Write((uint)packet.Length);  // Captured length
            _writer.Write((uint)packet.Length);  // Original length

            // Записываем данные пакета
            _writer.Write(packet);

            _packetCount++;

            // Флушим каждые 100 пакетов для производительности
            if (_packetCount % 100 == 0)
            {
                _fileStream.Flush();
            }
        }

        /// <summary>
        /// Принудительно сохранить все данные на диск
        /// </summary>
        public void Flush()
        {
            if (!_disposed)
            {
                _fileStream.Flush();
            }
        }

        /// <summary>
        /// Закрыть и удалить PCAP файл
        /// </summary>
        public void Delete()
        {
            Dispose();
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                    _logger.LogInformation("PCAP file deleted: {FilePath}", FilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete PCAP file: {FilePath}", FilePath);
            }
        }

        /// <summary>
        /// Закрыть и создать новый PCAP файл (ротация)
        /// </summary>
        public void Rotate()
        {
            Flush();
            Dispose();

            // Добавляем timestamp к имени для архива
            var dir = Path.GetDirectoryName(FilePath) ?? ".";
            var name = Path.GetFileNameWithoutExtension(FilePath);
            var ext = Path.GetExtension(FilePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var newPath = Path.Combine(dir, $"{name}_{timestamp}{ext}");

            if (File.Exists(FilePath))
            {
                File.Move(FilePath, newPath);
                _logger.LogInformation("PCAP file rotated: {NewPath}", newPath);
            }

            // Создаём новый файл
            _fileStream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_fileStream);
            _writer.Write(PcapGlobalHeader);
            _packetCount = 0;
            _disposed = false;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Flush();
                _writer.Dispose();
                _fileStream.Dispose();
                _disposed = true;
            }
        }
    }
}
