﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NetPrints.Core;
using System.Xml;
using System.IO;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NetPrintsEditor.Reflection
{
    public static class ISymbolExtensions
    {
        /// <summary>
        /// Gets all members of a symbol including inherited ones.
        /// </summary>
        public static IEnumerable<ISymbol> GetAllMembers(this ITypeSymbol symbol)
        {
            List<ISymbol> members = new List<ISymbol>();
            HashSet<ISymbol> overridenSymbols = new HashSet<ISymbol>();

            while (symbol != null)
            {
                var symbolMembers = symbol.GetMembers();

                // Add symbols which weren't overriden yet
                List<ISymbol> newMembers = symbolMembers.Where(m => !overridenSymbols.Contains(m)).ToList();

                members.AddRange(newMembers);

                // Remember which symbols were overriden
                foreach (ISymbol symbolMember in symbolMembers)
                {
                    if (!symbolMember.IsDefinition && symbolMember.OriginalDefinition != null)
                    {
                        overridenSymbols.Add(symbolMember.OriginalDefinition);
                    }
                }

                symbol = symbol.BaseType;
            }

            return members;
        }

        public static bool IsPublic(this ISymbol symbol)
        {
            return symbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public;
        }

        public static IEnumerable<IMethodSymbol> GetMethods(this ITypeSymbol symbol)
        {
            return symbol.GetAllMembers()
                    .Where(member => member.Kind == SymbolKind.Method)
                    .Cast<IMethodSymbol>()
                    .Where(method => method.MethodKind == MethodKind.Ordinary || method.MethodKind == MethodKind.BuiltinOperator || method.MethodKind == MethodKind.UserDefinedOperator);
        }

        public static IEnumerable<IMethodSymbol> GetConverters(this ITypeSymbol symbol)
        {
            return symbol.GetAllMembers()
                    .Where(member => member.Kind == SymbolKind.Method)
                    .Cast<IMethodSymbol>()
                    .Where(method => method.MethodKind == MethodKind.Conversion);
        }

        public static bool IsSubclassOf(this ITypeSymbol symbol, ITypeSymbol cls)
        {
            // Traverse base types to find out if symbol inherits from cls

            ITypeSymbol candidateBaseType = symbol;

            while (candidateBaseType != null)
            {
                if (candidateBaseType == cls)
                {
                    return true;
                }

                candidateBaseType = candidateBaseType.BaseType;
            }

            return false;
        }

        public static string GetFullName(this ITypeSymbol typeSymbol)
        {
            string fullName = typeSymbol.MetadataName;
            if (typeSymbol.ContainingNamespace != null)
            {
                fullName = $"{typeSymbol.ContainingNamespace.MetadataName}.{fullName}";
            }
            return fullName;
        }
    }

    public class ReflectionProvider : IReflectionProvider
    {
        private readonly CSharpCompilation compilation;
        private readonly DocumentationUtil documentationUtil;

        public ReflectionProvider(IEnumerable<string> assemblyPaths)
        {
            compilation = CSharpCompilation.Create("C")
                .AddReferences(assemblyPaths.Select(path =>
                {
                    DocumentationProvider documentationProvider = DocumentationProvider.Default;

                    // Try to find the documentation in the framework doc path
                    string docPath = Path.ChangeExtension(path, ".xml");
                    if (!File.Exists(docPath))
                    {
                        docPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                            "Reference Assemblies/Microsoft/Framework/.NETFramework/v4.X",
                            $"{Path.GetFileNameWithoutExtension(path)}.xml");
                    }

                    if (File.Exists(docPath))
                    {
                        documentationProvider = XmlDocumentationProvider.CreateFromFile(docPath);
                    }
                   
                    return MetadataReference.CreateFromFile(path, documentation: documentationProvider);
                }));

            documentationUtil = new DocumentationUtil(compilation);
        }

        private IEnumerable<INamedTypeSymbol> GetNamespaceTypes(INamespaceSymbol namespaceSymbol)
        {
            IEnumerable<INamedTypeSymbol> types = namespaceSymbol.GetTypeMembers();
            return types.Concat(namespaceSymbol.GetNamespaceMembers().SelectMany(ns => GetNamespaceTypes(ns)));
        }

        private IEnumerable<INamedTypeSymbol> GetValidTypes()
        {
            return compilation.SourceModule.ReferencedAssemblySymbols.SelectMany(module =>
                GetNamespaceTypes(module.GlobalNamespace));
        }

        private IEnumerable<INamedTypeSymbol> GetValidTypes(string name)
        {
            return compilation.SourceModule.ReferencedAssemblySymbols.Select(module =>
            {
                try { return module.GetTypeByMetadataName(name); }
                catch { return null; }
            }).Where(t => t != null);
        }

        #region IReflectionProvider
        public IEnumerable<TypeSpecifier> GetNonStaticTypes()
        {
            return GetValidTypes().Where(
                    t => t.IsPublic() && !(t.IsAbstract && t.IsSealed))
                .OrderBy(t => t.ContainingNamespace?.Name)
                .ThenBy(t => t.Name)
                .Select(t => ReflectionConverter.TypeSpecifierFromSymbol(t));
        }

        public IEnumerable<MethodSpecifier> GetPublicMethodsForType(TypeSpecifier typeSpecifier)
        {
            ITypeSymbol type = GetTypeFromSpecifier(typeSpecifier);

            if (type != null)
            {
                // Get all public instance methods, ignore special ones (properties / events)

                return type.GetMethods()
                    .Where(m => 
                        m.IsPublic() &&
                        !m.IsStatic &&
                        m.MethodKind == MethodKind.Ordinary || m.MethodKind == MethodKind.BuiltinOperator || m.MethodKind == MethodKind.UserDefinedOperator)
                    .OrderBy(m => m.ContainingNamespace?.Name)
                    .ThenBy(m => m.ContainingType?.Name)
                    .ThenBy(m => m.Name)
                    .Select(m => ReflectionConverter.MethodSpecifierFromSymbol(m));
            }
            else
            {
                return new MethodSpecifier[] { };
            }
        }

        public IEnumerable<MethodSpecifier> GetPublicMethodOverloads(MethodSpecifier methodSpecifier)
        {
            ITypeSymbol type = GetTypeFromSpecifier(methodSpecifier.DeclaringType);

            if (type != null)
            {
                return type.GetMethods()
                        .Where(m =>
                            m.Name == methodSpecifier.Name &&
                            m.IsPublic() &&
                            m.IsStatic == methodSpecifier.Modifiers.HasFlag(MethodModifiers.Static) &&
                            m.MethodKind == MethodKind.Ordinary || m.MethodKind == MethodKind.BuiltinOperator || m.MethodKind == MethodKind.UserDefinedOperator)
                        .OrderBy(m => m.ContainingNamespace?.Name)
                        .ThenBy(m => m.ContainingType?.Name)
                        .ThenBy(m => m.Name)
                        .Select(m => ReflectionConverter.MethodSpecifierFromSymbol(m));
            }
            else
            {
                return new MethodSpecifier[0];
            }
        }

        public IEnumerable<MethodSpecifier> GetPublicStaticFunctionsForType(TypeSpecifier typeSpecifier)
        {
            ITypeSymbol type = GetTypeFromSpecifier(typeSpecifier);

            if (type != null)
            {
                // Get all public static methods, ignore special ones (properties / events),
                // ignore those with generic parameters since we cant set those yet

                return type.GetMethods()
                    .Where(m => 
                        m.IsPublic() &&
                        !m.IsStatic &&
                        m.MethodKind == MethodKind.Ordinary || m.MethodKind == MethodKind.BuiltinOperator || m.MethodKind == MethodKind.UserDefinedOperator)
                    .OrderBy(m => m.ContainingNamespace?.Name)
                    .ThenBy(m => m.ContainingType?.Name)
                    .ThenBy(m => m.Name)
                    .Select(m => ReflectionConverter.MethodSpecifierFromSymbol(m));
            }
            else
            {
                return new MethodSpecifier[] { };
            }
        }

        public IEnumerable<PropertySpecifier> GetPublicPropertiesForType(TypeSpecifier typeSpecifier)
        {
            var members = GetTypeFromSpecifier(typeSpecifier)
                .GetAllMembers();

            var properties = members
                .Where(m => m.Kind == SymbolKind.Property)
                .Cast<IPropertySymbol>()
                .OrderBy(p => p.ContainingNamespace?.Name)
                .ThenBy(p => p.ContainingType?.Name)
                .ThenBy(p => p.Name)
                .Select(p => ReflectionConverter.PropertySpecifierFromSymbol(p));

            // TODO: Move variables to seperate function / unify properties and variables in a better way.
            return properties.Concat(members
                .Where(m => m.Kind == SymbolKind.Field)
                .Cast<IFieldSymbol>()
                .OrderBy(f => f.ContainingNamespace?.Name)
                .ThenBy(f => f.ContainingType?.Name)
                .ThenBy(f => f.Name)
                .Select(f => ReflectionConverter.PropertySpecifierFromField(f))
            );
        }

        public IEnumerable<MethodSpecifier> GetStaticFunctions()
        {
            return GetValidTypes()
                    .Where(t => 
                        t.IsPublic() &&
                        !t.IsGenericType)
                    .SelectMany(t =>
                        t.GetMethods()
                        .Where(m => 
                            m.IsStatic && m.IsPublic() &&
                            !m.ContainingType.IsUnboundGenericType)
                        .OrderBy(m => m.ContainingNamespace?.Name)
                        .ThenBy(m => m.ContainingType?.Name)
                        .ThenBy(m => m.Name)
                        .Select(m => ReflectionConverter.MethodSpecifierFromSymbol(m)));
        }

        public IEnumerable<PropertySpecifier> GetPublicStaticProperties()
        {
            return GetValidTypes()
                    .Where(t =>
                        t.IsPublic() &&
                        !t.IsGenericType)
                    .SelectMany(t =>
                        t.GetMembers()
                            .Where(m => m.Kind == SymbolKind.Property)
                            .Cast<IPropertySymbol>()
                            .Where(p => p.IsStatic && p.IsPublic() && !p.IsAbstract)
                            .OrderBy(p => p.ContainingNamespace?.Name)
                            .ThenBy(p => p.ContainingType?.Name)
                            .ThenBy(p => p.Name)
                            .Select(p => ReflectionConverter.PropertySpecifierFromSymbol(p)));
        }
        
        public IEnumerable<ConstructorSpecifier> GetConstructors(TypeSpecifier typeSpecifier)
        {
            return GetTypeFromSpecifier<INamedTypeSymbol>(typeSpecifier)?.Constructors.Select(c => ReflectionConverter.ConstructorSpecifierFromSymbol(c));
        }

        public IEnumerable<string> GetEnumNames(TypeSpecifier typeSpecifier)
        {
            return GetTypeFromSpecifier(typeSpecifier).GetAllMembers()
                .Where(member => member.Kind == SymbolKind.Field)
                .Select(member => member.Name);
        }
        
        public bool TypeSpecifierIsSubclassOf(TypeSpecifier a, TypeSpecifier b)
        {
            ITypeSymbol typeA = GetTypeFromSpecifier(a);
            ITypeSymbol typeB = GetTypeFromSpecifier(b);

            return typeA != null && typeB != null && typeA.IsSubclassOf(typeB);
        }

        private T GetTypeFromSpecifier<T>(TypeSpecifier specifier)
        {
            return (T)GetTypeFromSpecifier(specifier);
        }

        private ITypeSymbol GetTypeFromSpecifier(TypeSpecifier specifier)
        {
            string lookupName = specifier.Name;

            // Find array ranks and remove them from the lookup name.
            // Example: int[][,] -> arrayRanks: { 1, 2 }, lookupName: int
            Stack<int> arrayRanks = new Stack<int>();
            while (lookupName.EndsWith("]"))
            {
                lookupName = lookupName.Remove(lookupName.Length - 1);
                int arrayRank = 1;
                while (lookupName.EndsWith(","))
                {
                    arrayRank++;
                    lookupName = lookupName.Remove(lookupName.Length - 1);
                }
                arrayRanks.Push(arrayRank);

                if (lookupName.Last() != '[')
                {
                    throw new Exception("Expected [ in lookupName");
                }

                lookupName = lookupName.Remove(lookupName.Length - 1);
            }

            if (specifier.GenericArguments.Count > 0)
                lookupName += $"`{specifier.GenericArguments.Count}";

            IEnumerable<INamedTypeSymbol> types = GetValidTypes(lookupName);

            ITypeSymbol foundType = null;

            foreach (INamedTypeSymbol t in types)
            {
                if (t != null)
                {
                    if (specifier.GenericArguments.Count > 0)
                    {
                        var typeArguments = specifier.GenericArguments
                            .Select(baseType => baseType is TypeSpecifier typeSpec ?
                                GetTypeFromSpecifier(typeSpec) :
                                t.TypeArguments[specifier.GenericArguments.IndexOf(baseType)])
                            .ToArray();
                        foundType = t.Construct(typeArguments);
                    }
                    else
                    {
                        foundType = t;
                    }

                    break;
                }
            }

            if (foundType != null)
            {
                // Make array
                while (arrayRanks.TryPop(out int arrayRank))
                {
                    foundType = compilation.CreateArrayTypeSymbol(foundType, arrayRank);
                }
            }

            return foundType;
        }

        private IMethodSymbol GetMethodInfoFromSpecifier(MethodSpecifier specifier)
        {
            INamedTypeSymbol declaringType = GetTypeFromSpecifier<INamedTypeSymbol>(specifier.DeclaringType);
            return declaringType?.GetMethods().Where(
                    m => m.Name == specifier.Name && 
                    m.Parameters.Select(p => ReflectionConverter.BaseTypeSpecifierFromSymbol(p.Type)).SequenceEqual(specifier.ArgumentTypes))
                .FirstOrDefault();
        }

        public IEnumerable<MethodSpecifier> GetStaticFunctionsWithReturnType(TypeSpecifier searchTypeSpec)
        {
            // Find all public static methods

            IEnumerable<IMethodSymbol> availableMethods = GetValidTypes()
                        .Where(t => t.IsPublic())
                        .SelectMany(t =>
                            t.GetMethods()
                            .Where(m => m.IsPublic() && m.IsStatic)
                            .Where(m => !m.ContainingType.IsUnboundGenericType))
                        .OrderBy(m => m.ContainingNamespace?.Name)
                        .ThenBy(m => m.ContainingType?.Name)
                        .ThenBy(m => m.Name);

            ITypeSymbol searchType = GetTypeFromSpecifier(searchTypeSpec);

            List<MethodSpecifier> foundMethods = new List<MethodSpecifier>();

            // Find compatible methods

            foreach (IMethodSymbol availableMethod in availableMethods)
            {
                // Check the return type whether it can be replaced by the wanted type
                // or if the return type is one of the type parameters.

                ITypeSymbol retType = availableMethod.ReturnType;
                BaseType ret = ReflectionConverter.BaseTypeSpecifierFromSymbol(retType);

                if (ret == searchTypeSpec || retType.IsSubclassOf(searchType) || retType.TypeKind == TypeKind.TypeParameter)
                {
                    // Find method and add it
                    MethodSpecifier foundMethod = ReflectionConverter.MethodSpecifierFromSymbol(availableMethod);

                    if (foundMethod != null)
                    {
                        foundMethods.Add(foundMethod);
                    }
                }
            }

            return foundMethods;
        }

        public IEnumerable<MethodSpecifier> GetStaticFunctionsWithArgumentType(TypeSpecifier searchTypeSpec)
        {
            // Find all public static methods

            IEnumerable<IMethodSymbol> availableMethods = GetValidTypes()
                        .Where(t => t.IsPublic())
                        .SelectMany(t =>
                            t.GetMethods()
                            .Where(m => m.IsPublic() && m.IsStatic && !m.ContainingType.IsUnboundGenericType))
                        .OrderBy(m => m?.ContainingNamespace.Name)
                        .ThenBy(m => m?.ContainingType.Name)
                        .ThenBy(m => m.Name);

            ITypeSymbol searchType = GetTypeFromSpecifier(searchTypeSpec);

            List<MethodSpecifier> foundMethods = new List<MethodSpecifier>();

            // Find compatible methods

            foreach (IMethodSymbol availableMethod in availableMethods)
            {
                MethodSpecifier availableMethodSpec = ReflectionConverter.MethodSpecifierFromSymbol(availableMethod);

                // Check each argument whether it can be replaced by the wanted type
                // or if the argument type is one of the type parameters.

                for (int i = 0; i < availableMethodSpec.Arguments.Count; i++) 
                {
                    ITypeSymbol argType = availableMethod.Parameters[i].Type;
                    BaseType arg = ReflectionConverter.BaseTypeSpecifierFromSymbol(argType);

                    if (arg == searchTypeSpec || searchType.IsSubclassOf(argType) || argType.TypeKind == TypeKind.TypeParameter)
                    {
                        // Find method and add it
                        MethodSpecifier foundMethod = ReflectionConverter.MethodSpecifierFromSymbol(availableMethod);

                        if (foundMethod != null && !foundMethods.Contains(foundMethod))
                        {
                            foundMethods.Add(foundMethod);
                        }
                    }
                }
            }

            return foundMethods;
        }

        // Documentation

        public string GetMethodDocumentation(MethodSpecifier methodSpecifier)
        {
            IMethodSymbol methodInfo = GetMethodInfoFromSpecifier(methodSpecifier);

            if(methodInfo == null)
            {
                return null;
            }

            return documentationUtil.GetMethodSummary(methodInfo);
        }

        public string GetMethodParameterDocumentation(MethodSpecifier methodSpecifier, int parameterIndex)
        {
            IMethodSymbol methodInfo = GetMethodInfoFromSpecifier(methodSpecifier);

            if (methodInfo == null)
            {
                return null;
            }

            return documentationUtil.GetMethodParameterInfo(methodInfo.Parameters[parameterIndex]);
        }

        public string GetMethodReturnDocumentation(MethodSpecifier methodSpecifier, int returnIndex)
        {
            IMethodSymbol methodInfo = GetMethodInfoFromSpecifier(methodSpecifier);

            if (methodInfo == null)
            {
                return null;
            }

            return documentationUtil.GetMethodReturnInfo(methodInfo);
        }

        public bool HasImplicitCast(TypeSpecifier fromType, TypeSpecifier toType)
        {
            // Check if there exists a conversion that is implicit between the types.

            ITypeSymbol fromSymbol = GetTypeFromSpecifier(fromType);
            ITypeSymbol toSymbol = GetTypeFromSpecifier(toType);

            return compilation.ClassifyConversion(fromSymbol, toSymbol).IsImplicit;
        }

        #endregion
    }
}
