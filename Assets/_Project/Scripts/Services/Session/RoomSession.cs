using MafiaNight.Data;

namespace MafiaNight.Services.Session
{
    /// <summary>
    /// In-memory state of the room the local player chose in the Main Menu.
    /// Replaces passing the tier / entry fee between scenes through PlayerPrefs.
    /// Registered as a plain service by the Bootstrap.
    /// </summary>
    public sealed class RoomSession
    {
        public RoomTierData SelectedTier { get; private set; }
        public int EntryFeePaid { get; private set; }
        public bool HasRoom => SelectedTier != null;

        public void Join(RoomTierData tier, int entryFeePaid)
        {
            SelectedTier = tier;
            EntryFeePaid = entryFeePaid;
        }

        /// <summary>Returns the fee to refund and clears it so it can't be refunded twice.</summary>
        public int TakeRefund()
        {
            int fee = EntryFeePaid;
            EntryFeePaid = 0;
            return fee;
        }

        /// <summary>Call when the match actually starts: the fee is now spent.</summary>
        public void MarkFeeSpent() => EntryFeePaid = 0;

        public void Clear()
        {
            SelectedTier = null;
            EntryFeePaid = 0;
        }
    }
}
