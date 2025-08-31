using System;

namespace GSRP.Services
{
    public interface IClipboardService : IDisposable
    {
        event EventHandler<string>? ClipboardChanged;
        bool IsListening { get; }
        void StartListening();
        void StopListening();
    }
}
