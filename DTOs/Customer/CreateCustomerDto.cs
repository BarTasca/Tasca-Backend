namespace BarTasca.DTOs.Customer
{
    public record CreateCustomerDto
    {
        public string FullName { get; set; } = null!;
        public string Phone { get; set; } = null!;
    }

}
