import React from 'react';
import styles from './Sidebar.module.css';
import { 
    Users, 
    FileText, 
    Terminal, 
    Database, 
    Settings, 
    Shield 
} from 'lucide-react';

interface SidebarProps {
    activeTab: string;
    onTabChange: (tab: string) => void;
}

export const Sidebar: React.FC<SidebarProps> = ({ activeTab, onTabChange }) => {
    
    const menuItems = [
        { id: 'Players', label: 'Players', icon: Users },
        { id: 'Report', label: 'Report Builder', icon: FileText },
        { id: 'Console', label: 'Console', icon: Terminal },
        { id: 'DB Search', label: 'Database', icon: Database },
    ];

    return (
        <div className={styles.sidebar}>
            {/* Logo - Icon Only */}
            <div className={styles.logoArea}>
                <Shield className={styles.logoIcon} size={24} strokeWidth={2.5} fill="currentColor" fillOpacity={0.1} />
            </div>

            {/* Main Nav */}
            <div className={styles.nav}>
                {menuItems.map(item => (
                    <button 
                        key={item.id}
                        className={`${styles.navItem} ${activeTab === item.id ? styles.navItemActive : ''}`}
                        onClick={() => onTabChange(item.id)}
                        title={item.label} // Tooltip is essential now
                    >
                        <item.icon className={styles.navIcon} size={22} strokeWidth={2} />
                    </button>
                ))}
            </div>

            {/* Bottom Settings */}
            <div className={styles.footer}>
                <button 
                    className={`${styles.navItem} ${activeTab === 'Settings' ? styles.navItemActive : ''}`}
                    onClick={() => onTabChange('Settings')}
                    title="Settings"
                >
                    <Settings className={styles.navIcon} size={22} strokeWidth={2} />
                </button>
            </div>
        </div>
    );
};