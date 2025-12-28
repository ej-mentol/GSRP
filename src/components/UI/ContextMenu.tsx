import React, { useEffect, useRef, useState } from 'react';
import styles from './ContextMenu.module.css';
import { ChevronRight, Check } from 'lucide-react';

export interface MenuItem {
    id?: string;
    label?: string;
    action?: () => void;
    icon?: React.ReactNode;
    iconColorClass?: 'blueIcon' | 'greenIcon' | 'orangeIcon' | 'redIcon' | 'purpleIcon' | 'pinkIcon';
    separator?: boolean;
    danger?: boolean;
    disabled?: boolean;
    children?: MenuItem[];
    type?: 'checkbox' | 'action';
    checked?: boolean;
}

interface ContextMenuProps {
    x: number;
    y: number;
    items: MenuItem[];
    onClose: () => void;
    depth?: number;
}

export const ContextMenu: React.FC<ContextMenuProps> = ({ x, y, items, onClose, depth = 0 }) => {
    const menuRef = useRef<HTMLDivElement>(null);
    const [activeSubIndex, setActiveSubIndex] = useState<number | null>(null);
    const [subPos, setSubPos] = useState({ x: 0, y: 0 });
    const [adjustedPos, setAdjustedPos] = useState({ top: y, left: x });

    useEffect(() => {
        if (depth === 0) {
            const handleGlobalClick = (e: MouseEvent) => {
                if (menuRef.current && !menuRef.current.contains(e.target as Node)) onClose();
            };
            document.addEventListener('mousedown', handleGlobalClick);
            return () => document.removeEventListener('mousedown', handleGlobalClick);
        }
    }, [onClose, depth]);

    useEffect(() => {
        if (menuRef.current) {
            const rect = menuRef.current.getBoundingClientRect();
            const winHeight = window.innerHeight;
            const winWidth = window.innerWidth;
            let newTop = y;
            let newLeft = x;
            if (y + rect.height > winHeight) newTop = Math.max(10, winHeight - rect.height - 10);
            if (x + rect.width > winWidth) newLeft = Math.max(10, winWidth - rect.width - 10);
            setAdjustedPos({ top: newTop, left: newLeft });
        }
    }, [x, y, items]);

    const handleMouseEnter = (e: React.MouseEvent, hasChildren: boolean, index: number) => {
        if (hasChildren) {
            const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
            let nx = rect.right - 2;
            let ny = rect.top - 4;
            if (nx + 220 > window.innerWidth) nx = rect.left - 220 + 2;
            setSubPos({ x: nx, y: ny });
            setActiveSubIndex(index);
        } else {
            setActiveSubIndex(null);
        }
    };

    return (
        <div
            className={styles.menu}
            style={{ top: adjustedPos.top, left: adjustedPos.left, zIndex: 10000 + depth }}
            ref={menuRef}
            onContextMenu={(e) => e.preventDefault()}
        >
            {items.map((item, index) => {
                if (!item) return null;
                if (item.separator) return <div key={`sep-${index}`} className={styles.separator} />;

                const isCheck = item.type === 'checkbox';
                const colorClass = item.iconColorClass ? styles[item.iconColorClass] : '';
                const hoverClass = item.iconColorClass ? styles[`hover-${item.iconColorClass}`] : '';

                return (
                    <div
                        key={`${depth}-${index}-${item.label}`}
                        className={`
                            ${styles.item} 
                            ${activeSubIndex === index ? styles.itemActive : ''} 
                            ${item.danger ? styles.itemDanger : ''}
                            ${item.disabled ? styles.itemDisabled : ''}
                            ${hoverClass}
                        `}
                        onClick={(e) => {
                            e.stopPropagation();
                            if (item.disabled) return;
                            if (item.action) item.action();
                            if (item.type !== 'checkbox' && !item.children) onClose();
                        }}
                        onMouseEnter={(e) => handleMouseEnter(e, !!item.children, index)}
                    >
                        <div className={styles.itemContent}>
                            <div className={`${styles.itemIcon} ${colorClass}`}>
                                {isCheck ? (
                                    <div className={`${styles.menuCheckbox} ${item.checked ? styles.checked : ''}`}>
                                        {item.checked && <Check size={10} strokeWidth={4} />}
                                    </div>
                                ) : (
                                    item.icon
                                )}
                            </div>
                            <span className={styles.itemLabel}>{item.label}</span>
                        </div>
                        {item.children && <ChevronRight size={12} className={styles.submenuArrow} />}

                        {item.children && activeSubIndex === index && (
                            <ContextMenu
                                x={subPos.x}
                                y={subPos.y}
                                items={item.children}
                                onClose={onClose}
                                depth={depth + 1}
                            />
                        )}
                    </div>
                );
            })}
        </div>
    );
};