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

        public event Action<RoomInfo> OnJoinRoom;
        public event Action OnQuickJoin;
        public event Action OnCreateRoom;

        private readonly List<RoomCardView> cards = new List<RoomCardView>();

        private void Awake()
        {
            if (quickJoinButton != null) quickJoinButton.onClick.AddListener(() => OnQuickJoin?.Invoke());
            if (createButton != null) createButton.onClick.AddListener(() => OnCreateRoom?.Invoke());
        }

        public void SetRooms(IList<RoomInfo> rooms)
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            cards.Clear();
            foreach (var r in rooms)
            {
                var go = Instantiate(roomCardPrefab, content);
                var v = go.GetComponent<RoomCardView>();
                v.Bind(r);
                v.OnJoin += info => OnJoinRoom?.Invoke(info);
                cards.Add(v);
            }
        }

        /// <summary>Edit-mode helper for the builder so the saved scene already shows the list.</summary>
        public void SetRoomsImmediate(IList<RoomInfo> rooms)
        {
            for (int i = content.childCount - 1; i >= 0; i--) DestroyImmediate(content.GetChild(i).gameObject);
            cards.Clear();
            foreach (var r in rooms)
            {
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
