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
    using System;
    using System.Xml.Linq;

    public class MiningCrystals
    {
        public MiningCrystals()
        {
        }

        public MiningCrystals(XElement MiningCrystals)
        {
            TypeId = (int)MiningCrystals.Attribute("typeId");
            OreType = (OreType)Enum.Parse(typeof(OreType), (string)MiningCrystals.Attribute("oreType"));
            Quantity = (int)MiningCrystals.Attribute("quantity");
            Description = (string)MiningCrystals.Attribute("description") ?? (string)MiningCrystals.Attribute("typeId");
        }

        public int TypeId { get; private set; }

        public OreType OreType { get; private set; }

        public int Range { get; private set; }

        public int Quantity { get; set; }

        public string Description { get; set; }

        public MiningCrystals Clone()
        {
            MiningCrystals _miningCrystals = new MiningCrystals
            {
                TypeId = TypeId,
                OreType = OreType,
                Quantity = Quantity,
                Description = Description
            };

            return _miningCrystals;
        }
    }
}