import { useState, useEffect } from 'react';

export function useDaemon() {
    const [isAppReady, setIsAppReady] = useState(false);
    const [initError, setInitError] = useState<string | null>(null);
    const [initStatusMsg, setInitStatusMsg] = useState('Initializing Core Engine...');
    const [initProgress, setInitProgress] = useState(0);
    const [migrationData, setMigrationData] = useState<{ recordCount: number, progress: number, status: string } | null>(null);

    useEffect(() => {
        let pollTimer: any;
        if (!isAppReady && !migrationData) {
            pollTimer = setInterval(async () => {
                try {
                    const res = await fetch('http://localhost:5000/health');
                    if (res.ok) {
                        const data = await res.json();
                        if (data.details) setInitStatusMsg(data.details);
                        if (data.progress) setInitProgress(data.progress);
                        
                        if (data.state === 'Ready') {
                            setIsAppReady(true);
                        } else if (data.migrationRequiredCount > 0) {
                            setMigrationData({ 
                                recordCount: data.migrationRequiredCount, 
                                progress: 0, 
                                status: 'Database upgrade needed (recovered)' 
                            });
                        }
                    }
                } catch (e) {
                    setInitStatusMsg('Waiting for daemon process...');
                }
            }, 1000);
        }
        return () => clearInterval(pollTimer);
    }, [isAppReady, migrationData]);

    useEffect(() => {
        const unsubscribe = window.ipcRenderer?.onBackend((msg) => {
            switch (msg.type) {
                case 'READY': setIsAppReady(true); setMigrationData(null); break;
                case 'MIGRATION_REQUIRED': setMigrationData({ recordCount: msg.data.count, progress: 0, status: 'Database upgrade needed' }); break;
                case 'MIGRATION_PROGRESS': setMigrationData(prev => prev ? { ...prev, progress: msg.data.percent, status: msg.data.status } : null); break;
                case 'MIGRATION_SUCCESS': setIsAppReady(true); setMigrationData(null); break;
            }
        });

        window.ipcRenderer?.on('backend-crash', (_e, code) => {
            setInitError(`Core Engine Failed (Exit Code: ${code})`);
            setIsAppReady(false);
        });

        return () => unsubscribe?.();
    }, []);

    const startMigration = (backup: boolean) => {
        window.ipcRenderer?.sendToBackend('START_MIGRATION', { backup });
    };

    return { isAppReady, initError, initStatusMsg, initProgress, migrationData, startMigration };
}
