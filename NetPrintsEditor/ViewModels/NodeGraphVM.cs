﻿using GalaSoft.MvvmLight;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrintsEditor.Controls;
using NetPrintsEditor.Messages;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace NetPrintsEditor.ViewModels
{
    public class NodeGraphVM : ViewModelBase
    {
        public IEnumerable<NodeVM> SelectedNodes
        {
            get => selectedNodes;
            private set
            {
                if (selectedNodes != value)
                {
                    // Deselect old nodes
                    if (selectedNodes != null)
                    {
                        foreach (var node in selectedNodes)
                        {
                            node.IsSelected = false;
                        }
                    }

                    selectedNodes = value;

                    // Select new nodes
                    if (selectedNodes != null)
                    {
                        foreach (var node in selectedNodes)
                        {
                            node.IsSelected = true;
                        }
                    }
                }
            }
        }

        private IEnumerable<NodeVM> selectedNodes = new NodeVM[0];

        public string Name
        {
            get => graph is MethodGraph methodGraph ? methodGraph.Name : graph.ToString();
            set
            {
                if (graph is MethodGraph methodGraph && methodGraph.Name != value)
                {
                    methodGraph.Name = value;
                }
            }
        }

        public bool IsConstructor => graph is ConstructorGraph;

        public ObservableViewModelCollection<NodeVM, Node> Nodes { get; set; }

        public IEnumerable<BaseType> ArgumentTypes =>
            graph is ExecutionGraph execGraph ? execGraph.ArgumentTypes : null;

        public IEnumerable<Named<BaseType>> NamedArgumentTypes =>
            graph is ExecutionGraph execGraph ? execGraph.NamedArgumentTypes : null;

        public IEnumerable<BaseType> ReturnTypes =>
            graph is MethodGraph methodGraph ? methodGraph.ReturnTypes : null;

        public ClassEditorVM Class { get; set; }

        public MethodModifiers Modifiers
        {
            get => graph is MethodGraph methodGraph ? methodGraph.Modifiers : MethodModifiers.None;
            set
            {
                if (graph is MethodGraph methodGraph && methodGraph.Modifiers != value)
                {
                    methodGraph.Modifiers = value;
                }
            }
        }

        public MemberVisibility Visibility
        {
            get => graph is ExecutionGraph execGraph ? execGraph.Visibility : MemberVisibility.Invalid;
            set
            {
                if (graph is ExecutionGraph execGraph && execGraph.Visibility != value)
                {
                    execGraph.Visibility = value;
                }
            }
        }

        public IEnumerable<MemberVisibility> PossibleVisibilities => new[]
        {
            MemberVisibility.Internal,
            MemberVisibility.Private,
            MemberVisibility.Protected,
            MemberVisibility.Public,
        };

        /*
        Scenarios:
        
        Method changed [0]:
            (Un)assign (old) nodes changed events [1]
            (Un)assign (old)new pins changed events [2]
            (Un)assign (old)new pin connection changed events [3]
            (Un)setup (old)new method pins' connections in VM

        Node changed [1]:
            (Un)assign (old)new pins changed events [2]
            (Un)assign (old)new pin connection changed events [3]
            (Un)setup (old)new node pins' connections in VM

        Pin changed [2]:
            (Un)assign (old)new pin connection changed events [3]
            (Un)setup (old)new pin connection in VM

        Pin connection changed [3]:
            (Un)setup (old)new connection in VM
        */

        private void SetupNodeEvents(NodeVM node, bool add)
        {
            // (Un)assign (old)new pins changed events [2]
            if (add)
            {
                node.InputDataPins.CollectionChanged += OnPinCollectionChanged;
                node.OutputDataPins.CollectionChanged += OnPinCollectionChanged;
                node.InputExecPins.CollectionChanged += OnPinCollectionChanged;
                node.OutputExecPins.CollectionChanged += OnPinCollectionChanged;
                node.InputTypePins.CollectionChanged += OnPinCollectionChanged;
                node.OutputTypePins.CollectionChanged += OnPinCollectionChanged;

                node.OnDragStart += OnNodeDragStart;
                node.OnDragEnd += OnNodeDragEnd;
                node.OnDragMove += OnNodeDragMove;
            }
            else
            {
                node.InputDataPins.CollectionChanged -= OnPinCollectionChanged;
                node.OutputDataPins.CollectionChanged -= OnPinCollectionChanged;
                node.InputExecPins.CollectionChanged -= OnPinCollectionChanged;
                node.OutputExecPins.CollectionChanged -= OnPinCollectionChanged;
                node.InputTypePins.CollectionChanged -= OnPinCollectionChanged;
                node.OutputTypePins.CollectionChanged -= OnPinCollectionChanged;

                node.OnDragStart -= OnNodeDragStart;
                node.OnDragEnd -= OnNodeDragEnd;
                node.OnDragMove -= OnNodeDragMove;
            }

            // (Un)assign (old)new pin connection changed events [3]
            node.InputDataPins.ToList().ForEach(p => SetupPinEvents(p, add));
            node.OutputExecPins.ToList().ForEach(p => SetupPinEvents(p, add));
            node.InputTypePins.ToList().ForEach(p => SetupPinEvents(p, add));
        }

        #region Node dragging
        public double NodeDragScale
        {
            get;
            set;
        } = 1;

        private double nodeDragAccumX;
        private double nodeDragAccumY;

        private readonly Dictionary<NodeVM, (double X, double Y)> nodeStartPositions = new Dictionary<NodeVM, (double, double)>();

        /// <summary>
        /// Called when a node starts dragging.
        /// </summary>
        private void OnNodeDragStart(NodeVM node)
        {
            nodeDragAccumX = 0;
            nodeDragAccumY = 0;
            nodeStartPositions.Clear();

            // Remember the initial positions of the selected nodes
            if (SelectedNodes != null)
            {
                foreach (var selectedNode in SelectedNodes)
                {
                    nodeStartPositions.Add(selectedNode, (selectedNode.Node.PositionX, selectedNode.Node.PositionY));
                }
            }
        }

        /// <summary>
        /// Called when a node ends dragging.
        /// </summary>
        private void OnNodeDragEnd(NodeVM node)
        {
            // Snap final position to grid
            if (SelectedNodes != null)
            {
                foreach (var selectedNode in SelectedNodes)
                {
                    selectedNode.Node.PositionX -= selectedNode.Node.PositionX % MethodEditorControl.GridCellSize;
                    selectedNode.Node.PositionY -= selectedNode.Node.PositionY % MethodEditorControl.GridCellSize;
                }
            }
        }

        /// <summary>
        /// Called when a node is dragging.
        /// </summary>
        private void OnNodeDragMove(NodeVM node, double dx, double dy)
        {
            nodeDragAccumX += dx * NodeDragScale;
            nodeDragAccumY += dy * NodeDragScale;

            // Move all selected nodes
            if (SelectedNodes != null)
            {
                foreach (var selectedNode in SelectedNodes)
                {
                    // Set position by taking total delta and adding it to the initial position
                    selectedNode.Node.PositionX = nodeStartPositions[selectedNode].X + nodeDragAccumX - nodeDragAccumX % MethodEditorControl.GridCellSize;
                    selectedNode.Node.PositionY = nodeStartPositions[selectedNode].Y + nodeDragAccumY - nodeDragAccumY % MethodEditorControl.GridCellSize;
                }
            }
        }
        #endregion

        private void SetupPinEvents(NodePinVM pin, bool add)
        {
            // (Un)assign (old)new pin connection changed events [3]

            if (pin.Pin is NodeInputDataPin idp)
            {
                if (add)
                {
                    idp.IncomingPinChanged += OnInputDataPinIncomingPinChanged;
                }
                else
                {
                    idp.IncomingPinChanged -= OnInputDataPinIncomingPinChanged;
                }
            }
            else if (pin.Pin is NodeOutputExecPin oxp)
            {
                if (add)
                {
                    oxp.OutgoingPinChanged += OnOutputExecPinIncomingPinChanged;
                }
                else
                {
                    oxp.OutgoingPinChanged -= OnOutputExecPinIncomingPinChanged;
                }
            }
            else if (pin.Pin is NodeInputTypePin itp)
            {
                if (add)
                {
                    itp.IncomingPinChanged += OnInputTypePinIncomingPinChanged;
                }
                else
                {
                    itp.IncomingPinChanged -= OnInputTypePinIncomingPinChanged;
                }
            }
        }

        private void SetupPinConnection(NodePinVM pin, bool add)
        {
            if (pin.Pin is NodeInputDataPin idp)
            {
                if (idp.IncomingPin != null)
                {
                    if (add)
                    {
                        NodeOutputDataPin connPin = idp.IncomingPin as NodeOutputDataPin;
                        pin.ConnectedPin = Nodes
                            .SingleOrDefault(n => n.Node == connPin.Node)
                            ?.OutputDataPins
                            ?.Single(x => x.Pin == connPin);
                    }
                    else
                    {
                        pin.ConnectedPin = null;
                    }
                }
            }
            else if (pin.Pin is NodeOutputExecPin oxp)
            {
                if (oxp.OutgoingPin != null)
                {
                    if (add)
                    {
                        NodeInputExecPin connPin = oxp.OutgoingPin as NodeInputExecPin;
                        pin.ConnectedPin = Nodes
                            .Single(n => n.Node == connPin.Node)
                            .InputExecPins
                            .Single(x => x.Pin == connPin);
                    }
                    else
                    {
                        pin.ConnectedPin = null;
                    }
                }
            }
            else if (pin.Pin is NodeInputTypePin itp)
            {
                if (itp.IncomingPin != null)
                {
                    if (add)
                    {
                        NodeOutputTypePin connPin = itp.IncomingPin as NodeOutputTypePin;
                        pin.ConnectedPin = Nodes
                            .Single(n => n.Node == connPin.Node)
                            .OutputTypePins
                            .Single(x => x.Pin == connPin);
                    }
                    else
                    {
                        pin.ConnectedPin = null;
                    }
                }
            }
        }

        private void SetupNodeConnections(NodeVM node, bool add)
        {
            node.OutputExecPins.ToList().ForEach(p => SetupPinConnection(p, add));
            node.InputDataPins.ToList().ForEach(p => SetupPinConnection(p, add));
            node.InputTypePins.ToList().ForEach(p => SetupPinConnection(p, add));

            // Make sure to remove the IXP and ODP connections 
            // any other nodes are connecting to too

            if (!add)
            {
                AllPins.Where(p =>
                    node.InputExecPins.Contains(p.ConnectedPin)
                    || node.OutputDataPins.Contains(p.ConnectedPin)
                    || node.OutputTypePins.Contains(p.ConnectedPin)
                ).ToList().ForEach(p => p.ConnectedPin = null);
            }
        }

        public NodeGraph Graph
        {
            get => graph;
            set
            {
                if (graph != value)
                {
                    if (graph != null)
                    {
                        Nodes.CollectionChanged -= OnNodeCollectionChanged;
                        Nodes.ToList().ForEach(n => SetupNodeEvents(n, false));

                        Nodes.ToList().ForEach(n => SetupNodeConnections(n, false));
                    }

                    graph = value;
                    RaisePropertyChanged(nameof(AllPins));
                    RaisePropertyChanged(nameof(Visibility));

                    Nodes = new ObservableViewModelCollection<NodeVM, Node>(Graph.Nodes, n => new NodeVM(n));

                    if (graph != null)
                    {
                        Nodes.CollectionChanged += OnNodeCollectionChanged;
                        Nodes.ToList().ForEach(n => SetupNodeEvents(n, true));

                        // Connections on normal pins already exist,
                        // Set them up for the viewmodels of the pins
                        Nodes.ToList().ForEach(n => SetupNodeConnections(n, true));
                    }
                }
            }
        }

        private NodeGraph graph;

        private void OnNodeCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                var removedNodes = e.OldItems.Cast<NodeVM>();
                removedNodes.ToList().ForEach(n => SetupNodeEvents(n, false));

                removedNodes.ToList().ForEach(n => SetupNodeConnections(n, false));
            }

            if (e.NewItems != null)
            {
                var addedNodes = e.NewItems.Cast<NodeVM>();
                addedNodes.ToList().ForEach(n => SetupNodeEvents(n, true));

                addedNodes.ToList().ForEach(n => SetupNodeConnections(n, true));
            }

            RaisePropertyChanged(nameof(AllPins));
        }

        private void OnPinCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                var removedPins = e.OldItems.Cast<NodePinVM>();

                // Unsetup initial connections of added pins
                removedPins.ToList().ForEach(p => SetupPinConnection(p, false));

                // Remove events for removed pins
                removedPins.ToList().ForEach(p => SetupPinEvents(p, false));
            }

            if (e.NewItems != null)
            {
                var addedPins = e.NewItems.Cast<NodePinVM>();

                // Setup initial connections of added pins
                addedPins.ToList().ForEach(p => SetupPinConnection(p, true));

                // Add events for added pins
                addedPins.ToList().ForEach(p => SetupPinEvents(p, true));
            }

            RaisePropertyChanged(nameof(AllPins));
        }

        private void OnInputDataPinIncomingPinChanged(NodeInputDataPin pin, NodeOutputDataPin oldPin, NodeOutputDataPin newPin)
        {
            // Connect pinVM newPinVM (or null if newPin is null)

            NodePinVM pinVM = FindPinVMFromPin(pin);
            pinVM.ConnectedPin = newPin == null ? null : FindPinVMFromPin(newPin);
        }

        private void OnOutputExecPinIncomingPinChanged(NodeOutputExecPin pin, NodeInputExecPin oldPin, NodeInputExecPin newPin)
        {
            // Connect pinVM newPinVM (or null if newPin is null)

            NodePinVM pinVM = FindPinVMFromPin(pin);
            pinVM.ConnectedPin = newPin == null ? null : FindPinVMFromPin(newPin);
        }

        private void OnInputTypePinIncomingPinChanged(NodeInputTypePin pin, NodeOutputTypePin oldPin, NodeOutputTypePin newPin)
        {
            // Connect pinVM newPinVM (or null if newPin is null)

            NodePinVM pinVM = FindPinVMFromPin(pin);
            if (pinVM != null)
            {
                pinVM.ConnectedPin = newPin == null ? null : FindPinVMFromPin(newPin);
            }
        }

        private NodePinVM FindPinVMFromPin(NodePin pin)
        {
            return AllPins.SingleOrDefault(p => p.Pin == pin);
        }

        public IEnumerable<NodePinVM> AllPins
        {
            get
            {
                List<NodePinVM> pins = new List<NodePinVM>();

                if (Graph != null)
                {
                    foreach (NodeVM node in Nodes)
                    {
                        pins.AddRange(node.InputDataPins);
                        pins.AddRange(node.OutputDataPins);
                        pins.AddRange(node.InputExecPins);
                        pins.AddRange(node.OutputExecPins);
                        pins.AddRange(node.InputTypePins);
                        pins.AddRange(node.OutputTypePins);
                    }
                }

                return pins;
            }
        }

        /// <summary>
        /// Sends a message to deselect all nodes and select the given nodes.
        /// </summary>
        /// <param name="nodes">Nodes to be selected.</param>
        public void SelectNodes(IEnumerable<NodeVM> nodes)
        {
            MessengerInstance.Send(new NodeSelectionMessage(nodes, true));
        }

        /// <summary>
        /// Sends a message to deselect all nodes.
        /// </summary>
        public void DeselectNodes()
        {
            MessengerInstance.Send(NodeSelectionMessage.DeselectAll);
        }

        public NodeGraphVM(NodeGraph graph)
        {
            Graph = graph;

            MessengerInstance.Register<NodeSelectionMessage>(this, OnNodeSelectionReceived);
        }

        private void OnNodeSelectionReceived(NodeSelectionMessage msg)
        {
            if (msg.DeselectPrevious)
            {
                SelectedNodes = new NodeVM[0] { };
            }

            SelectedNodes = SelectedNodes.Concat(msg.Nodes).Distinct();
        }
    }
}
