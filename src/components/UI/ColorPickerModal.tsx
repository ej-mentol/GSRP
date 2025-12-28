import React, { useState } from 'react';
import { Modal } from './Modal';

interface ColorPickerModalProps {
    isOpen: boolean;
    onClose: () => void;
    initialColor: string;
    onApply: (color: string) => void;
    title?: string;
}

const PRESETS = [
    '#FFFFFF', '#99AAB5', '#2C2F33', '#23272A',
    '#7289DA', '#2ECC71', '#FAA61A', '#F04747', // Added #2ECC71
    '#593695', '#EB459E', '#3B82F6', '#10B981',
    '#EF4444', '#F59E0B', '#8B5CF6', '#EC4899',
    // Gradients
    '#3B82F6;#10B981', '#8B5CF6;#EC4899',
    '#F59E0B;#EF4444', '#6366F1;#A855F7',
    '#00C6FF;#0072FF', '#2ECC71;#7289DA' // Added Discord Green Gradient
];

export const ColorPickerModal: React.FC<ColorPickerModalProps> = ({ isOpen, onClose, initialColor, onApply, title = "Pick Color" }) => {
    const [color, setColor] = useState(initialColor || '#3b82f6');

    const getPreviewStyle = () => {
        if (color.includes(';')) {
            const parts = color.split(';');
            return { background: `linear-gradient(135deg, ${parts[0].trim()}, ${parts[1].trim()})` };
        }
        return { backgroundColor: color };
    };

    return (
        <Modal title={title} isOpen={isOpen} onClose={onClose}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 24, width: 380 }}>
                {/* PREVIEW BOX */}
                <div
                    style={{
                        width: '100%',
                        height: 64,
                        borderRadius: 12,
                        ...getPreviewStyle(),
                        border: '1px solid rgba(255,255,255,0.1)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontSize: 16,
                        fontWeight: 800,
                        color: '#fff',
                        textShadow: '0 2px 4px rgba(0,0,0,0.5)',
                        letterSpacing: 1,
                        boxShadow: 'inset 0 0 20px rgba(0,0,0,0.2)'
                    }}
                >
                    {color.toUpperCase()}
                </div>

                {/* PRESETS GRID */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 10, width: '100%' }}>
                    {PRESETS.map((p, i) => (
                        <button
                            key={i}
                            onClick={() => setColor(p)}
                            style={{
                                width: '100%',
                                height: 18,
                                borderRadius: 9,
                                border: color === p ? '2px solid #fff' : '1px solid rgba(255,255,255,0.05)',
                                cursor: 'pointer',
                                padding: 0,
                                outline: 'none',
                                background: p.includes(';')
                                    ? `linear-gradient(135deg, ${p.split(';')[0]}, ${p.split(';')[1]})`
                                    : p,
                                transition: 'all 0.1s ease',
                                transform: color === p ? 'scale(1.05)' : 'scale(1)'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.transform = 'scale(1.1)';
                                e.currentTarget.style.borderColor = 'rgba(255,255,255,0.3)';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.transform = color === p ? 'scale(1.05)' : 'scale(1)';
                                e.currentTarget.style.borderColor = color === p ? '#fff' : '1px solid rgba(255,255,255,0.05)';
                            }}
                            title={p}
                        />
                    ))}
                </div>

                {/* INPUT AREA */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                    <div style={{ display: 'flex', gap: 8 }}>
                        <div style={{ position: 'relative', width: 44, height: 44, borderRadius: 8, overflow: 'hidden', border: '1px solid var(--border-bright)', flexShrink: 0 }}>
                            <input
                                type="color"
                                value={color.split(';')[0]}
                                onChange={(e) => setColor(e.target.value)}
                                style={{
                                    position: 'absolute',
                                    top: -10, left: -10,
                                    width: 64, height: 64,
                                    cursor: 'pointer',
                                    border: 'none'
                                }}
                            />
                        </div>
                        <input
                            type="text"
                            placeholder="#HEX or #HEX;#HEX"
                            style={{
                                flex: 1,
                                height: 44,
                                backgroundColor: 'var(--bg-main)',
                                border: '1px solid var(--border-bright)',
                                borderRadius: 8,
                                color: 'var(--text-primary)',
                                fontSize: 13,
                                fontFamily: 'JetBrains Mono',
                                padding: '0 12px',
                                outline: 'none'
                            }}
                            value={color}
                            onChange={(e) => setColor(e.target.value)}
                        />
                    </div>
                    <span style={{ fontSize: 10, color: 'var(--text-muted)', textAlign: 'center' }}>
                        Tip: Use <b>;</b> to create a gradient (e.g. #FF0000;#0000FF)
                    </span>
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, width: '100%', marginTop: 10 }}>
                    <button
                        onClick={() => { onApply(""); onClose(); }}
                        style={{
                            background: 'transparent',
                            color: 'var(--accent-red)',
                            border: '1px solid rgba(239, 68, 68, 0.3)',
                            padding: '10px 16px',
                            borderRadius: 8,
                            cursor: 'pointer',
                            fontSize: 12,
                            fontWeight: 600,
                            marginRight: 'auto'
                        }}
                    >
                        Reset
                    </button>
                    <button
                        onClick={onClose}
                        style={{
                            background: 'rgba(255,255,255,0.05)',
                            color: 'var(--text-secondary)',
                            border: '1px solid var(--border-subtle)',
                            padding: '10px 20px',
                            borderRadius: 8,
                            cursor: 'pointer',
                            fontSize: 12,
                            fontWeight: 600
                        }}
                    >
                        Cancel
                    </button>
                    <button
                        onClick={() => { onApply(color); onClose(); }}
                        style={{
                            background: 'var(--accent-blue)',
                            color: 'white',
                            border: 'none',
                            padding: '10px 24px',
                            borderRadius: 8,
                            cursor: 'pointer',
                            fontWeight: 700,
                            fontSize: 12,
                            boxShadow: '0 4px 12px rgba(59, 130, 246, 0.3)'
                        }}
                    >
                        Apply
                    </button>
                </div>
            </div>
        </Modal>
    );
};
