export interface AppSettings {
    udpListenPort: number;
    udpSendPort: number;
    udpSendAddress: string;
    reportTemplate: string;
    servers: string[];
    dbSidebarWidth: number;
    iconCorner: number; // 0: TL, 1: TR, 2: BL, 3: BR
    enablePeriodicVacCheck: boolean;
    enableAvatarCdn: boolean;
}

export const defaultSettings: AppSettings = {
    udpListenPort: 26000,
    udpSendPort: 26001,
    udpSendAddress: "127.0.0.1",
    reportTemplate: "Server name: ${ServerName}\nWho are you reporting?: ${PlayerName}\nHis SteamId: ${SteamId}\nWhat happened?: ${Details}\nEvidence:\n",
    servers: [
        "[EU] Die-Hard (+Anti-Rush)",
        "[EU] Hardcore Survival (+Anti-Rush)",
        "[US] Hardcore Survival (+Anti-Rush)",
        "[EU] Survival (+Anti-Rush)",
        "[US] Survival (+Anti-Rush)"
    ],
    dbSidebarWidth: 280,
    iconCorner: 3, // Default to Bottom-Right
    enablePeriodicVacCheck: true,
    enableAvatarCdn: true
};