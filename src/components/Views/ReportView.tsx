import React, { useState, useEffect, useRef } from 'react';
import styles from './ReportView.module.css';
import { X, UserPlus, AlertTriangle, ChevronDown, Search, Plus, Calendar, Hash, User, Check } from 'lucide-react';
import { defaultSettings } from '../../data/mockSettings';
import { Player } from '../../types';
import { ContextMenu } from '../UI/ContextMenu';
import { buildReportSlotMenu } from '../../menus/playerMenu';
import { ServerEditorModal } from './ServerEditorModal';

interface ReportViewProps {
    allPlayers: Player[];
    reportTargets: Player[];
    onAddTarget: (p: Player) => void;
    onRemoveTarget: (p: Player) => void;
    servers: string[];
    onUpdateServers: (servers: string[]) => void;
    onReportChange: (data: { server: string, tags: string[], details: string, template: string, prefix: string, suffix: string }) => void;
}

const PRESET_TAGS = ['CHEATING', 'HACK', 'ABUSE', 'WALLHACK', 'AIMBOT', 'SPAM', 'TOXIC'];

export const ReportView: React.FC<ReportViewProps> = ({ allPlayers, reportTargets, onAddTarget, onRemoveTarget, servers, onUpdateServers, onReportChange }) => {
    const [isPickerOpen, setIsPickerOpen] = useState(false);
    const [pickerSearch, setPickerSearch] = useState('');
    
    const [selectedServer, setSelectedServer] = useState(servers[0] || '');
    const [isServerModalOpen, setIsServerModalOpen] = useState(false);

    const [tags, setTags] = useState<string[]>([]);
    const [template, setTemplate] = useState(defaultSettings.reportTemplate);
    const [rawDetails, setDetails] = useState('');
    
    // Prefix/Suffix State
    const [prefix, setPrefix] = useState('`');
    const [suffix, setSuffix] = useState('`');

    const [chipMenu, setChipMenu] = useState<{ x: number, y: number, player: Player } | null>(null);
    const pickerRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        window.ipcRenderer?.invoke('get-setting', 'report').then(t => {
            if (t) setTemplate(t);
        });
    }, []);

    useEffect(() => {
        if (servers.length > 0) {
            if (!selectedServer || !servers.includes(selectedServer)) {
                setSelectedServer(servers[0]);
            }
        } else {
            setSelectedServer('');
        }
    }, [servers, selectedServer]);

    // Sync changes back to parent for Header action
    useEffect(() => {
        onReportChange({ server: selectedServer, tags, details: rawDetails, template, prefix, suffix });
    }, [selectedServer, tags, rawDetails, template, prefix, suffix, onReportChange]);

    const handleQuickTag = (tag: string) => {
        if (!tags.includes(tag)) setTags([...tags, tag]);
    };

    const handleChipContextMenu = (e: React.MouseEvent, player: Player) => {
        e.preventDefault();
        setChipMenu({ x: e.clientX, y: e.clientY, player });
    };

    return (
        <div className={styles.container} onClick={() => setChipMenu(null)}>
            <main className={styles.builderArea}>
                
                <section className={styles.section}>
                    <div className={styles.headerRow}>
                        <div className={styles.label}>Players to Report</div>
                    </div>
                    <div className={styles.playerGrid}>
                        {reportTargets.map(player => (
                            <div key={player.steamId64} className={styles.playerChip} onContextMenu={(e) => handleChipContextMenu(e, player)}>
                                <div className={styles.chipInfo}>
                                    <span className={styles.chipName}>{player.displayName}</span>
                                    <span className={styles.chipId}>{player.steamId2}</span>
                                </div>
                                <X size={14} className={styles.removeChip} onClick={() => onRemoveTarget(player)} />
                            </div>
                        ))}
                        
                        <div style={{ position: 'relative' }} ref={pickerRef}>
                            <div className={styles.addPlayerSlot} onClick={() => setIsPickerOpen(!isPickerOpen)}>
                                <UserPlus size={18} />
                            </div>
                            {isPickerOpen && (
                                <div className={styles.pickerDropdown}>
                                    <div className={styles.pickerSearch}><Search size={14} /><input autoFocus placeholder="Find..." value={pickerSearch} onChange={e => setPickerSearch(e.target.value)} /></div>
                                    <div className={styles.pickerList}>
                                        {allPlayers.filter(p => p.displayName.toLowerCase().includes(pickerSearch.toLowerCase())).map(p => (
                                            <div key={p.steamId64} className={styles.pickerItem} onClick={() => { onAddTarget(p); setIsPickerOpen(false); setPickerSearch(''); }}>
                                                <span className={styles.chipName}>{p.displayName}</span>
                                                <span className={styles.chipId}>{p.steamId2}</span>
                                            </div>
                                        ))}
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>
                </section>

                <section className={styles.section}>
                    <div className={styles.headerRow}>
                        <div className={styles.label}>Server</div>
                        <button className={styles.labelPlus} onClick={() => setIsServerModalOpen(true)} title="Edit Server List"><Plus size={14} /></button>
                    </div>
                    <div className={styles.selectWrapper}>
                        <select className={styles.customSelect} value={selectedServer} onChange={(e) => setSelectedServer(e.target.value)}>
                            {servers.map(s => <option key={s} value={s}>{s}</option>)}
                        </select>
                        <ChevronDown size={14} style={{ position: 'absolute', right: 12, top: 12, pointerEvents: 'none', color: 'var(--text-muted)' }} />
                    </div>
                </section>

                <section className={styles.section}>
                    <div className={styles.headerRow}>
                        <div className={styles.label}>Quick Presets</div>
                    </div>
                    <div className={styles.quickTagsBar}>
                        {PRESET_TAGS.map(t => (
                            <button key={t} className={styles.quickTagButton} onClick={() => handleQuickTag(t)}>{t}</button>
                        ))}
                    </div>
                </section>

                <section className={`${styles.section} ${styles.flexSection}`}>
                    <div className={styles.label}>What happened?</div>
                    <div className={styles.tagInputContainer}>
                        <div className={styles.tagRow}>
                            {tags.map(tag => (
                                <div key={tag} className={styles.smartTag}><AlertTriangle size={12} /> {tag}<X size={12} style={{ cursor: 'pointer' }} onClick={() => setTags(tags.filter(t => t !== tag))} /></div>
                            ))}
                        </div>
                        <textarea className={styles.memoField} placeholder="Describe details..." value={rawDetails} onChange={(e) => setDetails(e.target.value)} />
                    </div>
                </section>
            </main>

            <aside className={styles.templateSidebar}>
                <div className={styles.label}>Report Template</div>
                <textarea className={styles.memoField} style={{ backgroundColor: 'var(--bg-main)', padding: 12, borderRadius: 8, height: 160, fontSize: 12, fontFamily: 'JetBrains Mono', border: '1px solid var(--border-subtle)', flexShrink: 0 }} value={template} onChange={(e) => setTemplate(e.target.value)} />
                <div className={styles.section} style={{ marginTop: 10 }}>
                    <span className={styles.label} style={{ fontSize: 10, color: 'var(--accent-blue)' }}>Insert Helper</span>
                    
                    <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
                        <div style={{ flex: 1 }}>
                            <div style={{ fontSize: 9, color: 'var(--text-muted)', marginBottom: 4, fontWeight: 700, textTransform: 'uppercase' }}>Prefix</div>
                            <input className={styles.compactInput} style={{ height: 28, fontSize: 13, textAlign: 'center' }} placeholder="`" value={prefix} onChange={e => setPrefix(e.target.value)} />
                        </div>
                        <div style={{ flex: 1 }}>
                            <div style={{ fontSize: 9, color: 'var(--text-muted)', marginBottom: 4, fontWeight: 700, textTransform: 'uppercase' }}>Suffix</div>
                            <input className={styles.compactInput} style={{ height: 28, fontSize: 13, textAlign: 'center' }} placeholder="`" value={suffix} onChange={e => setSuffix(e.target.value)} />
                        </div>
                    </div>

                    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                        {['ServerName', 'PlayerName', 'SteamId', 'Details'].map(v => (
                            <button key={v} className={styles.variableButton} onClick={() => setTemplate(prev => prev + `\${${v}}`)}>${v}</button>
                        ))}
                    </div>
                </div>
            </aside>

            <ServerEditorModal 
                isOpen={isServerModalOpen} 
                onClose={() => setIsServerModalOpen(false)} 
                currentServers={servers}
                onSave={onUpdateServers}
            />

            {chipMenu && (
                <ContextMenu x={chipMenu.x} y={chipMenu.y} items={buildReportSlotMenu(chipMenu.player, () => onRemoveTarget(chipMenu.player))} onClose={() => setChipMenu(null)} />
            )}
        </div>
    );
};