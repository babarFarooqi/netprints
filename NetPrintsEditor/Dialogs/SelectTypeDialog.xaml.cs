﻿using NetPrints.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NetPrintsEditor.Dialogs
{
    /// <summary>
    /// Interaction logic for SelectTypeDialog.xaml
    /// </summary>
    public partial class SelectTypeDialog : Window
    {
        public static readonly DependencyProperty SelectedTypeProperty = DependencyProperty.Register(
            nameof(SelectedType), typeof(TypeSpecifier), typeof(SelectTypeDialog));

        public TypeSpecifier SelectedType
        {
            get => (TypeSpecifier)GetValue(SelectedTypeProperty);
            set => SetValue(SelectedTypeProperty, value);
        }

        public SelectTypeDialog()
        {
            InitializeComponent();

            SelectedType = TypeSpecifier.FromType<object>();
        }

        private void OnSelectButtonClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
