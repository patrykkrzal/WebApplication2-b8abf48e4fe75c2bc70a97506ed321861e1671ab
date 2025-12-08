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
 public void RegisterWorker_returns_worker_with_expected_fields_and_id_increments()
 {
 var dto1 = new CreateWorkerDTO { FirstName = "A", LastName = "B", Email = "a@e.com", PhoneNumber = "123456789", Job_Title = "J", RentalInfoId =1, Password = "abcdef" };
 var w1 = _svc.RegisterWorker(dto1);

 Assert.IsNotNull(w1);
 Assert.AreEqual("a@e.com", w1.Email);
 Assert.AreEqual("A", w1.First_name);
 Assert.AreEqual("B", w1.Last_name);
 Assert.AreEqual("J", w1.Job_Title);

 var dto2 = new CreateWorkerDTO { FirstName = "C", LastName = "D", Email = "c@e.com", PhoneNumber = "987654321", Job_Title = "K", RentalInfoId =1, Password = "abcdef" };
 var w2 = _svc.RegisterWorker(dto2);

 Assert.IsNotNull(w2);
 Assert.AreNotEqual(w1.Id, w2.Id, "Each registered worker should get a unique id");
 }

 [Test]
 public void DeleteWorker_removes_worker_and_allows_reregistration()
 {
 var dto = new CreateWorkerDTO { FirstName = "A", LastName = "B", Email = "del@e.com", PhoneNumber = "111222333", Job_Title = "J", RentalInfoId =1, Password = "abcdef" };
 var w = _svc.RegisterWorker(dto);
 Assert.IsNotNull(w);

 // delete
 _svc.DeleteWorker("del@e.com");

 // after deletion, registering same email should succeed and produce a new worker (not null)
 var w2 = _svc.RegisterWorker(dto);
 Assert.IsNotNull(w2);
 Assert.AreNotEqual(w.Id, w2.Id, "Re-registered worker should have a different id than deleted one");
 }
 }
}
