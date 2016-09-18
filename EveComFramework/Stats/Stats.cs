#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using EveCom;
using EveComFramework.Core;

namespace EveComFramework.Stats
{
    public class StatsSettings : Settings
    {
        public StatsSettings() : base("Stats") { }
        public bool optIn;
        public bool optOut;
        public string guid;
        public int configversion = 2;
    }

    public class Stats : State
    {
        #region Instantiation

        static Stats _Instance;
        public static Stats Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new Stats();
                }
                return _Instance;
            }
        }

        private Stats()
        {
            QueueState(Control);
        }

        #endregion

        #region Variables
        public StatsSettings Config = new StatsSettings();
        readonly Logger Log = new Logger("Stats");
        private String StatsHost = "http://104.238.149.13/evecom-stats/";
        #endregion

        #region States

        bool Control(Object[] Params)
        {
            // no data submission allowed
            if (Config.optOut) return true;

            // Generate GUID if none present
            if (Config.guid == null)
            {
                Config.guid = Guid.NewGuid().ToString();
                Config.Save();
            }

            // Wait for proper session state
            if (!Session.InSpace && !Session.InSpace) return false;

            String data = String.Format(@"GUID={0}&regionID={1}&allianceID={2}&groupID={3}", Config.guid, Session.RegionID, Me.AllianceID, (int)MyShip.ToItem.GroupID);
            if (Config.optIn) // Please do not enable this unless you know what you are doing and that you need to enable this
            {
                data = data + String.Format(@"&solarSystemID={0}&characterID={1}&typeID={2}", Session.SolarSystemID, Me.CharID, MyShip.ToItem.TypeID);
                QueueState(DatabaseFeeder);
            }

            try
            {
                WebRequest.Create(StatsHost + "?" + data).GetResponse().Close();
            }
            catch
            {
                Log.Log("|rNetwork connection failed");
            }
            return true;
        }

        private readonly List<long> CustomsOffices = new List<long>();
        private readonly List<long> ReportedPOS = new List<long>();
        private readonly List<long> ReportedStructures = new List<long>();

        private bool DatabaseFeeder(object[] Params)
        {
            if (Session.Safe && Session.InSpace)
            {
                Entity POS = Cache.Instance.AllEntities.FirstOrDefault(a => a.GroupID == Group.ControlTower && !ReportedPOS.Contains(a.ID));
                if (POS != null)
                {
                    Entity ClosestMoon = Cache.Instance.AllEntities.Where(a => a.GroupID == Group.Moon).OrderBy(a => a.Distance).First();
                    Entity ForceField = Cache.Instance.AllEntities.FirstOrDefault(a => a.GroupID == Group.ForceField);

                    String data = String.Format(@"GUID={0}&moonID={1}&corpID={2}&typeID={3}&online={4}", Config.guid, ClosestMoon.ID, POS.OwnerID, POS.TypeID, (ForceField != null ? 1 : 0));
                    Log.Log("Submit StarbasePresence data: " + data, LogType.DEBUG);
                    (new Thread(() =>
                    {
                        try
                        {
                            EVEFrame.Log(StatsHost + "starbasepresence.php?" + data);
                            WebRequest.Create(StatsHost + "starbasepresence.php?" + data).GetResponse().Close();
                            EVEFrame.Log("Request completed.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.ToString());
                            EVEFrame.Log(ex.ToString());
                        }
                    })).Start();
                    ReportedPOS.Add(POS.ID);
                    return false;
                }

                List<Entity> ReportCustomsOffices = Cache.Instance.AllEntities.Where(a => (a.TypeID == 2233 || a.TypeID == 4318) && !CustomsOffices.Contains(a.ID)).ToList();
                if (ReportCustomsOffices.Any())
                {
                    String data = String.Format(@"GUID={0}&solarSystemID={1}", Config.guid, Session.SolarSystemID);

                    foreach (Entity POCO in ReportCustomsOffices)
                    {
                        data += String.Format(@"&ownerID[]={0}&itemID[]={1}&typeID[]={2}&x[]={3}&y[]={4}&z[]={5}",
                            POCO.OwnerID, POCO.ID, POCO.TypeID,
                            POCO.Position.X.ToString(CultureInfo.InvariantCulture),
                            POCO.Position.Y.ToString(CultureInfo.InvariantCulture),
                            POCO.Position.Z.ToString(CultureInfo.InvariantCulture));
                        CustomsOffices.Add(POCO.ID);
                    }

                    Log.Log("Submit POCO data: " + data, LogType.DEBUG);
                    (new Thread(() =>
                    {
                        try
                        {
                            EVEFrame.Log(StatsHost + "poco.php?" + data);
                            WebRequest.Create(StatsHost + "poco.php?" + data).GetResponse().Close();
                            EVEFrame.Log("Request completed.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.ToString());
                            EVEFrame.Log(ex.ToString());
                        }
                    })).Start();
                }

                List<Entity> ReportStructures = Cache.Instance.AllEntities.Where(a => (a.CategoryID == Category.Starbase|| a.CategoryID == Category.Structure) && !ReportedStructures.Contains(a.ID) && !CustomsOffices.Contains(a.ID)).ToList();
                if (ReportStructures.Any())
                {
                    String data = String.Format(@"GUID={0}&solarSystemID={1}", Config.guid, Session.SolarSystemID);

                    foreach (Entity structure in ReportStructures)
                    {
                        if (data.Length < 2048)
                        {
                            EVEFrame.Log(structure.Type + " " + structure.CategoryID.ToString());
                            data += String.Format(@"&ownerID[]={0}&itemID[]={1}&typeID[]={2}&x[]={3}&y[]={4}&z[]={5}",
                                structure.OwnerID, structure.ID, structure.TypeID,
                                structure.Position.X.ToString(CultureInfo.InvariantCulture),
                                structure.Position.Y.ToString(CultureInfo.InvariantCulture),
                                structure.Position.Z.ToString(CultureInfo.InvariantCulture));
                            ReportedStructures.Add(structure.ID);
                        }
                    }

                    Log.Log("Submit structure data: " + data, LogType.DEBUG);
                    (new Thread(() =>
                    {
                        try
                        {
                            EVEFrame.Log(StatsHost + "structure.php?" + data);
                            WebRequest.Create(StatsHost + "structure.php?" + data).GetResponse().Close();
                            EVEFrame.Log("Request completed.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.ToString());
                            EVEFrame.Log(ex.ToString());
                        }
                    })).Start();

                }

            }
            return false;
        }
        #endregion

        #region Helper Methods
        public bool UploadLog(string uploadFile)
        {
            try
            {
                WebClient client = new WebClient();
                client.Headers.Add("Content-Type", "binary/octet-stream");
                Byte[] result = client.UploadFile(StatsHost + "uploadlog.php", "POST", uploadFile);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                return false;
            }
        }
        #endregion

    }
}
