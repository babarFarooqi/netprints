﻿using Microsoft.CodeAnalysis;
using NetPrints.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NetPrintsEditor.Reflection
{
    /// <summary>
    /// Helper class for converting from Roslyn symbols to NetPrints specifiers.
    /// </summary>
    public static class ReflectionConverter
    {
        private static readonly Dictionary<Microsoft.CodeAnalysis.Accessibility, MemberVisibility> roslynToNetprintsVisibility = new Dictionary<Microsoft.CodeAnalysis.Accessibility, MemberVisibility>()
        {
            [Microsoft.CodeAnalysis.Accessibility.Private] = MemberVisibility.Private,
            [Microsoft.CodeAnalysis.Accessibility.Protected] = MemberVisibility.Protected,
            [Microsoft.CodeAnalysis.Accessibility.Public] = MemberVisibility.Public,
            [Microsoft.CodeAnalysis.Accessibility.Internal] = MemberVisibility.Internal,
        };

        public static TypeSpecifier TypeSpecifierFromSymbol(ITypeSymbol type)
        {
            string typeName;

            if (type is IArrayTypeSymbol arrayType)
            {
                // TODO: Get more interesting type?
                typeName = typeof(Array).FullName;
            }
            else
            {
                // Get the nested name (represented by + between classes)
                // See https://stackoverflow.com/questions/2443244/having-a-in-the-class-name
                string nestedPrefix = "";
                ITypeSymbol containingType = type.ContainingType;
                while (containingType != null)
                {
                    nestedPrefix = $"{containingType.Name}+{nestedPrefix}";
                    containingType = containingType.ContainingType;
                }

                typeName = nestedPrefix + type.Name.Split('`').First();
                if (type.ContainingNamespace != null)
                {
                    typeName = type.ContainingNamespace + "." + typeName;
                }
            }

            TypeSpecifier typeSpecifier = new TypeSpecifier(typeName,
                    type.TypeKind == TypeKind.Enum,
                    type.TypeKind == TypeKind.Interface);
            
            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.IsUnboundGenericType)
                {
                    throw new ArgumentException(nameof(type));
                }

                foreach (ITypeSymbol genType in namedType.TypeArguments)
                {
                    if (genType is ITypeParameterSymbol genTypeParam)
                    {
                        // TODO: Convert and add constraints
                        typeSpecifier.GenericArguments.Add(GenericTypeSpecifierFromSymbol(genTypeParam));
                    }
                    else
                    {
                        typeSpecifier.GenericArguments.Add(TypeSpecifierFromSymbol(genType));
                    }
                }
            }
            
            return typeSpecifier;
        }

        public static GenericType GenericTypeSpecifierFromSymbol(ITypeParameterSymbol type)
        {
            // TODO: Convert constraints
            GenericType genericType = new GenericType(type.Name);

            return genericType;
        }

        public static BaseType BaseTypeSpecifierFromSymbol(ITypeSymbol type)
        {
            if (type is ITypeParameterSymbol typeParam)
            {
                return GenericTypeSpecifierFromSymbol(typeParam);
            }
            else
            {
                return TypeSpecifierFromSymbol(type);
            }
        }

        public static Named<BaseType> NamedBaseTypeSpecifierFromSymbol(IParameterSymbol paramSymbol)
        {
            return new Named<BaseType>(paramSymbol.Name, BaseTypeSpecifierFromSymbol(paramSymbol.Type));
        }

        private static readonly Dictionary<RefKind, MethodParameterPassType> refKindToPassType = new Dictionary<RefKind, MethodParameterPassType>()
        {
            [RefKind.None] = MethodParameterPassType.Default,
            [RefKind.Ref] = MethodParameterPassType.Reference,
            [RefKind.Out] = MethodParameterPassType.Out,
            [RefKind.In] = MethodParameterPassType.In,
        };

        public static MethodParameter MethodParameterFromSymbol(in IParameterSymbol paramSymbol)
        {
            return new MethodParameter(paramSymbol.Name, BaseTypeSpecifierFromSymbol(paramSymbol.Type), refKindToPassType[paramSymbol.RefKind]);
        }

        public static MethodSpecifier MethodSpecifierFromSymbol(IMethodSymbol method)
        {
            MemberVisibility visibility = roslynToNetprintsVisibility[method.DeclaredAccessibility];

            var modifiers = MethodModifiers.None;

            if (method.IsVirtual)
            {
                modifiers |= MethodModifiers.Virtual;
            }

            if (method.IsSealed)
            {
                modifiers |= MethodModifiers.Sealed;
            }

            if (method.IsAbstract)
            {
                modifiers |= MethodModifiers.Abstract;
            }

            if (method.IsStatic)
            {
                modifiers |= MethodModifiers.Static;
            }

            if (method.IsOverride)
            {
                modifiers |= MethodModifiers.Override;
            }

            // TODO: Protected / Internal

            BaseType[] returnTypes = method.ReturnsVoid ?
                new BaseType[] { } :
                new BaseType[] { BaseTypeSpecifierFromSymbol(method.ReturnType) };

            MethodParameter[] parameters = method.Parameters.Select(
                p => MethodParameterFromSymbol(p)).ToArray();

            BaseType[] genericArgs = method.TypeParameters.Select(
                p => BaseTypeSpecifierFromSymbol(p)).ToArray();

            return new MethodSpecifier(
                method.Name,
                parameters,
                returnTypes,
                modifiers,
                visibility,
                TypeSpecifierFromSymbol(method.ContainingType),
                genericArgs);
        }

        public static PropertySpecifier PropertySpecifierFromSymbol(IPropertySymbol property)
        {
            bool hasPublicGetter = property.GetMethod != null ? property.GetMethod.IsPublic() : false;
            bool hasPublicSetter = property.SetMethod != null ? property.SetMethod.IsPublic() : false;

            return new PropertySpecifier(
                property.Name,
                TypeSpecifierFromSymbol(property.Type),
                hasPublicGetter,
                hasPublicSetter,
                TypeSpecifierFromSymbol(property.ContainingType),
                property.IsStatic);
        }

        public static PropertySpecifier PropertySpecifierFromField(IFieldSymbol field)
        {
            // TODO: Create own specifier for fields / unify with properties

            return new PropertySpecifier(
                field.Name,
                TypeSpecifierFromSymbol(field.Type),
                field.IsPublic(),
                field.IsPublic() ? !field.IsReadOnly : false,
                TypeSpecifierFromSymbol(field.ContainingType),
                field.IsStatic
            );
        }

        public static ConstructorSpecifier ConstructorSpecifierFromSymbol(IMethodSymbol constructorMethodSymbol)
        {
            return new ConstructorSpecifier(
                constructorMethodSymbol.Parameters.Select(p => NamedBaseTypeSpecifierFromSymbol(p)),
                TypeSpecifierFromSymbol(constructorMethodSymbol.ContainingType));
        }
    }
}
