﻿using NetPrints.Core;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing a reroute node. Does nothing by itself.
    /// Used for layouting in the editor.
    /// </summary>
    [DataContract]
    public class RerouteNode : Node
    {
        public int ExecRerouteCount { get => InputExecPins.Count; }
        public int DataRerouteCount { get => InputDataPins.Count; }
        public int TypeRerouteCount { get => InputTypePins.Count; }

        private RerouteNode(NodeGraph graph)
            : base(graph)
        {
        }

        public static RerouteNode MakeExecution(NodeGraph graph, int numExecs)
        {
            var node = new RerouteNode(graph);

            for (int i = 0; i < numExecs; i++)
            {
                node.AddInputExecPin($"Exec{i}");
                node.AddOutputExecPin($"Exec{i}");
            }

            return node;
        }

        public static RerouteNode MakeData(NodeGraph graph, IEnumerable<Tuple<BaseType, BaseType>> dataTypes)
        {
            if (dataTypes is null)
            {
                throw new ArgumentException("dataTypes was null in RerouteNode.MakeData.");
            }

            var node = new RerouteNode(graph);

            int index = 0;
            foreach (var dataType in dataTypes)
            {
                node.AddInputDataPin($"Data{index}", dataType.Item1);
                node.AddOutputDataPin($"Data{index}", dataType.Item2);
                index++;
            }

            return node;
        }

        public static RerouteNode MakeType(NodeGraph graph, int numTypes)
        {
            var node = new RerouteNode(graph);

            for (int i = 0; i < numTypes; i++)
            {
                node.AddInputTypePin($"Type{i}");
                node.AddOutputTypePin($"Type{i}", new ObservableValue<BaseType>(null));
            }

            return node;
        }

        private void UpdateOutputType()
        {
            if (OutputTypePins.Count > 0)
            {
                OutputTypePins[0].InferredType.Value = InputTypePins[0].InferredType?.Value;
            }
        }

        protected override void OnInputTypeChanged(object sender, EventArgs eventArgs)
        {
            base.OnInputTypeChanged(sender, eventArgs);
            UpdateOutputType();
        }

        public override string ToString()
        {
            return "Reroute";
        }
    }
}
