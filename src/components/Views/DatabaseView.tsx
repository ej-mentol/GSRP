import React, { useState, useCallback, useEffect, useRef, useMemo } from 'react';
import styles from './DatabaseView.module.css';
import { Player } from '../../types';
import { PlayerCard } from '../PlayerCard/PlayerCard';
import { Search, SortAsc, ArrowUpDown } from 'lucide-react';
import { ColorGroup } from '../../hooks/useAppSettings';

interface DatabaseViewProps {
    searchTerm: string;
    selectedColor: string | null;
    onSearchChange: (term: string, caseSensitive: boolean, color: string | null, vac: boolean, game: boolean, comm: boolean, eco: boolean) => void;
    players: Player[];
    favoriteColors: string[];
    colorGroups?: ColorGroup[];
    dbUniqueColors?: string[];
    onUpdateGroups?: (groups: ColorGroup[]) => void;
    onAddFavoriteColor?: (color: string) => void;
    onRemoveFavoriteColor?: (color: string) => void;
    onContextMenu: (e: React.MouseEvent, player: Player) => void;
    onAvatarContextMenu: (e: React.MouseEvent, player: Player) => void;
    enableAvatarCdn?: boolean;
    vacBanned: boolean;
    gameBanned: boolean;
    communityBanned: boolean;
    economyBanned: boolean;
    caseSensitive: boolean;
    isLoading?: boolean;
}

export const DatabaseView: React.FC<DatabaseViewProps> = ({
    searchTerm,
    selectedColor,
    onSearchChange,
    players,
    favoriteColors,
    colorGroups = [],
    dbUniqueColors = [],
    onUpdateGroups,
    onAddFavoriteColor,
    onRemoveFavoriteColor,
    onContextMenu,
    onAvatarContextMenu,
    enableAvatarCdn = true,
    vacBanned,
    gameBanned,
    communityBanned,
    economyBanned,
    caseSensitive,
    isLoading = false
}) => {
    const [exactMatch, setExactMatch] = useState(false);
    const [sortBy, setSortBy] = useState('last_updated');
    const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
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

    const sortedResults = useMemo(() => {
        const orderMod = sortOrder === 'asc' ? 1 : -1;
        return [...(players || [])].filter(p => !!p && !!p.steamId64).sort((a, b) => {
            let res = 0;
            switch (sortBy) {
                case 'name': 
                    res = (a.displayName || a.personaName || "").localeCompare(b.displayName || b.personaName || "");
                    break;
                case 'age':
                    res = (Number(a.timeCreated) || 0) - (Number(b.timeCreated) || 0);
                    break;
                case 'bans':
                    res = ((Number(a.numberOfVacBans) || 0) + (Number(a.numberOfGameBans) || 0)) - 
                          ((Number(b.numberOfVacBans) || 0) + (Number(b.numberOfGameBans) || 0));
                    break;
                case 'last_updated': 
                default: 
                    res = (Number(a.lastUpdated) || 0) - (Number(b.lastUpdated) || 0);
                    break;
            }
            return res * orderMod;
        });
    }, [players, sortBy, sortOrder]);

    useEffect(() => {
        setDisplayLimit(50);
    }, [searchTerm, selectedColor, sortBy, sortOrder, players]);

    useEffect(() => {
        const observer = new IntersectionObserver(
            (entries) => {
                if (entries[0].isIntersecting && displayLimit < sortedResults.length) {
                    setDisplayLimit(prev => Math.min(prev + 50, sortedResults.length));
                }
            },
            { threshold: 0.1, rootMargin: '200px' }
        );
        if (loadMoreRef.current) observer.observe(loadMoreRef.current);
        return () => observer.disconnect();
    }, [sortedResults.length, displayLimit]);

    const visibleResults = sortedResults.slice(0, displayLimit);

    const handleCreateGroup = () => {
        if (!selectedColor || !onUpdateGroups) return;
        const name = prompt("Enter group name:");
        if (!name) return;
        const colors = selectedColor.split(',');
        const newGroup: ColorGroup = {
            id: Date.now().toString(),
            name,
            colors
        };
        onUpdateGroups([...colorGroups, newGroup]);
    };

    const handleDeleteGroup = (e: React.MouseEvent, id: string) => {
        e.preventDefault();
        e.stopPropagation();
        if (!onUpdateGroups) return;
        if (confirm("Delete this group?")) {
            onUpdateGroups(colorGroups.filter(g => g.id !== id));
        }
    };

    const handleColorContextMenu = (e: React.MouseEvent, color: string) => {
        e.preventDefault();
        e.stopPropagation();
        const isFavorite = favoriteColors.includes(color);
        if (isFavorite) {
            if (onRemoveFavoriteColor) onRemoveFavoriteColor(color);
        } else {
            if (onAddFavoriteColor) onAddFavoriteColor(color);
        }
    };

    return (
        <div className={styles.container}>
            <main className={styles.mainArea}>
                <div className={styles.toolbar}>
                    <span className={styles.resultsCount}>Found <b>{sortedResults.length}</b> players</span>
                    <div className={styles.sortGroup}>
                        <SortAsc size={14} />
                        <span>Sort by:</span>
                        <select className={styles.select} value={sortBy} onChange={(e) => setSortBy(e.target.value)}>
                            <option value="last_updated">Last Updated</option>
                            <option value="name">Name (A-Z)</option>
                            <option value="age">Account Age</option>
                            <option value="bans">Ban Count</option>
                        </select>
                        <button 
                            onClick={() => setSortOrder(prev => prev === 'asc' ? 'desc' : 'asc')}
                            title={`Order: ${sortOrder === 'asc' ? 'Ascending' : 'Descending'}`}
                            style={{ 
                                background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer',
                                display: 'flex', alignItems: 'center', padding: '4px', borderRadius: '4px',
                                transition: 'all 0.2s', transform: sortOrder === 'desc' ? 'rotate(180deg)' : 'none'
                            }}
                        >
                            <ArrowUpDown size={14} />
                        </button>
                    </div>
                </div>

                <div className={styles.resultsList} style={{ opacity: isLoading ? 0.5 : 1, transition: 'opacity 0.2s', pointerEvents: isLoading ? 'none' : 'auto' }}>
                    {isLoading && (
                        <div style={{ position: 'absolute', top: '50%', left: '50%', transform: 'translate(-50%, -50%)', zIndex: 10 }}>
                            <div className="loader"></div>
                        </div>
                    )}
                    {sortedResults.length > 0 ? (
                        <div className="cardGrid">
                            {visibleResults.map((player: Player) => (
                                <PlayerCard key={player.steamId64} player={player} avatarPriority={enableAvatarCdn ? 'cdn' : 'cache'} isDatabaseEntry={true} onContextMenu={onContextMenu} onAvatarContextMenu={onAvatarContextMenu} />
                            ))}
                            {displayLimit < sortedResults.length && (
                                <div ref={loadMoreRef} style={{ height: 20, width: '100%', gridColumn: '1 / -1', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                    <div className="loader" style={{ width: 20, height: 20 }}></div>
                                </div>
                            )}
                        </div>
                    ) : (
                        !isLoading && (
                            <div className={styles.emptyState}>
                                <Search size={48} className={styles.emptyIcon} />
                                <span>No results for your query</span>
                            </div>
                        )
                    )}
                </div>
            </main>

            <div className={`${styles.resizeHandle} ${isResizing ? styles.resizeHandleActive : ''}`} onMouseDown={startResizing} />

            <aside className={styles.searchSidebar} style={{ width: sidebarWidth }}>
                <div className={styles.section}>
                    <span className={styles.sectionTitle}>Search Database</span>
                    <input type="text" className={styles.input} placeholder="Search name or SteamID..." value={searchTerm} onChange={(e) => onSearchChange(e.target.value, caseSensitive, selectedColor, vacBanned, gameBanned, communityBanned, economyBanned)} />
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                        <label className={styles.checkboxLabel}>
                            <input type="checkbox" className={styles.checkbox} checked={exactMatch} onChange={(e) => setExactMatch(e.target.checked)} /> Exact Match
                        </label>
                        <label className={styles.checkboxLabel}>
                            <input type="checkbox" className={styles.checkbox} checked={caseSensitive} onChange={(e) => onSearchChange(searchTerm, e.target.checked, selectedColor, vacBanned, gameBanned, communityBanned, economyBanned)} /> Case Sensitive
                        </label>
                    </div>
                </div>

                <div className={styles.section}>
                    <span className={styles.sectionTitle}>Filter by Group / Color</span>
                    <div className={styles.colorList}>
                        <div
                            className={`${styles.colorItem} ${!selectedColor ? styles.colorItemActive : ''}`}
                            style={{ background: 'radial-gradient(circle at 30% 25%, rgba(255,255,255,0.2) 0%, rgba(255,255,255,0.05) 10%, transparent 45%), linear-gradient(45deg, #333, #666)' }}
                            onClick={() => onSearchChange(searchTerm, caseSensitive, null, vacBanned, gameBanned, communityBanned, economyBanned)}
                            title="All players"
                        />
                        {colorGroups.map((g: ColorGroup) => {
                            const style = g.colors.length > 1 
                                ? { background: `conic-gradient(${g.colors.map((c, i) => `${c} ${i * (100 / g.colors.length)}% ${(i + 1) * (100 / g.colors.length)}%`).join(', ')})` }
                                : { background: g.colors[0] || '#333' };
                            const fv = g.colors.join(',');
                            return (
                                <div key={g.id} className={`${styles.colorItem} ${selectedColor === fv ? styles.colorItemActive : ''}`} style={style}
                                    onClick={() => onSearchChange(searchTerm, caseSensitive, fv, vacBanned, gameBanned, communityBanned, economyBanned)}
                                    onContextMenu={(e) => handleDeleteGroup(e, g.id)}
                                    title={`${g.name} (Right-click to delete)`} />
                            );
                        })}
                        
                        {(dbUniqueColors.length > 0 || favoriteColors.length > 0) && <div style={{width: '100%', height: 1, backgroundColor: 'rgba(255,255,255,0.05)', margin: '4px 0'}} />}

                        {[...new Set([...favoriteColors, ...dbUniqueColors])].filter(c => typeof c === 'string' && c.length > 0).map((c: string) => {
                            const style = c.includes(';') ? { background: `linear-gradient(135deg, ${c.split(';')[0]}, ${c.split(';')[1]})` } : { background: c };
                            const isFav = favoriteColors.includes(c);
                            return (
                                <div key={c} className={`${styles.colorItem} ${selectedColor === c ? styles.colorItemActive : ''}`} style={style}
                                    onClick={() => onSearchChange(searchTerm, caseSensitive, c, vacBanned, gameBanned, communityBanned, economyBanned)}
                                    onContextMenu={(e) => handleColorContextMenu(e, c)}
                                    title={`${c} ${isFav ? '(Pinned)' : '(Right-click to pin)'}`} />
                            );
                        })}
                    </div>
                    {selectedColor && !selectedColor.includes(',') && (
                        <button className={styles.btnSmall} style={{ width: '100%', marginTop: 8, padding: '4px', fontSize: 11, background: 'rgba(255,255,255,0.05)', border: '1px solid var(--border-subtle)', borderRadius: 4, color: 'var(--text-secondary)', cursor: 'pointer' }}
                            onClick={handleCreateGroup}>
                            Create Group from Selection
                        </button>
                    )}
                </div>

                <div className={styles.section}>
                    <span className={styles.sectionTitle}>Ban Status</span>
                    <label className={styles.checkboxLabel}>
                        <input type="checkbox" className={styles.checkbox} checked={vacBanned} onChange={(e) => onSearchChange(searchTerm, caseSensitive, selectedColor, e.target.checked, gameBanned, communityBanned, economyBanned)} /> VAC Banned
                    </label>
                    <label className={styles.checkboxLabel}>
                        <input type="checkbox" className={styles.checkbox} checked={gameBanned} onChange={(e) => onSearchChange(searchTerm, caseSensitive, selectedColor, vacBanned, e.target.checked, communityBanned, economyBanned)} /> Game Banned
                    </label>
                    <label className={styles.checkboxLabel}>
                        <input type="checkbox" className={styles.checkbox} checked={communityBanned} onChange={(e) => onSearchChange(searchTerm, caseSensitive, selectedColor, vacBanned, gameBanned, e.target.checked, economyBanned)} /> Community Banned
                    </label>
                    <label className={styles.checkboxLabel}>
                        <input type="checkbox" className={styles.checkbox} checked={economyBanned} onChange={(e) => onSearchChange(searchTerm, caseSensitive, selectedColor, vacBanned, gameBanned, communityBanned, e.target.checked)} /> Economy Banned
                    </label>
                </div>
            </aside>
        </div>
    );
};
