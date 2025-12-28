// Player Helper Utilities
// Shared functions to ensure consistent logic across components

/**
 * Determines if a Steam profile is private based on TimeCreated field
 * @param timeCreated - Unix timestamp from Steam API (0 or < 100M = private)
 * @returns true if profile is private or invalid
 */
export const isPrivateProfile = (timeCreated: number | undefined): boolean => {
    // Steam launched in 2003, earliest valid timestamps are ~1B
    // Values below 100M (< March 1973) are considered invalid/private
    return !timeCreated || timeCreated < 100000000;
};

/**
 * Validates ban count to detect corrupted data from Steam API
 * @param count - Number of bans (VAC or Game)
 * @returns true if count is invalid/corrupted
 */
export const isCorruptedBanCount = (count: number | undefined): boolean => {
    // Realistic maximum: very few players have >100 bans
    // Values above this threshold are likely corrupt API responses
    return typeof count !== 'number' || count < 0 || count > 100;
};

/**
 * Safely gets ban count, returning 0 for corrupted/invalid values
 * @param count - Raw ban count from API
 * @returns Sanitized ban count (0 if invalid)
 */
export const getSafeBanCount = (count: number | undefined): number => {
    return isCorruptedBanCount(count) ? 0 : (count || 0);
};
