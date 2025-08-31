using System;
using System.Threading;
using System.Windows;

namespace GSRP.Services
{
    public class SingleInstanceService : ISingleInstanceService, IDisposable
    {
        private readonly Mutex _mutex;
        private bool _hasHandle;
        private bool _disposed;

        public SingleInstanceService()
        {
            const string mutexName = "Global\\GSRP_Application_Singleton_Mutex";
            _mutex = new Mutex(true, mutexName, out _hasHandle);
        }

        public bool IsFirstInstance()
        {
            if (_disposed)
                return false;

            if (_hasHandle)
                return true;

            try
            {
                _hasHandle = _mutex.WaitOne(TimeSpan.Zero);
                if (!_hasHandle)
                {
                    MessageBox.Show("GSRP is already running.", "Application Already Running", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return _hasHandle;
            }
            catch (AbandonedMutexException)
            {
                _hasHandle = true;
                return true;
            }
        }

        public void ReleaseInstance()
        {
            if (_hasHandle && !_disposed)
            {
                _mutex.ReleaseMutex();
                _hasHandle = false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            ReleaseInstance();
            _mutex?.Dispose();
            _disposed = true;
        }
    }
}