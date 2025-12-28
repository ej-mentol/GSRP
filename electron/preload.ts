import { ipcRenderer, contextBridge } from 'electron'

// --------- Expose some API to the Renderer process ---------
contextBridge.exposeInMainWorld('ipcRenderer', {
  on(...args: Parameters<typeof ipcRenderer.on>) {
    const [channel, listener] = args
    return ipcRenderer.on(channel, (event, ...args) => listener(event, ...args))
  },
  off(...args: Parameters<typeof ipcRenderer.off>) {
    const [channel, listener] = args
    return ipcRenderer.off(channel, listener)
  },
  send(...args: Parameters<typeof ipcRenderer.send>) {
    const [channel, ...omit] = args
    return ipcRenderer.send(channel, ...omit)
  },
  invoke(...args: Parameters<typeof ipcRenderer.invoke>) {
    const [channel, ...omit] = args
    return ipcRenderer.invoke(channel, ...omit)
  },
  
  onBackend: (callback: (msg: any) => void) => {
    const subscription = (_: any, message: string) => {
        try { callback(JSON.parse(message)); } catch (e) { console.error('IPC Parse Error', e); }
    }
    ipcRenderer.on('backend-message', subscription);
    return () => ipcRenderer.removeListener('backend-message', subscription);
  },
  
  sendToBackend(type: string, payload: any) {
    ipcRenderer.send('to-backend', JSON.stringify({ type, payload }));
  }
})