using System.Collections.Generic;
using UnityEngine;

namespace MafiaNight.Data.Cards
{
    /// <summary>
    /// What a role's card says (Docs/mockups/role-cards.html): its name, how dangerous it is (gold bullets), what it does and when it wins. The portrait of a role is fixed
    /// (Art/Cards/Portraits); what a player buys in the store are the four layers of <see cref="CardLayers"/> around it.
    /// </summary>
    public sealed class RoleCardDef
    {
        /// <summary>The id of the portrait file (portrait_&lt;Id&gt;) and of the role in the match rules.</summary>
        public string Id = "";
        public string Name = "", English = "", Sub = "", Ttl = "", Win = "";
        /// <summary>1..4 gold bullets.</summary>
        public int Danger = 1;
        public Color32 Accent = new Color32(0xC9, 0xA0, 0x50, 255), AccentDark = new Color32(0x6A, 0x4A, 0x1A, 255);
        public string[] Lines = new string[0];
    }

    /// <summary>The cards of the roles that exist in the match now. A new role adds its entry here (and its portrait); nothing else on the card changes.</summary>
    public static class RoleCards
    {
        public static readonly RoleCardDef[] All =
        {
            new RoleCardDef
            {
                Id = "mafia", Name = "مافيا", English = "MAFIA", Sub = "من عائلة المافيا", Ttl = "يقتل بالليل", Danger = 4,
                Accent = new Color32(0xE0, 0x58, 0x5F, 255), AccentDark = new Color32(0x8A, 0x1C, 0x24, 255),
                Lines = new[] { "بتعرف رفقاءك المافيا وبتتشاوروا بالليل.", "كل ليلة بتتفقوا على ضحية وبتنقتل.", "نهاراً مثّل إنك مواطن وغطّي على رفقاءك." },
                Win = "بتفوز لما يتساوى عددكم مع باقي اللاعبين.",
            },
            new RoleCardDef
            {
                Id = "sheikh", Name = "شيخ المواطنين", English = "THE ELDER", Sub = "كبير الحارة", Ttl = "يكشف المافيا", Danger = 3,
                Accent = new Color32(0x6F, 0xA0, 0xD6, 255), AccentDark = new Color32(0x24, 0x47, 0x6E, 255),
                Lines = new[] { "كل ليلة بتفحص لاعب بتختاره.", "بيجيك الجواب: مافيا أو مش مافيا (إلك وحدك).", "استعمل معلوماتك بذكاء، المافيا بتصطادك أول." },
                Win = "بتفوز مع المواطنين لما يطلع كل المافيا.",
            },
            new RoleCardDef
            {
                Id = "doctor", Name = "طبيب", English = "DOCTOR", Sub = "يحمي الأرواح", Ttl = "ينقذ بالليل", Danger = 2,
                Accent = new Color32(0x4F, 0xA5, 0x8B, 255), AccentDark = new Color32(0x1D, 0x4A, 0x3C, 255),
                Lines = new[] { "كل ليلة بتختار لاعب تحميه من القتل.", "إذا اختارته المافيا بينجو ولا حدا بيعرف.", "بتقدر تحمي حالك مرة وحدة بس بالمباراة." },
                Win = "بتفوز مع المواطنين لما يطلع كل المافيا.",
            },
            new RoleCardDef
            {
                Id = "civilian", Name = "مواطن", English = "CITIZEN", Sub = "من أهل الحارة", Ttl = "صوته سلاحه", Danger = 1,
                Accent = new Color32(0xC9, 0xA0, 0x50, 255), AccentDark = new Color32(0x6A, 0x4A, 0x1A, 255),
                Lines = new[] { "ما عندك قدرة بالليل.", "سلاحك النقاش والتصويت.", "لاحظ مين بيتصرف بغرابة وصوّت بحكمة." },
                Win = "بتفوز لما يطلع كل المافيا.",
            },
            new RoleCardDef
            {
                Id = "silencer", Name = "كاتم الصوت", English = "SILENCER", Sub = "مافيا بدون ضجّة", Ttl = "يسكّت بالليل", Danger = 3,
                Accent = new Color32(0xD0, 0x5A, 0x7A, 255), AccentDark = new Color32(0x7A, 0x1C, 0x34, 255),
                Lines = new[] { "بتعرف رفقاءك المافيا وبتشاركهم قرار القتل.", "كل ليلة بتختار لاعب بينسكت طول النهار: لا بيحكي ولا بيكتب.", "المُسكَت بيقدر يصوّت بس. والكل بيعرف إنه انسكت، بس مش مين سكّته." },
                Win = "بتفوز لما يتساوى عددكم مع باقي اللاعبين.",
            },
            new RoleCardDef
            {
                Id = "sniper", Name = "قنّاص", English = "SNIPER", Sub = "طلقة وحدة بس", Ttl = "يقنص مرة", Danger = 3,
                Accent = new Color32(0xB8, 0x8A, 0x3C, 255), AccentDark = new Color32(0x5A, 0x3E, 0x12, 255),
                Lines = new[] { "معك طلقة وحدة بالمباراة، بتطلقها بالليل.", "إذا أصبت مافيا بيطلع وإنت بتضل باللعبة.", "إذا أصبت مواطن بيطلعوا الاتنين. والطبيب ما بيقدر يوقف طلقتك، وما حدا بيعرف مين قنص." },
                Win = "بتفوز مع المواطنين لما يطلع كل المافيا.",
            },
            new RoleCardDef
            {
                Id = "avenger", Name = "الثأري", English = "THE AVENGER", Sub = "ما بيروح لحاله", Ttl = "ياخد معه واحد", Danger = 4,
                Accent = new Color32(0xD9, 0x7A, 0x3A, 255), AccentDark = new Color32(0x7A, 0x3A, 0x10, 255),
                Lines = new[] { "ليلاً نايم مثل المواطن وما عندك قدرة.", "لما بتطلع، بالتصويت أو بالاغتيال، كرتك بينكشف والكل بيناقشك ٣٠ ثانية.", "بتختار علناً مين بيطلع معك، ومعك ٣ ثواني تثبّت قرارك." },
                Win = "بتفوز مع المواطنين لما يطلع كل المافيا.",
            },
        };

        public static RoleCardDef Find(string id)
        {
            foreach (var d in All) if (d.Id == id) return d;
            return null;
        }
    }

    /// <summary>What a player wears on his card: one id per layer, all bought in the store (the first of each is free). Others see it on his seat.</summary>
    public sealed class CardLayers
    {
        public string Frame = "brass", Background = "leather", Motion = "none", Back = "diamond";

        public CardLayers Clone() { return new CardLayers { Frame = Frame, Background = Background, Motion = Motion, Back = Back }; }
        public override string ToString() { return Frame + "/" + Background + "/" + Motion + "/" + Back; }
    }

    public sealed class FrameStyle { public Color32 F1, F2, F3, Line, Orn; }
    public sealed class BackgroundStyle { public Color32 Top, Bottom; public bool Fire; }

    /// <summary>The look of every layer id (the tables of the mockup). An unknown id falls back to the free one, so an old client never breaks on a new item.</summary>
    public static class CardStyles
    {
        public static readonly string[] Frames = { "brass", "gold", "silver", "black" };
        public static readonly string[] Backgrounds = { "leather", "velvet", "steel", "carbon", "fire", "night" };
        public static readonly string[] Motions = { "none", "shine", "embers", "smoke" };
        public static readonly string[] Backs = { "diamond", "lattice", "rose", "pistols", "skull" };

        private static readonly Dictionary<string, FrameStyle> frames = new Dictionary<string, FrameStyle>
        {
            { "brass", new FrameStyle { F1 = new Color32(0xE0, 0xC2, 0x7A, 255), F2 = new Color32(0x9A, 0x74, 0x33, 255), F3 = new Color32(0x5A, 0x3A, 0x10, 255), Line = new Color32(0xC9, 0xA0, 0x50, 0x88), Orn = new Color32(0xE8, 0xC7, 0x77, 255) } },
            { "gold", new FrameStyle { F1 = new Color32(0xFF, 0xF3, 0xBF, 255), F2 = new Color32(0xE0, 0xA9, 0x2C, 255), F3 = new Color32(0x8A, 0x5A, 0x10, 255), Line = new Color32(0xF5, 0xE2, 0xA8, 0xCC), Orn = new Color32(0xFF, 0xF1, 0xB8, 255) } },
            { "silver", new FrameStyle { F1 = new Color32(0xF6, 0xF8, 0xFB, 255), F2 = new Color32(0x9A, 0xA4, 0xB2, 255), F3 = new Color32(0x4A, 0x52, 0x60, 255), Line = new Color32(0xE0, 0xE6, 0xEF, 0xCC), Orn = new Color32(0xEE, 0xF2, 0xF8, 255) } },
            { "black", new FrameStyle { F1 = new Color32(0x4A, 0x4A, 0x4A, 255), F2 = new Color32(0x1C, 0x1C, 0x1C, 255), F3 = new Color32(0x05, 0x05, 0x05, 255), Line = new Color32(0xD4, 0xA8, 0x4A, 0xCC), Orn = new Color32(0xD4, 0xA8, 0x4A, 255) } },
        };

        private static readonly Dictionary<string, BackgroundStyle> backgrounds = new Dictionary<string, BackgroundStyle>
        {
            { "leather", new BackgroundStyle { Top = new Color32(0x3B, 0x2A, 0x20, 255), Bottom = new Color32(0x1A, 0x11, 0x0D, 255) } },
            { "velvet", new BackgroundStyle { Top = new Color32(0x7A, 0x18, 0x20, 255), Bottom = new Color32(0x2A, 0x05, 0x0A, 255) } },
            { "steel", new BackgroundStyle { Top = new Color32(0x2B, 0x31, 0x3B, 255), Bottom = new Color32(0x10, 0x14, 0x1A, 255) } },
            { "carbon", new BackgroundStyle { Top = new Color32(0x18, 0x18, 0x18, 255), Bottom = new Color32(0x0C, 0x0C, 0x0C, 255) } },
            { "fire", new BackgroundStyle { Top = new Color32(0x1A, 0x06, 0x04, 255), Bottom = new Color32(0xE0, 0x50, 0x1A, 255), Fire = true } },
            { "night", new BackgroundStyle { Top = new Color32(0x2A, 0x3A, 0x7A, 255), Bottom = new Color32(0x05, 0x06, 0x0B, 255) } },
        };

        public static FrameStyle Frame(string id) { return id != null && frames.TryGetValue(id, out var f) ? f : frames["brass"]; }
        public static BackgroundStyle Background(string id) { return id != null && backgrounds.TryGetValue(id, out var b) ? b : backgrounds["leather"]; }
        public static bool IsMotion(string id) { return id == "shine" || id == "embers" || id == "smoke"; }
    }
}
