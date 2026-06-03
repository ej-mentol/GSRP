import { useState, useEffect } from 'react';
import { Player } from '../types';

export function usePlayersData() {
    const [players, setPlayers] = useState<Player[]>([]);
    
    useEffect(() => {
        const unsubscribe = window.ipcRenderer?.onBackend((msg) => {
            switch (msg.type) {
                case 'PLAYERS_DETECTED': 
                    setPlayers(msg.data.players); 
                    break;
                case 'UPDATE_PLAYER':
                    setPlayers(prev => prev.map(p => {
                        if (p.steamId64 === msg.data.steamId64) {
                            return { ...p, ...msg.data, displayName: msg.data.displayName || p.displayName };
                        }
                        return p;
                    }));
                    break;
            }
        });
        return () => unsubscribe?.();
    }, []);

    return { players, setPlayers };
}
