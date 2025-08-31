namespace GSRP.Models.SteamApi
{
    public class PlayerData
    {
        public string PersonaName { get; set; } = string.Empty;
        public uint TimeCreated { get; set; }
        public string AvatarHash { get; set; } = string.Empty;
        public bool IsPrivate { get; set; }
    }
}
