import { useState, useEffect } from 'react';
import { defaultSettings } from '../data/mockSettings';

export interface ColorGroup {
    id: string;
    name: string;
    colors: string[];
}

export function useAppSettings() {
    const [servers, setServers] = useState<string[]>(defaultSettings.servers);
    const [enableAvatarCdn, setEnableAvatarCdn] = useState(true);
    const [favoriteColors, setFavoriteColors] = useState<string[]>(['#ff4444', '#66ccff', '#ffd700', '#10b981', '#a855f7']);
    const [colorGroups, setColorGroups] = useState<ColorGroup[]>([
        { id: '1', name: 'VIP', colors: ['#ffd700', '#f59e0b'] },
        { id: '2', name: 'Friends', colors: ['#10b981', '#34d399'] },
        { id: '3', name: 'Admin', colors: ['#ef4444', '#f87171'] }
    ]);
    const [availableIcons, setAvailableIcons] = useState<string[]>([]);
    const [udpTargetIp, setUdpTargetIp] = useState('127.0.0.1');
    const [udpTargetPort, setUdpTargetPort] = useState(26000);

    const loadSettings = async () => {
        const s = await window.ipcRenderer?.invoke('get-setting', 'servers');
        if (Array.isArray(s)) setServers(s);
        
        const colors = await window.ipcRenderer?.invoke('get-setting', 'favoriteColors');
        if (Array.isArray(colors)) setFavoriteColors(colors);
        
        const groups = await window.ipcRenderer?.invoke('get-setting', 'colorGroups');
        if (Array.isArray(groups)) setColorGroups(groups);

        
        const cdn = await window.ipcRenderer?.invoke('get-setting', 'enableAvatarCdn');
        if (cdn !== undefined && cdn !== null) setEnableAvatarCdn(!!cdn);
        
        const uIp = await window.ipcRenderer?.invoke('get-setting', 'udpTargetIp');
        const uPort = await window.ipcRenderer?.invoke('get-setting', 'udpTargetPort');
        if (uIp) setUdpTargetIp(uIp);
        if (uPort) setUdpTargetPort(Number(uPort));
        
        const icons = await window.ipcRenderer?.invoke('scan-icons');
        if (icons) setAvailableIcons(icons);
    };

    useEffect(() => {
        loadSettings();
    }, []);

    const updateServers = (newServers: string[]) => {
        setServers(newServers);
        window.ipcRenderer?.send('save-setting', { key: 'servers', value: newServers });
        window.ipcRenderer?.sendToBackend('UPDATE_SETTING', {});
    };

    const addFavoriteColor = (color: string) => {
        const newColors = [...new Set([color, ...favoriteColors])].slice(0, 30);
        setFavoriteColors(newColors);
        window.ipcRenderer?.send('save-setting', { key: 'favoriteColors', value: newColors });
    };

    const removeFavoriteColor = (color: string) => {
        const newColors = favoriteColors.filter(c => c !== color);
        setFavoriteColors(newColors);
        window.ipcRenderer?.send('save-setting', { key: 'favoriteColors', value: newColors });
    };

    const updateColorGroups = (newGroups: ColorGroup[]) => {
        setColorGroups(newGroups);
        window.ipcRenderer?.send('save-setting', { key: 'colorGroups', value: newGroups });
    };

    return { 
        servers, 
        enableAvatarCdn, 
        favoriteColors, 
        colorGroups,
        availableIcons, 
        udpTargetIp, 
        udpTargetPort,
        refreshSettings: loadSettings,
        updateServers,
        addFavoriteColor,
        removeFavoriteColor,
        updateColorGroups
    };
}
