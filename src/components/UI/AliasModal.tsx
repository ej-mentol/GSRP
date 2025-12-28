import React, { useState, useEffect } from 'react';
import { Modal } from './Modal';
import { Player } from '../../types';

interface AliasModalProps {
    isOpen: boolean;
    player: Player | null;
    onClose: () => void;
    onApply: (alias: string) => void;
}

export const AliasModal: React.FC<AliasModalProps> = ({ isOpen, player, onClose, onApply }) => {
    const [alias, setAlias] = useState('');

    useEffect(() => {
        if (isOpen && player) {
            setAlias(player.alias || '');
        }
    }, [isOpen, player]);

    if (!player) return null;

    return (
        <Modal title={`Set Alias: ${player.displayName}`} isOpen={isOpen} onClose={onClose}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                    <span style={{ fontSize: 12, color: 'var(--text-secondary)', fontWeight: 600 }}>ENTER ALIAS:</span>
                    <input
                        type="text"
                        autoFocus
                        style={{
                            width: '100%',
                            height: 44,
                            backgroundColor: 'var(--bg-main)',
                            border: '1px solid var(--border-bright)',
                            borderRadius: 8,
                            color: 'var(--text-primary)',
                            fontSize: 16,
                            padding: '0 12px',
                            outline: 'none'
                        }}
                        placeholder="e.g. Admin, Cheater, etc."
                        value={alias}
                        onChange={(e) => setAlias(e.target.value)}
                        onKeyDown={(e) => {
                            if (e.key === 'Enter') {
                                onApply(alias);
                                onClose();
                            }
                        }}
                    />
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 4 }}>
                    <button
                        onClick={() => { onApply(''); onClose(); }}
                        style={{
                            background: 'transparent',
                            color: 'var(--accent-red)',
                            border: '1px solid var(--accent-red)',
                            padding: '8px 16px',
                            borderRadius: 6,
                            cursor: 'pointer',
                            fontSize: 13,
                            marginRight: 'auto'
                        }}
                    >
                        Clear Alias
                    </button>
                    <button
                        onClick={onClose}
                        style={{
                            background: 'transparent',
                            color: 'var(--text-secondary)',
                            border: '1px solid var(--border-subtle)',
                            padding: '8px 16px',
                            borderRadius: 6,
                            cursor: 'pointer',
                            fontSize: 13
                        }}
                    >
                        Cancel
                    </button>
                    <button
                        onClick={() => { onApply(alias); onClose(); }}
                        style={{
                            background: 'var(--accent-blue)',
                            color: 'white',
                            border: 'none',
                            padding: '8px 24px',
                            borderRadius: 6,
                            cursor: 'pointer',
                            fontWeight: 600,
                            fontSize: 13
                        }}
                    >
                        Save Alias
                    </button>
                </div>
            </div>
        </Modal>
    );
};
