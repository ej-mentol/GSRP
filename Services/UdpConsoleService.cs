using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GSRP.Services
{
    public enum ConsoleMessageType { UserInput, ServerOutput, System }

    public record ConsoleMessage(string Text, ConsoleMessageType Type, DateTime Timestamp);

    public class UdpConsoleService : IUdpConsoleService
    {
        private UdpClient? _listener;
        private CancellationTokenSource? _cancellationTokenSource;

        public ObservableCollection<ConsoleMessage> ConsoleOutput { get; }
        public bool IsListening { get; private set; }

        public UdpConsoleService()
        {
            ConsoleOutput = new ObservableCollection<ConsoleMessage>();
        }

        public void StartListening(int port)
        {
            if (IsListening) return;

            try
            {
                _listener = new UdpClient(port);
                IsListening = true;
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => ListenForMessages(_cancellationTokenSource.Token));
                Log("UDP Listener started on port " + port, ConsoleMessageType.System);
            }
            catch (Exception ex)
            {
                Log("Error starting listener: " + ex.Message, ConsoleMessageType.System);
                IsListening = false;
            }
        }

        public void StopListening()
        {
            if (!IsListening) return;

            try
            {
                _cancellationTokenSource?.Cancel();
                _listener?.Close();
                _listener?.Dispose();
                Log("UDP Listener stopped.", ConsoleMessageType.System);
            }
            catch (Exception ex)
            {
                Log("Error stopping listener: " + ex.Message, ConsoleMessageType.System);
            }
            finally
            {
                IsListening = false;
                _cancellationTokenSource?.Dispose();
            }
        }

        private async Task ListenForMessages(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await _listener!.ReceiveAsync(token);
                    // Per user hint, check for a prefix to filter messages.
                    // A common pattern is 4 bytes of 0xFF, then a type byte.
                    if (result.Buffer.Length > 5 && result.Buffer[4] == 0x02) 
                    {
                        // Assuming the message is after the header
                        var message = Encoding.UTF8.GetString(result.Buffer, 5, result.Buffer.Length - 5);
                        Log(message, ConsoleMessageType.ServerOutput, result.RemoteEndPoint.ToString());
                    }
                    else
                    {
                        var message = Encoding.UTF8.GetString(result.Buffer);
                        Log(message, ConsoleMessageType.ServerOutput, result.RemoteEndPoint.ToString());
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log("Listener error: " + ex.Message, ConsoleMessageType.System);
                }
            }
        }

        public async Task SendMessage(string message, string address, int port)
        {
            if (string.IsNullOrEmpty(message)) return;

            try
            {
                using var client = new UdpClient();
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                await client.SendAsync(buffer, buffer.Length, address, port);
                Log("Sent: " + message, ConsoleMessageType.UserInput);
            }
            catch (Exception ex)
            {
                Log("Error sending message: " + ex.Message, ConsoleMessageType.System);
            }
        }

        private void Log(string message, ConsoleMessageType type, string source = "System")
        {
            var logEntry = new ConsoleMessage(message, type, DateTime.Now);
            Application.Current.Dispatcher.Invoke(() =>
            {
                ConsoleOutput.Add(logEntry);
                if (ConsoleOutput.Count > 1000) 
                {
                    ConsoleOutput.RemoveAt(0);
                }
            });
        }

        public void Dispose()
        {
            StopListening();
        }
    }
}