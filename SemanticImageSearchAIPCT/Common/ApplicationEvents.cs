using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticImageSearchAIPCT.Common
{
    public static class ApplicationEvents
    {
        public static event EventHandler<bool>? InferenceServicesReadyStateChanged;

        public static void RaiseInferenceServicesReadyStateChanged(bool state)
        {
            try
            {
                InferenceServicesReadyStateChanged?.Invoke(null, state);
            }
            catch { }
        }
    }
}
