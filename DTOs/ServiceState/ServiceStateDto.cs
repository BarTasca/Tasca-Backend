namespace BarTasca.DTOs.ServiceState
{
    public record ServiceStateDto
    {
        public bool IsOpen { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
