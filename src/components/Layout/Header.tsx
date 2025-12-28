import React from 'react';
import styles from './Header.module.css';
import { WindowControls } from './WindowControls';
import packageJson from '../../../package.json';

interface HeaderProps {
    activeTab: string;
    playerCount: number;
    primaryAction?: {
        label: string;
        icon: React.ReactNode;
        onClick: () => void;
        colorClass?: string;
    };
}

export const Header: React.FC<HeaderProps> = ({ activeTab, playerCount, primaryAction }) => {
    
    return (
        <header className={styles.header}>
            <div className={styles.leftSection}>
                <span className={styles.pageTitle}>{activeTab}</span>
                <span className={styles.versionTag}>v{packageJson.version}</span>
                {activeTab === 'Players' && (
                    <span className={styles.playerCount}>{playerCount}</span>
                )}
            </div>

            <div className={styles.rightSection}>
                {primaryAction && (
                    <button 
                        className={styles.headerActionButton} 
                        onClick={primaryAction.onClick}
                    >
                        {primaryAction.icon}
                        <span>{primaryAction.label}</span>
                    </button>
                )}
                <WindowControls />
            </div>
        </header>
    );
};