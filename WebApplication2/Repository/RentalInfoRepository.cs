using System.Collections.Generic;
using System.Linq;
using Rent.Data;
using Rent.Interfaces;
using Rent.Models;

namespace Rent.Ropository
{
    public class RentalInfoRepository : IRentalInfoRepository
    {
        private readonly DataContext _context;


        public RentalInfoRepository(DataContext context)

        { 
            _context = context;
        } 
        public ICollection<RentalInfo> GetRentalInfos()
        {
            return _context.RentalInfo.OrderBy(r => r.Id ).ToList();
        }
    }
}
