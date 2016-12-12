#pragma warning disable 1591
using System.Linq;
using ILoveEVE.Framework;
using ILEF.Caching;

namespace ILEF.Core
{
    public class Helper
    {
        Cache Cache = Cache.Instance;

        /**
        bool RepairComplete = false;
        public bool RepairShip(Logger Console)
        {
            // If ship needs to be repaired, do so
            DirectWindow repairShopWindow;
            if ((Cache.ArmorPercent != 1 || Cache.HullPercent != 1 || Cache.DamagedDrones) && Station.HasService(Station.Services.RepairFacilities))
            {
                if (QMCache.Instance.Windows.Any(a => a.Name != null && a.Name == "Set Quantity"))
                {
                    Console.Log("|oAccepting repair quantity");
                    DirectWindow repairQuantityWindow = QMCache.Instance.Windows.FirstOrDefault(a => a.Name != null && a.Name == "Set Quantity");
                    if (repairQuantityWindow != null)
                    {
                        repairQuantityWindow.AnswerModal("OK");
                        Cache.ArmorPercent = 1;
                        Cache.HullPercent = 1;
                        Cache.DamagedDrones = false;
                    }
                    return false;
                }
                if (RepairComplete)
                {
                    Cache.ArmorPercent = 1;
                    Cache.HullPercent = 1;
                    Cache.DamagedDrones = false;
                    return false;
                }
                if (!QMCache.Instance.Windows.Any(a => a.Name != null && a.Name == "repairshop"))
                {
                    Console.Log("|oShip needs to be repaired");
                    Console.Log("|oHull: |w{0} |oArmor: |w{1} |oDamaged Drones: |w{2}", Cache.HullPercent, Cache.ArmorPercent, Cache.DamagedDrones);
                    QMCache.Instance.DirectEve.ActiveShip. //RepairQuote();
                }
                else
                {
                    Console.Log("|oClicking RepairAll");
                    repairShopWindow = QMCache.Instance.Windows.FirstOrDefault(a => a.Name != null && a.Name == "repairshop");
                    if (repairShopWindow != null)
                    {
                        repairShopWindow.AnswerModal("") //(Window.Button.RepairAll);
                    }
                    RepairComplete = true;
                }
                return false;
            }
            repairShopWindow = QMCache.Instance.Windows.FirstOrDefault(a => a.Name != null && a.Name == "repairshop");
            if (repairShopWindow != null)
            {
                RepairComplete = true;
                repairShopWindow.Close();
                return false;
            }

            return true;
        }
        **/
    }
}
