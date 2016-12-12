using System;
using System.Collections.Generic;
using System.Linq;
using ILoveEVE.Framework;
using ILEF.Core;
using ILEF.Lookup;
using ILEF.Caching;

namespace ILEF.Cargo
{
    /// <summary>
    /// This class handles cargo operation, including navigation
    /// </summary>
    public class Cargo : State
    {
        private class CargoAction
        {
            internal Func<object[], bool> Action { get; set; }
            internal ILoveEVE.Framework.DirectBookmark Bookmark { get; }
            internal Func<DirectItem, bool> QueryString { get; set; }
            internal int Quantity { get; set; }
            internal Func<DirectContainer> Source { get; }
            internal string Container { get; }
            internal Func<DirectContainer> Target { get; set; }
            internal bool Compress { get; set; }

            internal CargoAction(Func<object[], bool> Action, ILoveEVE.Framework.DirectBookmark Bookmark, Func<DirectContainer> Source, string Container, Func<DirectItem, bool> QueryString, int Quantity, Func<DirectContainer> Target, bool Compress=false)
            {
                this.Action = Action;
                this.Bookmark = Bookmark;
                this.Source = Source;
                this.Container = Container;
                this.QueryString = QueryString;
                this.Quantity = Quantity;
                this.Target = Target;
                this.Compress = Compress;
            }

            public CargoAction Clone()
            {
                return new CargoAction(Action, Bookmark, Source, Container, QueryString, Quantity, Target, Compress);
            }
        }

        #region Variables

        LinkedList<CargoAction> CargoQueue = new LinkedList<CargoAction>();
        CargoAction CurrentCargoAction;
        CargoAction BuildCargoAction;
        Move.Move Move = ILEF.Move.Move.Instance;
        /// <summary>
        /// Log for Cargo module
        /// </summary>
        public Logger Log = new Logger("Cargo");

        #endregion

        #region Instantiation

        static Cargo _Instance;
        /// <summary>
        /// Singletoner
        /// </summary>
        public static Cargo Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new Cargo();
                }
                return _Instance;
            }
        }

        private Cargo()
        {

        }

        #endregion

        #region Actions

        /// <summary>
        /// Specify the location to perform the cargo operation
        /// </summary>
        /// <param name="Bookmark">Bookmark object for the location to perform the cargo operation</param>
        /// <param name="Source">InventoryContainer to use for the operation (load source for Load, unload destination for Unload)  Default: Station Item Hangar</param>
        /// <param name="ContainerName">Name of the entity to use for the operation (for entities with inventory containers in space)</param>
        /// <returns></returns>
        public Cargo At(ILoveEVE.Framework.DirectBookmark Bookmark, Func<DirectContainer> Source = null, string ContainerName = "")
        {
            BuildCargoAction = new CargoAction(null, Bookmark, Source ?? (() => QMCache.Instance.ItemHangar), ContainerName, null, 0, null);
            return this;
        }

        /// <summary>
        /// Add a Load operation
        /// </summary>
        /// <param name="QueryString">Linq parameters for specifying the items to load.</param>
        /// <param name="Quantity">Quantity of the item to load (Must specify a single item type using QueryString)</param>
        /// <param name="Target">Where to load the item(s) - Default: Cargo Hold</param>
        /// <returns></returns>
        public Cargo Load(Func<DirectItem, bool> QueryString = null, int Quantity = 0, Func<DirectContainer> Target = null)
        {
            BuildCargoAction.Action = Load;
            BuildCargoAction.QueryString = QueryString ?? (item => true);
            BuildCargoAction.Quantity = Quantity;
            BuildCargoAction.Target = Target ?? (() => QMCache.Instance.CurrentShipsCargo);
            CargoQueue.AddFirst(BuildCargoAction.Clone());
            if (Idle) QueueState(Process);
            return this;
        }

        /// <summary>
        /// Add an Unload operation
        /// </summary>
        /// <param name="QueryString">Linq parameters for specifying the items to unload.</param>
        /// <param name="Quantity">Quantity of the item to unload (Must specify a single item type using QueryString)</param>
        /// <param name="Target">Where to unload the item(s) from - Default: Cargo Hold</param>
        /// <param name="Compress">Compress all compressible items after unloading. Only applicable if target is a compression array - Default: false</param>
        /// <returns></returns>
        public Cargo Unload(Func<DirectItem, bool> QueryString = null, int Quantity = 0, Func<DirectContainer> Target = null, bool Compress=false)
        {
            BuildCargoAction.Action = Unload;
            BuildCargoAction.QueryString = QueryString ?? (item => true);
            BuildCargoAction.Quantity = Quantity;
            BuildCargoAction.Target = Target ?? (() => QMCache.Instance.CurrentShipsCargo);
            BuildCargoAction.Compress = Compress;
            CargoQueue.AddFirst(BuildCargoAction.Clone());
            if (Idle) QueueState(Process);
            return this;
        }

        /// <summary>
        /// Don't do anything - used in conjunction with Cargo.At to queue up a move to a location without performing a cargo operation
        /// </summary>
        /// <returns></returns>
        public Cargo NoOp()
        {
            BuildCargoAction.Action = NoOp;
            BuildCargoAction.QueryString = null;
            BuildCargoAction.Quantity = 0;
            BuildCargoAction.Target = null;
            CargoQueue.AddFirst(BuildCargoAction.Clone());
            if (Idle) QueueState(Process);
            return this;
        }

        #endregion

        #region States

        bool Process(object[] Params)
        {
            if (CargoQueue.Any())
            {
                CurrentCargoAction = CargoQueue.Last();
                CargoQueue.RemoveLast();
            }
            else
            {
                CurrentCargoAction = null;
                return true;
            }

            Move.Bookmark(CurrentCargoAction.Bookmark);

            QueueState(Traveling);
            QueueState(WarpFleetMember);
            QueueState(Traveling);
            if(CurrentCargoAction.Action == Load || CurrentCargoAction.Action == Unload)
            {
                QueueState(LoadUnloadPrime);
            }
            QueueState(CurrentCargoAction.Action);
            if (CurrentCargoAction.Compress)
            {
                QueueState(Stack);
                QueueState(Compress);
            }
            QueueState(Stack);
            QueueState(Process);

            return true;
        }

        bool Traveling(object[] Params)
        {
            if (!Move.Idle)
            {
                return false;
            }
            return true;
        }

        bool WarpFleetMember(object[] Params)
        {
            return true;
        }

        bool Compress(object[] Params)
        {
            //if (!Cache.Instance.AllEntities.Any(a => a.GroupId == Group.CompressionArray && a.Distance < 3000) || Cache.Instance.AllEntities.Any(a => a.GroupId == (int)Group.CompressionArray && a.Distance >= 3000)) return true;

            //foreach (DirectItem item in CurrentCargoAction.Source().Items.Where(a => a.Compressible && (a.GroupId == (int)Group.Ice || (a.CategoryId == (int)CategoryID.Asteroid && a.Quantity > 100))))
            //{
            //    item.Compress();
            //    return false;
            //}
            return true;
        }

        bool Stack(object[] Params)
        {
            try
            {
                if (CurrentCargoAction.Action == Load)
                {
                    CurrentCargoAction.Target().StackAll();
                }
                if (CurrentCargoAction.Action == Unload)
                {
                    CurrentCargoAction.Source().StackAll();
                }

            }
            catch { }
            return true;
        }

        private bool LoadUnloadPrime(object[] Params)
        {
            if (!CurrentCargoAction.Target().IsPrimed)
            {
#if DEBUG
                Log.Log("Calling Prime() on Target of Type " + CurrentCargoAction.Source().GetType().FullName, LogType.DEBUG);
#endif
                CurrentCargoAction.Target().Prime();
                return false;
            }
            if (!CurrentCargoAction.Source().IsPrimed)
            {
#if DEBUG
                Log.Log("Calling Prime() on Source of Type "+ CurrentCargoAction.Source().GetType().FullName, LogType.DEBUG);
#endif
                CurrentCargoAction.Source().Prime();
                return false;
            }
            return true;
        }

        private bool Load(object[] Params)
        {
            Log.Log("|oLoading");
            try
            {
                if (CurrentCargoAction.Quantity != 0)
                {
                    int DesiredQuantity = CurrentCargoAction.Quantity;
                    DirectContainer Target = CurrentCargoAction.Target();
                    foreach (DirectItem item in CurrentCargoAction.Source().Items.Where(CurrentCargoAction.QueryString))
                    {
                        if (Math.Abs(item.Quantity) < DesiredQuantity)
                        {
                            DesiredQuantity -= Math.Abs(item.Quantity);
                            Target.Add(item);
                        }
                        else
                        {
                            Target.Add(item, DesiredQuantity);
                            return true;
                        }
                    }
                }
                else
                {
                    double AvailableSpace = CurrentCargoAction.Target().Capacity - CurrentCargoAction.Target().UsedCapacity;
                    foreach (DirectItem item in CurrentCargoAction.Source().Items.Where(CurrentCargoAction.QueryString))
                    {
                        if (Math.Abs(item.Quantity) * item.Volume <= AvailableSpace)
                        {
                            CurrentCargoAction.Target().Add(item);
                            AvailableSpace = AvailableSpace - Math.Abs(item.Quantity) * item.Volume;
                        }
                        else if (item.Volume <= AvailableSpace)
                        {
                            int nextMove = (int)Math.Floor(AvailableSpace / item.Volume);
                            CurrentCargoAction.Target().Add(item, nextMove);
                            AvailableSpace = AvailableSpace - nextMove * item.Volume;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Log(ex.Message);
            }
            return true;
        }

        bool Unload(object[] Params)
        {
            Log.Log("|oUnloading");
            try
            {
                if (CurrentCargoAction.Quantity != 0)
                {
                    int DesiredQuantity = CurrentCargoAction.Quantity;
                    DirectContainer Target = CurrentCargoAction.Source();
                    foreach (DirectItem item in CurrentCargoAction.Target().Items.Where(CurrentCargoAction.QueryString))
                    {
                        if (Math.Abs(item.Quantity) < DesiredQuantity)
                        {
                            DesiredQuantity -= Math.Abs(item.Quantity);
                            Target.Add(item);
                        }
                        else
                        {
                            Target.Add(item, DesiredQuantity);
                            return true;
                        }
                    }
                }
                else
                {
                    CurrentCargoAction.Target().Items.Where(CurrentCargoAction.QueryString).MoveTo(CurrentCargoAction.Source());
                }
            }
            catch { }
            return true;
        }

        bool NoOp(object[] Params)
        {
            return true;
        }

        #endregion

    }

}
