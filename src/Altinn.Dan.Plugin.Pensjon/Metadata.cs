using System.Collections.Generic;
using Altinn.Dan.Plugin.Pensjon.Models;
using Nadobe.Common.Interfaces;
using Nadobe.Common.Models;
using Nadobe.Common.Models.Enums;
using NJsonSchema;

namespace Altinn.Dan.Plugin.Pensjon
{
    public class Metadata : IEvidenceSourceMetadata
    {
        private const string SERIVCECONTEXT_OED = "OED";

        public const string SOURCE = "Norsk Pensjon AS";
        public const int ERROR_CCR_UPSTREAM_ERROR = 2;
        public const int ERROR_ORGANIZATION_NOT_FOUND = 1;
        public const int ERROR_NO_REPORT_AVAILABLE = 3;
        public const int ERROR_ASYNC_REQUIRED_PARAMS_MISSING = 4;
        public const int ERROR_ASYNC_ALREADY_INITIALIZED = 5;
        public const int ERROR_ASYNC_NOT_INITIALIZED = 6;
        public const int ERROR_AYNC_STATE_STORAGE = 7;
        public const int ERROR_ASYNC_HARVEST_NOT_AVAILABLE = 8;
        public const int ERROR_CERTIFICATE_OF_REGISTRATION_NOT_AVAILABLE = 9;

        public const string METADATA_FUNCTION_NAME = "evidencecodes";

        public List<EvidenceCode> GetEvidenceCodes()
        {
            return new List<EvidenceCode>()
            {
                new EvidenceCode()
                {
                    EvidenceCodeName = "NorskPensjon",
                    EvidenceSource = SOURCE,
                    BelongsToServiceContexts = new List<string>() { SERIVCECONTEXT_OED },                    
                    Values = new List<EvidenceValue>()
                    {
                        new EvidenceValue()
                        {
                            EvidenceValueName = "default",
                            ValueType = EvidenceValueType.JsonSchema,
                            JsonSchemaDefintion = null
                        },
                    }
                }
            };
        }
    }
}
