using System.Collections.Generic;

namespace MafiaNight.MatchRules
{
    public enum Team { Town, Mafia }

    /// <summary>What a role does at night. The match resolves them in a fixed order (silence, protect, kill, shoot), so a new ability is one entry here and one step there.</summary>
    public enum NightAbility { None, Kill, Silence, Protect, Check, Shoot }

    /// <summary>
    /// A role as data: its team, what it can do at night and the message that carries the act. The rules, the bots and the screens ask this table instead of
    /// comparing roles one by one, so a new role is one entry (plus its card) and not a hunt through the code.
    /// </summary>
    public sealed class RoleDef
    {
        public Role Id;
        public Team Team;
        /// <summary>The night abilities, in the order the player acts.</summary>
        public NightAbility[] Abilities = new NightAbility[0];
        /// <summary>The role takes someone with him when he leaves, for any reason (the Avenger).</summary>
        public bool AvengesOnExit;
        /// <summary>The ability can be used this many times in a match (0 = no limit).</summary>
        public int Uses;
    }

    public static class RoleDefs
    {
        private static readonly Dictionary<Role, RoleDef> table = new Dictionary<Role, RoleDef>
        {
            { Role.Civilian, new RoleDef { Id = Role.Civilian, Team = Team.Town } },
            { Role.Mafia,    new RoleDef { Id = Role.Mafia, Team = Team.Mafia, Abilities = new[] { NightAbility.Kill } } },
            { Role.Silencer, new RoleDef { Id = Role.Silencer, Team = Team.Mafia, Abilities = new[] { NightAbility.Kill, NightAbility.Silence } } },
            { Role.Doctor,   new RoleDef { Id = Role.Doctor, Team = Team.Town, Abilities = new[] { NightAbility.Protect } } },
            { Role.Sheriff,  new RoleDef { Id = Role.Sheriff, Team = Team.Town, Abilities = new[] { NightAbility.Check } } },
            { Role.Sniper,   new RoleDef { Id = Role.Sniper, Team = Team.Town, Abilities = new[] { NightAbility.Shoot }, Uses = 1 } },
            { Role.Avenger,  new RoleDef { Id = Role.Avenger, Team = Team.Town, AvengesOnExit = true } },
        };

        public static RoleDef Of(Role role) { return table[role]; }
        public static IEnumerable<RoleDef> All { get { return table.Values; } }

        public static bool IsMafiaTeam(this Role role) { return table[role].Team == Team.Mafia; }
        public static bool Can(this Role role, NightAbility ability) { return System.Array.IndexOf(table[role].Abilities, ability) >= 0; }
        public static bool ActsAtNight(this Role role) { return table[role].Abilities.Length > 0; }
    }
}
