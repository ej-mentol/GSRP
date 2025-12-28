import React, { useState, useEffect } from 'react';
import styles from './PlayerEditorModal.module.css';
import { Player } from '../../types';
import { X, Eraser, ArrowRight } from 'lucide-react';

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
    const [activeTarget, setActiveTarget] = useState<TargetType | null>('game');
    const [hex1, setHex1] = useState('');
    const [hex2, setHex2] = useState('');
    const [aliasText, setAliasText] = useState('');

    const discordColors = ['#5865F2', '#57F287', '#FEE75C', '#ED4245', '#EB459E', '#FFFFFF'];
    const gradients = [
        '#5865F2;#EB459E', '#FEE75C;#ED4245', '#57F287;#3b82f6', '#3b82f6;#a855f7'
    ];
    const allColors = [...favoriteColors, ...discordColors.filter(c => !favoriteColors.includes(c))];

    useEffect(() => {
        if (player) {
            setEditedPlayer({ ...player });
            setAliasText(player.alias || '');
        }
    }, [player]);

    // Sync HEX inputs when target or player data changes
    useEffect(() => {
        if (!editedPlayer || !activeTarget) return;
        
        let currentColor = '';
        switch (activeTarget) {
            case 'game': currentColor = editedPlayer.playerColor || ''; break;
            case 'steam': currentColor = editedPlayer.personaNameColor || ''; break;
            case 'alias': currentColor = editedPlayer.aliasColor || ''; break;
            case 'card': currentColor = editedPlayer.cardColor || ''; break;
        }

        if (currentColor.includes(';')) {
            const [c1, c2] = currentColor.split(';');
            setHex1(c1);
            setHex2(c2);
        } else {
            setHex1(currentColor);
            setHex2('');
        }
    }, [activeTarget, editedPlayer]);

    if (!isOpen || !editedPlayer) return null;

    const applyColor = (c: string | undefined) => {
        if (!activeTarget) return;
        setEditedPlayer(prev => {
            if (!prev) return null;
            const next = { ...prev };
            switch (activeTarget) {
                case 'game': next.playerColor = c; break;
                case 'steam': next.personaNameColor = c; break;
                case 'alias': next.aliasColor = c; break;
                case 'card': next.cardColor = c; break;
            }
            return next;
        });
    };

    const handleHexInputChange = (val1: string, val2: string) => {
        setHex1(val1);
        setHex2(val2);
        
        const clean1 = val1.startsWith('#') ? val1 : (val1 ? '#' + val1 : '');
        const clean2 = val2.startsWith('#') ? val2 : (val2 ? '#' + val2 : '');

        if (!clean1) {
            applyColor(undefined);
        } else {
            applyColor(clean2 ? `${clean1};${clean2}` : clean1);
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

    const renderColorBtn = (c: string) => {
        const style = c.includes(';') 
            ? { background: `linear-gradient(135deg, ${c.split(';')[0]}, ${c.split(';')[1]})` }
            : { backgroundColor: c };
        
        return (
            <button key={c} className={styles.colorBtn} style={style} onClick={() => applyColor(c)} title={c} />
        );
    };

    const cardBorderColor = editedPlayer.cardColor && editedPlayer.cardColor !== '0' 
        ? editedPlayer.cardColor 
        : ((editedPlayer.numberOfVacBans > 0 || editedPlayer.numberOfGameBans > 0) ? '#ef4444' : 'var(--border-subtle)');

    const isCustomized = (target: TargetType) => {
        switch(target) {
            case 'game': return !!editedPlayer.playerColor;
            case 'steam': return !!editedPlayer.personaNameColor;
            case 'alias': return !!editedPlayer.aliasColor;
            case 'card': return !!editedPlayer.cardColor;
        }
    };

    return (
        <div className={styles.overlay} onClick={onClose}>
            <div className={styles.modal} onClick={e => e.stopPropagation()}>
                <div className={styles.header}>
                    <div className={styles.title}>Customize: {editedPlayer.displayName}</div>
                    <button className={styles.closeButton} onClick={onClose}><X size={20} /></button>
                </div>

                <div className={styles.content}>
                    <div className={styles.previewArea}>
                        <div 
                            className={styles.interactivePreview}
                            style={{
                                width: '100%',
                                backgroundColor: 'var(--bg-card)',
                                borderRadius: '12px',
                                border: '1px solid var(--border-bright)',
                                padding: '20px',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '20px',
                                borderLeft: `6px solid ${cardBorderColor}`,
                                position: 'relative',
                                transition: 'all 0.2s'
                            }}
                        >
                            {/* STRIPE TARGET */}
                            <div 
                                className={`${styles.stripeZone} ${activeTarget === 'card' ? styles.stripeZoneActive : ''}`}
                                onClick={() => setActiveTarget('card')}
                            >
                                {isCustomized('card') && <div className={styles.dotIndicator} style={{ left: 8 }} />}
                            </div>

                            <div style={{ width: 80, height: 80, borderRadius: 12, backgroundColor: '#27272a', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 28, color: '#fff', flexShrink: 0 }}>
                                {editedPlayer.displayName.substring(0, 2).toUpperCase()}
                            </div>
                            
                            <div style={{ display: 'flex', flexDirection: 'column', gap: 8, flex: 1 }}>
                                <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                                    <span 
                                        className={`${styles.targetText} ${activeTarget === 'game' ? styles.targetActive : ''}`}
                                        style={getStyle(editedPlayer.playerColor, '#f4f4f5')}
                                        onClick={() => setActiveTarget('game')}
                                    >
                                        {editedPlayer.displayName}
                                        {isCustomized('game') && <div className={styles.miniDot} />}
                                    </span>
                                    
                                    <span 
                                        className={`${styles.targetBadge} ${activeTarget === 'alias' ? styles.badgeActive : ''}`}
                                        style={getStyle(editedPlayer.aliasColor, 'var(--accent-blue)', false)}
                                        onClick={() => setActiveTarget('alias')}
                                    >
                                        {aliasText || 'ALIAS'}
                                        {isCustomized('alias') && <div className={styles.miniDot} style={{ background: '#fff' }} />}
                                    </span>
                                </div>

                                <div style={{ fontSize: 13, fontFamily: 'monospace', color: '#ef4444', opacity: 0.8 }}>{editedPlayer.steamId2}</div>

                                <div 
                                    className={`${styles.targetText} ${activeTarget === 'steam' ? styles.targetActive : ''}`}
                                    style={getStyle(editedPlayer.personaNameColor, '#60a5fa')}
                                    onClick={() => setActiveTarget('steam')}
                                >
                                    {editedPlayer.personaName || editedPlayer.displayName}
                                    {isCustomized('steam') && <div className={styles.miniDot} />}
                                </div>
                            </div>
                        </div>
                    </div>

                    <div className={styles.paletteArea}>
                        <div className={styles.sectionLabel} style={{ display: 'flex', justifyContent: 'space-between' }}>
                            <span>Painting: <b style={{ color: 'var(--accent-blue)' }}>{activeTarget?.toUpperCase()}</b></span>
                            {isCustomized(activeTarget!) && <span style={{ fontSize: 10, color: 'var(--accent-green)' }}>Customized</span>}
                        </div>
                        
                        <div className={styles.hexInputGroup}>
                            <div className={styles.hexInputWrapper}>
                                <span className={styles.hexHash}>#</span>
                                <input className={styles.hexInput} value={hex1.replace('#','')} onChange={e => handleHexInputChange(e.target.value, hex2)} placeholder="HEX 1" />
                            </div>
                            <ArrowRight size={14} className={styles.gradientArrow} />
                            <div className={styles.hexInputWrapper}>
                                <span className={styles.hexHash}>#</span>
                                <input className={styles.hexInput} value={hex2.replace('#','')} onChange={e => handleHexInputChange(hex1, e.target.value)} placeholder="HEX 2" />
                            </div>
                        </div>

                        <div className={styles.colorGrid}>
                            <button className={`${styles.eraserBtn} ${!isCustomized(activeTarget!) ? styles.eraserBtnActive : ''}`} onClick={() => applyColor(undefined)} title="Reset to Default"><Eraser size={16} /></button>
                            {allColors.map(renderColorBtn)}
                            <div className={styles.divider} />
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
                    <button className={`${styles.btn} ${styles.btnSave}`} onClick={handleSave}>Save Changes</button>
                </div>
            </div>
        </div>
    );
};
