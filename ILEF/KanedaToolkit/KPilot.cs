#pragma warning disable 1591
using ILEF.Caching;

namespace ILEF.KanedaToolkit
{
    /// <summary>
    /// extension methods for Pilot
    /// </summary>
    public static class KPilot
    {
        public static double DerivedStanding(this Pilot pilot)
        {
            double relationship = 0.0;
            double[] relationships = {
				pilot.ToCorp.FromCharDouble,
				pilot.ToChar.FromCharDouble,
				pilot.ToAlliance.FromCharDouble,
				pilot.ToChar.FromCorpDouble,
				pilot.ToCorp.FromCorpDouble,
				pilot.ToAlliance.FromCorpDouble,
				pilot.ToChar.FromAllianceDouble,
				pilot.ToCorp.FromAllianceDouble,
				pilot.ToAlliance.FromAllianceDouble
			};

            foreach (double r in relationships)
            {
                if (r != 0.0 && r > relationship || relationship == 0.0)
                {
                    relationship = r;
                }
            }

            return relationship;
        }

        public static bool Hostile(this Pilot pilot)
        {
            if (QMCache.Instance.DirectEve.Session.CorporationId > 999999 && pilot.CorpID == QMCache.Instance.DirectEve.Session.CorporationId) return false;
            if (QMCache.Instance.DirectEve.Session.AllianceId > 0 && pilot.AllianceID == QMCache.Instance.DirectEve.Session.AllianceId) return false;
            if (pilot.DerivedStanding() > 0.0) return false;
            return true;
        }

        public static string StandingsStatus(this Pilot pilot)
        {
            if (QMCache.Instance.DirectEve.Session.CorporationId > 999999 && pilot.CorpID == QMCache.Instance.DirectEve.Session.CorporationId) return "blue";
            if (QMCache.Instance.DirectEve.Session.AllianceId > 0 && pilot.AllianceID == QMCache.Instance.DirectEve.Session.AllianceId) return "blue";
            if (pilot.DerivedStanding() > 0.0) return "blue";
            if (pilot.DerivedStanding() < 0.0) return "red";
            return "neutral";
        }
    }

}
