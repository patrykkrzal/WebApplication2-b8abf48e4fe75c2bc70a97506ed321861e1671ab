using System;
using System.Collections.Generic;
using System.Linq;
using Rent.Data;
using Rent.Models;
using Rent.Enums;

namespace Rent
{
    public class Seed
    {
        private readonly DataContext dataContext;

        public Seed(DataContext context)
        {
            this.dataContext = context;
        }

        public void SeedDataContext()
        {
            // Seed only if brak rekordów w RentalInfo (nie Users bo admin już istnieje)
            if (!dataContext.RentalInfo.Any())
            {
                var rentalInfo = new RentalInfo()
                {
                    OpenHour = new TimeSpan(8, 0, 0),
                    CloseHour = new TimeSpan(18, 0, 0),
                    Address = "ul. Centralna1",
                    PhoneNumber = "123456789",
                    OpenDays = "Mon-Fri",
                    Email = "info@rental.com",
                    Users = new List<User>(),
                    Workers = new List<Worker>(),
                    Equipment = new List<Equipment>(),
                    Orders = new List<Order>()
                };

                var user1 = new User()
                {
                    First_name = "Paweł",
                    Last_name = "Kowalski",
                    Login = "pawel",
                    Email = "pawel@example.com",
                    PhoneNumber = "111222333",
                    RentalInfo = rentalInfo,
                    Orders = new List<Order>()
                };

                var user2 = new User()
                {
                    First_name = "Anna",
                    Last_name = "Nowak",
                    Login = "anna",
                    Email = "anna@example.com",
                    PhoneNumber = "444555666",
                    RentalInfo = rentalInfo,
                    Orders = new List<Order>()
                };

                rentalInfo.Users.Add(user1);
                rentalInfo.Users.Add(user2);

                var worker1 = new Worker()
                {
                    First_name = "Jan",
                    Last_name = "Kowal",
                    Email = "jan@example.com",
                    Phone_number = "777888999",
                    Address = "ul. Działkowa3",
                    Role = "administrator",
                    WorkStart = new TimeSpan(8, 0, 0),
                    WorkEnd = new TimeSpan(16, 0, 0),
                    Working_Days = "Mon-Fri",
                    Job_Title = "Manager",
                    RentalInfo = rentalInfo
                };

                var worker2 = new Worker()
                {
                    First_name = "Ewa",
                    Last_name = "Zielińska",
                    Email = "ewa@example.com",
                    Phone_number = "222333444",
                    Address = "ul. Kwiatowa5",
                    Role = "worker",
                    WorkStart = new TimeSpan(10, 0, 0),
                    WorkEnd = new TimeSpan(18, 0, 0),
                    Working_Days = "Mon-Fri",
                    Job_Title = "Cashier",
                    RentalInfo = rentalInfo
                };

                rentalInfo.Workers.Add(worker1);
                rentalInfo.Workers.Add(worker2);

                // Generate items
                void AddItems(EquipmentType type, Size size, decimal price, int count = 5)
                {
                    for (int i = 0; i < count; i++)
                    {
                        rentalInfo.Equipment.Add(new Equipment
                        {
                            Type = type,
                            Size = size,
                            Is_In_Werehouse = true,
                            Is_Reserved = false,
                            Price = price,
                            RentalInfo = rentalInfo
                        });
                    }
                }

                AddItems(EquipmentType.Skis, Size.Small, 120m);
                AddItems(EquipmentType.Skis, Size.Medium, 130m);
                AddItems(EquipmentType.Skis, Size.Large, 140m);
                AddItems(EquipmentType.Helmet, Size.Universal, 35m);
                AddItems(EquipmentType.Gloves, Size.Small, 15m);
                AddItems(EquipmentType.Gloves, Size.Medium, 15m);
                AddItems(EquipmentType.Gloves, Size.Large, 15m);
                AddItems(EquipmentType.Poles, Size.Medium, 22m);
                AddItems(EquipmentType.Snowboard, Size.Medium, 160m);
                AddItems(EquipmentType.Goggles, Size.Universal, 55m);

                // Pierwszy zapis: RentalInfo + Users + Workers + Equipment
                dataContext.RentalInfo.Add(rentalInfo);
                dataContext.SaveChanges();

                // Dodaj przykładowe zamówienie po zapisaniu bazowych danych
                var order1 = new Order()
                {
                    Rented_Items = "Skis Small",
                    OrderDate = DateTime.UtcNow,
                    Price = 120m,
                    Date_Of_submission = DateOnly.FromDateTime(DateTime.UtcNow),
                    Was_It_Returned = false,
                    User = user1,
                    RentalInfo = rentalInfo
                };
                dataContext.Orders.Add(order1);
                dataContext.SaveChanges();
            }
        }
    }
}
