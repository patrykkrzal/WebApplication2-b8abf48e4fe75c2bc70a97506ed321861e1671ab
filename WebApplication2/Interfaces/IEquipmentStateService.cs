using Rent.Models;
using System.Threading.Tasks;

namespace Rent.Interfaces
{
 public interface IEquipmentStateService
 {
 void Reserve(OrderedItem oi);
 void Restore(OrderedItem oi);
 }
}
