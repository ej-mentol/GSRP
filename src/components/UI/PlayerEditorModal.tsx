import React, { useState, useEffect } from 'react';
import styles from './PlayerEditorModal.module.css';
import { Player } from '../../types';
import { X, Eraser, Save, PaintBucket } from 'lucide-react';

interface PlayerEditorModalProps {
    isOpen: boolean;
    onClose: () => void;
    player: Player | null;
    favoriteColors: string[];
    onSave: (updatedPlayer: Player) => void;
}

export const PlayerEditorModal: React.FC<PlayerEditorModalProps> = ({
    isOpen,
    onClose,
    player,
    favoriteColors,
    onSave
}) => {
    const [editedPlayer, setEditedPlayer] = useState<Player | null>(null);
    const [activeColor, setActiveColor] = useState<string | null>(favoriteColors[0] || '#3b82f6');
    const [aliasText, setAliasText] = useState('');

    const discordColors = ['#5865F2', '#57F287', '#FEE75C', '#ED4245', '#EB459E', '#FFFFFF', '#000000'];
    const gradients = [
        '#5865F2;#EB459E', // Blurple -> Fuchsia
        '#FEE75C;#ED4245', // Fire
        '#57F287;#3b82f6', // Nature
        '#3b82f6;#a855f7'  // Cosmic
    ];

    const allColors = [...favoriteColors, ...discordColors.filter(c => !favoriteColors.includes(c))];

    useEffect(() => {
        if (player) {
            setEditedPlayer({ ...player });
            setAliasText(player.alias || '');
        }
    }, [player]);

    if (!isOpen || !editedPlayer) return null;

    const handlePaint = (target: 'game' | 'steam' | 'alias' | 'card') => {
        setEditedPlayer(prev => {
            if (!prev) return null;
            const next = { ...prev };
            // NULL means reset to system constant
            const colorVal = activeColor === null ? undefined : activeColor;

            switch (target) {
                case 'game': next.playerColor = colorVal; break;
                case 'steam': next.personaNameColor = colorVal; break;
                case 'alias': next.aliasColor = colorVal; break;
                case 'card': next.cardColor = colorVal; break;
            }
            return next;
        });
    };

    const handleSave = () => {
        if (editedPlayer) {
            onSave({ ...editedPlayer, alias: aliasText });
        }
        onClose();
    };

    const getStyle = (color?: string, defaultColor: string = 'var(--text-primary)', isText: boolean = true) => {
        const activeColorVal = color || defaultColor;
        if (activeColorVal.includes(';')) {
            const parts = activeColorVal.split(';');
            const grad = `linear-gradient(135deg, ${parts[0]}, ${parts[1]})`;
            return isText ? { background: grad, WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent', display: 'inline-block' } : { background: grad };
        }
        return isText ? { color: activeColorVal } : { backgroundColor: activeColorVal };
    };

    const renderColorBtn = (c: string) => {
        const style = c.includes(';') 
            ? { background: `linear-gradient(135deg, ${c.split(';')[0]}, ${c.split(';')[1]})` }
            : { backgroundColor: c };
        
        return (
            <button
                key={c}
                className={`${styles.colorBtn} ${activeColor === c ? styles.colorBtnActive : ''}`}
                style={style}
                onClick={() => setActiveColor(c)}
                title={c}
            />
        );
    };

    const cardBorderColor = editedPlayer.cardColor && editedPlayer.cardColor !== '0' 
        ? editedPlayer.cardColor 
        : ((editedPlayer.numberOfVacBans > 0 || editedPlayer.numberOfGameBans > 0) ? '#ef4444' : 'var(--border-subtle)');

    return (
        <div className={styles.overlay} onClick={onClose}>
            <div className={styles.modal} onClick={e => e.stopPropagation()}>
                <div className={styles.header}>
                    <div className={styles.title}>Customize Appearance: {editedPlayer.displayName}</div>
                    <button className={styles.closeButton} onClick={onClose}><X size={20} /></button>
                </div>

                <div className={styles.content}>
                    <div className={styles.paletteArea}>
                        <div className={styles.sectionLabel}>Palette & Tool</div>
                        <div className={styles.colorGrid}>
                            <button 
                                className={`${styles.eraserBtn} ${activeColor === null ? styles.eraserBtnActive : ''}`}
                                onClick={() => setActiveColor(null)}
                                title="Eraser (Reset to Default)"
                            >
                                <Eraser size={16} />
                            </button>
                            {allColors.map(renderColorBtn)}
                            <div className={styles.divider} style={{ width: 1, height: 24, background: 'var(--border-subtle)', margin: '0 4px' }}></div>
                            {gradients.map(renderColorBtn)}
                        </div>
                    </div>

                    <div className={styles.previewArea}>
                        <div 
                            className={styles.interactivePreview}
                            style={{
                                width: '100%',
                                backgroundColor: 'var(--bg-card)',
                                borderRadius: '12px',
                                border: '1px solid var(--border-subtle)',
                                padding: '12px 16px',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '16px',
                                borderLeft: `4px solid ${cardBorderColor}`,
                                transition: 'all 0.2s ease',
                                position: 'relative'
                            }}
                        >
                            {/* Stripe Click Zone */}
                            <div 
                                style={{ position: 'absolute', left: 0, top: 0, bottom: 0, width: 12, cursor: 'pointer', zIndex: 10 }}
                                onClick={() => handlePaint('card')}
                                className={styles.hoverZone}
                                title="Paint Stripe"
                            />

                            <div style={{ width: 64, height: 64, borderRadius: 10, backgroundColor: '#27272a', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 20, color: '#fff' }}>
                                {editedPlayer.displayName.substring(0, 2).toUpperCase()}
                            </div>
                            
                            <div style={{ display: 'flex', flexDirection: 'column', gap: 4, flex: 1 }}>
                                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                                    <span 
                                        style={{ fontSize: 17, fontWeight: 800, cursor: 'pointer', transition: 'all 0.2s', ...getStyle(editedPlayer.playerColor, '#f4f4f5') }}
                                        onClick={(e) => { e.stopPropagation(); handlePaint('game'); }}
                                        className={styles.hoverText}
                                    >
                                        {editedPlayer.displayName}
                                    </span>
                                    {aliasText && (
                                        <span 
                                            style={{ 
                                                fontSize: 11, fontWeight: 800, padding: '2px 8px', borderRadius: 4, 
                                                cursor: 'pointer', transition: 'all 0.2s',
                                                color: '#fff',
                                                ...getStyle(editedPlayer.aliasColor, '#3b82f6', false)
                                            }}
                                            onClick={(e) => { e.stopPropagation(); handlePaint('alias'); }}
                                            className={styles.hoverElement}
                                        >
                                            {aliasText}
                                        </span>
                                    )}
                                </div>
                                <div style={{ fontSize: 13, fontFamily: 'monospace', color: '#ef4444' }}>{editedPlayer.steamId2}</div>
                                <div 
                                    style={{ fontSize: 13, fontWeight: 500, cursor: 'pointer', transition: 'all 0.2s', ...getStyle(editedPlayer.personaNameColor, '#60a5fa') }}
                                    onClick={(e) => { e.stopPropagation(); handlePaint('steam'); }}
                                    className={styles.hoverText}
                                >
                                    {editedPlayer.personaName || editedPlayer.displayName}
                                </div>
                            </div>
                        </div>
                    </div>

                    <div className={styles.paletteArea}>
                        <div className={styles.sectionLabel}>Alias Text</div>
                        <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
                            <input type="text" className={styles.aliasInput} value={aliasText} onChange={e => setAliasText(e.target.value)} placeholder="Enter custom alias..." />
                            {aliasText && (
                                <button className={styles.clearAliasBtn} onClick={() => setAliasText('')}><X size={14} /></button>
                            )}
                        </div>
                    </div>
                </div>

                <div className={styles.footer}>
                    <button className={`${styles.btn} ${styles.btnCancel}`} onClick={onClose}>Cancel</button>
                    <button className={`${styles.btn} ${styles.btnSave}`} onClick={handleSave}>
                        <Save size={16} style={{ display: 'inline-block', verticalAlign: 'middle', marginRight: 6 }} />
                        Save Changes
                    </button>
                </div>
            </div>
        </div>
    );
};
