using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GSRP.Daemon.Core;

namespace GSRP.Daemon.Services
{
    public class UdpConsoleService : IDisposable
    {
        private UdpClient? _listener;
        private CancellationTokenSource? _cts;
        private readonly Action<string, object?> _sendToElectron;
        private bool _isListening;

        public UdpConsoleService(Action<string, object?> sendToElectron)
        {
            _sendToElectron = sendToElectron;
        }

        public void Start(int port)
        {
            if (_isListening) Stop();

            try
            {
                _listener = new UdpClient(port);
                _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _cts = new CancellationTokenSource();
                _isListening = true;
                _ = Task.Run(() => ReceiveLoop(_cts.Token));
                _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"UDP Listener started on port {port}"));
            }
            catch (Exception ex)
            {
                _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"Failed to start UDP: {ex.Message}"));
            }
        }

        public void Stop()
        {
            _isListening = false;
            _cts?.Cancel();
            _listener?.Close();
            _listener?.Dispose();
            _listener = null;
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null)
            {
                try
                {
                    var result = await _listener.ReceiveAsync(token);
                    var bytes = result.Buffer;
                    if (bytes.Length == 0) continue;

                    string tag = "RAW";
                    string text;

                    // Protocol: First byte is tag
                    byte firstByte = bytes[0];
                    int payloadStart = 1;

                    switch (firstByte)
                    {
                        case 0x01: tag = "LOG"; break;
                        case 0x02: tag = "CHAT"; break;
                        case 0x03: tag = "GAME"; break;
                        case 0x04: tag = "NET"; break;
                        case 0x05: tag = "SYS"; break;
                        case 0x06: tag = "STUFF"; break;
                        default:
                            tag = "RAW";
                            payloadStart = 0; // No tag byte, treat whole as text
                            break;
                    }

                    if (bytes.Length > payloadStart)
                    {
                        text = Encoding.UTF8.GetString(bytes, payloadStart, bytes.Length - payloadStart);
                        text = Sanitize(text);
                    }
                    else
                    {
                        text = "";
                    }

                    if (!string.IsNullOrEmpty(text))
                    {
                        _sendToElectron("CONSOLE_LOG", new LogData(tag, text));
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (_isListening)
                        _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"UDP Error: {ex.Message}"));
                }
            }
        }

        public async Task SendMessageAsync(string ip, int port, string message)
        {
            try
            {
                using var client = new UdpClient();
                var data = Encoding.UTF8.GetBytes(message);
                await client.SendAsync(data, data.Length, ip, port);
                _sendToElectron("CONSOLE_LOG", new LogData("USER", message));
            }
            catch (Exception ex)
            {
                _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"Send Error: {ex.Message}"));
            }
        }

        private string Sanitize(string input)
        {
            // Professional Sanitization:
            // Allow Printable ASCII (32-126)
            // Allow all Unicode (c > 127) for Cyrillic/International support
            // Allow GoldSource color codes (0x01-0x04)
            // Allow standard whitespace (\n, \r, \t)
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c >= 32 || c == '\n' || c == '\r' || c == '\t' || (c >= 0x01 && c <= 0x04))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
