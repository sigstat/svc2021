using System;
using System.Collections.Generic;
using System.Text;

namespace SVC2021.Entities
{
    public class Neighborhood
    {
        public string PrimarySignatureId { get; set; }
        public string [] NeighborSignatureIds { get; set; }
        public double [] NeighborDistances { get; set; }
    }
}
