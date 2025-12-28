import React from 'react';
import styles from './WindowControls.module.css';
import { Minus, Square, X } from 'lucide-react';

export const WindowControls: React.FC = () => {
    
    const sendCommand = (action: 'minimize' | 'maximize' | 'close') => {
        console.log(`UI: Triggering window-control: ${action}`);
        if (!window.ipcRenderer) {
            console.error('UI: ipcRenderer is not available on window object!');
        }
        window.ipcRenderer?.send('window-control', action);
    };

    return (
        <div className={styles.container}>
            <div className={styles.button} onClick={() => sendCommand('minimize')} title="Minimize">
                <svg width="12" height="12" viewBox="0 0 12 12">
                    <rect x="2" y="10" width="8" height="1.2" fill="currentColor" />
                </svg>
            </div>
            <div className={styles.button} onClick={() => sendCommand('maximize')} title="Maximize">
                <Square size={14} strokeWidth={1.5} />
            </div>
            <div className={`${styles.button} ${styles.closeButton}`} onClick={() => sendCommand('close')} title="Close">
                <X size={16} strokeWidth={1.5} />
            </div>
        </div>
    );
};
