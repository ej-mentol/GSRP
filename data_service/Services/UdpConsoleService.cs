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
        private Task? _listenTask;
        private readonly Action<string, object?> _sendToElectron;
        private bool _isListening;

        public UdpConsoleService(Action<string, object?> sendToElectron)
        {
            _sendToElectron = sendToElectron;
        }

        public void Start(int port, string bindIp = "0.0.0.0")
        {
            if (_isListening) Stop();

            try
            {
                var ipAddress = string.IsNullOrEmpty(bindIp) ? IPAddress.Any : IPAddress.Parse(bindIp);
                _listener = new UdpClient();
                _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Client.Bind(new IPEndPoint(ipAddress, port));
                
                _cts = new CancellationTokenSource();
                _isListening = true;
                _listenTask = Task.Run(() => ReceiveLoop(_cts.Token));
                _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"UDP Listener started on {ipAddress}:{port}"));
            }
            catch (Exception ex)
            {
                _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"Failed to start UDP on {bindIp}:{port}: {ex.Message}"));
            }
        }

        public void Stop()
        {
            if (!_isListening) return;
            _isListening = false;

            try
            {
                _cts?.Cancel();
                _listener?.Close();
                _listener?.Dispose();
                _listener = null;

                if (_listenTask != null)
                {
                    _listenTask.Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch { }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _listenTask = null;
            }
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

                    // DEBUG: Log raw packet arrival
                    _sendToElectron("CONSOLE_LOG", new LogData("RAW", $"[UDP] Packet received: {bytes.Length} bytes from {result.RemoteEndPoint}. First byte: 0x{bytes[0]:X2}"));

                    string tag = "RAW";
                    int payloadStart = 0;
                    
                    if (bytes.Length >= 9) 
                    {
                        byte firstByte = bytes[0];
                        payloadStart = 9; // Tag (1) + SteamID (8)
                        
                        switch (firstByte)
                        {
                            case 0x12: tag = "CHAT"; break;
                            case 0x13: tag = "GAME"; break;
                            case 0x14: tag = "NET"; break;
                            case 0x15: tag = "SYS"; break;
                            case 0x16: tag = "STUFF"; break;
                            default:
                                tag = "RAW";
                                payloadStart = 0; 
                                break;
                        }
                    }
                    else
                    {
                        tag = "RAW";
                        payloadStart = 0;
                    }

                    string text = bytes.Length > payloadStart 
                        ? Sanitize(Encoding.UTF8.GetString(bytes, payloadStart, bytes.Length - payloadStart)) 
                        : "";

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
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c >= 32 || c == '\n' || c == '\r' || c == '\t' || (c >= 0x01 && c <= 0x04)) sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        public void Dispose() => Stop();
    }
}
