import React from 'react';
import { 
    Copy, Clipboard, Scissors, Trash2, CheckSquare, RotateCcw
} from 'lucide-react';
import { MenuItem } from '../components/UI/ContextMenu';

export const buildInputMenu = (
    element: HTMLInputElement | HTMLTextAreaElement,
    onUpdate: () => void
): MenuItem[] => {
    const hasSelection = element.selectionStart !== element.selectionEnd;
    const hasText = element.value.length > 0;

    return [
        { 
            label: 'Undo', 
            icon: React.createElement(RotateCcw, { size: 14 }),
            disabled: false, 
            action: () => { element.focus(); document.execCommand('undo'); } 
        },
        { separator: true },
        { 
            label: 'Cut', 
            icon: React.createElement(Scissors, { size: 14 }),
            disabled: !hasSelection, 
            action: () => {
                element.focus();
                document.execCommand('cut');
                onUpdate();
            } 
        },
        { 
            label: 'Copy', 
            icon: React.createElement(Copy, { size: 14 }),
            disabled: !hasSelection, 
            action: () => {
                element.focus();
                document.execCommand('copy');
            } 
        },
        { 
            label: 'Paste', 
            icon: React.createElement(Clipboard, { size: 14 }),
            action: async () => {
                element.focus();
                const text = await navigator.clipboard.readText();
                document.execCommand('insertText', false, text);
                onUpdate();
            } 
        },
        { separator: true },
        { 
            label: 'Select All', 
            icon: React.createElement(CheckSquare, { size: 14 }),
            disabled: !hasText,
            action: () => {
                element.focus();
                element.select();
            } 
        },
        { 
            label: 'Clear All', 
            icon: React.createElement(Trash2, { size: 14 }),
            danger: true,
            disabled: !hasText,
            action: () => {
                const nativeInputValueSetter = Object.getOwnPropertyDescriptor(
                    element instanceof HTMLTextAreaElement ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype,
                    "value"
                )?.set;
                nativeInputValueSetter?.call(element, "");
                element.dispatchEvent(new Event("input", { bubbles: true }));
                onUpdate();
            } 
        }
    ];
};
