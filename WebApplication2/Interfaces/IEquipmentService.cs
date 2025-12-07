using System.Collections.Generic;
using Rent.Models;
using Rent.DTO;

namespace Rent.Interfaces
{
 public interface IEquipmentService
 {
 Equipment AddEquipment(CreateEquipmentDTO dto);
 IEnumerable<Equipment> GetAll();
 }
}