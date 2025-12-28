export interface Player {
    steamId64: string;
    steamId2: string;
    displayName: string;
    personaName: string;
    alias?: string;

    // Visuals
    avatarHash: string;
    playerColor?: string;
    cardColor?: string;
    personaNameColor?: string;
    aliasColor?: string;
    iconName?: string;
    iconColor?: string;

    // Status
    profileStatus: 'Public' | 'Private' | 'Unknown';
    isCommunityBanned: boolean;
    numberOfVacBans: number;
    numberOfGameBans: number;
    economyBan: string;
    lastVacCheck: number;
    banDate: number;

    // Meta
    timeCreated: number;
    lastUpdated?: number; // Unix timestamp, optional (0 or undefined = never updated)
}
