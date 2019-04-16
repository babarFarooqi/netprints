﻿using NetPrints.Core;
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

        private const string CLASS_TEMPLATE_NO_NAMESPACE =
            @"%ClassModifiers%class %ClassName%%GenericArguments% : %SuperType%
            {
                %Content%
            }";

        private const string VARIABLE_TEMPLATE = "%VariableModifiers%%VariableType% %VariableName%;";

        private const string PROPERTY_TEMPLATE = @"%VariableModifiers%%VariableType% %VariableName%
            {
                %Get%
                %Set%
            }";

        private readonly MethodTranslator methodTranslator = new MethodTranslator();

        /// <summary>
        /// Translates a class into C#.
        /// </summary>
        /// <param name="c">Class to translate.</param>
        /// <returns>C# code for the class.</returns>
        public string TranslateClass(Class c)
        {
            StringBuilder content = new StringBuilder();

            foreach (Variable v in c.Variables)
            {
                content.AppendLine(TranslateVariable(v));
            }

            foreach (Method constructor in c.Constructors)
            {
                content.AppendLine(TranslateConstructor(constructor));
            }

            foreach (Method m in c.Methods)
            {
                content.AppendLine(TranslateMethod(m));
            }

            StringBuilder modifiers = new StringBuilder();

            modifiers.Append($"{TranslatorUtil.VisibilityTokens[c.Visibility]} ");

            if (c.Modifiers.HasFlag(ClassModifiers.Static))
            {
                modifiers.Append("static ");
            }

            if (c.Modifiers.HasFlag(ClassModifiers.Abstract))
            {
                modifiers.Append("abstract ");
            }

            if (c.Modifiers.HasFlag(ClassModifiers.Sealed))
            {
                modifiers.Append("sealed ");
            }

            if (c.Modifiers.HasFlag(ClassModifiers.Partial))
            {
                modifiers.Append("partial ");
            }

            string genericArguments = "";
            if (c.DeclaredGenericArguments.Count > 0)
            {
                genericArguments = "<" + string.Join(", ", c.DeclaredGenericArguments) + ">";
            }

            string generatedCode = (string.IsNullOrWhiteSpace(c.Namespace) ? CLASS_TEMPLATE_NO_NAMESPACE : CLASS_TEMPLATE)
                .Replace("%Namespace%", c.Namespace)
                .Replace("%ClassModifiers%", modifiers.ToString())
                .Replace("%ClassName%", c.Name)
                .Replace("%GenericArguments%", genericArguments)
                .Replace("%SuperType%", c.SuperType.FullCodeName)
                .Replace("%Content%", content.ToString());

            return TranslatorUtil.FormatCode(generatedCode);
        }

        /// <summary>
        /// Translates a variable into C#.
        /// </summary>
        /// <param name="variable">Variable to translate.</param>
        /// <returns>C# code for the variable.</returns>
        public string TranslateVariable(Variable variable)
        {
            StringBuilder modifiers = new StringBuilder();

            modifiers.Append($"{TranslatorUtil.VisibilityTokens[variable.Visibility]} ");

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

            if (variable.HasAccessors)
            {
                // Translate get / set methods

                string output = PROPERTY_TEMPLATE
                    .Replace("%VariableModifiers%", modifiers.ToString())
                    .Replace("%VariableType%", variable.Type.FullCodeName)
                    .Replace("%VariableName%", variable.Name);

                if (variable.GetterMethod != null)
                {
                    string getterMethodCode = methodTranslator.Translate(variable.GetterMethod, false, false);
                    string visibilityPrefix = variable.GetterMethod.Visibility != variable.Visibility ? $"{TranslatorUtil.VisibilityTokens[variable.GetterMethod.Visibility]} " : "";

                    output = output.Replace("%Get%", $"{visibilityPrefix}get\n{getterMethodCode}");
                }
                else
                {
                    output = output.Replace("%Get%", "");
                }

                if (variable.SetterMethod != null)
                {
                    string setterMethodCode = methodTranslator.Translate(variable.SetterMethod, false, false);
                    string visibilityPrefix = variable.SetterMethod.Visibility != variable.Visibility ? $"{TranslatorUtil.VisibilityTokens[variable.SetterMethod.Visibility]} " : "";

                    output = output.Replace("%Set%", $"{visibilityPrefix}set\n{setterMethodCode}");
                }
                else
                {
                    output = output.Replace("%Set%", "");
                }

                return output;
            }
            else
            {
                return VARIABLE_TEMPLATE
                    .Replace("%VariableModifiers%", modifiers.ToString())
                    .Replace("%VariableType%", variable.Type.FullCodeName)
                    .Replace("%VariableName%", variable.Name);
            }
        }

        /// <summary>
        /// Translates a method to C#.
        /// </summary>
        /// <param name="m">Method to translate.</param>
        /// <returns>C# code for the method.</returns>
        public string TranslateMethod(Method m)
        {
            return methodTranslator.Translate(m, true, false);
        }

        public string TranslateConstructor(Method m)
        {
            return methodTranslator.Translate(m, true, true);
        }
    }
}
