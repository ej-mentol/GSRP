import React, { useState } from 'react';
import { Modal } from './Modal';
import { Pipette } from 'lucide-react'; // Assuming lucide-react is installed

interface ColorPickerModalProps {
    isOpen: boolean;
    onClose: () => void;
    initialColor: string;
    onApply: (color: string) => void;
    title?: string;
}

const PRESETS = [
    '#FFFFFF', '#99AAB5', '#2C2F33', '#23272A',
    '#7289DA', '#2ECC71', '#FAA61A', '#F04747',
    '#593695', '#EB459E', '#3B82F6', '#10B981',
    '#EF4444', '#F59E0B', '#8B5CF6', '#EC4899',
    '#3B82F6;#10B981', '#8B5CF6;#EC4899',
    '#F59E0B;#EF4444', '#6366F1;#A855F7',
    '#00C6FF;#0072FF', '#2ECC71;#7289DA'
];

export const ColorPickerModal: React.FC<ColorPickerModalProps> = ({ isOpen, onClose, initialColor, onApply, title = "Pick Color" }) => {
    const [color, setColor] = useState(initialColor || '#3b82f6');
    const [activeSlot, setActiveSlot] = useState<0 | 1>(0); // 0 = Start, 1 = End

    const parts = color.split(';');
    const isGradient = parts.length > 1;
    const c1 = parts[0];
    const c2 = isGradient ? parts[1] : c1;

    // Helper to update specific part of gradient
    const updateColor = (index: 0 | 1, newVal: string) => {
        // If applying a gradient preset (has ';'), take its primary color
        const cleanVal = newVal.includes(';') ? newVal.split(';')[0] : newVal;

        if (!isGradient) {
            if (index === 0) setColor(cleanVal);
            else {
                setColor(`${c1};${cleanVal}`); // Upgrade to gradient
                setActiveSlot(1);
            }
        } else {
            if (index === 0) setColor(`${cleanVal};${c2}`);
            else setColor(`${c1};${cleanVal}`);
        }
    };

    const handleEyeDropper = async () => {
        if ('EyeDropper' in window) {
            try {
                // @ts-ignore
                const eyeDropper = new window.EyeDropper();
                const result = await eyeDropper.open();
                updateColor(activeSlot, result.sRGBHex);
            } catch (e) {
                // User cancelled
            }
        } else {
            alert('Eyedropper not supported in this environment');
        }
    };

    return (
        <Modal title={title} isOpen={isOpen} onClose={onClose}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 20, width: 380 }}>
                
                {/* TEXT PREVIEW */}
                <div style={{
                    width: '100%', height: 70,
                    borderRadius: 12,
                    backgroundColor: '#121214',
                    border: '1px solid var(--border-subtle)',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    fontSize: 24, fontWeight: 800, letterSpacing: 1
                }}>
                    <span style={isGradient ? {
                        background: `linear-gradient(135deg, ${c1}, ${c2})`,
                        backgroundClip: 'text',
                        WebkitBackgroundClip: 'text',
                        color: 'transparent',
                        WebkitTextFillColor: 'transparent',
                        display: 'inline-block'
                    } : { color: c1 }}>
                        PREVIEW NAME
                    </span>
                </div>

                {/* SLOTS SELECTION */}
                <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                    
                    {/* SLOT 1 (Start) */}
                    <div 
                        onClick={() => setActiveSlot(0)}
                        style={{ 
                            flex: 1, padding: 8, borderRadius: 8, 
                            border: `2px solid ${activeSlot === 0 ? 'var(--accent-blue)' : 'transparent'}`,
                            backgroundColor: activeSlot === 0 ? 'rgba(59, 130, 246, 0.1)' : 'transparent',
                            cursor: 'pointer', transition: 'all 0.2s'
                        }}
                    >
                        <label style={{ fontSize: 10, color: activeSlot === 0 ? 'var(--accent-blue)' : 'var(--text-muted)', fontWeight: 700, display: 'block', marginBottom: 6 }}>START COLOR</label>
                        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                            <div style={{ width: 32, height: 32, borderRadius: 6, background: c1, border: '1px solid var(--border-subtle)' }} />
                            <span style={{ fontSize: 12, fontFamily: 'monospace', color: 'var(--text-primary)' }}>{c1}</span>
                        </div>
                    </div>

                    <div style={{ color: 'var(--text-muted)', opacity: isGradient ? 1 : 0.2 }}>➜</div>

                    {/* SLOT 2 (End) */}
                    <div 
                        onClick={() => setActiveSlot(1)}
                        style={{ 
                            flex: 1, padding: 8, borderRadius: 8, 
                            border: `2px solid ${activeSlot === 1 ? 'var(--accent-blue)' : 'transparent'}`,
                            backgroundColor: activeSlot === 1 ? 'rgba(59, 130, 246, 0.1)' : 'transparent',
                            cursor: 'pointer', transition: 'all 0.2s',
                            opacity: isGradient ? 1 : (activeSlot === 1 ? 1 : 0.6)
                        }}
                    >
                        <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                            <label style={{ fontSize: 10, color: activeSlot === 1 ? 'var(--accent-blue)' : 'var(--text-muted)', fontWeight: 700 }}>END COLOR</label>
                            {isGradient && <span onClick={(e) => { e.stopPropagation(); setColor(c1); setActiveSlot(0); }} style={{ fontSize: 10, color: 'var(--accent-red)', cursor: 'pointer' }}>✕</span>}
                        </div>
                        <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 6 }}>
                            {isGradient || activeSlot === 1 ? (
                                <>
                                    <div style={{ width: 32, height: 32, borderRadius: 6, background: c2, border: '1px solid var(--border-subtle)' }} />
                                    <span style={{ fontSize: 12, fontFamily: 'monospace', color: 'var(--text-primary)' }}>{c2}</span>
                                </>
                            ) : (
                                <span style={{ fontSize: 11, color: 'var(--text-muted)', fontStyle: 'italic' }}>(None)</span>
                            )}
                        </div>
                    </div>
                </div>

                {/* TOOLS ROW */}
                <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                    <div style={{ position: 'relative', width: 40, height: 40, borderRadius: 8, overflow: 'hidden', border: '1px solid var(--border-bright)' }}>
                        {/* Native Picker for precise adjustment */}
                        <input type="color" value={activeSlot === 0 ? c1 : c2} onChange={e => updateColor(activeSlot, e.target.value)} 
                               style={{ position: 'absolute', top: -5, left: -5, width: 50, height: 50, cursor: 'pointer', border: 'none' }} />
                    </div>

                    <button 
                        onClick={handleEyeDropper}
                        style={{ 
                            display: 'flex', alignItems: 'center', gap: 8,
                            height: 40, padding: '0 16px', 
                            background: 'var(--bg-secondary)', border: '1px solid var(--border-subtle)', borderRadius: 8,
                            color: 'var(--text-primary)', fontSize: 12, fontWeight: 600, cursor: 'pointer'
                        }}
                        title="Pick color from screen"
                    >
                        <Pipette size={14} /> Pipette
                    </button>
                    
                    <input 
                        type="text" 
                        value={color} 
                        onChange={(e) => setColor(e.target.value)}
                        placeholder="Manual Hex Code"
                        style={{ flex: 1, height: 40, padding: '0 10px', background: 'var(--bg-main)', border: '1px solid var(--border-subtle)', borderRadius: 8, color: '#fff', fontFamily: 'monospace', fontSize: 12 }} 
                    />
                </div>

                {/* PRESETS */}
                <div>
                    <div style={{ fontSize: 10, color: 'var(--text-muted)', marginBottom: 8 }}>
                        PRESETS (Applies to <b>{activeSlot === 0 ? 'START' : 'END'}</b> color)
                    </div>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(8, 1fr)', gap: 8 }}>
                        {PRESETS.map((p, i) => {
                            // Only show gradient in preview if we are setting Start, otherwise just show color? 
                            // Actually showing gradient preview is nice.
                            const pC1 = p.split(';')[0];
                            const pC2 = p.includes(';') ? p.split(';')[1] : pC1;
                            const isPGradient = p.includes(';');
                            
                            return (
                                <button
                                    key={i}
                                    onClick={() => updateColor(activeSlot, pC1)} // Applies to active slot!
                                    style={{
                                        width: '100%', aspectRatio: '1/1', borderRadius: '50%',
                                        background: isPGradient ? `linear-gradient(135deg, ${pC1}, ${pC2})` : pC1,
                                        border: '1px solid rgba(255,255,255,0.1)',
                                        cursor: 'pointer', outline: 'none', padding: 0,
                                        boxShadow: (activeSlot === 0 ? c1 : c2) === pC1 ? '0 0 0 2px #fff' : 'none',
                                        transform: 'scale(1)', transition: 'transform 0.1s'
                                    }}
                                    title={p}
                                />
                            );
                        })}
                    </div>
                </div>

                {/* ACTIONS */}
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 10 }}>
                    <button onClick={() => { onApply(""); onClose(); }} style={{ marginRight: 'auto', background: 'transparent', color: 'var(--accent-red)', border: '1px solid var(--border-subtle)', borderRadius: 6, padding: '8px 12px', fontSize: 12, cursor: 'pointer' }}>Clear</button>
                    <button onClick={onClose} style={{ background: 'var(--bg-secondary)', color: 'var(--text-primary)', border: '1px solid var(--border-subtle)', borderRadius: 6, padding: '8px 16px', fontSize: 12, cursor: 'pointer' }}>Cancel</button>
                    <button onClick={() => { onApply(color); onClose(); }} style={{ background: 'var(--accent-blue)', color: 'white', border: 'none', borderRadius: 6, padding: '8px 20px', fontSize: 12, fontWeight: 600, cursor: 'pointer' }}>Apply</button>
                </div>
            </div>
        </Modal>
    );
};