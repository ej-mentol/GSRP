import React, { useState, useCallback, useEffect, useRef } from 'react';
import styles from './DatabaseView.module.css';
import { Player } from '../../types';
import { PlayerCard } from '../PlayerCard/PlayerCard';
import { Search, SortAsc, Plus } from 'lucide-react';

interface DatabaseViewProps {
    searchTerm: string;
    onSearchChange: (term: string, caseSensitive: boolean, color: string | null, vac: boolean, game: boolean, comm: boolean, eco: boolean) => void;
    players: Player[];
    favoriteColors: string[];
    onContextMenu: (e: React.MouseEvent, player: Player) => void;
    onAvatarContextMenu: (e: React.MouseEvent, player: Player) => void;
    onPickCustomColor: () => void;
    enableAvatarCdn?: boolean;
    vacBanned: boolean;
    gameBanned: boolean;
    communityBanned: boolean;
    economyBanned: boolean;
    caseSensitive: boolean;
    isLoading?: boolean;
}

const MOCK_COLORS = ['#ff4444', '#66ccff', '#ffd700', '#10b981', '#a855f7'];

export const DatabaseView: React.FC<DatabaseViewProps> = ({
    searchTerm,
    onSearchChange,
    players,
    favoriteColors,
    onContextMenu,
    onAvatarContextMenu,
    onPickCustomColor,
    enableAvatarCdn = true,
    vacBanned,
    gameBanned,
    communityBanned,
    economyBanned,
    caseSensitive,
    isLoading = false
}) => {
    const [exactMatch, setExactMatch] = useState(false);
    const [selectedColor, setSelectedColor] = useState<string | null>(null);
    const [sortBy, setSortBy] = useState('last_updated');



    const [sidebarWidth, setSidebarWidth] = useState(280);

    const [isResizing, setIsResizing] = useState(false);
    const [displayLimit, setDisplayLimit] = useState(50);
    const loadMoreRef = useRef<HTMLDivElement>(null);



    useEffect(() => {

        window.ipcRenderer?.invoke('get-setting', 'dbSidebarWidth').then(width => {

            if (width) setSidebarWidth(Number(width));

        });

    }, []);



    const startResizing = useCallback((e: React.MouseEvent) => {

        e.preventDefault();

        setIsResizing(true);

    }, []);



    const stopResizing = useCallback(() => {

        if (isResizing) {

            setIsResizing(false);

            window.ipcRenderer?.send('save-setting', { key: 'dbSidebarWidth', value: sidebarWidth });

        }

    }, [isResizing, sidebarWidth]);



    const resize = useCallback((e: MouseEvent) => {

        if (isResizing) {

            const newWidth = window.innerWidth - e.clientX;

            if (newWidth > 200 && newWidth < 600) {

                setSidebarWidth(newWidth);

            }

        }

    }, [isResizing]);



    useEffect(() => {

        if (isResizing) {

            window.addEventListener('mousemove', resize);

            window.addEventListener('mouseup', stopResizing);

        }

        return () => {

            window.removeEventListener('mousemove', resize);

            window.removeEventListener('mouseup', stopResizing);

        };

    }, [isResizing, resize, stopResizing]);



    const filteredResults = (players || []).filter(p => {
        // 1. Color Filter (Already applied by DB in most cases, but good for local safety)
        if (selectedColor) {
            const hasColorMatch = p.playerColor === selectedColor || p.personaNameColor === selectedColor || p.aliasColor === selectedColor;
            if (!hasColorMatch) return false;

            // 2. IF COLOR IS SELECTED, we do LOCAL sub-filtering by text
            if (searchTerm && searchTerm.trim() !== '') {
                const term = searchTerm.toLowerCase();
                const matchName = (p.displayName?.toLowerCase() || "").includes(term) ||
                    (p.personaName?.toLowerCase() || "").includes(term);
                const matchId = (p.steamId2?.toLowerCase() || "").includes(term) ||
                    (p.steamId64?.toLowerCase() || "").includes(term);
                if (!matchName && !matchId) return false;
            }
        }

        return true;
    }).sort((a, b) => {
        switch (sortBy) {
            case 'name': return a.displayName.localeCompare(b.displayName);
            case 'age': return (a.timeCreated || 0) - (b.timeCreated || 0);
            case 'bans': return (b.numberOfVacBans + b.numberOfGameBans) - (a.numberOfVacBans + a.numberOfGameBans);
            case 'last_updated': default: return (b.lastUpdated || 0) - (a.lastUpdated || 0);
        }
    });

    // --- INFINITE SCROLL LOGIC ---
    useEffect(() => {
        setDisplayLimit(50); // Reset limit when search parameters change
    }, [searchTerm, selectedColor, sortBy, players]);

    useEffect(() => {
        const observer = new IntersectionObserver(
            (entries) => {
                if (entries[0].isIntersecting && displayLimit < filteredResults.length) {
                    setDisplayLimit(prev => Math.min(prev + 50, filteredResults.length));
                }
            },
            { threshold: 0.1, rootMargin: '200px' }
        );

        if (loadMoreRef.current) {
            observer.observe(loadMoreRef.current);
        }

        return () => observer.disconnect();
    }, [filteredResults.length, displayLimit]);

    const visibleResults = filteredResults.slice(0, displayLimit);



    return (

        <div className={styles.container}>

            <main className={styles.mainArea}>

                <div className={styles.toolbar}>

                    <span className={styles.resultsCount}>

                        Found <b>{filteredResults.length}</b> players

                    </span>



                    <div className={styles.sortGroup}>

                        <SortAsc size={14} />

                        <span>Sort by:</span>

                        <select className={styles.select} value={sortBy} onChange={(e) => setSortBy(e.target.value)}>

                            <option value="last_updated">Last Updated</option>

                            <option value="name">Name (A-Z)</option>

                            <option value="age">Account Age</option>

                            <option value="bans">Ban Count</option>

                        </select>

                    </div>

                </div>



                <div className={styles.resultsList} style={{ opacity: isLoading ? 0.5 : 1, transition: 'opacity 0.2s', pointerEvents: isLoading ? 'none' : 'auto' }}>
                    {isLoading && (
                        <div style={{ position: 'absolute', top: '50%', left: '50%', transform: 'translate(-50%, -50%)', zIndex: 10 }}>
                            <div className="loader"></div>
                        </div>
                    )}
                    {filteredResults.length > 0 ? (

                        <div className="cardGrid">

                            {visibleResults.map(player => (

                                <PlayerCard
                                    key={player.steamId64}
                                    player={player}
                                    avatarPriority={enableAvatarCdn ? 'cdn' : 'cache'}
                                    isDatabaseEntry={true}
                                    onContextMenu={onContextMenu}
                                    onAvatarContextMenu={onAvatarContextMenu}
                                />

                            ))}

                            {/* Sentinel for infinite scroll */}
                            {displayLimit < filteredResults.length && (
                                <div ref={loadMoreRef} style={{ height: 20, width: '100%', gridColumn: '1 / -1', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                    <div className="loader" style={{ width: 20, height: 20 }}></div>
                                </div>
                            )}

                        </div>

                    ) : (


                        <div className={styles.emptyState}>
                            <Search size={48} className={styles.emptyIcon} />
                            <span>No results for your query</span>
                        </div>
                    )}
                </div>
            </main>

            <div
                className={`${styles.resizeHandle} ${isResizing ? styles.resizeHandleActive : ''}`}
                onMouseDown={startResizing}
            />

            <aside className={styles.searchSidebar} style={{ width: sidebarWidth }}>
                <div className={styles.section}>
                    <span className={styles.sectionTitle}>Search Database</span>
                    <input
                        type="text"
                        className={styles.input}
                        placeholder="Search name or SteamID..."
                        value={searchTerm}
                        onChange={(e) => {
                            onSearchChange(e.target.value, caseSensitive, selectedColor, vacBanned, gameBanned, communityBanned, economyBanned);
                        }}
                    />
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                        <label className={styles.checkboxLabel}>
                            <input
                                type="checkbox"
                                className={styles.checkbox}
                                checked={exactMatch}
                                onChange={(e) => setExactMatch(e.target.checked)}
                            />
                            Exact Match
                        </label>
                        <label className={styles.checkboxLabel}>
                            <input
                                type="checkbox"
                                className={styles.checkbox}
                                checked={caseSensitive}
                                onChange={(e) => {
                                    onSearchChange(searchTerm, e.target.checked, selectedColor, vacBanned, gameBanned, communityBanned, economyBanned);
                                }}
                            />
                            Case Sensitive
                        </label>
                    </div>
                </div>

                <div className={styles.section}>
                    <span className={styles.sectionTitle}>Filter by Color</span>
                    <div className={styles.colorList}>
                        <div
                            className={`${styles.colorItem} ${!selectedColor ? styles.colorItemActive : ''}`}
                            style={{ background: 'linear-gradient(45deg, #333, #666)' }}
                            onClick={() => {
                                setSelectedColor(null);
                                onSearchChange(searchTerm, caseSensitive, null, vacBanned, gameBanned, communityBanned, economyBanned);
                            }}
                            title="All colors"
                        />
                        {(favoriteColors && favoriteColors.length > 0 ? favoriteColors : MOCK_COLORS).map(c => {
                            const style = (() => {
                                if (c.includes(';')) {
                                    const [a, b] = c.split(';');
                                    return { background: `radial-gradient(circle at 30% 25%, rgba(255,255,255,0.22) 0%, rgba(255,255,255,0.06) 8%, transparent 40%), linear-gradient(135deg, ${a}, ${b})` };
                                } else {
                                    return { background: `radial-gradient(circle at 30% 25%, rgba(255,255,255,0.22) 0%, rgba(255,255,255,0.06) 8%, transparent 40%), ${c}` };
                                }
                            })();
                            
                            return (
                                <div
                                    key={c}
                                    className={`${styles.colorItem} ${selectedColor === c ? styles.colorItemActive : ''}`}
                                    style={style}
                                    onClick={() => {
                                        setSelectedColor(c);
                                        onSearchChange(searchTerm, caseSensitive, c, vacBanned, gameBanned, communityBanned, economyBanned);
                                    }}
                                    title={c}
                                />
                            );
                        })}
                        <div
                            className={styles.addColorItem}
                            onClick={onPickCustomColor}
                            title="Pick custom color"
                        >
                            <Plus size={14} />
                        </div>
                    </div>
                </div>

                <div className={styles.section}>
                    <span className={styles.sectionTitle}>Ban Status</span>
                    <label className={styles.checkboxLabel}>
                        <input 
                            type="checkbox" 
                            className={styles.checkbox} 
                            checked={vacBanned}
                            onChange={(e) => onSearchChange(searchTerm, caseSensitive, selectedColor, e.target.checked, gameBanned, communityBanned, economyBanned)}
                        /> VAC Banned
                    </label>
                    <label className={styles.checkboxLabel}>
                        <input 
                            type="checkbox" 
                            className={styles.checkbox} 
                            checked={gameBanned}
                            onChange={(e) => onSearchChange(searchTerm, caseSensitive, selectedColor, vacBanned, e.target.checked, communityBanned, economyBanned)}
                        /> Game Banned
                    </label>
                    <label className={styles.checkboxLabel}>
                        <input 
                            type="checkbox" 
                            className={styles.checkbox} 
                            checked={communityBanned}
                            onChange={(e) => onSearchChange(searchTerm, caseSensitive, selectedColor, vacBanned, gameBanned, e.target.checked, economyBanned)}
                        /> Community Banned
                    </label>
                    <label className={styles.checkboxLabel}>
                        <input 
                            type="checkbox" 
                            className={styles.checkbox} 
                            checked={economyBanned}
                            onChange={(e) => onSearchChange(searchTerm, caseSensitive, selectedColor, vacBanned, gameBanned, communityBanned, e.target.checked)}
                        /> Economy Banned
                    </label>
                </div>
            </aside>
        </div>
    );
};
