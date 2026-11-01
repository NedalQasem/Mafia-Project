using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaNight.UI.Kit
{
    /// <summary>A chat message. Sizes its bubble to the text (wraps at a maximum width).</summary>
    public class ChatMessageView : MonoBehaviour
    {
        [SerializeField] private RectTransform bubble;
        [SerializeField] private ArabicText label, timeLabel, nameLabel, rankLabel;
        [SerializeField] private AvatarFrame avatar;
        [SerializeField] private LayoutElement layout;
        [SerializeField] private float maxTextWidth = 640f;
        [SerializeField] private float padX = 34f, padTop = 22f, padBottom = 46f;

        /// <summary>Makes the bubble tappable (to message, report or block the sender).</summary>
        public void SetTap(Action clicked) { if (bubble != null) KitTap.On(bubble, clicked); }

        /// <summary>The store bubble the player wears: the bubble's color and the text on it.</summary>
        public void Wear(int bubbleVariant)
        {
            if (!KitBubbleLooks.Has(bubbleVariant) || bubble == null) return;
            var look = KitBubbleLooks.Of(bubbleVariant);
            var img = bubble.GetComponent<Image>();
            if (img != null) { img.color = Color.Lerp(Color.white, look.Fill, 0.85f); }
            if (label != null) label.GetComponent<TMP_Text>().color = look.Text;
        }

        public void Bind(ChatMessage m, bool showSender)
        {
            if (label == null) return;
            var tmp = label.GetComponent<TMP_Text>();
            var lrt = (RectTransform)label.transform;
            label.Wrap = false;
            label.Text = m.text;
            float w = tmp.preferredWidth;
            float h;
            if (w > maxTextWidth)
            {
                lrt.sizeDelta = new Vector2(maxTextWidth, lrt.sizeDelta.y);
                label.Wrap = true;
                h = tmp.GetPreferredValues(tmp.text).y;
                w = maxTextWidth;
            }
            else
            {
                w = Mathf.Max(w, 120f);
                lrt.sizeDelta = new Vector2(w, lrt.sizeDelta.y);
                h = tmp.preferredHeight;
            }
            lrt.sizeDelta = new Vector2(w, h);
            bubble.sizeDelta = new Vector2(w + padX * 2f, h + padTop + padBottom);
            if (timeLabel != null) timeLabel.Text = m.time;
            if (nameLabel != null)
            {
                nameLabel.gameObject.SetActive(showSender);
                if (showSender) nameLabel.Text = m.from + "  ·  " + KitTheme.RankNames[Mathf.Clamp(m.rank, 0, 7)];
                if (rankLabel != null) rankLabel.gameObject.SetActive(showSender);
            }
            if (avatar != null) { avatar.gameObject.SetActive(showSender); if (showSender) avatar.Set(m.rank); }
            float total = bubble.sizeDelta.y + (showSender ? 44f : 0f) + 10f;
            float rowH = Mathf.Max(total, avatar != null && showSender ? 130f : 0f);
            if (layout != null) layout.preferredHeight = rowH;
            var rt = (RectTransform)transform; rt.sizeDelta = new Vector2(rt.sizeDelta.x, rowH);
            // keep the bubble under the name row
            var p = bubble.anchoredPosition; bubble.anchoredPosition = new Vector2(p.x, showSender ? -44f : -4f);
        }
    }
}
