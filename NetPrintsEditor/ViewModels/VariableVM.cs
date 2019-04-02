﻿using NetPrints.Core;
using NetPrints.Graph;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetPrintsEditor.ViewModels
{
    public class VariableVM : INotifyPropertyChanged
    {
        public TypeSpecifier Type
        {
            get => variable.Type;
            set
            {
                if (variable.Type != value)
                {
                    variable.Type = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Name
        {
            get => variable.Name;
            set
            {
                if (variable.Name != value)
                {
                    variable.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public VariableModifiers Modifiers
        {
            get => variable.Modifiers;
            set
            {
                if (variable.Modifiers != value)
                {
                    variable.Modifiers = value;
                    OnPropertyChanged();
                }
            }
        }

        public MemberVisibility Visibility
        {
            get => variable.Visibility;
            set
            {
                if (variable.Visibility != value)
                {
                    variable.Visibility = value;
                    OnPropertyChanged();
                }
            }
        }

        public VariableSpecifier Specifier
        {
            get => variable.Specifier;
        }

        public bool HasGetter
        {
            get => variable.GetterMethod != null;
        }

        public bool HasSetter
        {
            get => variable.SetterMethod != null;
        }

        public MethodVM GetterMethod
        {
            get;
            private set;
        }

        public MethodVM SetterMethod
        {
            get;
            private set;
        }

        public string VisibilityName
        {
            get => Enum.GetName(typeof(MemberVisibility), Visibility);
            set => Visibility = Enum.Parse<MemberVisibility>(value);
        }

        public IEnumerable<MemberVisibility> PossibleVisibilities
        {
            get => new[]
                {
                    MemberVisibility.Internal,
                    MemberVisibility.Private,
                    MemberVisibility.Protected,
                    MemberVisibility.Public,
                };
        }

        public Variable Variable
        {
            get => variable;
            set
            {
                if (variable != value)
                {
                    variable = value;
                    OnPropertyChanged();
                }
            }
        }

        private Variable variable;

        public VariableVM(Variable variable)
        {
            Variable = variable;
            GetterMethod = variable.GetterMethod != null ? new MethodVM(variable.GetterMethod) : null;
            SetterMethod = variable.SetterMethod != null ? new MethodVM(variable.SetterMethod) : null;
        }

        public void AddGetter()
        {
            var method = new Method($"get_{Name}")
            {
                Class = variable.Class,
            };

            // Create return input pin with correct type
            // TODO: Make sure we can't delete type pins.
            TypeNode returnTypeNode = GraphUtil.CreateNestedTypeNode(method, Type, method.MainReturnNode.PositionX, method.MainReturnNode.PositionY);
            method.MainReturnNode.AddReturnType();
            GraphUtil.ConnectTypePins(returnTypeNode.OutputTypePins[0], method.MainReturnNode.InputTypePins[0]);

            GetterMethod = new MethodVM(method);
            variable.GetterMethod = method;
            OnPropertyChanged(nameof(HasGetter));
            OnPropertyChanged(nameof(GetterMethod));
        }

        public void RemoveGetter()
        {
            GetterMethod = null;
            variable.GetterMethod = null;
            OnPropertyChanged(nameof(HasGetter));
            OnPropertyChanged(nameof(GetterMethod));
        }

        public void AddSetter()
        {
            var method = new Method($"set_{Name}")
            {
                Class = variable.Class
            };

            // Create argument output pin with correct type
            // TODO: Make sure we can't delete type pins.
            TypeNode argTypeNode = GraphUtil.CreateNestedTypeNode(method, Type, method.EntryNode.PositionX, method.EntryNode.PositionY);
            method.EntryNode.AddArgument();
            GraphUtil.ConnectTypePins(argTypeNode.OutputTypePins[0], method.EntryNode.InputTypePins[0]);

            SetterMethod = new MethodVM(method);
            variable.SetterMethod = method;
            OnPropertyChanged(nameof(HasSetter));
            OnPropertyChanged(nameof(SetterMethod));
        }

        public void RemoveSetter()
        {
            SetterMethod = null;
            variable.SetterMethod = null;
            OnPropertyChanged(nameof(HasSetter));
            OnPropertyChanged(nameof(SetterMethod));
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
