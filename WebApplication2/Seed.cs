using System;
using System.Collections.Generic;
using System.Linq;
using Rent.Data;
using Rent.Models;

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
                void AddItems(string type, string size, decimal price, int count = 5)
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

                AddItems("Skis", "Small", 120m);
                AddItems("Skis", "Medium", 130m);
                AddItems("Skis", "Large", 140m);
                AddItems("Helmet", "Universal", 35m);
                AddItems("Gloves", "Small", 15m);
                AddItems("Gloves", "Medium", 15m);
                AddItems("Gloves", "Large", 15m);
                AddItems("Poles", "Medium", 22m);
                AddItems("Snowboard", "Medium", 160m);
                AddItems("Goggles", "Universal", 55m);

                dataContext.RentalInfo.Add(rentalInfo);
                dataContext.SaveChanges();
            }


            if (!dataContext.Set<Rent.Models.EquipmentPrice>().Any())
            {
                var prices = new List<Rent.Models.EquipmentPrice>
                {
                    new Rent.Models.EquipmentPrice { Type = "Skis", Size = "Small", Price =120m },
                    new Rent.Models.EquipmentPrice { Type = "Skis", Size = "Medium", Price =130m },
                    new Rent.Models.EquipmentPrice { Type = "Skis", Size = "Large", Price =140m },
                    new Rent.Models.EquipmentPrice { Type = "Helmet", Size = "Universal", Price =35m },
                    new Rent.Models.EquipmentPrice { Type = "Gloves", Size = "Small", Price =15m },
                    new Rent.Models.EquipmentPrice { Type = "Gloves", Size = "Medium", Price =15m },
                    new Rent.Models.EquipmentPrice { Type = "Gloves", Size = "Large", Price =15m },
                    new Rent.Models.EquipmentPrice { Type = "Poles", Size = "Medium", Price =22m },
                    new Rent.Models.EquipmentPrice { Type = "Snowboard", Size = "Medium", Price =160m },
                    new Rent.Models.EquipmentPrice { Type = "Goggles", Size = "Universal", Price =55m }
                };
                dataContext.Set<Rent.Models.EquipmentPrice>().AddRange(prices);
                dataContext.SaveChanges();
            }
        }
    }
}
