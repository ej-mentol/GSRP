import React, { forwardRef } from 'react';
import styles from './ShareablePlayerCard.module.css';
import { Player } from '../../types';
import { Shield, ShieldAlert, Hammer, ShoppingBag } from 'lucide-react';

const getStyleForColor = (color: string | null | undefined, isText: boolean = true, defaultColor: string = '#fff') => {
    if (!color || color === '0' || color === 'none') {
        return isText ? { color: defaultColor } : { backgroundColor: defaultColor };
    }

    if (color.includes(';')) {
        const parts = color.split(';');
        const gradient = `linear-gradient(135deg, ${parts[0].trim()}, ${parts[1].trim()})`;
        if (isText) {
            return {
                background: gradient,
                WebkitBackgroundClip: 'text',
                WebkitTextFillColor: 'transparent',
                display: 'inline-block'
            };
        }
        return { background: gradient };
    }

    return isText ? { color } : { backgroundColor: color };
};

interface ShareableCardProps {
    player: Player | null;
}

export const ShareablePlayerCard = forwardRef<HTMLDivElement, ShareableCardProps>(({ player }, ref) => {
    const [imgError, setImgError] = React.useState(false);

    if (!player) return null;

    // Always use fallback to avoid CORS issues during export
    // const avatarUrl = ... 

    return (
        <div className={styles.shareCard} ref={ref} id="shareable-card">
            <div className={styles.avatarContainer}>
                <div className={styles.avatar} style={{ backgroundColor: '#27272a', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 24, fontWeight: 'bold' }}>
                    {player.displayName?.substring(0, 2).toUpperCase() || '??'}
                </div>
            </div>

            <div className={styles.info}>
                {/* Row 1: Display Name */}
                <div className={styles.row}>
                    <div className={styles.displayName} style={getStyleForColor(player.playerColor)}>{player.displayName}</div>
                </div>

                {/* Row 2: SteamID */}
                <div className={styles.row}>
                    <div className={styles.steamId}>{player.steamId2}</div>
                </div>

                {/* Row 3: Persona Name + Badges */}
                <div className={styles.row}>
                    <div className={styles.personaName} style={getStyleForColor(player.personaNameColor, true, 'var(--accent-steam)')}>
                        {player.personaName || player.displayName}
                    </div>
                    <div className={styles.badges}>
                        {(player.numberOfVacBans || 0) > 0 && (
                            <div className={`${styles.badge} ${styles.badgeRed}`}>
                                <ShieldAlert size={10} style={{ marginRight: 4 }} />
                                VAC Banned
                            </div>
                        )}
                        {player.isCommunityBanned && (
                            <div className={`${styles.badge} ${styles.badgeOrange}`}>
                                <Hammer size={10} style={{ marginRight: 4 }} />
                                Community
                            </div>
                        )}
                        {(player.economyBan && player.economyBan !== "0" && player.economyBan.toLowerCase() !== "none") && (
                            <div className={`${styles.badge} ${styles.badgePurple}`}>
                                <ShoppingBag size={10} style={{ marginRight: 4 }} />
                                Economy
                            </div>
                        )}
                    </div>
                </div>
            </div>

            <div className={styles.branding}>
                <Shield size={10} style={{ marginRight: 4 }} />
                <span>GSRP</span>
            </div>
        </div>
    );
});
