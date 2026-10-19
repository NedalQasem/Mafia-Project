using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit.Editor
{
    /// <summary>The host drawer of the voice room: a tab on the left edge that slides out the room controls (lock, chat, players + kick).</summary>
    public static partial class KitBuilder
    {
        private const float DrawerWidth = 820f;

        private static KitHostDrawer BuildHostDrawer(Transform parent, KitTheme th)
        {
            var root = Go("HostDrawer", parent); Stretch(Rt(root));
            var drawer = root.AddComponent<KitHostDrawer>();

            // dim area behind the panel (catches taps only while the panel is out)
            var scrim = Go("Scrim", root.transform); Stretch(Rt(scrim));
            var scrimImg = scrim.AddComponent<Image>(); scrimImg.color = new Color(0, 0, 0, 0); scrimImg.raycastTarget = false;

            // panel, parked just off the left edge
            var panel = Go("Panel", root.transform); var pr = Rt(panel);
            pr.anchorMin = new Vector2(0, 0); pr.anchorMax = new Vector2(0, 1); pr.pivot = new Vector2(0, 0.5f);
            pr.sizeDelta = new Vector2(DrawerWidth, 0); pr.anchoredPosition = new Vector2(-DrawerWidth, 0);
            var bg = Img(panel, th.leatherDark, true, 1.3f, true);
            bg.color = new Color(0.82f, 0.82f, 0.82f, 1f);

            var title = Txt(panel.transform, "Title", "تحكّم الغرفة", th.fontCalligraphy, 70, th.leatherText, TextAlignmentOptions.MidlineRight);
            Place(Rt(title.gameObject), new Vector2(1, 1), new Vector2(-400, -130), new Vector2(700, 96));
            var sub = Txt(panel.transform, "Sub", "للمضيف فقط", th.fontBody, 28, th.brass, TextAlignmentOptions.MidlineRight);
            Place(Rt(sub.gameObject), new Vector2(1, 1), new Vector2(-400, -196), new Vector2(700, 40));

            // scrolling column of cards
            var scrollGo = Go("Scroll", panel.transform);
            var sr = Rt(scrollGo); sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = new Vector2(0, 40); sr.offsetMax = new Vector2(0, -250);
            var scroll = scrollGo.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40f;
            var vp = Go("Viewport", scrollGo.transform); Stretch(Rt(vp)); vp.AddComponent<RectMask2D>();
            var vpi = vp.AddComponent<Image>(); vpi.color = Color.clear; vpi.raycastTarget = true;
            var content = Go("Content", vp.transform);
            var cRt = Rt(content); cRt.anchorMin = new Vector2(0, 1); cRt.anchorMax = new Vector2(1, 1); cRt.pivot = new Vector2(0.5f, 1); cRt.anchoredPosition = Vector2.zero; cRt.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(26, 26, 6, 40); vlg.spacing = 26; vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = Rt(vp); scroll.content = cRt;

            var doorCard = MakeRoomCard(content.transform, th, "الدخول", null, out _);
            MakeToggleRow(doorCard.transform, th, "قفل الغرفة", "ما حدا جديد بيقدر يدخل", false, out var lockToggle);

            var chatCard = MakeRoomCard(content.transform, th, "الدردشة", null, out _);
            MakeToggleRow(chatCard.transform, th, "دردشة الغرفة", "اللاعبين بيكتبوا رسائل", true, out var chatToggle);

            var playersCard = MakeRoomCard(content.transform, th, "اللاعبون", null, out _);
            var membersTitle = playersCard.transform.Find("Title/T").GetComponent<ArabicText>();
            var members = Go("Members", playersCard.transform);
            var mv = members.AddComponent<VerticalLayoutGroup>();
            mv.spacing = 14; mv.childControlWidth = true; mv.childControlHeight = true; mv.childForceExpandWidth = true; mv.childForceExpandHeight = false;

            // template row, cloned at runtime
            var row = Go("MemberRow", members.transform);
            row.AddComponent<LayoutElement>().preferredHeight = 112;
            Img(row, th.slotDark, true, 2f);
            var star = Go("Star", row.transform); Place(Rt(star), new Vector2(1, 0.5f), new Vector2(-48, 0), new Vector2(54, 54)); Img(star, th.hostStar);
            var nm = Txt(row.transform, "Name", "لاعب", th.fontBody, 38, th.leatherText, TextAlignmentOptions.MidlineRight);
            Stretch(Rt(nm.gameObject), 200, 6, 100, 6);
            var kick = MakeChip(row.transform, th, "طرد", 160, 78, 34);
            Place(Rt(kick.gameObject), new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(160, 78));
            var memberRow = row.AddComponent<KitDrawerMemberRow>();
            SetRef(memberRow, "nameLabel", nm.GetComponent<ArabicText>());
            SetRef(memberRow, "kickLabel", kick.GetComponentInChildren<ArabicText>());
            SetRef(memberRow, "kickButton", kick);
            SetRef(memberRow, "star", star);
            row.SetActive(false);

            // the tab: half hidden at the screen edge, rides on the panel's right side
            var handle = Go("Handle", panel.transform); var hr = Rt(handle);
            hr.anchorMin = hr.anchorMax = new Vector2(1, 0.5f); hr.pivot = new Vector2(0, 0.5f); hr.anchoredPosition = new Vector2(0, 0); hr.sizeDelta = new Vector2(70, 210);
            var himg = Img(handle, th.brassPlate, true, 2f, true); himg.color = new Color(1f, 1f, 1f, 0.8f);
            var icon = Go("Icon", handle.transform); Place(Rt(icon), new Vector2(0.5f, 0.5f), new Vector2(4, 0), new Vector2(42, 42));
            var iconImg = Img(icon, th.iconLock); iconImg.color = new Color(0.16f, 0.1f, 0.05f, 0.9f);

            SetRef(drawer, "panel", pr); SetRef(drawer, "scrim", scrimImg); SetRef(drawer, "handle", handle);
            SetRef(drawer, "lockToggle", lockToggle); SetRef(drawer, "chatToggle", chatToggle);
            SetRef(drawer, "membersRoot", Rt(members)); SetRef(drawer, "memberTemplate", memberRow); SetRef(drawer, "membersTitle", membersTitle);
            root.SetActive(false);   // only a host gets it
            return drawer;
        }
    }
}
