using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Dan.Plugin.Pensjon.Models
{
    public class PensionResponse
    {
        public Poliser[] poliser { get; set; }
    }

    public class Poliser
    {
        public string fodselsnummer { get; set; }
        public DateTime opplysningsdato { get; set; }
        public string referanse { get; set; }
        public Pensjonsinnretning pensjonsinnretning { get; set; }
        public string produkttype { get; set; }
        public string produktinformasjon { get; set; }
        public string url { get; set; }
    }

    public class Pensjonsinnretning
    {
        public string orgnr { get; set; }
        public string navn { get; set; }
    }
}
