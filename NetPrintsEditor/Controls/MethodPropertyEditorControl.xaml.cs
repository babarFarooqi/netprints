﻿using NetPrints.Core;
using NetPrintsEditor.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NetPrintsEditor.Controls
{
    /// <summary>
    /// Interaction logic for MethodPropertyEditorControl.xaml
    /// </summary>
    public partial class MethodPropertyEditorControl : UserControl
    {
        public static DependencyProperty MethodProperty = DependencyProperty.Register(
            nameof(Method), typeof(MethodVM), typeof(MethodPropertyEditorControl));

        public MethodVM Method
        {
            get => GetValue(MethodProperty) as MethodVM;
            set => SetValue(MethodProperty, value);
        }

        public MethodPropertyEditorControl()
        {
            InitializeComponent();
        }

        private void OnAddArgumentTypeClick(object sender, RoutedEventArgs e)
        {
            Method.ArgumentTypes.Add(TypeSpecifier.FromType<object>());
        }

        private void OnAddReturnTypeClick(object sender, RoutedEventArgs e)
        {
            Method.ReturnTypes.Add(TypeSpecifier.FromType<object>());
        }

        private int GetControlIndex(Control c, int childIndex)
        {
            var grid = VisualTreeHelper.GetParent(c);
            var presenter = VisualTreeHelper.GetParent(grid);
            var stackPanel = VisualTreeHelper.GetParent(presenter);

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(stackPanel); i++)
            {
                if (VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(
                    stackPanel, i), 0), childIndex) == c)
                {
                    return i;
                }
            }

            return -1;
        }

        private void OnArgumentTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            // Find index of combobox and set the type
            if(sender is ComboBox box && e.AddedItems.Count > 0 && e.AddedItems[0] is BaseType newType)
            {
                int index = GetControlIndex(box, 0);

                if(Method.ArgumentTypes[index] != newType)
                    Method.ArgumentTypes[index] = newType;
            }
        }

        private void OnReturnTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            // Find index of combobox and set the type
            if (sender is ComboBox box && e.AddedItems.Count > 0 && e.AddedItems[0] is BaseType newType)
            {
                int index = GetControlIndex(box, 0);

                if(Method.ReturnTypes[index] != newType)
                    Method.ReturnTypes[index] = newType;
            }
        }

        private void OnRemoveArgumentTypeClick(object sender, RoutedEventArgs e)
        {
            if(sender is Button b)
            {
                int index = GetControlIndex(b, 1);
                Method.ArgumentTypes.RemoveAt(index);
            }
        }

        private void OnRemoveReturnTypeClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button b)
            {
                int index = GetControlIndex(b, 1);
                Method.ReturnTypes.RemoveAt(index);
            }
        }
    }
}
