namespace Rent.Services
{
 public interface IPriceResolver
 {
 decimal ResolvePrice(string type, string size);
 }
}