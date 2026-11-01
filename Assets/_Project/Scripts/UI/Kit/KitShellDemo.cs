using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>Prototype wiring for the shell scene: top bar, nav bar and the hanging profile card.</summary>
    public class KitShellDemo : MonoBehaviour
    {
        [SerializeField] private KitTopBar topBar;
        [SerializeField] private KitNavBar navBar;
        [SerializeField] private KitProfileCard card;
        [SerializeField] private ArabicText placeholder;

        private static readonly string[] Names = { "السوق", "العائلة", "الغرف", "الرتب", "الإعدادات" };

        private void Start()
        {
            topBar.SetPlayer("أبو سالم", "صاحب كلمة", 7, 2, 0.62f, 1250);
            card.SetPlayer("أبو سالم", "صاحب كلمة", 7, 2, 0.62f, 620, 1000, 34, 12);
            card.SetRoles(15, 5, 7, 7);
            topBar.OnProfileClicked += card.Show;
            navBar.OnTabSelected += i => placeholder.Text = Names[i];
            placeholder.Text = Names[navBar.Selected];
            navBar.SnapToSelected();
        }
    }
}
