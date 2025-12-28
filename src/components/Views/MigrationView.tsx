import React, { useState } from 'react';
import styles from './MigrationView.module.css';
import { Coffee, CheckCircle2, AlertCircle } from 'lucide-react';

interface MigrationViewProps {
    recordCount: number;
    progress: number;
    status: string;
    onStart: (backup: boolean) => void;
}

export const MigrationView: React.FC<MigrationViewProps> = ({ recordCount, progress, status, onStart }) => {
    const [doBackup, setBackup] = useState(true);
    const [started, setStarted] = useState(false);

    const handleStart = () => {
        setStarted(true);
        onStart(doBackup);
    };

    return (
        <div className={styles.migrationScreen}>
            <Coffee size={64} className={styles.coffeeIcon} />
            
            <h1 className={styles.title}>Welcome back!</h1>
            <p className={styles.desc}>
                We found a legacy database with <b>{recordCount.toLocaleString()}</b> records. 
                We need to upgrade it to the new 2025 JS-friendly format before we can continue.
            </p>

            {!started ? (
                <>
                    <label className={styles.backupCheck}>
                        <input type="checkbox" checked={doBackup} onChange={e => setBackup(e.target.checked)} />
                        Create backup (recommended)
                    </label>
                    <button className={styles.goButton} onClick={handleStart}>
                        Let's Go!
                    </button>
                </>
            ) : (
                <div className={styles.progressBox}>
                    <div className={styles.progressBar}>
                        <div className={styles.progressFill} style={{ width: `${progress}%` }} />
                    </div>
                    <span className={styles.statusText}>{status || 'Initializing...'}</span>
                    {progress === 100 && (
                        <div style={{ color: 'var(--accent-green)', display: 'flex', alignItems: 'center', gap: 8, justifyContent: 'center', marginTop: 10 }}>
                            <CheckCircle2 size={16} /> Migration successful!
                        </div>
                    )}
                </div>
            )}
        </div>
    );
};
