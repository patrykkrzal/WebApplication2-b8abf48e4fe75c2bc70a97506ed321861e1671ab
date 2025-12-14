using NUnit.Framework;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using System.Threading.Tasks;
using Rent;
using System.Net.Http.Json;
using Rent.DTO;

namespace Tests
{
 public class OrderControllerIntegrationTests
 {
 private WebApplicationFactory<Program>? _factory;
 private HttpClient? _client;

 [SetUp]
 public void Setup()
 {
 _factory = new WebApplicationFactory<Program>();
 _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
 }

 [TearDown]
 public void TearDown()
 {
 _client?.Dispose();
 _factory?.Dispose();
 }

 [Test]
 public async Task Preview_Returns_Price_And_Warning_When_Stock_Insufficient()
 {
 // arrange: create a preview DTO requesting large quantity that exceeds seed availability
 var dto = new CreateOrderDto
 {
 BasePrice =10m,
 Days =1,
 ItemsCount =1000,
 ItemsDetail = new System.Collections.Generic.List<ItemDetailDto>
 {
 new ItemDetailDto { Type = "Goggles", Size = "Universal", Quantity =1000 }
 }
 };

 // act
 var res = await _client!.PostAsJsonAsync("/api/orders/preview", dto);
 Assert.IsTrue(res.IsSuccessStatusCode);
 var obj = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
 Assert.IsTrue(obj.TryGetProperty("Price", out _));
 Assert.IsTrue(obj.TryGetProperty("DiscountPct", out _));
 // when stock insufficient preview returns Warning
 Assert.IsTrue(obj.TryGetProperty("Warning", out var w) && w.GetString() != null);
 }
 }
}
