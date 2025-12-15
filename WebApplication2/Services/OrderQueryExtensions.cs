using System.Linq;
using Microsoft.EntityFrameworkCore;
using Rent.Models;

namespace Rent.Services
{
 public static class OrderQueryExtensions
 {
 // include navigation
 public static IQueryable<Order> Full(this IQueryable<Order> q)
 {
 return q.Include(o => o.User).Include(o => o.OrderedItems).ThenInclude(oi => oi.Equipment);
 }
 }
}
