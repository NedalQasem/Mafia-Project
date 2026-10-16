namespace MafiaNight.Data.Player
{
    /// <summary>
    /// Player rank tiers with point thresholds
    /// </summary>
    public enum PlayerRank
    {
        Bronze = 0,      // 0-999 points
        Silver = 1,      // 1000-1999
        Gold = 2,        // 2000-2999
        Platinum = 3,    // 3000-3999
        Diamond = 4,     // 4000-4999
        Master = 5,      // 5000-5999
        Grandmaster = 6  // 6000+
    }
    
    /// <summary>
    /// Rarity levels for cosmetic items
    /// </summary>
    public enum ItemRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }
}
