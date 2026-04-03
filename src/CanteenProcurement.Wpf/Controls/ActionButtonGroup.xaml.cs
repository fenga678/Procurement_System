using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CanteenProcurement.Wpf.Controls
{
    public partial class ActionButtonGroup : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(ActionButtonGroup), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty BodyContentProperty = DependencyProperty.Register(
            nameof(BodyContent), typeof(object), typeof(ActionButtonGroup), new PropertyMetadata(null));

        public static readonly DependencyProperty TitleForegroundProperty = DependencyProperty.Register(
            nameof(TitleForeground), typeof(Brush), typeof(ActionButtonGroup), new PropertyMetadata(null));

        public ActionButtonGroup()
        {
            InitializeComponent();
            TitleForeground = (Brush)FindResource("Brush.Text.Secondary");
        }


        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public object? BodyContent
        {
            get => GetValue(BodyContentProperty);
            set => SetValue(BodyContentProperty, value);
        }

        public Brush TitleForeground
        {
            get => (Brush)GetValue(TitleForegroundProperty);
            set => SetValue(TitleForegroundProperty, value);
        }
    }
}

