import React from 'react';
import styles from './PlayerCard.module.css';
import { Player } from '../../types';
import { Clock, Lock, ShieldAlert, Hammer, ShoppingBag, Info } from 'lucide-react';
import { defaultSettings } from '../../data/mockSettings';
import { isPrivateProfile, getSafeBanCount } from '../../utils/playerHelpers';

const getStyleForColor = (color: string | null | undefined, isText: boolean = true, defaultColor: string = 'var(--text-primary)') => {
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

interface PlayerCardProps {
    player: Player;
    onContextMenu: (e: React.MouseEvent, player: Player) => void;
    onAvatarContextMenu: (e: React.MouseEvent, player: Player) => void;
    avatarPriority?: 'cache' | 'cdn'; // New
    isDatabaseEntry?: boolean;
}

export const PlayerCard: React.FC<PlayerCardProps> = ({
    player,
    onContextMenu,
    onAvatarContextMenu,
    avatarPriority = 'cache',
    isDatabaseEntry = false
}) => {
    const [imgError, setImgError] = React.useState(false);
    const [source, setSource] = React.useState<'cache' | 'cdn'>(avatarPriority);

    // Reset state when player changes or priority changes
    React.useEffect(() => {
        setImgError(false);
        setSource(avatarPriority);
    }, [player.avatarHash, player.steamId64, avatarPriority]);

    const getInitials = (name: string) => (name || "??").substring(0, 2).toUpperCase();

    // --- REGISTRATION ---
    const getRegistrationInfo = () => {
        if (isPrivateProfile(player.timeCreated)) {
            return { text: 'Private Profile', icon: Lock, color: 'var(--text-muted)' };
        }

        const regDate = new Date(player.timeCreated * 1000);
        const ageInMs = Date.now() - regDate.getTime();
        const ageInDays = Math.floor(ageInMs / 86400000);
        const ageInYears = Math.floor(ageInDays / 365);

        const dateText = regDate.toLocaleDateString('ru-RU');
        const displayLabel = ageInYears > 0 ? `${dateText} (${ageInYears}y)` : dateText;

        let color = 'var(--text-secondary)';
        if (ageInDays >= 0 && ageInDays <= 30) {
            if (ageInDays <= 7) color = 'var(--accent-red)';
            else color = '#f97316'; // Orange for relatively new
        }
        return { text: displayLabel, icon: Clock, color };
    };
    const regInfo = getRegistrationInfo();

    // --- BAN LOGIC ---
    const vacCount = getSafeBanCount(player.numberOfVacBans);
    const gameCount = getSafeBanCount(player.numberOfGameBans);
    const isComBanned = player.isCommunityBanned === true;
    const ecoStatus = (player.economyBan && player.economyBan !== "0") ? player.economyBan : null;

    const getDaysSinceBan = () => {
        const bd = Number(player.banDate);
        if (!bd || bd < 1000000) return null;
        return Math.floor((Date.now() - (bd * 1000)) / 86400000);
    };

    const formatBanAge = (days: number | null) => {
        if (days === null) return '';
        if (days === 0) return 'today';
        if (days > 365) return `${Math.floor(days / 365)}y ago`;
        return `${days}d ago`;
    };
    const banAge = formatBanAge(getDaysSinceBan());

    let redBadgeText = '';
    if (vacCount > 0) {
        redBadgeText += vacCount === 1 ? 'VAC ban' : `${vacCount} VAC bans`;
        if (banAge) redBadgeText += ` ${banAge}`;
    }
    if (gameCount > 0) {
        if (redBadgeText) redBadgeText += ' | ';
        redBadgeText += gameCount === 1 ? 'Game ban' : `${gameCount} Game bans`;
    }

    // --- AVATAR URL LOGIC ---
    const avatarUrl = player.avatarHash && player.avatarHash.length > 5 && player.avatarHash !== "0"
        ? (source === 'cache'
            ? `gsrp-cache://${player.avatarHash}.jpg`
            : `https://avatars.steamstatic.com/${player.avatarHash}_medium.jpg`)
        : null;

    const handleImgError = () => {
        if (source === 'cache') {
            setSource('cdn');
        } else {
            setImgError(true);
        }
    };

    const renderRankIcon = () => {
        if (!player.iconName) return null;
        const cornerMap: Record<number, string> = { 0: styles['top-left'], 1: styles['top-right'], 2: styles['bottom-left'], 3: styles['bottom-right'] };
        const posClass = cornerMap[defaultSettings.iconCorner] || styles['bottom-right'];
        const iconUrl = `gsrp-icon://${player.iconName}`;
        return (
            <div className={`${styles.rankIconContainer} ${posClass}`}>
                {player.iconName.endsWith('.svg') ? (
                    <div className={styles.rankIconSvg} style={{ backgroundColor: player.iconColor || '#fff', maskImage: `url(${iconUrl})`, WebkitMaskImage: `url(${iconUrl})` }} />
                ) : (
                    <img src={iconUrl} className={styles.rankIconImg} alt="rank" crossOrigin="anonymous" />
                )}
            </div>
        );
    };

    const isNewPlayer = !player.lastUpdated || player.lastUpdated === 0;

    const hasAnyBan = (vacCount > 0) || (gameCount > 0) || isComBanned || ecoStatus;

    return (
        <div className={`${styles.card} ${hasAnyBan ? styles.hasBan : ''}`} onContextMenu={(e) => onContextMenu(e, player)}>
            <div className={`${styles.avatarContainer} ${isNewPlayer ? styles.newPlayerRing : ''}`} onContextMenu={(e) => { e.stopPropagation(); onAvatarContextMenu(e, player); }}>
                {avatarUrl && !imgError ? (
                    <img
                        src={avatarUrl}
                        className={styles.avatar}
                        style={{ opacity: 1 }}
                        onError={handleImgError}
                        loading="lazy"
                        crossOrigin="anonymous"
                    />
                ) : (
                    <div className={styles.avatarFallback}>{getInitials(player.displayName)}</div>
                )}
                {renderRankIcon()}
            </div>

            <div className={styles.info}>
                {/* ROW 1: Display Name [Alias] */}
                <div className={styles.row}>
                    <span className={styles.displayName} style={getStyleForColor(player.playerColor)}>
                        {isDatabaseEntry ? player.personaName : player.displayName}
                    </span>
                    {isDatabaseEntry && (
                        <span
                            title="Archival name from Steam Profile (not from game console)."
                            style={{ marginLeft: 6, display: 'inline-flex', alignItems: 'center', opacity: 0.4, cursor: 'help' }}
                        >
                            <Info size={10} color="var(--text-muted)" />
                        </span>
                    )}
                    {!isDatabaseEntry && player.alias && (
                        <div className={styles.alias} style={getStyleForColor(player.aliasColor, false, 'var(--accent-blue)')}>
                            {player.alias}
                        </div>
                    )}
                </div>

                {/* ROW 2: SteamID */}
                <div className={styles.row}>
                    <div className={styles.steamId}>
                        {player.steamId2 || "Unknown"}
                        {isDatabaseEntry && (
                            <span
                                title="Calculated from Steam64. May vary slightly on some servers (STEAM_0 vs STEAM_1)."
                                style={{ marginLeft: 4, cursor: 'help', opacity: 0.5, fontSize: 10 }}
                            >
                                *
                            </span>
                        )}
                    </div>
                </div>

                {/* ROW 3: Steam Nick [Ban Badges] */}
                <div className={styles.row}>
                    <div className={styles.personaName} style={getStyleForColor(player.personaNameColor, true, 'var(--accent-steam)')}>
                        {player.personaName || player.displayName}
                    </div>
                    <div className={styles.badges}>
                        {redBadgeText && (
                            <div className={`${styles.badge} ${styles.badgeRed}`}>
                                <ShieldAlert size={10} style={{ marginRight: 4 }} />
                                {redBadgeText}
                            </div>
                        )}
                        {isComBanned && (
                            <div className={`${styles.badge} ${styles.badgeOrange}`}>
                                <Hammer size={10} style={{ marginRight: 4 }} />
                                Community Ban
                            </div>
                        )}
                        {ecoStatus && (
                            <div className={`${styles.badge} ${styles.badgePurple}`}>
                                <ShoppingBag size={10} style={{ marginRight: 4 }} />
                                Economy Ban
                            </div>
                        )}
                    </div>
                </div>
            </div>

            <div className={styles.status}>
                {regInfo.icon && <regInfo.icon size={14} style={{ marginRight: 6, color: 'var(--text-muted)' }} />}
                <span className={styles.statusText} style={{ color: regInfo.color }}>{regInfo.text}</span>
            </div>
        </div>
    );
};
