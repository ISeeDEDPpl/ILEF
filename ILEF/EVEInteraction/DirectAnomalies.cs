using ILEF.Caching;

namespace ILEF.EVEInteration
{
    #region Anomalies
    internal class DirectAnomalies
    {

        internal string Id { get; private set; }
        internal string Name { get; private set; }
        internal AnomalyID Type { get; private set; }
        internal float SignalStrength { get; private set; }

        internal enum AnomalyID
        {
            Unknow = 0,
            Data = 208,
            Gas = 209,
            Relic = 210,
            Ore = 211,
            Cosmic = 1136,
            Wormhole = 1908,
        }

        internal DirectAnomalies(string _Id, string _Name, int _AttributeId, float _SignalStrength)
        {
            Id = _Id;
            Name = _Name;
            Type = AnomalyID.Unknow;
            Type = (AnomalyID)_AttributeId;
            SignalStrength = _SignalStrength;
        }

        internal void WarpToAnomaly(int distance)
        {
            if (!string.IsNullOrEmpty(Id) && SignalStrength == 1)
            {
                QMCache.Instance.DirectEve.ThreadedLocalSvcCall("menu", "WarpToScanResult", Id, distance);
            }
        }

        internal void WarpToAnomaly()
        {
            if (!string.IsNullOrEmpty(Id) && SignalStrength == 1)
            {
                QMCache.Instance.DirectEve.ThreadedLocalSvcCall("menu", "WarpToScanResult", Id);
            }
        }

        internal void WarpFleetToAnomaly(int distance)
        {
            if (!string.IsNullOrEmpty(Id) && SignalStrength == 1)
            {
                QMCache.Instance.DirectEve.ThreadedLocalSvcCall("menu", "WarpFleetToScanResult", Id, distance);
            }
        }

        internal void WarpFleetToAnomaly()
        {
            if (!string.IsNullOrEmpty(Id) && SignalStrength == 1)
            {
                QMCache.Instance.DirectEve.ThreadedLocalSvcCall("menu", "WarpFleetToScanResult", Id);
            }
        }
    }

    #endregion Anomalies
}