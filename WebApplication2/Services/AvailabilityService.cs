using System.Collections.Generic;
using System.Linq;
using Rent.Interfaces;
using Rent.Models;

namespace Rent.Services
{
 public class AvailabilityService : IAvailabilityService
 {
 private readonly List<Equipment> equipments;
 public AvailabilityService(List<Equipment> equipments) { this.equipments = equipments; }
 public IEnumerable<Equipment> GetAvailableEquipment() =>
 equipments.Where(e => e.Is_In_Werehouse && !e.Is_Reserved);
 }
}