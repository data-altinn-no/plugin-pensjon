using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Dan.Plugin.Pensjon.Models
{
    public class NorskPensjonResponse
    {
        public List<InsurancePolicy> InsurancesPolicies { get; set; }
    }

    public class InsurancePolicy
    {
        public DateTimeOffset DisclosureDate { get; set; }

        public string Url { get; set; }

        public string ProductType { get; set; }

        public string PensionScheme { get; set; }

        public string Reference { get; set; }

        public string Description { get; set; }
    }
}
