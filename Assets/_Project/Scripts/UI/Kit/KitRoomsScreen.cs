using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>Rooms list: quick-join bar, "open your table", and a scrolling list of <see cref="RoomCardView"/>.</summary>
    public class KitRoomsScreen : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private GameObject roomCardPrefab;
        [SerializeField] private Button quickJoinButton;
        [SerializeField] private Button createButton;
        [SerializeField] private Button codeButton;
        [SerializeField] private GameObject emptyNote;

        public event Action<RoomInfo> OnJoinRoom;
        public event Action OnQuickJoin;
        public event Action OnCreateRoom;
        public event Action OnEnterCode;

        private readonly List<RoomCardView> cards = new List<RoomCardView>();

        private void Awake()
        {
            if (quickJoinButton != null) quickJoinButton.onClick.AddListener(() => OnQuickJoin?.Invoke());
            if (createButton != null) createButton.onClick.AddListener(() => OnCreateRoom?.Invoke());
            if (codeButton != null) codeButton.onClick.AddListener(() => OnEnterCode?.Invoke());
        }

        private string emptyText;

        /// <summary>Until the first answer arrives the list says it is loading, not that there are no rooms.</summary>
        public void SetLoading()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            cards.Clear();
            ShowEmpty("جاري تحميل الغرف...");
        }

        private void ShowEmpty(string text)
        {
            if (emptyNote == null) return;
            var label = emptyNote.GetComponentInChildren<ArabicText>(true);
            if (label != null) { if (emptyText == null) emptyText = label.Text; label.Text = text ?? emptyText; }
            emptyNote.SetActive(true);
        }

        public void SetRooms(IList<RoomInfo> rooms)
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            cards.Clear();
            if (rooms.Count == 0) ShowEmpty(null); else if (emptyNote != null) emptyNote.SetActive(false);
            if (rooms.Count > 0 && rooms[0].official) Spacer(30f);       // the seal and the pin tag of the first card stand out above it
            foreach (var r in rooms)
            {
                if (r.official)
                {
                    var oc = KitOfficialCard.Create(content, r, 1000f, info => OnJoinRoom?.Invoke(info));
                    continue;
                }
                var go = Instantiate(roomCardPrefab, content);
                var v = go.GetComponent<RoomCardView>();
                v.Bind(r);
                v.OnJoin += info => OnJoinRoom?.Invoke(info);
                cards.Add(v);
            }
        }

        private void Spacer(float h)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(content, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(1000f, h);
        }

        /// <summary>Edit-mode helper for the builder so the saved scene already shows the list.</summary>
        public void SetRoomsImmediate(IList<RoomInfo> rooms)
        {
            for (int i = content.childCount - 1; i >= 0; i--) DestroyImmediate(content.GetChild(i).gameObject);
            cards.Clear();
            if (rooms.Count > 0 && rooms[0].official) Spacer(30f);
            foreach (var r in rooms)
            {
                if (r.official) { KitOfficialCard.Create(content, r, 1000f, null); continue; }
#if UNITY_EDITOR
                var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(roomCardPrefab, content);
#else
                var go = Instantiate(roomCardPrefab, content);
#endif
                var v = go.GetComponent<RoomCardView>();
                v.Bind(r);
                cards.Add(v);
            }
        }
    }
}
