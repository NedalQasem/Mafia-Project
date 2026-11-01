using System;
using System.Collections.Generic;

namespace MafiaNight.UI.Kit
{
    public enum ChatKind { Public, Direct, Group }
    public enum MsgKind { Normal, System, RankUp, Day }

    [Serializable]
    public class ChatMessage
    {
        public MsgKind kind;
        public string from;        // sender name, "me" for the local player
        public string text;
        public string time;
        public int rank;
        public int id;             // server id (0 for the demo data)
        public string fromId;      // sender PlayFab id
        public bool Mine => from == "me";
    }

    [Serializable]
    public class ChatConversation
    {
        public string id;          // channel on the server ("public", "dm:..", "grp:.."); empty for the demo data
        public string peerId;      // direct chats: the other player's PlayFab id
        public ChatKind kind;
        public string name;
        public int rank;
        public bool online;
        public string[] members;
        public int unread;
        public string last, time;
        public List<ChatMessage> messages = new List<ChatMessage>();
    }

    public static class ChatData
    {
        private static ChatMessage M(string from, string text, string time, int rank = 0) => new ChatMessage { from = from, text = text, time = time, rank = rank };
        private static ChatMessage S(string text, MsgKind k = MsgKind.System) => new ChatMessage { kind = k, text = text };

        public static ChatConversation Public() => new ChatConversation
        {
            kind = ChatKind.Public, name = "ساحة الحارة",
            messages = new List<ChatMessage> {
                S("اليوم", MsgKind.Day),
                M("الحج حسن", "مساء الخير يا شباب، مين جاهز لسهرة الليلة؟", "٩:٢٠", 6),
                M("أم سعيد", "جاهزة، وفي مقعدين فاضيين بغرفتي", "٩:٢١", 4),
                S("أبو فارس ترقّى إلى رتبة الجندي", MsgKind.RankUp),
                M("مازن", "مبروك أبو فارس!", "٩:٢٣", 2),
                M("لينا", "أنا أبحث عن مجلس هادئ، فيه أحد؟", "٩:٢٤", 3),
                M("أم سعيد", "غرفة \"مجلس الكبار\" فيها مكان", "٩:٢٥", 4),
                S("ريما دخلت الساحة"),
                M("ريما", "السلام عليكم جميعاً", "٩:٢٧", 1) }
        };

        public static List<ChatConversation> Direct() => new List<ChatConversation> {
            new ChatConversation { kind = ChatKind.Direct, name = "أم سعيد", rank = 4, online = true, unread = 2, last = "تعال لغرفتي لو فاضي", time = "٩:٣١", messages = new List<ChatMessage> { M("أم سعيد", "هلا أبو سالم", "٩:٢٨", 4), M("me", "هلا بأم سعيد", "٩:٢٩"), M("أم سعيد", "تعال لغرفتي لو فاضي", "٩:٣١", 4) } },
            new ChatConversation { kind = ChatKind.Direct, name = "البيك", rank = 5, online = false, unread = 1, last = "سهرة أمس كانت خرافية", time = "٨:١٠", messages = new List<ChatMessage> { M("البيك", "سهرة أمس كانت خرافية", "٨:١٠", 5) } },
            new ChatConversation { kind = ChatKind.Direct, name = "لينا", rank = 3, online = true, last = "شكراً على الهدية", time = "أمس", messages = new List<ChatMessage> { M("لينا", "شكراً على الهدية", "٢١:٤٠", 3), M("me", "على الرحب والسعة", "٢١:٤١") } },
            new ChatConversation { kind = ChatKind.Direct, name = "الحج حسن", rank = 6, online = true, unread = 1, last = "خلّينا نكمل بالغرفة", time = "أمس", messages = new List<ChatMessage> { M("الحج حسن", "خلّينا نكمل بالغرفة", "٢٣:٠٥", 6) } },
            new ChatConversation { kind = ChatKind.Direct, name = "سارة", rank = 1, last = "تمام", time = "الأحد", messages = new List<ChatMessage> { M("me", "بكرة نلتقي؟", "١٦:٠٠"), M("سارة", "تمام", "١٦:٠٥", 1) } },
        };

        public static List<ChatConversation> Groups() => new List<ChatConversation> {
            new ChatConversation { kind = ChatKind.Group, name = "شلّة الحارة", members = new[] { "أبو فارس", "أم سعيد", "لينا", "مازن", "ريما" }, unread = 5, last = "مازن: الليلة الساعة ١٠؟", time = "٩:٤٠", messages = new List<ChatMessage> { M("مازن", "الليلة الساعة ١٠؟", "٩:٤٠", 2), M("لينا", "أنا جاهزة", "٩:٤١", 3) } },
            new ChatConversation { kind = ChatKind.Group, name = "محققو السهرة", members = new[] { "الحج حسن", "نور", "البيك" }, last = "نور: كشفتهم من أول ليلة", time = "٨:٠٠", messages = new List<ChatMessage> { M("نور", "كشفتهم من أول ليلة", "٨:٠٠", 3) } },
            new ChatConversation { kind = ChatKind.Group, name = "المبتدئون", members = new[] { "سارة", "ريما", "مازن", "أبو فارس" }, unread = 2, last = "سارة: كيف يشتغل الطبيب؟", time = "أمس", messages = new List<ChatMessage> { M("سارة", "كيف يشتغل الطبيب؟", "٢٠:١٥", 1) } },
        };
    }
}
