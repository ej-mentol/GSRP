import React, { useState, useEffect } from 'react';
import styles from './PlayerEditorModal.module.css';
import { Player } from '../../types';
import { X, Eraser, Save, PaintBucket, MousePointer2 } from 'lucide-react';

interface PlayerEditorModalProps {
    isOpen: boolean;
    onClose: () => void;
    player: Player | null;
    favoriteColors: string[];
    onSave: (updatedPlayer: Player) => void;
}

type TargetType = 'game' | 'steam' | 'alias' | 'card';

export const PlayerEditorModal: React.FC<PlayerEditorModalProps> = ({
    isOpen,
    onClose,
    player,
    favoriteColors,
    onSave
}) => {
    const [editedPlayer, setEditedPlayer] = useState<Player | null>(null);
    const [activeTarget, setActiveTarget] = useState<TargetType | null>(null);
    const [pickedColor, setPickedColor] = useState<string>(''); // For inputs visual only
    const [aliasText, setAliasText] = useState('');

    const discordColors = ['#5865F2', '#57F287', '#FEE75C', '#ED4245', '#EB459E', '#FFFFFF', '#000000'];
    const gradients = [
        '#5865F2;#EB459E', '#FEE75C;#ED4245', '#57F287;#3b82f6', '#3b82f6;#a855f7'
    ];
    const allColors = [...favoriteColors, ...discordColors.filter(c => !favoriteColors.includes(c))];

    useEffect(() => {
        if (player) {
            setEditedPlayer({ ...player });
            setAliasText(player.alias || '');
            setActiveTarget('game'); // Default selection
        }
    }, [player]);

    // Update inputs when target changes or player color changes
    useEffect(() => {
        if (!editedPlayer || !activeTarget) return;
        
        let currentColor = undefined;
        switch (activeTarget) {
            case 'game': currentColor = editedPlayer.playerColor; break;
            case 'steam': currentColor = editedPlayer.personaNameColor; break;
            case 'alias': currentColor = editedPlayer.aliasColor; break;
            case 'card': currentColor = editedPlayer.cardColor; break;
        }
        setPickedColor(currentColor || '');
    }, [activeTarget, editedPlayer]);

    if (!isOpen || !editedPlayer) return null;

    const handleApplyColor = (color: string | undefined) => {
        if (!activeTarget) return;
        
        setEditedPlayer(prev => {
            if (!prev) return null;
            const next = { ...prev };
            switch (activeTarget) {
                case 'game': next.playerColor = color; break;
                case 'steam': next.personaNameColor = color; break;
                case 'alias': next.aliasColor = color; break;
                case 'card': next.cardColor = color; break;
            }
            return next;
        });
    };

    const handleHexChange = (val: string) => {
        setPickedColor(val);
        // Try to apply if valid hex
        if (val.startsWith('#') && (val.length === 4 || val.length === 7)) {
            handleApplyColor(val);
        } else if (val.includes(';')) {
            handleApplyColor(val); // Gradient manual input
        }
    };

    const handleSave = () => {
        if (editedPlayer) {
            onSave({ ...editedPlayer, alias: aliasText });
        }
        onClose();
    };

    const getStyle = (color?: string, defaultColor: string = 'var(--text-primary)', isText: boolean = true) => {
        const c = color || defaultColor;
        if (c.includes(';')) {
            const parts = c.split(';');
            const grad = `linear-gradient(135deg, ${parts[0]}, ${parts[1]})`;
            return isText ? { background: grad, WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent', display: 'inline-block' } : { background: grad };
        }
        return isText ? { color: c } : { backgroundColor: c };
    };

    // Helper to render color button
    const renderColorBtn = (c: string) => {
        const style = c.includes(';') 
            ? { background: `linear-gradient(135deg, ${c.split(';')[0]}, ${c.split(';')[1]})` }
            : { backgroundColor: c };
        
        // Highlight logic: is this color currently applied to the active target?
        // Simplifying: just show it as clickable.
        
        return (
            <button
                key={c}
                className={styles.colorBtn}
                style={style}
                onClick={() => handleApplyColor(c)}
                title={c}
            />
        );
    };

    const cardBorderColor = editedPlayer.cardColor && editedPlayer.cardColor !== '0' 
        ? editedPlayer.cardColor 
        : ((editedPlayer.numberOfVacBans > 0 || editedPlayer.numberOfGameBans > 0) ? '#ef4444' : 'var(--border-subtle)');

    // Selection Ring Style
    const selectionStyle = {
        boxShadow: '0 0 0 2px var(--bg-card), 0 0 0 4px var(--accent-blue)',
        borderRadius: '4px',
        zIndex: 20
    };

    return (
        <div className={styles.overlay} onClick={onClose}>
            <div className={styles.modal} onClick={e => e.stopPropagation()}>
                <div className={styles.header}>
                    <div className={styles.title}>Customize Appearance</div>
                    <button className={styles.closeButton} onClick={onClose}><X size={20} /></button>
                </div>

                <div className={styles.content}>
                    <div className={styles.previewArea}>
                        <div className={styles.sectionLabel} style={{ marginBottom: 12, textAlign: 'center', opacity: 0.7 }}>
                            <MousePointer2 size={12} style={{ display: 'inline', marginRight: 4 }}/>
                            Select an element to edit
                        </div>
                        
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
                            {/* Card Stripe Selection Overlay */}
                            <div 
                                style={{ 
                                    position: 'absolute', left: -4, top: -4, bottom: -4, width: 16, 
                                    cursor: 'pointer', zIndex: 10,
                                    border: activeTarget === 'card' ? '2px solid var(--accent-blue)' : 'none',
                                    borderRadius: '4px'
                                }}
                                onClick={() => setActiveTarget('card')}
                                title="Select Stripe"
                            />

                            <div style={{ width: 64, height: 64, borderRadius: 10, backgroundColor: '#27272a', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 20, color: '#fff' }}>
                                {editedPlayer.displayName.substring(0, 2).toUpperCase()}
                            </div>
                            
                            <div style={{ display: 'flex', flexDirection: 'column', gap: 4, flex: 1 }}>
                                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                                    <span 
                                        style={{ 
                                            fontSize: 17, fontWeight: 800, cursor: 'pointer', transition: 'all 0.2s', 
                                            ...getStyle(editedPlayer.playerColor, '#f4f4f5'),
                                            ...(activeTarget === 'game' ? { textDecoration: 'underline', textDecorationColor: 'var(--accent-blue)', textUnderlineOffset: '4px' } : {})
                                        }}
                                        onClick={(e) => { e.stopPropagation(); setActiveTarget('game'); }}
                                        title="Select Name"
                                    >
                                        {editedPlayer.displayName}
                                    </span>
                                    {aliasText && (
                                        <span 
                                            style={{ 
                                                fontSize: 11, fontWeight: 800, padding: '2px 8px', borderRadius: 4, 
                                                cursor: 'pointer', transition: 'all 0.2s',
                                                color: '#fff',
                                                ...getStyle(editedPlayer.aliasColor, '#3b82f6', false),
                                                ...(activeTarget === 'alias' ? selectionStyle : {})
                                            }}
                                            onClick={(e) => { e.stopPropagation(); setActiveTarget('alias'); }}
                                            title="Select Alias Badge"
                                        >
                                            {aliasText}
                                        </span>
                                    )}
                                </div>
                                <div style={{ fontSize: 12, fontFamily: 'monospace', color: 'var(--text-secondary)' }}>
                                    {editedPlayer.steamId2}
                                </div>
                                <div 
                                    style={{ 
                                        fontSize: 13, fontWeight: 500, cursor: 'pointer', transition: 'all 0.2s', 
                                        ...getStyle(editedPlayer.personaNameColor, '#60a5fa'),
                                        ...(activeTarget === 'steam' ? { textDecoration: 'underline', textDecorationColor: 'var(--accent-blue)', textUnderlineOffset: '4px' } : {})
                                    }}
                                    onClick={(e) => { e.stopPropagation(); setActiveTarget('steam'); }}
                                    title="Select Persona"
                                >
                                    {editedPlayer.personaName || editedPlayer.displayName}
                                </div>
                            </div>
                        </div>
                    </div>

                    <div className={styles.paletteArea}>
                        <div className={styles.sectionLabel} style={{ display: 'flex', justifyContent: 'space-between' }}>
                            <span>Apply Color to Selection</span>
                            {activeTarget && <span style={{ color: 'var(--accent-blue)' }}>Selected: {activeTarget.toUpperCase()}</span>}
                        </div>
                        
                        <div className={styles.hexInputGroup}>
                            <div className={styles.hexInputWrapper}>
                                <span className={styles.hexHash}>#</span>
                                <input 
                                    className={styles.hexInput} 
                                    value={pickedColor} 
                                    onChange={e => handleHexChange(e.target.value)} 
                                    placeholder="HEX or HEX1;HEX2"
                                />
                            </div>
                        </div>

                        <div className={styles.colorGrid}>
                            <button 
                                className={`${styles.eraserBtn} ${!pickedColor ? styles.eraserBtnActive : ''}`}
                                onClick={() => handleApplyColor(undefined)}
                                title="Reset Selection to Default"
                            >
                                <Eraser size={16} />
                            </button>
                            {allColors.map(renderColorBtn)}
                            <div className={styles.divider} style={{ width: 1, height: 24, background: 'var(--border-subtle)', margin: '0 4px' }}></div>
                            {gradients.map(renderColorBtn)}
                        </div>
                    </div>

                    <div className={styles.paletteArea}>
                        <div className={styles.sectionLabel}>Alias Text</div>
                        <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
                            <input type="text" className={styles.aliasInput} value={aliasText} onChange={e => setAliasText(e.target.value)} placeholder="Enter custom alias..." />
                            {aliasText && <button className={styles.clearAliasBtn} onClick={() => setAliasText('')}><X size={14} /></button>}
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
