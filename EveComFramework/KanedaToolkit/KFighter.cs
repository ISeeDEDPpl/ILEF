using EveCom;
using System.Collections.Generic;

namespace EveComFramework.KanedaToolkit
{
    static class KFighter
    {
        public static List<long> lightFightersAttack = new List<long>() { 23057, 40557, 23055, 40556, 23059, 40558, 23061, 40559 };
        public static List<long> lightFightersSuperiority = new List<long>() { 40358, 40552, 40361, 40553, 40359, 40554, 40555, 40360 };
        public static List<long> heavyFightersAttack = new List<long>() { 32344, 40567, 40561, 32340, 32342, 40565, 32325, 40563 };
        public static List<long> heavyFightersLongRange = new List<long>() { 40362, 40560, 40365, 40564, 40363, 40566, 40364, 40562 };
        public static List<long> supportFighters = new List<long>() { 37599, 40568, 40347, 40571, 40570, 40346, 40569, 40345 };
        public static List<long> shadowFighters = new List<long>() { 2948 };
        public enum AbilityType
        {
            Attack,
            SecondaryAttack,
            Afterburner,
            MicroWarpDrive,
            MicroJumpDrive,
            AOEBomb,
            Suicide
        }

        /// <summary>
        /// Slot number for a given AbilityType
        /// </summary>
        public static int? AbilitySlot(this Fighters.Fighter fighter, AbilityType ability)
        {
            if (ability == AbilityType.Attack) return 0;

            if (ability == AbilityType.Afterburner && lightFightersSuperiority.Contains(fighter.TypeID)) return 1;
            if (ability == AbilityType.MicroJumpDrive && heavyFightersLongRange.Contains(fighter.TypeID)) return 1;
            if (ability == AbilityType.MicroWarpDrive && (lightFightersAttack.Contains(fighter.TypeID) || heavyFightersAttack.Contains(fighter.TypeID) || supportFighters.Contains(fighter.TypeID))) return 1;

            if (ability == AbilityType.SecondaryAttack && (lightFightersAttack.Contains(fighter.TypeID) || lightFightersSuperiority.Contains(fighter.TypeID) || heavyFightersAttack.Contains(fighter.TypeID))) return 2;
            if (ability == AbilityType.AOEBomb && heavyFightersLongRange.Contains(fighter.TypeID)) return 2;

            return null;
        }

    }
}
