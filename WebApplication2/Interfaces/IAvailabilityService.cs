using System.Collections.Generic;
using Rent.Models;

namespace Rent.Interfaces
{
 public interface IAvailabilityService
 {
 IEnumerable<Equipment> GetAvailableEquipment();
 }
}