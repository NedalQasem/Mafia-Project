using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>Fills the top bar / profile card with mock data and wires the card. Used by every screen prototype scene.</summary>
    public class KitBarsDemo : MonoBehaviour
    {
        [SerializeField] private KitTopBar topBar;
        [SerializeField] private KitNavBar navBar;
        [SerializeField] private KitProfileCard card;

        public KitTopBar TopBar => topBar;
        public KitNavBar NavBar => navBar;

        public static int Coins = 1250;

        private void Start()
        {
            topBar.SetPlayer("أبو سالم", "صاحب كلمة", 7, 2, 0.62f, Coins);
            card.SetPlayer("أبو سالم", "صاحب كلمة", 7, 2, 0.62f, 620, 1000, 34, 12);
            card.SetRoles(15, 5, 7, 7);
            topBar.OnProfileClicked += card.Show;
            navBar.SnapToSelected();
            var store = FindAnyObjectByType<KitStoreScreen>();
            if (store != null) { store.Coins = Coins; store.OnCoinsChanged += SetCoins; }
        }

        public void SetCoins(int value) { Coins = value; topBar.SetPlayer("أبو سالم", "صاحب كلمة", 7, 2, 0.62f, value, true); }
    }
}
