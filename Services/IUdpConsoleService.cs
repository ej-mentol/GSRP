using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace GSRP.Services
{
    public interface IUdpConsoleService : IDisposable
    {
        ObservableCollection<ConsoleMessage> ConsoleOutput { get; }
        bool IsListening { get; }
        void StartListening(int port);
        void StopListening();
        Task SendMessage(string message, string address, int port);
    }
}