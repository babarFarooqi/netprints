﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    [Flags]
    public enum ClassModifiers
    {
        Private = 0,
        Public = 1,
        Protected = 2,
        Internal = 4,
        Sealed = 8,
        Abstract = 16,
        Static = 32,
    }

    [DataContract]
    public class Class
    {
        [DataMember]
        public ObservableRangeCollection<Variable> Attributes { get; set; } = new ObservableRangeCollection<Variable>();

        [DataMember]
        public ObservableRangeCollection<Method> Methods { get; set; } = new ObservableRangeCollection<Method>();

        [DataMember]
        public TypeSpecifier SuperType { get; set; } = TypeSpecifier.FromType<object>();

        [DataMember]
        public string Namespace { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public ClassModifiers Modifiers { get; set; } = ClassModifiers.Internal;

        [DataMember]
        public ObservableRangeCollection<GenericType> DeclaredGenericArguments { get; set; } = new ObservableRangeCollection<GenericType>();

        public TypeSpecifier Type
        {
            get => new TypeSpecifier($"{Namespace}.{Name}", SuperType.IsEnum, SuperType.IsInterface,
                DeclaredGenericArguments.Cast<BaseType>().ToList());
        }

        public Class()
        {

        }
    }
}
