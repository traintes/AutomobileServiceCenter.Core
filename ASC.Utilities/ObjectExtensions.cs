using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ASC.Utilities
{
    public static class ObjectExtensions
    {
        public static T CopyObject<T>(this object objSource)
        {
            string serialized = JsonConvert.SerializeObject(objSource);
            return JsonConvert.DeserializeObject<T>(serialized);
        }
    }
}
