using EveCom;
using System.Collections.Generic;
using System.Linq;

namespace EveComFramework.KanedaToolkit
{
    /// <summary>
    /// Extension methods for fighters
    /// </summary>
    public static class KFighter
    {
        /// <summary>
        /// Light attack fighter types
        /// </summary>
        public static List<long> lightFightersAttack = new List<long> { 23057, 40557, 23055, 40556, 23059, 40558, 23061, 40559 };
        /// <summary>
        /// Light superiority fighter types
        /// </summary>
        public static List<long> lightFightersSuperiority = new List<long> { 40358, 40552, 40361, 40553, 40359, 40554, 40555, 40360 };
        /// <summary>
        /// Heavy attack fighter types
        /// </summary>
        public static List<long> heavyFightersAttack = new List<long> { 32344, 40567, 40561, 32340, 32342, 40565, 32325, 40563 };
        /// <summary>
        /// Heavy long-range attack fighter types
        /// </summary>
        public static List<long> heavyFightersLongRange = new List<long> { 40362, 40560, 40365, 40564, 40363, 40566, 40364, 40562 };
        /// <summary>
        /// Support fighter types
        /// </summary>
        public static List<long> supportFighters = new List<long> { 37599, 40568, 40347, 40571, 40570, 40346, 40569, 40345 };
        /// <summary>
        /// shadow fighters
        /// </summary>
        public static List<long> shadowFighters = new List<long> { 2948 };

        /// <summary>
        /// Does this fighter have afterburner ability?
        /// </summary>
        public static bool HasAfterburner(this Fighters.Fighter fighter) => fighter["fighterAbilityAfterburnerSpeedBonus"] != null;

        /// <summary>
        /// Does this fighter have evasive maneuvers ability?
        /// </summary>
        public static bool HasEvasiveManeuvers(this Fighters.Fighter fighter) => fighter["fighterAbilityEvasiveManeuversSpeedBonus"] != null;

        /// <summary>
        /// Does this fighter have MWD ability?
        /// </summary>
        public static bool HasMWD(this Fighters.Fighter fighter) => fighter["fighterAbilityMicroWarpDriveSpeedBonus"] != null;

        /// <summary>
        /// Does this fighter have MJD ability?
        /// </summary>
        public static bool HasMJD(this Fighters.Fighter fighter) => fighter["fighterAbilityMicroJumpDriveDistance"] != null;

        /// <summary>
        /// Does this fighter have missile ability?
        /// </summary>
        public static bool HasMissiles(this Fighters.Fighter fighter) => fighter["fighterAbilityMissilesRange"] != null;

        /// <summary>
        /// Does this fighter have kamikaze ability?
        /// </summary>
        public static bool HasKamikaze(this Fighters.Fighter fighter) => fighter["fighterAbilityKamikazeRange"] != null;

        /// <summary>
        /// Does this fighter have bomb ability?
        /// </summary>
        public static bool HasBomb(this Fighters.Fighter fighter) => fighter["fighterAbilityLaunchBombType"] != null;

        /// <summary>
        /// Does this fighter have any propmod ability?
        /// </summary>
        public static bool HasPropmod(this Fighters.Fighter fighter) => fighter.HasAfterburner() || fighter.HasEvasiveManeuvers() || fighter.HasMWD();

        /// <summary>
        /// Does this fighter require ammo and is out of ammo?
        /// </summary>
        public static bool RequiresAmmo(this Fighters.Fighter fighter)
        {
            if (fighter.HasMissiles())
            {
                return fighter.Slot3.Charges == 0;
            }
            return false;
        }

        /// <summary>
        /// Optimal range of normal attack of this fighter
        /// </summary>
        public static double OptimalRange(this Fighters.Fighter fighter)
        {
            return (double)fighter["fighterAbilityAttackMissileRangeOptimal"] * GetSkillBonus();
        }

        /// <summary>
        /// Falloff range of normal attack of this fighter
        /// </summary>
        public static double FalloffRange(this Fighters.Fighter fighter)
        {
            return (double)fighter["fighterAbilityAttackMissileRangeFalloff"] * GetSkillBonus();
        }

        /// <summary>
        /// Falloff range of missile attack of this fighter
        /// </summary>
        public static double MissileOptimalRange(this Fighters.Fighter fighter)
        {
            if (!fighter.HasMissiles()) return 0.0;
            return (double)fighter["fighterAbilityMissilesRange"] * GetSkillBonus();
        }

        /// <summary>
        /// max target range of this fighter
        /// </summary>
        public static double MaxTargetRange(this Fighters.Fighter fighter)
        {
            return (double)fighter["maxTargetRange"] - 10;
        }

        private static double GetSkillBonus()
        {
            Skill DroneSharpshooting = Skill.All.FirstOrDefault(a=>a.TypeID == 23606);
            if(DroneSharpshooting != null)
            {
                return 1 + 0.05 * DroneSharpshooting.SkillLevel;
            }
            return 1.0;
        }

    }
}
