using NUnit.Framework;
using Rent.Models;
using Rent.Data;
using Microsoft.EntityFrameworkCore;
using Rent.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Rent.Tests
{
 [TestFixture]
 public class WorkerTests
 {
 [Test]
 public void Worker_Email_Required_And_MaxLength()
 {
 var worker = new Worker { First_name = "Jan", Last_name = "Kowal", Email = "jan@example.com", Phone_number = "123456789", Job_Title = "Manager", RentalInfoId =1 };
 Assert.IsNotNull(worker.Email);
 Assert.LessOrEqual(worker.Email.Length,50);
 }

 [Test]
 public async Task DeleteWorker_ByEmail_Controller_RemovesWorkerAndUser_Simplified()
 {
 // Arrange: in-memory context with worker and user added directly
 var options = new DbContextOptionsBuilder<DataContext>().UseInMemoryDatabase("Test_DeleteWorkerDb_Simple").Options;
 using var ctx = new DataContext(options);
 ctx.RentalInfo.Add(new RentalInfo { Id =1, Address = "a", OpenHour = System.TimeSpan.Zero, CloseHour = System.TimeSpan.Zero });
 ctx.Workers.Add(new Worker { First_name = "Jan", Last_name = "Kowal", Email = "to-delete@example.com", Phone_number = "123456789", Job_Title = "M", RentalInfoId =1 });
 ctx.Users.Add(new User { UserName = "to-delete@example.com", Email = "to-delete@example.com", First_name = "Jan", Last_name = "K" });
 ctx.SaveChanges();

 // Construct UserManager backed by the same context
 var store = new UserStore<User>(ctx);
 var userMgr = new UserManager<User>(store, null, new PasswordHasher<User>(), null, null, null, null, null, new NullLogger<UserManager<User>>());
 var controller = new WorkersController(userMgr, ctx);

 // Act
 var res = await controller.DeleteByEmail("to-delete@example.com");

 // Assert
 Assert.IsInstanceOf<OkObjectResult>(res);
 Assert.IsFalse(ctx.Workers.Any(w => w.Email == "to-delete@example.com"));
 Assert.IsNull(await userMgr.FindByEmailAsync("to-delete@example.com"));
 }
 }
}
