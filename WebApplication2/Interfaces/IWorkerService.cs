using Rent.Models;
using Rent.DTO;

namespace Rent.Interfaces
{
 public interface IWorkerService
 {
 Worker RegisterWorker(CreateWorkerDTO dto);
 void DeleteWorker(string email);
 }
}