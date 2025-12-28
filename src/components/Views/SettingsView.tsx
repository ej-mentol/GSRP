import React, { useState, useEffect } from 'react';
import styles from './SettingsView.module.css';
import { HardDrive, Database, Save } from 'lucide-react';
import { AppSettings } from '../../data/mockSettings'; // Import Type only

const INITIAL_SETTINGS: AppSettings = {
    udpListenPort: 26000,
    udpSendPort: 26001,
    udpSendAddress: "127.0.0.1",
    reportTemplate: "",
    servers: [],
    dbSidebarWidth: 280,
    iconCorner: 3,
    enablePeriodicVacCheck: true,
    enableAvatarCdn: true
};

interface SettingsViewProps {
    onSettingsSaved?: () => void;
}

export const SettingsView: React.FC<SettingsViewProps> = ({ onSettingsSaved }) => {
    const [settings, setSettings] = useState<AppSettings>(INITIAL_SETTINGS);
    const [apiKey, setApiKey] = useState('');
    const [statusMsg, setStatusMsg] = useState('');

    useEffect(() => {
        // Load all settings
        const load = async () => {
            const keys = Object.keys(INITIAL_SETTINGS) as (keyof AppSettings)[];
            const loaded = { ...INITIAL_SETTINGS };
            for (const key of keys) {
                const val = await window.ipcRenderer?.invoke('get-setting', key);
                if (val !== undefined && val !== null) {
                    // @ts-ignore
                    loaded[key] = val;
                }
            }
            setSettings(loaded);
        };
        load();
    }, []);

    const handleChange = (key: keyof AppSettings, value: any) => {
        setSettings(prev => ({ ...prev, [key]: value }));
    };

    const saveAll = async () => {
        setStatusMsg('Saving...');
        try {
            // Save settings.json
            for (const [key, value] of Object.entries(settings)) {
                window.ipcRenderer?.send('save-setting', { key, value });
            }

            // Notify Backend to reload settings
            window.ipcRenderer?.sendToBackend('UPDATE_SETTING', {});

            // Save API Key if changed
            if (apiKey.trim()) {
                window.ipcRenderer?.sendToBackend('SET_API_KEY', { key: apiKey.trim() });
                setApiKey('');
            }

            if (onSettingsSaved) onSettingsSaved(); // REFRESH GLOBAL STATE

            setStatusMsg('Settings Saved & Applied');
            setTimeout(() => setStatusMsg(''), 3000);
        } catch (e) {
            setStatusMsg('Error saving settings');
        }
    };

    const runMaintenance = (type: string) => {
        setStatusMsg('Running maintenance...');
        window.ipcRenderer?.sendToBackend(type, {});
        setTimeout(() => setStatusMsg(''), 3000);
    };

    return (
        <div className={styles.container}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
                <h1 className={styles.title}>Settings</h1>
                {statusMsg && <span style={{ color: 'var(--accent-green)', fontSize: 13, fontWeight: 600 }}>{statusMsg}</span>}
            </div>

            {/* NETWORK */}
            <section className={styles.section}>
                <h2 className={styles.sectionTitle}>Network & API</h2>
                <div className={styles.settingItem}>
                    <div className={styles.settingInfo}>
                        <span className={styles.settingLabel}>Steam Web API Key</span>
                        <span className={styles.settingDesc} style={{ color: 'var(--accent-blue)', cursor: 'pointer', textDecoration: 'underline' }} onClick={() => window.ipcRenderer?.send('open-external', 'https://steamcommunity.com/dev/apikey')}>Get Key</span>
                    </div>
                    <div style={{ display: 'flex', gap: 10 }}>
                        <input
                            type="password"
                            className={styles.input}
                            placeholder="••••••••••••••••••••••••••••••••"
                            value={apiKey}
                            onChange={(e) => setApiKey(e.target.value)}
                            style={{ width: 240 }}
                        />
                        <button className={styles.button} onClick={() => {
                            if (apiKey) {
                                console.log('UI: Applying API Key, length:', apiKey.length);
                                window.ipcRenderer?.sendToBackend('SET_API_KEY', { key: apiKey });
                            }
                            setApiKey('');
                        }}>Apply</button>
                    </div>
                </div>
                <div className={styles.settingItem}>
                    <div className={styles.settingInfo}>
                        <span className={styles.settingLabel}>Periodic VAC Check</span>
                        <span className={styles.settingDesc}>Regularly re-verify ban status for all players in database (every 24h).</span>
                    </div>
                    <label className={styles.checkboxLabel} style={{ marginLeft: 'auto' }}>
                        <input
                            type="checkbox"
                            className={styles.checkbox}
                            checked={!!settings.enablePeriodicVacCheck}
                            onChange={(e) => handleChange('enablePeriodicVacCheck', e.target.checked)}
                        />
                    </label>
                </div>
                <div className={styles.settingItem}>
                    <div className={styles.settingInfo}>
                        <span className={styles.settingLabel}>UDP Listener Port</span>
                        <span className={styles.settingDesc}>Receiving logs from MetaHookSV plugin.</span>
                    </div>
                    <input
                        type="number"
                        className={styles.input}
                        value={settings.udpListenPort}
                        onChange={(e) => handleChange('udpListenPort', parseInt(e.target.value) || 0)}
                    />
                </div>
            </section>

            {/* APPEARANCE */}
            <section className={styles.section}>
                <h2 className={styles.sectionTitle}>Appearance</h2>
                <div className={styles.settingItem}>
                    <div className={styles.settingInfo}>
                        <span className={styles.settingLabel}>Icon Corner</span>
                        <span className={styles.settingDesc}>Global position for rank icons on avatars.</span>
                    </div>
                    <select
                        className={styles.input}
                        value={settings.iconCorner}
                        onChange={(e) => handleChange('iconCorner', parseInt(e.target.value))}
                    >
                        <option value={0}>Top Left</option>
                        <option value={1}>Top Right</option>
                        <option value={2}>Bottom Left</option>
                        <option value={3}>Bottom Right</option>
                    </select>
                </div>
                <div className={styles.settingItem}>
                    <div className={styles.settingInfo}>
                        <span className={styles.settingLabel}>CDN Avatar Priority</span>
                        <span className={styles.settingDesc}>Skip local cache check and load avatars directly from Steam.</span>
                    </div>
                    <label className={styles.checkboxLabel} style={{ marginLeft: 'auto' }}>
                        <input
                            type="checkbox"
                            className={styles.checkbox}
                            checked={!!settings.enableAvatarCdn}
                            onChange={(e) => handleChange('enableAvatarCdn', e.target.checked)}
                        />
                    </label>
                </div>
            </section>

            {/* SERVERS LIST */}
            <section className={styles.section}>
                <h2 className={styles.sectionTitle}>Servers List</h2>
                <div className={styles.settingItem} style={{ flexDirection: 'column', alignItems: 'flex-start', gap: 10 }}>
                    <div className={styles.settingDesc}>Enter each server name on a new line.</div>
                    <textarea
                        className={styles.input}
                        style={{ width: '100%', height: 120, fontFamily: 'inherit', fontSize: 13 }}
                        value={settings.servers.join('\n')}
                        onChange={(e) => handleChange('servers', e.target.value.split('\n').filter(s => s.trim()))}
                        placeholder="[EU] My Server..."
                    />
                </div>
            </section>

            {/* REPORT TEMPLATE */}
            <section className={styles.section}>
                <h2 className={styles.sectionTitle}>Report Template</h2>
                <div className={styles.settingItem} style={{ flexDirection: 'column', alignItems: 'flex-start', gap: 10 }}>
                    <div className={styles.settingDesc}>Variables: ${'{ServerName}'}, ${'{PlayerName}'}, ${'{SteamId}'}, ${'{Details}'}</div>
                    <textarea
                        className={styles.input}
                        style={{ width: '100%', height: 100, fontFamily: 'monospace', fontSize: 12 }}
                        value={settings.reportTemplate}
                        onChange={(e) => handleChange('reportTemplate', e.target.value)}
                    />
                </div>
            </section>

            {/* MAINTENANCE */}
            <section className={styles.section}>
                <h2 className={styles.sectionTitle}>Maintenance</h2>
                <div className={styles.settingItem}>
                    <div className={styles.settingInfo}>
                        <span className={styles.settingLabel}>Clean Orphaned Avatars</span>
                        <span className={styles.settingDesc}>Remove images from cache that are no longer in the database.</span>
                    </div>
                    <button className={`${styles.button} ${styles.buttonSecondary}`} onClick={() => runMaintenance('CLEAN_CACHE')}>
                        <HardDrive size={14} style={{ marginRight: 8 }} /> Clean Cache
                    </button>
                </div>
                <div className={styles.settingItem}>
                    <div className={styles.settingInfo}>
                        <span className={styles.settingLabel}>Optimize Database</span>
                        <span className={styles.settingDesc}>Run VACUUM to reduce database size and improve performance.</span>
                    </div>
                    <button className={`${styles.button} ${styles.buttonSecondary}`} onClick={() => runMaintenance('OPTIMIZE_DB')}>
                        <Database size={14} style={{ marginRight: 8 }} /> Optimize DB
                    </button>
                </div>
            </section>

            <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end', marginTop: 20 }}>
                <button className={styles.button} onClick={saveAll}>
                    <Save size={16} style={{ marginRight: 8 }} /> Save All Settings
                </button>
            </div>
        </div>
    );
};
