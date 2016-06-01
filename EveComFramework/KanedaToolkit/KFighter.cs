using EveCom;

namespace EveComFramework.KanedaToolkit
{
    static class KFighter
    {

        public enum AbilityType
        {
            Attack,
            Propmod,
            MJD
        }

        /// <summary>
        /// Max squadron size for a given fighter type
        /// </summary>
        public static int MaxSquadronSize(this Fighters.Fighter fighter)
        {
            // @TODO
            return 0;
        }

        /// <summary>
        /// Slot number for a given AbilityType
        /// </summary>
        public static int? AbilitySlot(this Fighters.Fighter fighter, AbilityType ability)
        {
            if (ability == AbilityType.Attack) return 0;

            // @TODO
            return null;
        }

    }
}
