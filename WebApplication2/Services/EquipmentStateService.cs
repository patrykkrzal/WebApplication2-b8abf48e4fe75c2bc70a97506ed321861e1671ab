using System.Collections.Generic;
using Rent.Models;
using Rent.Interfaces;

namespace Rent.Services
{
    public class EquipmentStateService : IEquipmentStateService
    {
        // reserve/restore helpers
        public void Reserve(OrderedItem oi)
        {
            var e = oi.Equipment;
            if (e == null) return;
            e.Is_In_Werehouse = false;
            e.Is_Reserved = true;
        }

        public void Restore(OrderedItem oi)
        {
            var e = oi.Equipment;
            if (e == null) return;
            e.Is_In_Werehouse = true;
            e.Is_Reserved = false;
        }
    }
}
