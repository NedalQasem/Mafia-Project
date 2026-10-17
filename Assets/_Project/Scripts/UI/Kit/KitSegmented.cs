using System;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>Row of options in a dark groove; the chosen one is a brass plate.</summary>
    public class KitSegmented : MonoBehaviour
    {
        [SerializeField] private Button[] options;
        [SerializeField] private int selected;

        public event Action<int> OnChanged;
        public int Selected => selected;

        private void Awake()
        {
            for (int i = 0; i < options.Length; i++) { int idx = i; options[i].onClick.AddListener(() => { selected = idx; Paint(); OnChanged?.Invoke(idx); }); }
            Paint();
        }

        public void Set(int index) { selected = index; Paint(); }

        public void Paint()
        {
            var th = KitTheme.Current;
            if (th == null || options == null) return;
            for (int i = 0; i < options.Length; i++)
            {
                bool on = i == selected;
                var img = options[i].GetComponent<Image>();
                img.sprite = th.brassPlate; img.color = on ? Color.white : new Color(1, 1, 1, 0f);
                options[i].GetComponentInChildren<TMPro.TMP_Text>().color = on ? th.brassText : th.ivoryDim;
            }
        }
    }
}
