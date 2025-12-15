using System.Collections.Generic;
using System.Linq;
using Rent.Interfaces;
using Rent.Models;
using Rent.DTO;

namespace Rent.Services
{
 public class EquipmentService : IEquipmentService
 {
 private readonly List<Equipment> equipments;
 private int idCounter =1;
 public EquipmentService(List<Equipment>? equipments = null) { this.equipments = equipments ?? new List<Equipment>(); }

 public Equipment AddEquipment(CreateEquipmentDTO dto)
 {
 var eq = new Equipment
 {
 Id = idCounter++,
 Type = dto.Type,
 Size = dto.Size,
 Price = dto.Price ??0m,
 Is_In_Werehouse = true,
 Is_Reserved = false
 };
 equipments.Add(eq);
 return eq;
 }
 public IEnumerable<Equipment> GetAll() => equipments;
 }
}