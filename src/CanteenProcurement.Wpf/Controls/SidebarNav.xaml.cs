using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CanteenProcurement.Wpf.Controls
{
    public sealed class ShellSectionChangedEventArgs : EventArgs
    {
        public ShellSectionChangedEventArgs(ShellSection section)
        {
            Section = section;
        }

        public ShellSection Section { get; }
    }

    public partial class SidebarNav : UserControl
    {
        public static readonly DependencyProperty SelectedSectionProperty = DependencyProperty.Register(
            nameof(SelectedSection), typeof(ShellSection), typeof(SidebarNav),
            new PropertyMetadata(ShellSection.CategoryManagement, OnSelectedSectionChanged));

        public event EventHandler<ShellSectionChangedEventArgs>? SectionSelected;


        public SidebarNav()
        {
            InitializeComponent();
            Loaded += (_, _) => UpdateVisualState();
        }

        public ShellSection SelectedSection
        {
            get => (ShellSection)GetValue(SelectedSectionProperty);
            set => SetValue(SelectedSectionProperty, value);
        }

        private static void OnSelectedSectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SidebarNav nav)
            {
                nav.UpdateVisualState();
            }
        }

        private void UpdateVisualState()
        {
            var map = new Dictionary<Button, ShellSection>
            {
                [CategoryManagementButton] = ShellSection.CategoryManagement,
                [ProductManagementButton] = ShellSection.ProductManagement,
                [TaskManagementButton] = ShellSection.TaskManagement,
                [SettingsButton] = ShellSection.Settings
            };

            foreach (var pair in map)
            {
                pair.Key.Tag = pair.Value == SelectedSection ? "Active" : null;
            }
        }

        private void RaiseSelection(ShellSection section)
        {
            SelectedSection = section;
            SectionSelected?.Invoke(this, new ShellSectionChangedEventArgs(section));
        }

        private void CategoryManagementButton_Click(object sender, RoutedEventArgs e) => RaiseSelection(ShellSection.CategoryManagement);
        private void ProductManagementButton_Click(object sender, RoutedEventArgs e) => RaiseSelection(ShellSection.ProductManagement);
        private void TaskManagementButton_Click(object sender, RoutedEventArgs e) => RaiseSelection(ShellSection.TaskManagement);
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => RaiseSelection(ShellSection.Settings);
    }
}
