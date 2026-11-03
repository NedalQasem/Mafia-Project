using System.Collections.Generic;
using System.IO;
using System.Linq;
using MafiaNight.UI.Kit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.Tests
{
    /// <summary>
    /// A screen that opens on top of another one must stop every tap: an empty spot of it (an empty chat, a gap between seats) that lets the tap fall through
    /// lands on whatever is below (in the room screen it joined an official room by itself). So every overlay has to carry a graphic with "Raycast Target" on
    /// that covers its whole rect, and the overlay itself has to be stretched over the whole screen. Every prefab of the kit is classified here; a new prefab fails
    /// the test until it is put in one of the lists, so nobody can add an overlay and forget the rule (see Docs/13-performance-rules.md, section on overlays).
    /// </summary>
    public class OverlayTapBlockTests
    {
        private const string PrefabDir = "Assets/_Project/Prefabs/UIKit";

        /// <summary>Full-screen overlays: they must block taps everywhere.</summary>
        private static readonly string[] Blocking =
        {
            "AccountSheet", "CodeSheet", "ConflictSheet", "CreateRoomScreen", "EventsBoard", "LaunchBanner", "LoginChoice", "MailBoard", "QuickMenu", "RecoverySheet", "RewardPopup",
        };

        /// <summary>Let taps through on purpose (a toast, a slip, the loading picture that has nothing to tap).</summary>
        private static readonly string[] PassThrough = { "QuickNotice", "Toast", "BootLoading" };

        /// <summary>Pieces that live inside a screen (rows, cards, buttons, pills): not overlays.</summary>
        private static readonly string[] Parts =
        {
            "AvatarFrame", "ChatListItem", "ChatMsgDay", "ChatMsgMine", "ChatMsgOther", "ChatMsgSys", "CoinPill", "DirectLetter", "EventCard", "EventPrizePill", "EventRankGap", "EventRankRow",
            "KitButton_Brass", "KitButton_Primary", "KitButton_Secondary", "KitNavBar", "KitPlaque", "KitProfileCard", "KitTopBar", "LaunchAdPage", "LbChip", "LbRow", "MailCard",
            "NameLabel_Brass", "NameLabel_Leather", "NameLabel_Paper", "QuickOfferRow", "RankMedal", "RankSetCard", "RoomCard", "StoreAdDot", "StoreAdPage", "StoreCat", "StoreHeader", "StoreItem", "StoreShelf",
        };

        private GameObject canvas;

        [SetUp]
        public void MakeCanvas()
        {
            canvas = new GameObject("overlayTestCanvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1080, 2400);
        }

        [TearDown]
        public void DropCanvas() { if (canvas != null) Object.DestroyImmediate(canvas); }

        private static Rect WorldRect(RectTransform r)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        /// <summary>Null when the overlay stops every tap over the screen, otherwise why it does not.</summary>
        private static string WhyItLetsTapsThrough(GameObject overlay)
        {
            var rt = (RectTransform)overlay.transform;
            if (rt.anchorMin != Vector2.zero || rt.anchorMax != Vector2.one || rt.offsetMin != Vector2.zero || rt.offsetMax != Vector2.zero)
                return "its root is not stretched over the whole screen";
            Canvas.ForceUpdateCanvases();
            var screen = WorldRect(rt);
            const float tol = 0.5f;
            foreach (var g in overlay.GetComponentsInChildren<Graphic>(false))
            {
                if (!g.raycastTarget || !g.enabled) continue;
                var r = WorldRect(g.rectTransform);
                if (r.xMin <= screen.xMin + tol && r.yMin <= screen.yMin + tol && r.xMax >= screen.xMax - tol && r.yMax >= screen.yMax - tol) return null;
            }
            return "no graphic with Raycast Target covers its whole area: an empty spot lets the tap fall to the screen below";
        }

        [Test]
        public void EveryPrefabOfTheKitIsClassified()
        {
            var known = new HashSet<string>(Blocking.Concat(PassThrough).Concat(Parts));
            var unknown = Directory.GetFiles(PrefabDir, "*.prefab").Select(Path.GetFileNameWithoutExtension).Where(n => !known.Contains(n)).ToList();
            Assert.IsEmpty(unknown, "New prefab(s) in " + PrefabDir + ": put each in Blocking (an overlay), PassThrough or Parts (a piece) in OverlayTapBlockTests: " + string.Join(", ", unknown));
        }

        [Test]
        public void EveryFullScreenOverlayPrefabStopsTaps()
        {
            var bad = new List<string>();
            foreach (var name in Blocking)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + name + ".prefab");
                Assert.IsNotNull(prefab, name + " is missing");
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                inst.SetActive(true);
                string why = WhyItLetsTapsThrough(inst);
                if (why != null) bad.Add(name + ": " + why);
                Object.DestroyImmediate(inst);
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void TheRoomScreenStopsTaps()
        {
            var screen = KitRoomScreen.Create(canvas.transform);
            screen.gameObject.SetActive(true);
            Assert.IsNull(WhyItLetsTapsThrough(screen.gameObject), "the room screen lets taps through");
        }

        [Test]
        public void TheSheetsBuiltInCodeStopTaps()
        {
            KitActionSheet.Show(canvas.transform, "t", new KitActionSheet.Choice("a", () => { }));
            var action = canvas.transform.Find("ActionSheet");
            Assert.IsNotNull(action, "the action sheet was not built");
            Assert.IsNull(WhyItLetsTapsThrough(action.gameObject), "the action sheet lets taps through");

            KitMembersSheet.Show(canvas.transform, "t", "1", new List<KitMembersSheet.Row> { new KitMembersSheet.Row { Id = "1", Name = "x" } }, _ => { });
            var members = canvas.transform.Find("MembersSheet");
            Assert.IsNotNull(members, "the members sheet was not built");
            Assert.IsNull(WhyItLetsTapsThrough(members.gameObject), "the members sheet lets taps through");
        }
    }
}
