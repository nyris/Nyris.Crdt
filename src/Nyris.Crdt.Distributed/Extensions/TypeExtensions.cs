using System;
using System.Linq;

namespace Nyris.Crdt.Distributed.Extensions
{
    internal static class TypeExtensions
    {
        public static bool IsSubclassOfRawGeneric(this Type? toCheck, Type generic)
        {
            if (toCheck is null) return false;

            if (generic.IsInterface)
            {
                return toCheck.GetInterfaces()
                              .Any(x => generic == (x.IsGenericType ? x.GetGenericTypeDefinition() : x));
            }

            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }

                toCheck = toCheck.BaseType;
            }

            return false;
        }
    }
}
