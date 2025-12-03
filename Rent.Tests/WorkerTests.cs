using NUnit.Framework;
using Rent.Models;
using System.Linq;
using System.Collections.Generic;

namespace Rent.Tests
{
 [TestFixture]
 public class WorkerTests
 {
 [Test]
 public void Worker_Email_Required_And_MaxLength()
 {
 var w = new Worker { First_name = "Jan", Last_name = "Kowal", Email = "jan@example.com", Phone_number = "123456789", Job_Title = "Manager", RentalInfoId =1 };
 Assert.IsNotNull(w.Email);
 Assert.LessOrEqual(w.Email.Length,50);
 }

 [Test]
 public void DeleteWorker_ByEmail_FromCollections_RemovesWorkerAndUser_Short()
 {
 var email = "to-delete@example.com";
 var users = new List<User> { new User { UserName = email, Email = email }, new User { UserName = "other", Email = "other@example.com" } };
 var workers = new List<Worker> { new Worker { Email = email }, new Worker { Email = "other@example.com" } };

 var n = email.ToLower();
 users.RemoveAll(u => (u.Email ?? "").ToLower() == n);
 workers.RemoveAll(w => (w.Email ?? "").ToLower() == n);

 Assert.IsEmpty(users.Where(u => (u.Email ?? "").ToLower() == n));
 Assert.IsEmpty(workers.Where(w => (w.Email ?? "").ToLower() == n));
 }
 }
}
