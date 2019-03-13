﻿using NetPrints.Core;
using System.Linq;
using System.Text;

namespace NetPrints.Translator
{
    /// <summary>
    /// Class for translating a class into C#.
    /// </summary>
    public class ClassTranslator
    {
        private const string CLASS_TEMPLATE =
            @"namespace %Namespace%
            {
                %ClassModifiers%class %ClassName%%GenericArguments% : %SuperType%
                {
                    %Content%
                }
            }";

        private const string VARIABLE_TEMPLATE = "%VariableModifiers%%VariableType% %VariableName%;";

        private MethodTranslator methodTranslator = new MethodTranslator();
        
        public ClassTranslator()
        {

        }

        /// <summary>
        /// Translates a class into C#.
        /// </summary>
        /// <param name="c">Class to translate.</param>
        /// <returns>C# code for the class.</returns>
        public string TranslateClass(Class c)
        {
            StringBuilder content = new StringBuilder();

            foreach (Variable v in c.Attributes)
            {
                content.AppendLine(TranslateVariable(v));
            }

            foreach(Method m in c.Methods)
            {
                content.AppendLine(TranslateMethod(m));
            }

            StringBuilder modifiers = new StringBuilder();

            if (c.Modifiers.HasFlag(ClassModifiers.Public))
            {
                modifiers.Append("public ");
            }

            if (c.Modifiers.HasFlag(ClassModifiers.Static))
            {
                modifiers.Append("static ");
            }

            if(c.Modifiers.HasFlag(ClassModifiers.Abstract))
            {
                modifiers.Append("abstract ");
            }

            if (c.Modifiers.HasFlag(ClassModifiers.Sealed))
            {
                modifiers.Append("sealed ");
            }

            string genericArguments = "";
            if(c.DeclaredGenericArguments.Count > 0)
            {
                genericArguments = "<" + string.Join(", ", c.DeclaredGenericArguments) + ">";
            }

            return CLASS_TEMPLATE
                .Replace("%Namespace%", c.Namespace)
                .Replace("%ClassModifiers%", modifiers.ToString())
                .Replace("%ClassName%", c.Name)
                .Replace("%GenericArguments%", genericArguments)
                .Replace("%SuperType%", c.SuperType.FullCodeName)
                .Replace("%Content%", content.ToString());
        }

        /// <summary>
        /// Translates a variable into C#.
        /// </summary>
        /// <param name="variable">Variable to translate.</param>
        /// <returns>C# code for the variable.</returns>
        public string TranslateVariable(Variable variable)
        {
            StringBuilder modifiers = new StringBuilder();
            
            if (variable.Modifiers.HasFlag(VariableModifiers.Protected))
            {
                modifiers.Append("protected ");
            }
            else if (variable.Modifiers.HasFlag(VariableModifiers.Public))
            {
                modifiers.Append("public ");
            }
            else if (variable.Modifiers.HasFlag(VariableModifiers.Internal))
            {
                modifiers.Append("internal ");
            }

            if (variable.Modifiers.HasFlag(VariableModifiers.Static))
            {
                modifiers.Append("static ");
            }

            if (variable.Modifiers.HasFlag(VariableModifiers.ReadOnly))
            {
                modifiers.Append("readonly ");
            }

            if (variable.Modifiers.HasFlag(VariableModifiers.New))
            {
                modifiers.Append("new ");
            }

            if (variable.Modifiers.HasFlag(VariableModifiers.Const))
            {
                modifiers.Append("const ");
            }

            return VARIABLE_TEMPLATE
                .Replace("%VariableModifiers%", modifiers.ToString())
                .Replace("%VariableType%", variable.VariableType.FullCodeName)
                .Replace("%VariableName%", variable.Name);
        }

        /// <summary>
        /// Translates a method to C#.
        /// </summary>
        /// <param name="m">Method to translate.</param>
        /// <returns>C# code for the method.</returns>
        public string TranslateMethod(Method m)
        {
            return methodTranslator.Translate(m);
        }
    }
}
