using System.Reflection;

namespace WpfExporter
{
    public class TypeResolver
    {
        public TypeResolver(IEnumerable<Assembly> searchAssemblies)
        {
            SearchAssemblies = new(searchAssemblies);
        }
        public TypeResolver(params Assembly[] searchAssemblies) : this((IEnumerable<Assembly>)searchAssemblies) { }

        public List<Assembly> SearchAssemblies { get; }

        public void PrependSearchAssemblyIfUnique(Assembly assembly)
        {
            if (SearchAssemblies.Contains(assembly)) return;
            SearchAssemblies.Insert(0, assembly);
        }
        public void AppendSearchAssemblyIfUnique(Assembly assembly)
        {
            if (SearchAssemblies.Contains(assembly)) return;
            SearchAssemblies.Add(assembly);
        }

        public IEnumerable<Type> EnumerateAllExportedTypes()
            => SearchAssemblies.SelectMany(asm => asm.GetExportedTypes());

        public IEnumerable<Type> ResolveAll(Func<Type, bool> predicate)
            => EnumerateAllExportedTypes().Where(predicate);

        public IEnumerable<Type> ResolveAllNamespaceSubclassesOf(Func<string, bool> namespacePredicate, IEnumerable<Type> baseTypes)
            => EnumerateAllExportedTypes().Where(type => !string.IsNullOrWhiteSpace(type.Namespace)
                                                 && baseTypes.Any(baseType => type.IsSubclassOf(baseType))
                                                 && namespacePredicate(type.Namespace));
        public IEnumerable<Type> ResolveAllNamespaceSubclassesOf(Func<string, bool> namespacePredicate, params Type[] baseTypes)
            => ResolveAllNamespaceSubclassesOf(namespacePredicate, (IEnumerable<Type>)baseTypes);

        public IEnumerable<Type> ResolveAllSubclassesOf(IEnumerable<Type> baseTypes)
            => EnumerateAllExportedTypes().Where(type => baseTypes.Any(baseType => type.IsSubclassOf(baseType)));
        public IEnumerable<Type> ResolveAllSubclassesOf(params Type[] baseTypes) => ResolveAllSubclassesOf((IEnumerable<Type>)baseTypes);

        public IEnumerable<Type> ResolveAllTypesByName(Func<string, bool> typeNamePredicate)
            => EnumerateAllExportedTypes().Where(type => typeNamePredicate(type.Name) || (type.FullName != null && typeNamePredicate(type.FullName)));

        public Type? ResolveFromTypeName(string typeName, StringComparison stringComparison = StringComparison.Ordinal)
            => EnumerateAllExportedTypes().FirstOrDefault(type => (typeName.Length > type.Name.Length && typeName.Equals(type.FullName, stringComparison))
                                        || typeName.Equals(type.Name, stringComparison));
    }
}