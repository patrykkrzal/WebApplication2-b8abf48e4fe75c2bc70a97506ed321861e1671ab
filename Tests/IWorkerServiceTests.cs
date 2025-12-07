using NUnit.Framework;
using Rent.Interfaces;
using Rent.Services;
using Rent.DTO;

namespace Tests
{
 public class IWorkerServiceTests
 {
 private IWorkerService _svc = null!;
 [SetUp]
 public void Setup() => _svc = new WorkerService();

 [Test]
 public void Worker_service_registers_worker()
 {
 var dto = new CreateWorkerDTO { FirstName = "A", LastName = "B", Email = "a@e.com", PhoneNumber = "1", Job_Title = "J", RentalInfoId =1, Password = "abcdef" };
 var w = _svc.RegisterWorker(dto);
 Assert.IsNotNull(w);
 Assert.AreEqual("a@e.com", w.Email);
 }

 [Test]
 public void Worker_service_delete_worker_removes_by_email()
 {
 var dto = new CreateWorkerDTO { FirstName = "A", LastName = "B", Email = "del@e.com", PhoneNumber = "1", Job_Title = "J", RentalInfoId =1, Password = "abcdef" };
 var w = _svc.RegisterWorker(dto);
 Assert.IsNotNull(w);
 _svc.DeleteWorker("del@e.com");
 // no exception and worker should be removed; try registering same email again
 var w2 = _svc.RegisterWorker(dto);
 Assert.IsNotNull(w2);
 }
 }
}
