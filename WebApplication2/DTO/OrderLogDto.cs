using System;

namespace Rent.DTO
{
    public class OrderLogDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime LogDate { get; set; }
    }
}
