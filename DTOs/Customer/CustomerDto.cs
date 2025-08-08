namespace BarTasca.DTOs.Customer
{
    public record CustomerDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = null!;
        public string Phone { get; set; } = null!;
    }

}
