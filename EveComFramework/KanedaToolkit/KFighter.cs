using EveCom;
using System.Collections.Generic;

namespace EveComFramework.KanedaToolkit
{
    public static class KFighter
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

            Afterburner,
            EvasiveManuevers,
            MicroWarpDrive,
            MicroJumpDrive,

            MissileAttack,
            AOEBomb,
            Suicide
        }

        public static bool HasAfterburner(this Fighters.Fighter fighter)
        {
            return fighter["fighterAbilityAfterburnerSpeedBonus"] != null;
        }

        public static bool HasEvasiveManeuvers(this Fighters.Fighter fighter)
        {
            return fighter["fighterAbilityEvasiveManeuversSpeedBonus"] != null;
        }

        public static bool HasMWD(this Fighters.Fighter fighter)
        {
            return fighter["fighterAbilityMicroWarpDriveSpeedBonus"] != null;
        }

        public static bool HasMJD(this Fighters.Fighter fighter)
        {
            return fighter["fighterAbilityMicroJumpDriveDistance"] != null;
        }

        public static bool HasMissiles(this Fighters.Fighter fighter)
        {
            return fighter["fighterAbilityMissilesRange"] != null;
        }

        public static bool HasKamikaze(this Fighters.Fighter fighter)
        {
            return fighter["fighterAbilityKamikazeRange"] != null;
        }

        public static bool HasBomb(this Fighters.Fighter fighter)
        {
            return fighter["fighterAbilityLaunchBombType"] != null;
        }

        public static bool HasPropmod(this Fighters.Fighter fighter)
        {
            return fighter.HasAfterburner() || fighter.HasEvasiveManeuvers() || fighter.HasMWD();
        }

    }
}
