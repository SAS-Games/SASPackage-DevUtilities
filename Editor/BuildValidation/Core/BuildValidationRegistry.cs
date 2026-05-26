using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace SAS.BuildValidation
{
    public static class BuildValidationRegistry
    {
        private static List<Type> _cachedValidationTypes;

        public static IReadOnlyList<Type> GetValidationTypes()
        {
            if (_cachedValidationTypes != null)
                return _cachedValidationTypes;

            _cachedValidationTypes = TypeCache.GetTypesDerivedFrom<IBuildValidation>()
                    .Where(type => !type.IsAbstract && !type.IsInterface)
                    .OrderBy(GetValidationOrder)
                    .ToList();
            
            return _cachedValidationTypes;
        }

        private static int GetValidationOrder(Type type)
        {
            var attribute = (BuildValidationAttribute)Attribute.GetCustomAttribute(type, typeof(BuildValidationAttribute));
            return attribute?.Order ?? 0;
        }
    }
}