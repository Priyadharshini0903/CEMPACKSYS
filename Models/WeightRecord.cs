using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CEMPACKSYS.Models
{
    public class WeightRecord
    {
        [Key]
        public int Id { get; set; }

        public int PackerNo { get; set; }

        public int SpoutNo { get; set; }

        [Column(TypeName = "decimal(10,2)")]   
        public decimal Weight { get; set; }

        public DateTime ReceivedAt { get; set; }
    }
}
