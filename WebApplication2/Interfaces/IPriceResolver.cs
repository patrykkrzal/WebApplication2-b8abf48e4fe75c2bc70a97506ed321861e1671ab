using Rent.Enums;

namespace Rent.Services
{
 public interface IPriceResolver
 {
 decimal ResolvePrice(EquipmentType type, Size size);
 }
}