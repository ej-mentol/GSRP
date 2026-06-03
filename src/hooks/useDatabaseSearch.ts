import { useState, useEffect } from 'react';
import { Player } from '../types';

export function useDatabaseSearch() {
    const [dbSearchTerm, setDbSearchTerm] = useState('');
    const [dbSearchCaseSensitive, setDbSearchCaseSensitive] = useState(false);
    const [dbSearchColor, setDbSearchColor] = useState<string | null>(null);
    const [dbSearchVac, setDbSearchVac] = useState(false);
    const [dbSearchGame, setDbSearchGame] = useState(false);
    const [dbSearchComm, setDbSearchComm] = useState(false);
    const [dbSearchEco, setDbSearchEco] = useState(false);
    
    const [dbResults, setDbResults] = useState<Player[]>([]);
    const [dbUniqueColors, setDbUniqueColors] = useState<string[]>([]);
    const [isDbLoading, setIsDbLoading] = useState(false);

    const refreshUniqueColors = () => {
        window.ipcRenderer?.sendToBackend('GET_DB_COLORS', {});
    };

    useEffect(() => {
        refreshUniqueColors();
        
        const unsubscribe = window.ipcRenderer?.onBackend((msg) => {
            if (msg.type === 'SEARCH_RESULT') {
                setDbResults(msg.data.players || []); 
                setIsDbLoading(false);
            } else if (msg.type === 'DB_COLORS_RESULT') {
                setDbUniqueColors(msg.data.colors || []);
            } else if (msg.type === 'UPDATE_PLAYER') {
                setDbResults(prev => prev.map(p => {
                    if (p.steamId64 === msg.data.steamId64) {
                        return { ...p, ...msg.data, displayName: msg.data.displayName || p.displayName };
                    }
                    return p;
                }));
                // Optionally refresh unique colors when a player is updated
                refreshUniqueColors();
            }
        });
        return () => unsubscribe?.();
    }, []);

    useEffect(() => {
        const timer = setTimeout(() => {
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
        }, 300);
        return () => clearTimeout(timer);
    }, [dbSearchTerm, dbSearchCaseSensitive, dbSearchColor, dbSearchVac, dbSearchGame, dbSearchComm, dbSearchEco]);

    const setDbFilters = (t: string, cs: boolean, color: string | null, vac: boolean, game: boolean, comm: boolean, eco: boolean) => {
        setDbSearchTerm(t); 
        setDbSearchCaseSensitive(cs); 
        setDbSearchColor(color); 
        setDbSearchVac(vac); 
        setDbSearchGame(game); 
        setDbSearchComm(comm); 
        setDbSearchEco(eco);
    };

    return {
        dbSearchTerm,
        dbSearchCaseSensitive,
        dbSearchColor,
        dbSearchVac,
        dbSearchGame,
        dbSearchComm,
        dbSearchEco,
        dbResults,
        dbUniqueColors,
        isDbLoading,
        setDbFilters
    };
}
