namespace Rent.Interfaces
{
 public interface IPriceResolver
 {
 decimal ResolvePrice(string type, string size);
 }
}