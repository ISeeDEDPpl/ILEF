﻿//------------------------------------------------------------------------------
//  <copyright from='2010' to='2015' company='THEHACKERWITHIN.COM'>
//    Copyright (c) TheHackerWithin.COM. All Rights Reserved.
//
//    Please look in the accompanying license.htm file for the license that
//    applies to this source code. (a copy can also be found at:
//    http://www.thehackerwithin.com/license.htm)
//  </copyright>
//-------------------------------------------------------------------------------

using Questor.Modules.Lookup;

namespace Questor.Modules.Caching
{
    using System.Collections.Generic;
    using DirectEve;

    public class ItemCacheMarket
    {
        public ItemCacheMarket(DirectItem item, bool cacheRefineOutput)
        {
            Id = item.ItemId;
            Name = item.TypeName;

            TypeId = item.TypeId;
            GroupId = item.GroupId;
            BasePrice = item.BasePrice;
            Volume = item.Volume;
            Capacity = item.Capacity;
            MarketGroupId = item.MarketGroupId;
            PortionSize = item.PortionSize;

            Quantity = item.Quantity;
            QuantitySold = 0;

            RefineOutput = new List<ItemCacheMarket>();
            if (cacheRefineOutput)
            {
                foreach (DirectItem i in item.Materials)
                    RefineOutput.Add(new ItemCacheMarket(i, false));
            }
        }

        public InvTypeMarket InvType { get; set; }

        public long Id { get; private set; }

        public string Name { get; private set; }

        public int TypeId { get; private set; }

        public int GroupId { get; private set; }

        public double BasePrice { get; private set; }

        public double Volume { get; private set; }

        public double Capacity { get; private set; }

        public int MarketGroupId { get; private set; }

        public int PortionSize { get; private set; }

        public int Quantity { get; private set; }

        public int QuantitySold { get; set; }

        public double? StationBuy { get; set; }

        public List<ItemCacheMarket> RefineOutput { get; private set; }
    }
}