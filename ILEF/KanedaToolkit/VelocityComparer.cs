#pragma warning disable 1591
using System.Collections.Generic;
using ILoveEVE.Framework;

namespace ILEF.KanedaToolkit
{
    public class VelocityComparer : Comparer<DirectEntity>
    {
        public override int Compare(DirectEntity x, DirectEntity y)
        {
            if (x == null && y == null)
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;
            if (x == y)
                return 0;

            if (x.Velocity < y.Velocity)
                return -1;
            if (x.Velocity > y.Velocity)
                return 1;
            if (x.Velocity == y.Velocity)
                return 0;
            return 0;
        }
    }
}
