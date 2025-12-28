import React, { useState, useEffect, useMemo, useCallback } from 'react'
import { toPng } from 'html-to-image'
import { Player } from './types'
import { PlayerCard } from './components/PlayerCard/PlayerCard'
import { ShareablePlayerCard } from './components/PlayerCard/ShareablePlayerCard'
import { Header } from './components/Layout/Header'
import { Sidebar } from './components/Layout/Sidebar'
import { WindowControls } from './components/Layout/WindowControls'
import { ContextMenu } from './components/UI/ContextMenu'
import { DatabaseView } from './components/Views/DatabaseView'
import { SettingsView } from './components/Views/SettingsView'
import { ReportView } from './components/Views/ReportView'
import { ConsoleView } from './components/Views/ConsoleView'
import { MigrationView } from './components/Views/MigrationView'
import { ColorPickerModal } from './components/UI/ColorPickerModal'
import { AliasModal } from './components/UI/AliasModal'
import { buildPlayerMainMenu, buildAvatarMenu } from './menus/playerMenu'
import { buildInputMenu } from './menus/inputMenu'
import { defaultSettings } from './data/mockSettings'
import { Search, Filter, SortAsc, Copy } from 'lucide-react'
import './App.css'

function App() {
    // --- DESIGN CONSTANTS ---
    const DEFAULT_COLORS = {
        game: '#f4f4f5',  // White
        steam: '#60a5fa', // Bright Blue
        alias: '#3b82f6'  // Accent Blue
    };

    const [activeTab, setActiveTab] = useState('Players');
    const [playersSearchTerm, setPlayersSearchTerm] = useState('');
    const [dbSearchTerm, setDbSearchTerm] = useState('');
    const [dbSearchCaseSensitive, setDbSearchCaseSensitive] = useState(false);
    const [dbSearchColor, setDbSearchColor] = useState<string | null>(null);
    const [dbSearchVac, setDbSearchVac] = useState(false);
    const [dbSearchGame, setDbSearchGame] = useState(false);
    const [dbSearchComm, setDbSearchComm] = useState(false);
    const [dbSearchEco, setDbSearchEco] = useState(false);
    const [isDbLoading, setIsDbLoading] = useState(false); // UI Feedback

    const [players, setPlayers] = useState<Player[]>([]);
    const [dbResults, setDbResults] = useState<Player[]>([]);
    const [availableIcons, setAvailableIcons] = useState<string[]>([]);
    const [servers, setServers] = useState<string[]>(defaultSettings.servers);
    const [enableAvatarCdn, setEnableAvatarCdn] = useState(true);
    const [favoriteColors, setFavoriteColors] = useState<string[]>(['#ff4444', '#66ccff', '#ffd700', '#10b981', '#a855f7']);
    const [reportTargets, setReportTargets] = useState<Player[]>([]);

    // Report Builder State
    const [reportData, setReportData] = useState({ server: '', tags: [] as string[], details: '', template: '', prefix: '`', suffix: '`' });
    const [copyStatus, setCopyStatus] = useState('Copy Report');

    // Design Defaults
    const [designDefaults, setDesignDefaults] = useState({
        game: '#f4f4f5',
        steam: '#60a5fa',
        alias: '#3b82f6'
    });

    const [migrationData, setMigrationData] = useState<{ recordCount: number, progress: number, status: string } | null>(null);
    const [isAppReady, setIsAppReady] = useState(false);
    const [initError, setInitError] = useState<string | null>(null);

    const [colorPickerConfig, setColorPickerConfig] = useState<{ isOpen: boolean, initialColor: string, targetPlayer?: Player, targetType?: 'game' | 'steam' | 'alias', title?: string } | null>(null);
    const [onlyBans, setOnlyBans] = React.useState(false);
    const [aliasModalConfig, setAliasModalConfig] = useState<{ isOpen: boolean, player: Player | null } | null>(null);

    const [menuConfig, setMenuConfig] = useState<{ x: number, y: number, player?: Player, type: 'main' | 'avatar' | 'input', element?: any } | null>(null);
    const [udpTargetIp, setUdpTargetIp] = useState('127.0.0.1');
    const [udpTargetPort, setUdpTargetPort] = useState(26001);
    const [selectedQuickTags, setSelectedQuickTags] = useState<string[]>([]);
    const [playerForImage, setPlayerForImage] = useState<Player | null>(null);

    useEffect(() => {
        const unsubscribe: (() => void) | void | undefined = window.ipcRenderer?.onBackend((msg) => {
            console.log('[Backend Pipe]', msg);
            switch (msg.type) {
                case 'READY': setIsAppReady(true); setMigrationData(null); break;
                case 'MIGRATION_REQUIRED': setMigrationData({ recordCount: msg.data.count, progress: 0, status: 'Database upgrade needed' }); break;
                case 'MIGRATION_PROGRESS': setMigrationData(prev => prev ? { ...prev, progress: msg.data.percent, status: msg.data.status } : null); break;
                case 'MIGRATION_SUCCESS': setIsAppReady(true); setMigrationData(null); break;
                case 'PLAYERS_DETECTED': setPlayers(msg.data.players); break;
                case 'UPDATE_PLAYER':
                    const updater = (list: Player[]) => list.map(p => {
                        if (p.steamId64 === msg.data.steamId64) {
                            const { displayName, steamId2, personaName, ...enrichment } = msg.data;
                            const merged: any = { ...p, ...enrichment };
                            if (personaName && personaName.trim() !== "") merged.personaName = personaName;
                            return merged;
                        }
                        return p;
                    });
                    setPlayers(prev => updater(prev));
                    setDbResults(prev => updater(prev));
                    break;
                case 'SEARCH_RESULT': 
                    setDbResults(msg.data.players || []); 
                    setIsDbLoading(false);
                    break;
            }
        });

        window.ipcRenderer?.on('backend-crash', (_e, code) => {
            console.error(`Core Engine Crashed (Code: ${code})`);
            setInitError(`Core Engine Failed (Exit Code: ${code})`);
            setIsAppReady(false);
        });

        const initData = async () => {
            const s = await window.ipcRenderer?.invoke('get-setting', 'servers') || await window.ipcRenderer?.invoke('get-setting', 'Servers');
            if (Array.isArray(s)) setServers(s);
            const colors = await window.ipcRenderer?.invoke('get-setting', 'favoriteColors');
            if (Array.isArray(colors)) setFavoriteColors(colors);
            const dg = await window.ipcRenderer?.invoke('get-setting', 'defaultGameColor');
            const ds = await window.ipcRenderer?.invoke('get-setting', 'defaultSteamColor');
            const da = await window.ipcRenderer?.invoke('get-setting', 'defaultAliasColor');
            const cdn = await window.ipcRenderer?.invoke('get-setting', 'enableAvatarCdn');
            if (cdn !== undefined && cdn !== null) setEnableAvatarCdn(!!cdn);
            
            const uIp = await window.ipcRenderer?.invoke('get-setting', 'udpTargetIp');
            const uPort = await window.ipcRenderer?.invoke('get-setting', 'udpTargetPort');
            if (uIp) setUdpTargetIp(uIp);
            if (uPort) setUdpTargetPort(Number(uPort));

            setDesignDefaults({
                game: dg || DEFAULT_COLORS.game,
                steam: ds || DEFAULT_COLORS.steam,
                alias: da || DEFAULT_COLORS.alias
            });
            const icons = await window.ipcRenderer?.invoke('scan-icons');
            if (icons) setAvailableIcons(icons);
        };
        initData();

        return () => {
            unsubscribe?.();
        };
    }, []);

    const refreshSettings = async () => {
        const s = await window.ipcRenderer?.invoke('get-setting', 'servers') || await window.ipcRenderer?.invoke('get-setting', 'Servers');
        if (Array.isArray(s)) setServers(s);

        const colors = await window.ipcRenderer?.invoke('get-setting', 'favoriteColors');
        if (Array.isArray(colors)) setFavoriteColors(colors);

        const dg = await window.ipcRenderer?.invoke('get-setting', 'defaultGameColor');
        const ds = await window.ipcRenderer?.invoke('get-setting', 'defaultSteamColor');
        const da = await window.ipcRenderer?.invoke('get-setting', 'defaultAliasColor');
        const cdn = await window.ipcRenderer?.invoke('get-setting', 'enableAvatarCdn');
        if (cdn !== undefined && cdn !== null) setEnableAvatarCdn(!!cdn);
        setDesignDefaults({
            game: dg || DEFAULT_COLORS.game,
            steam: ds || DEFAULT_COLORS.steam,
            alias: da || DEFAULT_COLORS.alias
        });
    };

    const copyImage = async (player: Player) => {
        setPlayerForImage(player);
        setTimeout(async () => {
            const node = document.getElementById('shareable-card');
            if (!node) {
                console.error('[Copy Image] DOM node "shareable-card" not found');
                setPlayerForImage(null);
                return;
            }

            try {
                const dataUrl = await toPng(node, {
                    pixelRatio: 2,
                    backgroundColor: '#121214',
                    skipFonts: true, // Prevents hanging on font load
                    cacheBust: true, // Ensures fresh assets
                    style: { visibility: 'visible' } // Ensure it's captured
                });
                window.ipcRenderer?.send('copy-image', dataUrl);
            } catch (error) {
                console.error('[Copy Image] Failed to generate or copy image:', error);
            } finally {
                setPlayerForImage(null);
            }
        }, 500); // Wait for DOM render
    };



    const handleSetAlias = (player: Player) => {
        setAliasModalConfig({ isOpen: true, player });
    };

    const handleApplyAlias = (alias: string) => {
        if (aliasModalConfig?.player) {
            window.ipcRenderer?.sendToBackend('SET_ALIAS', { steamId: aliasModalConfig.player.steamId64, alias });
        }
        setAliasModalConfig(null);
    };

    const handleSetColor = (player: Player, target: 'game' | 'steam' | 'alias') => {
        let current = '';
        let title = "Pick Color";
        if (target === 'game') {
            current = player.playerColor || designDefaults.game;
            title = "Set Game Color";
        } else if (target === 'steam') {
            current = player.personaNameColor || designDefaults.steam;
            title = "Set Steam Color";
        } else if (target === 'alias') {
            current = player.aliasColor || designDefaults.alias;
            title = "Set Alias Color";
        }

        setColorPickerConfig({
            isOpen: true,
            initialColor: current,
            targetPlayer: player,
            targetType: target,
            title
        });
    };

    const handleApplyColor = (color: string) => {
        if (colorPickerConfig?.targetPlayer && colorPickerConfig?.targetType) {
            window.ipcRenderer?.sendToBackend('SET_COLOR', {
                steamId: colorPickerConfig.targetPlayer.steamId64,
                color,
                target: colorPickerConfig.targetType
            });
        } else if (activeTab === 'DB Search') {
            // If no target player, it's a search filter
            dbSearchHandler(dbSearchTerm, dbSearchCaseSensitive, color, dbSearchVac, dbSearchGame, dbSearchComm, dbSearchEco);
        }
        setColorPickerConfig(null);
    };

    const handleRefresh = (player: Player) => {
        window.ipcRenderer?.sendToBackend('REFRESH_PLAYER', { steamId: player.steamId64 });
    };

    const handleSetIcon = (player: Player, icon: string) => {
        window.ipcRenderer?.sendToBackend('SET_ICON', { steamId: player.steamId64, icon });
    };

    const handleAddToReport = (player: Player) => {
        setReportTargets(prev => {
            if (prev.find(t => t.steamId64 === player.steamId64)) return prev;
            return [...prev, player];
        });
        setActiveTab('Report');
    };

    const handleRemoveFromReport = (player: Player) => {
        setReportTargets(prev => prev.filter(t => t.steamId64 !== player.steamId64));
    };

    const handleUpdateServers = (newServers: string[]) => {
        setServers(newServers);
        window.ipcRenderer?.send('save-setting', { key: 'servers', value: newServers });
        window.ipcRenderer?.sendToBackend('UPDATE_SETTING', {});
    };

    const handleGlobalCopyReport = useCallback(() => {
        const { server, tags, details, template, prefix, suffix } = reportData;
        const pNames = reportTargets.length > 0 ? reportTargets.map(t => prefix + (t.displayName || t.personaName) + suffix).join(', ') : 'Unknown';
        const sIds = reportTargets.length > 0 ? reportTargets.map(t => prefix + t.steamId2 + suffix).join(', ') : 'Unknown';
        let det = details;
        if (tags.length > 0) det = `[${tags.join(', ')}] ` + det;
        let text = template || defaultSettings.reportTemplate;
        text = text.replace(/\${ServerName}/g, server).replace(/\${PlayerName}/g, pNames).replace(/\${SteamId}/g, sIds).replace(/\${Details}/g, det);
        navigator.clipboard.writeText(text);
        setCopyStatus('Copied!');
    }, [reportData, reportTargets]);

    const handleInputContextMenu = (e: React.MouseEvent) => {
        const target = e.target as HTMLInputElement | HTMLTextAreaElement;
        if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
            e.preventDefault(); e.stopPropagation();
            setMenuConfig({ x: e.clientX, y: e.clientY, type: 'input', element: target });
        }
    };

    const dbSearchHandler = (t: string, caseSensitive: boolean, color: string | null, vac: boolean, game: boolean, comm: boolean, eco: boolean) => {
        setDbSearchTerm(t);
        setDbSearchCaseSensitive(caseSensitive);
        setDbSearchColor(color);
        setDbSearchVac(vac);
        setDbSearchGame(game);
        setDbSearchComm(comm);
        setDbSearchEco(eco);
    };

    // Debounce DB Search
    useEffect(() => {
        const timer = setTimeout(() => {
            const hasFilters = dbSearchColor || dbSearchVac || dbSearchGame || dbSearchComm || dbSearchEco;
            const shouldQueryDb = hasFilters || (dbSearchTerm && dbSearchTerm.trim().length > 0);

            if (shouldQueryDb) {
                setIsDbLoading(true);
                window.ipcRenderer?.sendToBackend('SEARCH_DB', {
                    t: dbSearchTerm,
                    caseSensitive: dbSearchCaseSensitive,
                    color: dbSearchColor,
                    vacBanned: dbSearchVac,
                    gameBanned: dbSearchGame,
                    communityBanned: dbSearchComm,
                    economyBanned: dbSearchEco
                });
            } else if (!dbSearchTerm && !hasFilters) {
                setDbResults([]);
            }
        }, 300);
        return () => clearTimeout(timer);
    }, [dbSearchTerm, dbSearchCaseSensitive, dbSearchColor, dbSearchVac, dbSearchGame, dbSearchComm, dbSearchEco]);

    const menuItems = useMemo(() => {
        if (!menuConfig) return [];
        const { player, type, element } = menuConfig;
        if (type === 'input' && element) return buildInputMenu(element, () => { });
        if (!player) return [];
        if (type === 'avatar') return buildAvatarMenu(player, availableIcons, handleSetIcon);
        return buildPlayerMainMenu(player, selectedQuickTags, (tag) => setSelectedQuickTags(prev => prev.includes(tag) ? prev.filter(t => t !== tag) : [...prev, tag]), setActiveTab, () => { }, servers, {
            onSetAlias: handleSetAlias, onSetColor: handleSetColor, onCopyImage: copyImage, onRefresh: handleRefresh, onAddToReport: handleAddToReport
        });
    }, [menuConfig, selectedQuickTags, availableIcons, servers]);

    const filteredPlayers = players.filter(p => {
        if (!p) return false;

        // --- ONLY BANS FILTER ---
        if (onlyBans) {
            const hasBans = (p.numberOfVacBans > 0) || (p.numberOfGameBans > 0) || (p.isCommunityBanned === true) || (p.economyBan && p.economyBan !== "0" && p.economyBan.toLowerCase() !== "none");
            if (!hasBans) return false;
        }

        const term = (playersSearchTerm || '').toLowerCase().trim();
        if (!term) return true;
        if (term.startsWith('#')) return p.playerColor?.toLowerCase().includes(term);
        return (p.displayName?.toLowerCase().includes(term) || p.personaName?.toLowerCase().includes(term) || p.steamId2?.toLowerCase().includes(term));
    });

    if (migrationData) return <MigrationView recordCount={migrationData.recordCount} progress={migrationData.progress} status={migrationData.status} onStart={(backup) => window.ipcRenderer?.sendToBackend('START_MIGRATION', { backup })} />;

    if (!isAppReady) return (
        <div className="appContainer" style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <WindowControls />
            <div style={{ textAlign: 'center' }}>
                {initError ? (
                    <div style={{ color: 'var(--accent-red)' }}>⚠️ <h2>System Error</h2><p>{initError}</p><button onClick={() => window.location.reload()}>Restart</button></div>
                ) : (
                    <div style={{ textAlign: 'center' }}><div className="loader"></div><p style={{ marginTop: 20, color: 'var(--text-secondary)' }}>Initializing Core Engine...</p></div>
                )}
            </div>
        </div>
    );

    return (
        <div className="appContainer" onClick={() => setMenuConfig(null)} onContextMenu={handleInputContextMenu}>
            <div className="rootLayout">
                <Sidebar activeTab={activeTab} onTabChange={setActiveTab} />
                <div className="contentArea">
                    <Header activeTab={activeTab} playerCount={filteredPlayers.length} primaryAction={activeTab === 'Report' && reportTargets.length > 0 ? { label: copyStatus, icon: <Copy size={14} />, onClick: handleGlobalCopyReport } : undefined} />
                    <main className="mainContent">
                        {activeTab === 'Players' && (
                            <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                                <div className="viewToolbar">
                                    <div className="toolbarGroup">
                                        <div className="searchWrapper">
                                            <Search size={14} className="searchIcon" />
                                            <input type="text" className="toolbarInput" placeholder="Search players..." value={playersSearchTerm} onChange={(e) => setPlayersSearchTerm(e.target.value)} />
                                        </div>
                                        <button
                                            className={`toolbarButton ${onlyBans ? 'toolbarButtonActive' : ''}`}
                                            onClick={() => setOnlyBans(!onlyBans)}
                                        >
                                            <Filter size={14} /> Only Bans
                                        </button>
                                    </div>
                                    <div className="toolbarGroup"><SortAsc size={14} color="var(--text-muted)" /><select className="toolbarSelect"><option>Sort by Date</option></select></div>
                                </div>
                                <div className="scrollableContent">
                                    <div className="cardGrid">
                                        {filteredPlayers.map(player => (
                                            <PlayerCard
                                                key={player.steamId64}
                                                player={player}
                                                avatarPriority={enableAvatarCdn ? 'cdn' : 'cache'}
                                                onContextMenu={(e) => setMenuConfig({ x: e.clientX, y: e.clientY, player, type: 'main' })}
                                                onAvatarContextMenu={(e) => setMenuConfig({ x: e.clientX, y: e.clientY, player, type: 'avatar' })}
                                            />
                                        ))}
                                    </div>
                                </div>
                            </div>
                        )}
                        {activeTab === 'DB Search' && (
                            <div className="scrollableContent">
                                <DatabaseView
                                    searchTerm={dbSearchTerm}
                                    onSearchChange={dbSearchHandler}
                                    players={dbResults}
                                    enableAvatarCdn={enableAvatarCdn}
                                    favoriteColors={favoriteColors}
                                    onContextMenu={(e, p) => setMenuConfig({ x: e.clientX, y: e.clientY, player: p, type: 'main' })}
                                    onAvatarContextMenu={(e, p) => setMenuConfig({ x: e.clientX, y: e.clientY, player: p, type: 'avatar' })}
                                    onPickCustomColor={() => setColorPickerConfig({ isOpen: true, initialColor: '#3b82f6' })}
                                    vacBanned={dbSearchVac}
                                    gameBanned={dbSearchGame}
                                    communityBanned={dbSearchComm}
                                    economyBanned={dbSearchEco}
                                    caseSensitive={dbSearchCaseSensitive}
                                    isLoading={isDbLoading}
                                />
                            </div>
                        )}
                        {activeTab === 'Report' && (
                            <div className="scrollableContent">
                                <ReportView allPlayers={players} reportTargets={reportTargets} onAddTarget={handleAddToReport} onRemoveTarget={handleRemoveFromReport} servers={servers} onUpdateServers={handleUpdateServers} onReportChange={setReportData} />
                            </div>
                        )}
                        {activeTab === 'Settings' && (
                            <div className="scrollableContent">
                                <SettingsView onSettingsSaved={refreshSettings} />
                            </div>
                        )}
                        {activeTab === 'Console' && <ConsoleView targetIp={udpTargetIp} targetPort={udpTargetPort} />}
                    </main>
                </div>
            </div>
            {playerForImage && <ShareablePlayerCard player={playerForImage} />}
            {colorPickerConfig?.isOpen && (
                <ColorPickerModal
                    isOpen={true}
                    onClose={() => setColorPickerConfig(null)}
                    initialColor={colorPickerConfig.initialColor}
                    onApply={handleApplyColor}
                    title={colorPickerConfig.title}
                />
            )}
            <AliasModal
                isOpen={!!aliasModalConfig?.isOpen}
                player={aliasModalConfig?.player || null}
                onClose={() => setAliasModalConfig(null)}
                onApply={handleApplyAlias}
            />
            {menuConfig && <ContextMenu x={menuConfig.x} y={menuConfig.y} items={menuItems} onClose={() => setMenuConfig(null)} />}
        </div>
    )
}

export default App