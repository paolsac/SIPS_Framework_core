using SIPS.Framework.Core.Interfaces;

namespace SIPS.Framework.Core.Api
{
    public class FC_Base_Response : IFCResponse
    {
        public string StatusMessage { get; set; } = string.Empty;
        public string ErrorMessage { get; set; }
        public bool Success { get; set; }
        public object Value { get; set; }
    }

}
