import React, { useState, useEffect, useRef } from 'react';
import styles from './ConsoleView.module.css';
import { Trash2, ChevronRight } from 'lucide-react';

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

export const ConsoleView: React.FC<ConsoleViewProps> = ({
    targetIp = '127.0.0.1',
    targetPort = 26001
}) => {
    const [messages, setMessages] = useState<LogMessage[]>([
        { id: 1, timestamp: new Date().toLocaleTimeString('ru-RU', { hour12: false }), tags: new Set(['SYS']), text: 'Console Integrated & Connected', receivedAt: Date.now() },
    ]);
    
    const [visibleTags, setVisibleTags] = useState<Set<LogTag>>(new Set(['CHAT', 'GAME', 'NET', 'SYS', 'STUFF', 'RAW', 'USER']));
    const [inputValue, setInputValue] = useState('');
    const [history, setHistory] = useState<string[]>([]);
    const [historyIndex, setHistoryIndex] = useState(-1);
    const [autoScroll, setAutoScroll] = useState(true);
    const [isConnected, setIsConnected] = useState(false);
    const logEndRef = useRef<HTMLDivElement>(null);
    const logAreaRef = useRef<HTMLDivElement>(null);

    const toggleConnection = () => {
        if (isConnected) {
            window.ipcRenderer?.sendToBackend('UDP_STOP', {});
            setIsConnected(false);
            addMessage('UDP Listener Stopped', 'SYS');
        } else {
            // Assuming listen port is targetPort - 1 or same as setting. 
            // In GSRP typical setup: Listen on 26000, Send to 26001.
            // Let's use 26000 as the standard listen port.
            window.ipcRenderer?.sendToBackend('UDP_START', { port: 26000 });
            setIsConnected(true);
            addMessage('UDP Listener Starting on port 26000...', 'SYS');
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

        // Send to Backend
        window.ipcRenderer?.sendToBackend('SEND_UDP', { 
            ip: targetIp, 
            port: targetPort, 
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
                        {isConnected ? 'Stop Chat' : 'Connect Chat'}
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
