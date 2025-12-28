import { Player } from '../types';

const NOW = Date.now();
const DAY_MS = 86400000;

export const mockPlayers: Player[] = [
    {
        steamId64: "76561198012345678",
        steamId2: "STEAM_0:0:12345678",
        displayName: "NEW_ACCOUNT_TEST", 
        personaName: "FreshMeat", 
        alias: "Rage Hacker",
        avatarHash: "placeholder", 
        playerColor: "#ff4444", 
        profileStatus: 'Public',
        isCommunityBanned: false,
        numberOfVacBans: 1, 
        numberOfGameBans: 0,
        economyBan: "none",
        lastVacCheck: NOW,
        banDate: NOW - (DAY_MS * 10), 
        timeCreated: (NOW - (DAY_MS * 1)) / 1000 
    },
    {
        steamId64: "76561197960287930",
        steamId2: "STEAM_0:0:13101",
        displayName: "Gabe Newell",
        personaName: "Rabscuttle",
        alias: "The Boss",
        avatarHash: "placeholder",
        playerColor: "#66ccff",
        aliasColor: "#ffd700",
        iconName: "shield.svg", // We'll mock this as a tinted SVG
        iconColor: "#fbbf24", // Golden tint
        iconPosition: "bottom-right",
        profileStatus: 'Public',
        isCommunityBanned: false,
        numberOfVacBans: 0,
        numberOfGameBans: 0,
        economyBan: "none",
        lastVacCheck: NOW,
        banDate: 0,
        timeCreated: 1100000000 
    },
    {
        steamId64: "76561198999999999",
        steamId2: "STEAM_0:1:88888888",
        displayName: "GENERIC_GAME_BAN",
        personaName: "LegitPlayer",
        avatarHash: "placeholder",
        iconName: "star.png", // PNG Example (no tint)
        iconPosition: "top-right",
        profileStatus: 'Public',
        isCommunityBanned: false,
        numberOfVacBans: 0,
        numberOfGameBans: 1,
        economyBan: "none",
        lastVacCheck: NOW,
        banDate: NOW - (DAY_MS * 50),
        timeCreated: (NOW - (DAY_MS * 365 * 5)) / 1000 
    }
];