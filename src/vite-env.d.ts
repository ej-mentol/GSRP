/// <reference types="vite/client" />

interface Window {
  ipcRenderer: {
    send(channel: string, ...args: any[]): void;
    on(channel: string, func: (...args: any[]) => void): void;
    off(channel: string, func: (...args: any[]) => void): void;
    invoke(channel: string, ...args: any[]): Promise<any>;
    onBackend(callback: (msg: any) => void): () => void;
    sendToBackend(type: string, payload: any): void;
  };
}
