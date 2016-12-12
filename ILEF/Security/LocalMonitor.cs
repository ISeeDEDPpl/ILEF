#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Linq;
using ILoveEVE.Framework;
using ILEF.Core;
using ILEF.Caching;

namespace ILEF.Security
{
    public class LocalMonitor : State
    {
        #region Instantiation

        static LocalMonitor _Instance;
        public static LocalMonitor Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new LocalMonitor();
                }
                return _Instance;
            }
        }

        private LocalMonitor()
        {
            QueueState(Control);
        }

        #endregion

        #region Variables
        internal readonly Logger Log = new Logger("Local");
        private long solarSystem;
        private List<Pilot> localPilots;
        #endregion

        #region Events
        public event Action OnLocalChange;
        #endregion

        #region States

        bool Control(object[] Params)
        {
            try
            {
                if (!QMCache.Instance.InSpace && !QMCache.Instance.InStation) return false;

                if (solarSystem == DirectEve.Session.SolarSystemId && localPilots != null)
                {
                    //if (Local.Pilots.Count < 100 && (localPilots.Count != Local.Pilots.Count || Local.Pilots.Any(p => !localPilots.Contains(p)) || localPilots.Any(p => !Local.Pilots.Contains(p))))
                    //{
                    //    if (OnLocalChange != null)
                    //        OnLocalChange();
                    //}
                    //else
                    {
                        return false;
                    }
                }

                solarSystem = (long)DirectEve.Session.SolarSystemId;
                //localPilots = Local.Pilots;

                return false;
            }
            catch (Exception){}
            return false;
        }

        #endregion
    }

}
