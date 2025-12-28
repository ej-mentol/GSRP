import React from 'react';
import {
    User, FileText, ExternalLink, Flag, Edit2, Palette,
    Image as ImageIcon, CreditCard, Copy, Hash, X, Calendar, RefreshCw
} from 'lucide-react';
import { Player } from '../types';
import { MenuItem } from '../components/UI/ContextMenu';

import { isPrivateProfile } from '../utils/playerHelpers';

const SteamBadge = ({ text }: { text: string }) => (
    <div style={{ fontSize: '9px', fontWeight: 800, border: '1px solid currentColor', borderRadius: '3px', padding: '0 2px', minWidth: '20px', textAlign: 'center', lineHeight: '14px' }}>{text}</div>
);

// ... (existing code)


const generateQuickReport = (player: Player, server: string, tags: string) => {
    return `Server: ${server}\nTarget: ${player.displayName}\nID: ${player.steamId2}\nReason: ${tags || 'Reported for review'}\nEvidence:`
};

const copy = (text: string) => navigator.clipboard.writeText(text);

export const buildPlayerMainMenu = (
    player: Player,
    currentTags: string[],
    onToggleTag: (tag: string) => void,
    onTabChange: (tab: string) => void,
    onUpdateMenu: (items: MenuItem[]) => void,
    servers: string[],
    actions: {
        onSetAlias: (p: Player) => void,
        onSetColor: (p: Player, target: 'game' | 'steam' | 'alias') => void,
        onCopyImage: (p: Player) => void,
        onRefresh: (p: Player) => void,
        onAddToReport: (p: Player) => void,
        onCustomize: (p: Player) => void
    }
): MenuItem[] => {
    const quickReasons = ['Cheating', 'Abuse', 'Toxic', 'Teamkill', 'N-Word'];

    const serverSubMenu: MenuItem[] = (servers && servers.length > 0)
        ? servers.map(server => ({
            label: server,
            children: [
                ...quickReasons.map(reason => ({
                    label: reason,
                    type: 'checkbox' as const,
                    checked: currentTags.includes(reason),
                    action: () => onToggleTag(reason)
                })),
                { separator: true },
                {
                    label: `COPY REPORT [${currentTags.length}]`,
                    icon: <Copy size={14} />,
                    action: () => copy(generateQuickReport(player, server, currentTags.join(', ')))
                }
            ]
        }))
        : [{ label: 'No servers (check settings)', disabled: true }];

    return [
        { label: 'Update Profile', icon: <RefreshCw size={14} />, iconColorClass: 'blueIcon', action: () => actions.onRefresh(player) },
        { label: 'Customize Appearance', icon: <Palette size={14} />, iconColorClass: 'purpleIcon', action: () => actions.onCustomize(player) },
        { separator: true },
        { label: 'Copy Name', icon: <User size={14} />, iconColorClass: 'blueIcon', action: () => copy(player.displayName) },
        { label: 'Copy PersonaName', icon: <User size={14} />, iconColorClass: 'blueIcon', action: () => copy(player.personaName) },
        { label: 'Copy Alias', icon: <FileText size={14} />, iconColorClass: 'greenIcon', action: () => copy(player.alias || '') },
        { separator: true },
        { label: 'Copy SteamID64', icon: <SteamBadge text="S64" />, iconColorClass: 'orangeIcon', action: () => copy(player.steamId64) },
        { label: 'Copy SteamID', icon: <SteamBadge text="S2" />, iconColorClass: 'greenIcon', action: () => copy(player.steamId2) },
        { label: 'Open in Browser', icon: <ExternalLink size={14} />, iconColorClass: 'greenIcon', action: () => window.ipcRenderer?.send('open-external', `https://steamcommunity.com/profiles/${player.steamId64}`) },
        { separator: true },
        { label: 'Copy to Report (Builder)', icon: <Flag size={14} />, iconColorClass: 'redIcon', action: () => actions.onAddToReport(player) },
        { label: 'Copy as Report (Quick)', icon: <FileText size={14} />, iconColorClass: 'purpleIcon', children: serverSubMenu },
        { separator: true },
        { label: 'Copy as Image', icon: <ImageIcon size={14} />, iconColorClass: 'blueIcon', action: () => actions.onCopyImage(player) },
    ];
};

export const buildAvatarMenu = (player: Player, availableIcons: string[], onSetIcon: (p: Player, icon: string) => void): MenuItem[] => {
    const items: MenuItem[] = availableIcons.map(icon => ({
        label: icon,
        icon: <img src={`gsrp-icon://${icon}`} style={{ width: 14, height: 14, objectFit: 'contain' }} />,
        action: () => onSetIcon(player, icon)
    }));

    if (player.iconName) {
        items.unshift({ separator: true });
        items.unshift({
            label: 'Remove Icon',
            icon: <X size={14} />,
            danger: true,
            action: () => onSetIcon(player, '')
        });
    }

    if (items.length === 0) items.push({ label: 'No icons found', action: () => { } });
    return items;
};

export const buildReportSlotMenu = (player: Player, onRemove: () => void): MenuItem[] => [
    { label: `Copy Name: ${player.displayName}`, icon: <User size={14} />, action: () => copy(player.displayName) },
    { label: `Copy ID: ${player.steamId2}`, icon: <Hash size={14} />, action: () => copy(player.steamId2) },
    { separator: true },
    { label: `Registered: ${!isPrivateProfile(player.timeCreated) ? new Date(player.timeCreated * 1000).toLocaleDateString() : 'Private'}`, icon: <Calendar size={14} /> },
    { separator: true },
    { label: 'Remove from Report', icon: <X size={14} />, danger: true, action: onRemove }
];