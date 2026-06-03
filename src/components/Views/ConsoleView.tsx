import React, { useState, useEffect, useRef } from 'react';
import styles from './ConsoleView.module.css';
import { Trash2, ChevronRight, Settings as SettingsIcon } from 'lucide-react';

type LogTag = 'CHAT' | 'GAME' | 'NET' | 'SYS' | 'STUFF' | 'RAW' | 'USER' | 'LOG';

interface LogMessage {
    id: number;
    timestamp: string;
    tags: Set<LogTag>;
    text: string;
    receivedAt: number;
}

const TAG_ORDER: LogTag[] = ['SYS', 'NET', 'GAME', 'CHAT', 'STUFF', 'USER', 'LOG', 'RAW'];

// GoldSource Color Mapping
const GS_COLORS: Record<number, string> = {
    0x01: 'var(--text-primary)',   // Standard
    0x03: '#60a5fa',               // Team (Blueish for admin)
    0x04: '#10b981',               // Green
};

interface ConsoleViewProps {
    targetIp?: string;
    targetPort?: number;
}

export const ConsoleView: React.FC<ConsoleViewProps> = () => {
    const [messages, setMessages] = useState<LogMessage[]>([
        { id: 1, timestamp: new Date().toLocaleTimeString('ru-RU', { hour12: false }), tags: new Set(['SYS']), text: 'Console Integrated & Connected', receivedAt: Date.now() },
    ]);
    
    const [visibleTags, setVisibleTags] = useState<Set<LogTag>>(new Set(['CHAT', 'GAME', 'NET', 'SYS', 'STUFF', 'RAW', 'USER', 'LOG']));
    const [inputValue, setInputValue] = useState('');
    const [history, setHistory] = useState<string[]>([]);
    const [historyIndex, setHistoryIndex] = useState(-1);
    const [autoScroll, setAutoScroll] = useState(true);
    const [isConnected, setIsConnected] = useState(false);
    const [localIps, setLocalIps] = useState<string[]>(['0.0.0.0', '127.0.0.1']);
    const [selectedBindIp, setSelectedBindIp] = useState('127.0.0.1');
    
    // Persistent Console-specific settings
    const [customIp, setCustomIp] = useState('');
    const [listenPort, setListenPort] = useState(26000);
    const [sendPort, setSendPort] = useState(26001);
    const [showAdvanced, setShowAdvanced] = useState(false);

    const logEndRef = useRef<HTMLDivElement>(null);
    const logAreaRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const loadConsoleSettings = async () => {
            const cIp = await window.ipcRenderer?.invoke('get-setting', 'console_custom_ip');
            const lPort = await window.ipcRenderer?.invoke('get-setting', 'console_listen_port');
            const sPort = await window.ipcRenderer?.invoke('get-setting', 'console_send_port');
            const lastIp = await window.ipcRenderer?.invoke('get-setting', 'console_last_bind_ip');
            
            if (cIp) setCustomIp(cIp);
            if (lPort) setListenPort(Number(lPort));
            if (sPort) setSendPort(Number(sPort));
            if (lastIp) setSelectedBindIp(lastIp);
        };

        const unsubscribe = window.ipcRenderer?.onBackend((msg) => {
            if (msg.type === 'CONSOLE_LOG' && msg.data) {
                addMessage(msg.data.text, msg.data.tag);
            } else if (msg.type === 'LOCAL_IPS_RESULT') {
                if (msg.data?.ips) setLocalIps(msg.data.ips);
            }
        });
        
        loadConsoleSettings();
        window.ipcRenderer?.sendToBackend('GET_LOCAL_IPS', {});
        return () => unsubscribe?.();
    }, []);

    const toggleConnection = () => {
        if (isConnected) {
            window.ipcRenderer?.sendToBackend('UDP_STOP', {});
            setIsConnected(false);
            addMessage('UDP Listener Stopped', 'SYS');
        } else {
            const finalIp = selectedBindIp === 'custom' ? customIp : selectedBindIp;
            if (selectedBindIp === 'custom' && !customIp.trim()) {
                addMessage('Error: Please enter a valid IP address', 'SYS');
                return;
            }

            // Persist current selection
            window.ipcRenderer?.send('save-setting', { key: 'console_last_bind_ip', value: selectedBindIp });
            if (selectedBindIp === 'custom') {
                window.ipcRenderer?.send('save-setting', { key: 'console_custom_ip', value: customIp });
            }
            window.ipcRenderer?.send('save-setting', { key: 'console_listen_port', value: listenPort });
            window.ipcRenderer?.send('save-setting', { key: 'console_send_port', value: sendPort });

            window.ipcRenderer?.sendToBackend('UDP_START', { port: listenPort, ip: finalIp });
            setIsConnected(true);
            addMessage(`UDP Listener Starting on ${finalIp}:${listenPort}...`, 'SYS');
        }
    };

    const handleScroll = () => {
        if (!logAreaRef.current) return;
        const { scrollTop, scrollHeight, clientHeight } = logAreaRef.current;
        const isAtBottom = scrollHeight - scrollTop - clientHeight < 50;
        setAutoScroll(isAtBottom);
    };

    useEffect(() => {
        if (autoScroll) {
            logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
        }
    }, [messages, visibleTags]);

    const addMessage = (text: string, tag: LogTag) => {
        if (!text) return;
        const now = Date.now();
        const timeWindow = 100;

        setMessages((prev: LogMessage[]) => {
            const duplicateIndex = prev.findLastIndex((m: LogMessage) => 
                m.text === text && (now - m.receivedAt) < timeWindow
            );

            if (duplicateIndex !== -1) {
                const updated = [...prev];
                const target = { ...updated[duplicateIndex] };
                target.tags = new Set([...Array.from(target.tags), tag]);
                updated[duplicateIndex] = target;
                return updated;
            }

            // Limit message history to prevent UI lag (e.g. 1000 items)
            const next = [...prev, {
                id: now + Math.random(),
                timestamp: new Date().toLocaleTimeString('ru-RU', { hour12: false }),
                tags: new Set([tag]),
                text: text,
                receivedAt: now
            }];
            return next.slice(-1000);
        });
    };

    const handleSend = (text: string) => {
        if (!text.trim()) return;
        if (text === 'clear') { setMessages([]); setInputValue(''); return; }
        
        // Add to history
        if (history.length === 0 || history[history.length - 1] !== text) {
            setHistory(prev => [...prev, text]);
        }
        setHistoryIndex(-1);

        // Send to Backend - Use persistent sendPort
        window.ipcRenderer?.sendToBackend('SEND_UDP', { 
            ip: selectedBindIp === 'custom' ? customIp : selectedBindIp, 
            port: sendPort, 
            message: text 
        });

        // Optimistic local add
        addMessage(text, 'USER');
        setInputValue('');
        setAutoScroll(true); // Always scroll on user command
    };

    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
        if (e.key === 'ArrowUp') {
            e.preventDefault();
            if (history.length > 0) {
                const newIndex = historyIndex === -1 ? history.length - 1 : Math.max(0, historyIndex - 1);
                setHistoryIndex(newIndex);
                setInputValue(history[newIndex]);
            }
        } else if (e.key === 'ArrowDown') {
            e.preventDefault();
            if (historyIndex !== -1) {
                const newIndex = historyIndex + 1;
                if (newIndex < history.length) {
                    setHistoryIndex(newIndex);
                    setInputValue(history[newIndex]);
                } else {
                    setHistoryIndex(-1);
                    setInputValue('');
                }
            }
        } else if (e.key === 'Tab') {
            e.preventDefault();
            if (inputValue) {
                const matches = [
                    "say ", "status", "echo ", "connect ", "disconnect", "quit", "name ", 
                    "map ", "retry", "kill", "say_team ", "snapshot", "clear"
                ].filter((opt: string) => opt.startsWith(inputValue));
                if (matches.length === 1) setInputValue(matches[0]);
                else if (matches.length > 1) {
                    // Find common prefix
                    const shortest = matches.reduce((a: string, b: string) => a.length <= b.length ? a : b);
                    let prefix = inputValue;
                    for (let i = 0; i < shortest.length; i++) {
                        if (matches.every((m: string) => m.startsWith(shortest.slice(0, i + 1)))) {
                            prefix = shortest.slice(0, i + 1);
                        } else break;
                    }
                    setInputValue(prefix);
                }
            }
        }
    };

    // --- GOLDSOURCE COLOR PARSER ---
    const formatText = (text: string) => {
        if (!text) return null;
        const result = [];
        let currentColor = 'var(--text-primary)';

        for (let i = 0; i < text.length; i++) {
            const code = text.charCodeAt(i);
            
            if (GS_COLORS[code]) {
                currentColor = GS_COLORS[code];
                continue; // Skip the byte itself
            }

            if ((code < 32 && !['\n', '\r', '\t'].includes(text[i])) || code === 127) {
                result.push(<span key={i} className={styles.nonPrintable}>[${code.toString(16).toUpperCase().padStart(2, '0')}]</span>);
            } else {
                result.push(<span key={i} style={{ color: currentColor }}>{text[i]}</span>);
            }
        }
        return result;
    };

    const filteredMessages = messages.filter(m => 
        Array.from(m.tags).some(tag => visibleTags.has(tag))
    );

    return (
        <div className={styles.container}>
            <div className={styles.toolbar}>
                <div className={styles.filterGroup}>
                    <button 
                        className={`${styles.tagFilter} ${isConnected ? styles.connected : styles.disconnected}`}
                        onClick={toggleConnection}
                    >
                        {isConnected ? 'Disconnect' : 'Connect'}
                    </button>

                    <button 
                        className={`${styles.iconButton} ${showAdvanced ? styles.active : ''}`}
                        onClick={() => setShowAdvanced(!showAdvanced)}
                        title="Network Settings"
                    >
                        <SettingsIcon size={14} />
                    </button>

                    <div className={styles.divider} />
                    {(['CHAT', 'GAME', 'NET', 'SYS', 'STUFF', 'USER', 'LOG'] as LogTag[]).map(tag => (
                        <button 
                            key={tag}
                            className={`${styles.tagFilter} ${visibleTags.has(tag) ? styles.tagFilterActive : ''} ${styles['tag' + tag]}`}
                            onClick={() => {
                                const next = new Set(visibleTags);
                                if (next.has(tag)) next.delete(tag); else next.add(tag);
                                setVisibleTags(next);
                            }}
                        >
                            {tag}
                        </button>
                    ))}
                </div>
                <button className={styles.clearButton} onClick={() => setMessages([])}>
                    <Trash2 size={12} /> Clear
                </button>
            </div>

            {showAdvanced && (
                <div className={styles.advancedPanel}>
                    <div className={styles.field}>
                        <label>Interface:</label>
                        <select 
                            className={styles.interfaceSelect}
                            value={selectedBindIp}
                            onChange={(e) => setSelectedBindIp(e.target.value)}
                            disabled={isConnected}
                        >
                            {localIps.map(ip => <option key={ip} value={ip}>{ip}</option>)}
                            <option value="custom">Custom...</option>
                        </select>
                    </div>

                    {selectedBindIp === 'custom' && (
                        <div className={styles.field}>
                            <label>Custom IP:</label>
                            <input type="text" value={customIp} onChange={(e) => setCustomIp(e.target.value)} placeholder="e.g. 1.2.3.4" disabled={isConnected} />
                        </div>
                    )}
                    
                    <div className={styles.field}>
                        <label>Listen:</label>
                        <input type="number" value={listenPort} onChange={(e) => setListenPort(Number(e.target.value))} disabled={isConnected} />
                    </div>
                    
                    <div className={styles.field}>
                        <label>Target:</label>
                        <input type="number" value={sendPort} onChange={(e) => setSendPort(Number(e.target.value))} />
                    </div>
                </div>
            )}

            <div className={styles.logArea} ref={logAreaRef} onScroll={handleScroll}>
                {filteredMessages.map(msg => (
                    <div key={msg.id} className={styles.message}>
                        <span className={styles.timestamp}>[{msg.timestamp}]</span>
                        <div className={styles.tagList}>
                            {TAG_ORDER.filter(t => msg.tags.has(t)).map(t => (
                                <span key={t} className={`${styles.tag} ${styles['tag' + t]}`}>[{t}]</span>
                            ))}
                        </div>
                        <span className={styles.text}>{formatText(msg.text)}</span>
                    </div>
                ))}
                <div ref={logEndRef} />
                
                {!autoScroll && (
                    <button className={styles.scrollDownButton} onClick={() => setAutoScroll(true)}>
                        New messages below
                    </button>
                )}
            </div>

            <form className={styles.inputArea} onSubmit={(e) => { e.preventDefault(); handleSend(inputValue); }}>
                <ChevronRight className={styles.prompt} size={18} />
                <input 
                    type="text" 
                    className={styles.consoleInput} 
                    placeholder="Type server command..."
                    value={inputValue}
                    onKeyDown={handleKeyDown}
                    onChange={(e) => setInputValue(e.target.value)}
                />
            </form>
        </div>
    );
};
