#pragma warning disable 1591
using ILoveEVE.Framework;

namespace ILEF.KanedaToolkit
{
    /// <summary>
    /// extension methods for InventoryContainer
    /// </summary>
    public static class KInventoryContainer
    {
        public static double AvailCargo(this DirectContainer inventoryContainer)
        {
            return inventoryContainer.Capacity - inventoryContainer.UsedCapacity;
        }
    }
}
