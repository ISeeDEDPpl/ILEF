// ------------------------------------------------------------------------------
//   <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//     Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//     Please look in the accompanying license.htm file for the license that
//     applies to this source code. (a copy can also be found at:
//     http://www.thehackerwithin.com/license.htm)
//   </copyright>
// -------------------------------------------------------------------------------
namespace ILEF.Lookup
{
    public enum PrimaryWeaponPriority
    {
        WarpScrambler = 0,
        Jamming = 1,
        TrackingDisrupting = 2,
        Neutralizing = 3,
        TargetPainting = 4,
        Dampening = 5,
        Webbing = 6,
        PriorityKillTarget = 7,
        NotUsed = 99,
    }

    public enum DronePriority
    {
        WarpScrambler = 0,
        Webbing = 1,
        PriorityKillTarget = 2,
        LowPriorityTarget = 3,
        NotUsed = 99
    }
}