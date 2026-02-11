namespace BarTasca.DTOs.Queue
{
    public record QueueAheadDto
    {
        public bool IsOpen { get; init; }
        public int Ahead { get; init; }
    }
}
