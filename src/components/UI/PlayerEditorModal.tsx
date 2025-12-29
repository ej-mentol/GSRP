import React, { useState, useEffect } from 'react';
import styles from './PlayerEditorModal.module.css';
import { Player } from '../../types';
import { X, ArrowRight } from 'lucide-react';

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
    const [activeSlot, setActiveSlot] = useState<0 | 1>(0);
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

    useEffect(() => {
        if (!editedPlayer || !activeTarget) return;
        let currentColor = '';
        switch (activeTarget) {
            case 'game': currentColor = editedPlayer.playerColor || ''; break;
            case 'steam': currentColor = editedPlayer.personaNameColor || ''; break;
            case 'alias': currentColor = editedPlayer.aliasColor || ''; break;
            case 'card': currentColor = editedPlayer.cardColor || ''; break;
        }
        if (currentColor && currentColor.includes(';')) {
            const [c1, c2] = currentColor.split(';');
            setHex1(c1); setHex2(c2);
        } else {
            setHex1(currentColor || ''); setHex2('');
            if (activeSlot === 1 && !currentColor) setActiveSlot(0);
        }
    }, [activeTarget]);

    if (!isOpen || !editedPlayer) return null;

    const applyColor = (c: string | null) => {
        if (!activeTarget) return;
        setEditedPlayer(prev => {
            if (!prev) return null;
            const next = { ...prev };
            const finalColor = (c === '' || c === '0' || c === 'none') ? undefined : (c || undefined);
            switch (activeTarget) {
                case 'game': next.playerColor = finalColor; break;
                case 'steam': next.personaNameColor = finalColor; break;
                case 'alias': next.aliasColor = finalColor; break;
                case 'card': next.cardColor = finalColor; break;
            }
            return next;
        });
    };

    const handleHexInputChange = (val1: string, val2: string) => {
        setHex1(val1); setHex2(val2);
        const normalize = (v: string) => {
            const t = v.trim();
            if (!t || t === '#') return '';
            return t.startsWith('#') ? t : '#' + t;
        };
        const c1 = normalize(val1); const c2 = normalize(val2);
        if (!c1 && !c2) applyColor(null);
        else if (c1 && !c2) applyColor(c1);
        else if (!c1 && c2) applyColor(c2); 
        else applyColor(`${c1};${c2}`);
    };

    const updateSpecificSlot = (slot: 0 | 1, color: string) => {
        const cleanColor = color.includes(';') ? color.split(';')[0] : color;
        if (slot === 0) handleHexInputChange(cleanColor, hex2);
        else handleHexInputChange(hex1, cleanColor);
    };

    const getStyle = (color?: string, defaultColor: string = 'var(--text-primary)', isText: boolean = true) => {
        const c = color || defaultColor;
        if (c.includes(';')) {
            const parts = c.split(';');
            const grad = `linear-gradient(135deg, ${parts[0].trim()}, ${parts[1].trim()})`;
            return isText ? { 
                backgroundImage: grad, 
                WebkitBackgroundClip: 'text', 
                backgroundClip: 'text',
                WebkitTextFillColor: 'transparent', 
                color: 'transparent',
                backgroundColor: 'transparent',
                display: 'inline'
            } : { background: grad };
        }
        return isText ? { 
            color: c, 
            backgroundImage: 'none', 
            WebkitBackgroundClip: 'initial', 
            backgroundClip: 'initial',
            WebkitTextFillColor: 'initial',
            display: 'inline'
        } : { backgroundColor: c, backgroundImage: 'none' };
    };

    const renderColorBtn = (c: string) => {
        const isPGradient = c.includes(';');
        const pC1 = c.split(';')[0];
        const pC2 = isPGradient ? c.split(';')[1] : pC1;
        const style = isPGradient ? { background: `linear-gradient(135deg, ${pC1}, ${pC2})` } : { backgroundColor: pC1 };
        return (
            <button key={c} className={styles.colorBtn} style={style} 
                onClick={() => {
                    if (isPGradient) { handleHexInputChange(pC1, pC2); setActiveSlot(0); }
                    else { updateSpecificSlot(0, pC1); setActiveSlot(0); }
                }}
                onContextMenu={(e) => { e.preventDefault(); updateSpecificSlot(1, pC1); setActiveSlot(1); }}
                title={isPGradient ? 'L-Click: Full Gradient | R-Click: Set End' : 'L-Click: Set Start | R-Click: Set End'} />
        );
    };

    const getRegistrationColor = () => {
        if (!editedPlayer.timeCreated || editedPlayer.timeCreated < 100000000) return 'var(--text-muted)';
        const ageInDays = Math.floor((Date.now() - (editedPlayer.timeCreated * 1000)) / 86400000);
        if (ageInDays >= 0 && ageInDays <= 30) return ageInDays <= 7 ? 'var(--accent-red)' : '#f97316';
        return 'var(--text-secondary)';
    };

    const cardBorderColor = editedPlayer.cardColor && editedPlayer.cardColor !== '0' ? editedPlayer.cardColor : ((editedPlayer.numberOfVacBans > 0 || editedPlayer.numberOfGameBans > 0) ? '#ef4444' : 'var(--border-subtle)');
    const getStripeStyle = (cs: string) => {
        if (cs.includes(';')) { const [a, b] = cs.split(';'); return { background: `linear-gradient(to bottom, ${a}, ${b})` }; }
        return { backgroundColor: cs };
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
                        <div className={styles.interactivePreview} style={{ width: '100%', backgroundColor: 'var(--bg-card)', borderRadius: '12px', border: '1px solid var(--border-bright)', padding: '20px 20px 20px 26px', display: 'flex', alignItems: 'center', gap: '20px', position: 'relative', overflow: 'hidden' }}>
                            <div style={{ position: 'absolute', left: 0, top: 0, bottom: 0, width: 6, ...getStripeStyle(cardBorderColor) }} />
                            <div className={`${styles.stripeZone} ${activeTarget === 'card' ? styles.stripeZoneActive : ''}`} style={{ left: 0, width: 12, top: 0, bottom: 0, borderRadius: 0 }} onClick={() => setActiveTarget('card')}>
                                {!!editedPlayer.cardColor && <div className={styles.dotIndicator} style={{ left: 14 }} />}
                            </div>
                            <div style={{ width: 80, height: 80, borderRadius: 12, backgroundColor: '#27272a', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 28, color: '#fff', flexShrink: 0 }}>
                                {editedPlayer.displayName ? editedPlayer.displayName.substring(0, 2).toUpperCase() : '??'}
                            </div>
                            <div style={{ display: 'flex', flexDirection: 'column', gap: 8, flex: 1 }}>
                                <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                                    <span className={`${styles.targetText} ${activeTarget === 'game' ? styles.targetActive : ''}`} onClick={() => setActiveTarget('game')}>
                                        <span style={getStyle(editedPlayer.playerColor, '#f4f4f5', true)}>{editedPlayer.displayName || 'Unnamed'}</span>
                                        {!!editedPlayer.playerColor && <div className={styles.miniDot} />}
                                    </span>
                                    <span className={`${styles.targetBadge} ${activeTarget === 'alias' ? styles.badgeActive : ''}`} style={!editedPlayer.aliasColor || !editedPlayer.aliasColor.includes(';') ? { backgroundColor: editedPlayer.aliasColor || 'var(--accent-blue)' } : {}} onClick={() => setActiveTarget('alias')}>
                                        <span style={editedPlayer.aliasColor && editedPlayer.aliasColor.includes(';') ? getStyle(editedPlayer.aliasColor, '#fff', true) : { color: '#fff' }}>{aliasText || 'ALIAS'}</span>
                                        {!!editedPlayer.aliasColor && <div className={styles.miniDot} style={{ background: '#fff' }} />}
                                    </span>
                                </div>
                                <div style={{ fontSize: 13, fontFamily: 'monospace', color: getRegistrationColor(), opacity: 0.8 }}>{editedPlayer.steamId2}</div>
                                <div className={`${styles.targetText} ${activeTarget === 'steam' ? styles.targetActive : ''}`} onClick={() => setActiveTarget('steam')}>
                                    <span style={getStyle(editedPlayer.personaNameColor, '#60a5fa', true)}>{editedPlayer.personaName || editedPlayer.displayName || 'Unnamed'}</span>
                                    {!!editedPlayer.personaNameColor && <div className={styles.miniDot} />}
                                </div>
                            </div>
                        </div>
                    </div>
                    <div className={styles.paletteArea}>
                        <div className={styles.sectionLabel}>Painting: <b style={{ color: 'var(--accent-blue)' }}>{activeTarget?.toUpperCase()}</b></div>
                        <div className={styles.hexInputGroup}>
                            <div className={`${styles.hexInputWrapper} ${activeSlot === 0 ? styles.slotActive : ''}`} onClick={() => setActiveSlot(0)}>
                                <span className={styles.hexHash} style={{ color: activeSlot === 0 ? 'var(--accent-blue)' : 'inherit' }}>#</span>
                                <input className={styles.hexInput} value={hex1.replace('#','')} onChange={e => handleHexInputChange(e.target.value, hex2)} placeholder="HEX 1" />
                                {hex1 && <button className={styles.removeGradBtn} onClick={(e) => { e.stopPropagation(); handleHexInputChange('', hex2); setActiveSlot(0); }}>✕</button>}
                            </div>
                            <ArrowRight size={14} className={styles.gradientArrow} style={{ opacity: hex2 ? 1 : 0.3 }} />
                            <div className={`${styles.hexInputWrapper} ${activeSlot === 1 ? styles.slotActive : ''}`} onClick={() => setActiveSlot(1)}>
                                <span className={styles.hexHash} style={{ color: activeSlot === 1 ? 'var(--accent-blue)' : 'inherit' }}>#</span>
                                <input className={styles.hexInput} value={hex2.replace('#','')} onChange={e => handleHexInputChange(hex1, e.target.value)} placeholder={hex2 ? "HEX 2" : "ADD"} />
                                {hex2 && <button className={styles.removeGradBtn} onClick={(e) => { e.stopPropagation(); handleHexInputChange(hex1, ''); setActiveSlot(0); }}>✕</button>}
                            </div>
                        </div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', padding: '0 4px', marginTop: -8, marginBottom: 4 }}>
                            <span style={{ fontSize: 9, color: 'var(--text-muted)', fontWeight: 800, letterSpacing: '0.05em' }}>START</span>
                            <span style={{ fontSize: 9, color: 'var(--text-muted)', fontWeight: 800, letterSpacing: '0.05em' }}>STOP</span>
                        </div>
                        <div className={styles.colorGrid}>{allColors.map(renderColorBtn)}<div className={styles.divider} />{gradients.map(renderColorBtn)}</div>
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
                    <button className={`${styles.btn} ${styles.btnSave}`} onClick={() => { onSave({ ...editedPlayer, alias: aliasText }); onClose(); }}>Save Changes</button>
                </div>
            </div>
        </div>
    );
};