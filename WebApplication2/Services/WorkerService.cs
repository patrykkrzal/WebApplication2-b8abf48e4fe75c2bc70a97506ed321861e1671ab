using System.Collections.Generic;
using System.Linq;
using Rent.Interfaces;
using Rent.DTO;
using Rent.Models;

namespace Rent.Services
{
 public class WorkerService : IWorkerService
 {
 private readonly List<Worker> workers = new List<Worker>();
 private int idCounter =1;
 public Worker RegisterWorker(CreateWorkerDTO dto)
 {
 var worker = new Worker
 {
 Id = idCounter++,
 First_name = dto.FirstName,
 Last_name = dto.LastName,
 Email = dto.Email,
 Phone_number = dto.PhoneNumber,
 Job_Title = dto.Job_Title,
 RentalInfoId = dto.RentalInfoId
 };
 workers.Add(worker);
 return worker;
 }
 public void DeleteWorker(string email)
 {
 var w = workers.FirstOrDefault(x => x.Email.ToLower() == email.ToLower());
 if (w != null) workers.Remove(w);
 }
 }
}