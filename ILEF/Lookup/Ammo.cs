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
    using global::ILEF.Caching;
    using global::ILEF.Logging;

    public class Ammo
    {
        public Ammo()
        {
        }

        public Ammo(XElement ammo)
        {
            try
            {
                TypeId = (int)ammo.Attribute("typeId");
                DamageType = (DamageType)Enum.Parse(typeof(DamageType), (string)ammo.Attribute("damageType"));
                Name = (string)ammo.Attribute("name");
                Range = (int)ammo.Attribute("range");
                Quantity = (int)ammo.Attribute("quantity");
                Description = (string)ammo.Attribute("description") ?? (string)ammo.Attribute("typeId");
                //
                // the above is pulling from XML, not eve... the below is what we want to pull from eve
                //

                ILoveEVE.Framework.DirectInvType __directInvTypeItem;
                QMCache.Instance.DirectEve.InvTypes.TryGetValue(TypeId, out __directInvTypeItem);
                if (__directInvTypeItem != null)
                {
                    Name = __directInvTypeItem.TypeName;
                }

                if (Logging.DebugAmmo)
                {
                    Logging.Log("Ammo", " [01] Name [" + Name + "] - derived from XML", Logging.Debug);
                    Logging.Log("Ammo", " [01] TypeId [" + TypeId + "] - from XML", Logging.Debug);
                    Logging.Log("Ammo", " [02] DamageType [" + DamageType + "] - from XML", Logging.Debug);
                    Logging.Log("Ammo", " [03] Range [" + Range + "] - from XML", Logging.Debug);
                    Logging.Log("Ammo", " [04] Quantity [" + Quantity + "] - from XML", Logging.Debug);
                    Logging.Log("Ammo", " [05] Description [" + Description + "] - from XML", Logging.Debug);
                }
            }
            catch (Exception exception)
            {
                Logging.Log("Ammo","Exception [" + exception + "]",Logging.Debug);
            }
        }

        public string Name { get; set; }
        public int TypeId { get; private set; }
        public DamageType DamageType { get; private set; }
        public int Range { get; private set; }
        public int Quantity { get; set; }
        public string Description { get; set; }

        public Ammo Clone()
        {
            Ammo _ammo = new Ammo
                {
                    TypeId = TypeId,
                    DamageType = DamageType,
                    Range = Range,
                    Quantity = Quantity,
                    Description = Description,
                    Name = Name,
                };
            return _ammo;
        }
    }
}