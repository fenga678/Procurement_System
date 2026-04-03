using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace CanteenProcurement.Wpf.Controls
{
    [ContentProperty(nameof(BodyContent))]
    public partial class SearchFilterBar : UserControl
    {

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(SearchFilterBar), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(SearchFilterBar), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty BodyContentProperty = DependencyProperty.Register(
            nameof(BodyContent), typeof(object), typeof(SearchFilterBar), new PropertyMetadata(null));

        public static readonly DependencyProperty ActionsContentProperty = DependencyProperty.Register(
            nameof(ActionsContent), typeof(object), typeof(SearchFilterBar), new PropertyMetadata(null));

        public SearchFilterBar()
        {
            InitializeComponent();
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public object? BodyContent
        {
            get => GetValue(BodyContentProperty);
            set => SetValue(BodyContentProperty, value);
        }

        public object? ActionsContent
        {
            get => GetValue(ActionsContentProperty);
            set => SetValue(ActionsContentProperty, value);
        }
    }
}
