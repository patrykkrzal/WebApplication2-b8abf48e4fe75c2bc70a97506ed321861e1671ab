using System.Collections.Generic;
using Rent.Models;

namespace Rent.Interfaces
{
    public interface IRentalInfoRepository
    {
        ICollection<RentalInfo> GetRentalInfos();
    }
}
