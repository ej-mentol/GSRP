# GSRP - Player Database & Reporting

This is your "smart" player table. GSRP is a specialized desktop application designed to act as a "smart TAB button". Its primary function is to quickly identify players from copied game console output (e.g., `status` command), providing immediate, relevant information without needing to access friends lists or remember complex IDs. It offers a clean, efficient output for on-the-fly player identification.

**Note:** This version is a reimplementation using Electron (frontend) and .NET 9 (native backend).

## Important Notes

⚠️ A [Steam WEB API key](https://steamcommunity.com/dev/apikey) is required for full functionality. Without it, only the local database cache will be available.

**Security Advisory:** The theft of an API key can lead to account compromise. It is highly recommended to create a separate, alternate Steam account for use with this application. This isolates your main account from potential risks. The application encrypts the stored key using Windows DPAPI, meaning data files cannot be simply copied to another machine, but local malware could potentially access it.

## Features

*   **Quick Player Identification:** Copy game console output (e.g., `status` result) to clipboard, and GSRP automatically parses it to provide enriched player details.
*   **Steam API Integration:** Enriches profiles with data from Steam (VAC bans, avatars, account age). Data is cached to respect API rate limits.
*   **Database & Search:** Stores player history in a local SQLite database. Advanced search filters allow finding players by Ban Status (VAC, Game, Community, Economy) or assigned Color Tags.
*   **Visual Customization:** Assign custom colors to player names, Steam names, and aliases. Supports gradients and visual "card stripes" for quick identification of friend/foe.
*   **Console Integration:** View real-time in-game chat and logs via UDP (requires compatible Metahook plugin). Supports sending commands back to the game.
*   **Report Builder:** One-click generation of formatted reports (Server, Nick, SteamID, Reason) for server administrators.
*   **Shareable Cards:** Generate clean images of player cards for external sharing.

## Usage

### Players Tab
Shows players detected in the current session.
*   **Context Menu:** Right-click a card to customize appearance, set aliases, or copy data.
*   **Update Profile:** Manually refresh a player's data from Steam API.

### Database Tab
Search the local history of players.
*   **Filters:** Filter by Ban Status (VAC, Game, etc.) or Color Tags.
*   **Search:** Supports SteamID64, SteamID2, and Name search.

### Console Tab
A terminal for viewing game logs and chat via UDP.
*   **Protocol:** Compatible with MetahookSV ChatForwarder.
*   **Commands:** Allows sending rcon-like commands if configured.

## Getting Started

### Prerequisites
*   [Node.js](https://nodejs.org/) (v18+)
*   [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9) (for building the backend)
*   [Steam Web API Key](https://steamcommunity.com/dev/apikey)

### Building from Source

1.  Clone the repository:
    ```bash
    git clone https://github.com/ej-mentol/GSRP.git
    cd GSRP
    ```
2.  Install dependencies:
    ```bash
    yarn install
    ```
3.  Run in development mode:
    ```bash
    yarn dev
    ```
    *This starts the Vite server and automatically spawns the C# backend.*

4.  Build for release (Portable):
    ```bash
    yarn build
    ```
    *This compiles the .NET backend and packages the Electron app into `dist/win-unpacked`.*

### Data Location
The database (`gsrp.db`) and settings are stored in:
`%APPDATA%\GSRP\`

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
