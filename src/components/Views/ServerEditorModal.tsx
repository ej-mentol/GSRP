import React, { useState } from 'react';
import { Modal } from '../UI/Modal';

interface ServerEditorModalProps {
    isOpen: boolean;
    onClose: () => void;
    currentServers: string[];
    onSave: (newServers: string[]) => void;
}

export const ServerEditorModal: React.FC<ServerEditorModalProps> = ({ isOpen, onClose, currentServers, onSave }) => {
    const [text, setText] = useState(currentServers.join('\n'));

    const handleSave = () => {
        const list = text.split('\n').map(s => s.trim()).filter(s => s.length > 0);
        onSave(list);
        onClose();
    };

    return (
        <Modal title="Edit Server List" isOpen={isOpen} onClose={onClose}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>
                    Enter each server name on a new line (Memo style).
                </span>
                <textarea 
                    style={{ 
                        width: '100%', 
                        height: '300px', 
                        backgroundColor: 'var(--bg-main)', 
                        border: '1px solid var(--border-bright)',
                        borderRadius: '8px',
                        color: 'var(--text-primary)',
                        padding: '12px',
                        fontFamily: 'inherit',
                        fontSize: '14px',
                        outline: 'none',
                        resize: 'none'
                    }}
                    value={text}
                    onChange={e => setText(e.target.value)}
                    placeholder="Official Server #1&#10;Community Server #2..."
                />
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12 }}>
                    <button 
                        onClick={onClose}
                        style={{ 
                            background: 'transparent', 
                            color: 'var(--text-primary)', 
                            border: '1px solid var(--border-bright)',
                            padding: '8px 16px',
                            borderRadius: '6px',
                            cursor: 'pointer'
                        }}
                    >
                        Cancel
                    </button>
                    <button 
                        onClick={handleSave}
                        style={{ 
                            background: 'var(--accent-blue)', 
                            color: 'white', 
                            border: 'none',
                            padding: '8px 24px',
                            borderRadius: '6px',
                            cursor: 'pointer',
                            fontWeight: 600
                        }}
                    >
                        Save List
                    </button>
                </div>
            </div>
        </Modal>
    );
};
