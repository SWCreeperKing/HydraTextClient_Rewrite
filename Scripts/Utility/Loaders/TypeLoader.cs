using System;
using System.Linq;
using System.Reflection;

namespace HydraTextClient.Scripts.Utility.Loaders;

public static class TypeLoader
{
    public static Type[] Types = Assembly.GetExecutingAssembly().GetTypes();

    public static T[] CreateTypesWithAbstractClass<T>()
        =>
        [
            .. Types
              .Where(t => t is { IsClass: true, IsAbstract: false } &&
                          t.IsSubclassOf(typeof(T))
               )
              .Select(Activator.CreateInstance)
              .Cast<T>(),
        ];
}