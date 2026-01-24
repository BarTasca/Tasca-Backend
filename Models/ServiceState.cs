using System.ComponentModel.DataAnnotations.Schema;

namespace BarTasca.Models
{
    public class ServiceState
    {
        public int Id { get; set; }

        public bool IsOpen { get; set; }

        public DateTime UpdatedAt { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
