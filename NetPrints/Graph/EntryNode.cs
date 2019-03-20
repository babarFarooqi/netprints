﻿using NetPrints.Core;
using System;
using System.Runtime.Serialization;
using System.Linq;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing the initial execution node of a method.
    /// </summary>
    [DataContract]
    public class EntryNode : Node
    {
        /// <summary>
        /// Output execution pin that initially executes when a method gets called.
        /// </summary>
        public NodeOutputExecPin InitialExecutionPin
        {
            get { return OutputExecPins[0]; }
        }

        public EntryNode(Method method)
            : base(method)
        {
            AddOutputExecPin("Exec");
        }

        protected override void OnInputTypeChanged(object sender, EventArgs eventArgs)
        {
            base.OnInputTypeChanged(sender, eventArgs);

            for (int i = 0; i < InputTypePins.Count; i++)
            {
                OutputDataPins[i].PinType.Value = InputTypePins[i].InferredType?.Value ?? TypeSpecifier.FromType<object>();
            }
        }

        public override string ToString()
        {
            return $"{Method.Name} Entry";
        }

        public void AddArgument()
        {
            int argIndex = OutputDataPins.Count;
            AddOutputDataPin($"Input{argIndex}", new ObservableValue<BaseType>(TypeSpecifier.FromType<object>()));
            AddInputTypePin($"Input{argIndex}Type");
        }

        public void RemoveArgument()
        {
            if (OutputDataPins.Count > 0)
            {
                NodeOutputDataPin odpToRemove = OutputDataPins.Last();
                NodeInputTypePin itpToRemove = InputTypePins.Last();

                GraphUtil.DisconnectOutputDataPin(odpToRemove);
                GraphUtil.DisconnectInputTypePin(itpToRemove);

                OutputDataPins.Remove(odpToRemove);
                InputTypePins.Remove(itpToRemove);
            }
        }
    }
}
