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
    public enum Distances : long
    {
        ScoopRange = 2490,
        SafeScoopRange = ScoopRange - 700,
        TooCloseToStructure = 2000,
        SafeDistancefromStructure = 50000,
        WarptoDistance = 152000,
        NextPocketDistance = 100000, // If we moved more then 100km, assume next Pocket
        GateActivationRange = 2200,
        CloseToGateActivationRange = GateActivationRange + 5000,
        WayTooClose = -10100, // This is usually used to determine how far inside the 'docking ring' of an acceleration gate we are.
        OrbitDistanceCushion = 5000, // This is used to determine when to stop orbiting or approaching, if not speed tanking (orbit distance + orbitDistanceCushion)
        OptimalRangeCushion = 5000, // This is used to determine when to stop approaching, if not speed tanking (optimal distance + optimalDistanceCushion)
        InsideThisRangeIsLikelyToBeMostlyFrigates = 9000, // 9k - overall this assumption works, use with caution
        DecloakRange = 1500,
        JumpRange = 2450,
        SafeToCloakDistance = 2300,
        DockingRange = 1900,
        MissionWarpLimit = 150000000, // Mission bookmarks have a 1.000.000 distance warp-to limit (changed it to 150.000.000 as there are some bugged missions around)
        PanicDistanceToConsiderSafelyWarpedOff = 500000,
        WeCanWarpToStarFromHere = 500000000,
        OnGridWithMe = 250000, //250k by default
        AU = 149598000000, // 1 AU - 1 Astronomical Unit = 149 598 000 000 meters
        HalfOfALightYearInAU = OneAU * 63239 / 2,
        DirectionalScannerCloseRange = 2147483647,
        MaxPocketsDistanceKm = 70000,
        HalfAU = 2147483647,
        OneAU = 149598000000,
        MaxSlowboatDistanceToNextAsteroid = 65000,
        MaxSlowBoatDistanceToBelt = 30000,
    }
}