using TMPro;
using UnityEngine;

namespace MafiaNight.UI.Kit
{
    /// <summary>Design tokens + sprite/font references for the mafia UI kit (one asset: Resources/Kit/KitTheme).</summary>
    [CreateAssetMenu(fileName = "KitTheme", menuName = "Mafia Night/UI Kit Theme")]
    public class KitTheme : ScriptableObject
    {
        [Header("Fonts (TMP assets with Arabic Presentation Forms)")]
        public TMP_FontAsset fontBody;          // Cairo
        public TMP_FontAsset fontHeading;       // Reem Kufi
        public TMP_FontAsset fontCalligraphy;   // Aref Ruqaa
        public TMP_FontAsset fontLalezar, fontAref;   // match screens: the exact web-mockup fonts (converted, see Art/Fonts/FONT-LICENSES.txt)

        [Header("Prefabs the app creates at runtime")]
        public GameObject launchBannerPrefab;

        [Header("Colors")]
        public Color ivory = new Color32(0xD9, 0xD0, 0xBD, 255);
        public Color ivoryDim = new Color32(0x8F, 0x87, 0x77, 255);
        public Color brass = new Color32(0xB9, 0x90, 0x48, 255);
        public Color brassLight = new Color32(0xF5, 0xE2, 0xA8, 255);
        public Color crimson = new Color32(0x8A, 0x1C, 0x24, 255);
        public Color teal = new Color32(0x4F, 0xA5, 0x8B, 255);
        public Color ink = new Color32(0x1C, 0x17, 0x0F, 255);
        public Color paperInk = new Color32(0x1C, 0x17, 0x0F, 255);
        public Color leatherText = new Color32(0xE6, 0xD9, 0xBD, 255);
        public Color brassText = new Color32(0x2A, 0x1A, 0x08, 255);

        [Header("Panels & buttons (9-sliced)")]
        public Sprite leatherRed, leatherDark, brassPlate, paper, paperTicket, slotDark;

        [Header("Small parts")]
        public Sprite coin, bulletOn, bulletOff;
        public Sprite xpRing, medalDisc;
        public Sprite[] storeFrames = new Sprite[6];
        public Sprite iconWhatsapp, iconLink, iconCopy, segOn, segBar, typeOn, typeOff, iconGlobe, iconKey, iconCrown, iconLock;   // create-room type cards
        public Sprite[] catIcons = new Sprite[10];
        public Sprite catOn, catOff;
        public Sprite sheetBg, sheetHandle, btnGold, btnDark, chipSheet, togOff, togOn, togKnob, rarGlow, itemCone, shineBand, resetPill, wallBg, wallLines, wallVignette, fedSil, picBg, plaqueGold, lampCap, lampCone, hairGrad, recCard, mineBar, barRed, dashH, doorD1, doorD2, doorD3, doorRing, boardDay, chipOff, chipOn, rowBg, iconDay, iconWeek, iconWall;

        [Header("Bottom navigation parts")]
        public Sprite navBarTile, navShadow, navSeal, navSealGlow, navSpot, livePill, badgeDot;

        [Header("Top bar + profile card parts")]
        public Sprite dotsTile, avatarDefault, ribbonRed, strapRed, clipMetal, levelSeal, barcodeTile, stampFrame;

        [Header("Rooms list + voice room")]
        public Sprite steelPlate, vipFrame, tableTop, chair, propCup, propAshtray, propDeck, propDallah, iconMic, iconMicOff, hostStar, seatDashed, dotWhite, talkGlow;

        [Header("Store")]
        public Sprite plank, priceTag, thumbFill, thumbBorder, cardBack, glowFloor, giftWatch, giftKey, giftRose, giftCrown;

        [Header("Leaderboards + chat")]
        public Sprite knobFace, dialTicks, chalkboard, wallTile, goldFrame, iconGift, iconSend, triUp;

        [Header("Per rank (index 0..7 = الصبي..العرّاب)")]
        public Sprite[] ringByRank = new Sprite[8];
        public Sprite[] medalByRank = new Sprite[8];

        [Header("Nav icons: shop, family, rooms, ranks, settings")]
        public Sprite[] navIcons = new Sprite[5];

        public static readonly string[] RankNames = { "الصبي", "العميل", "الجندي", "الريّس", "المستشار", "اليد اليمنى", "الزعيم", "العرّاب" };

        private static KitTheme cached;
        public static KitTheme Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<KitTheme>("Kit/KitTheme");
                return cached;
            }
        }
    }
}
