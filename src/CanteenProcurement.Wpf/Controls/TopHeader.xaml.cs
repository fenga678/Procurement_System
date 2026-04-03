using System.Windows;
using System.Windows.Controls;

namespace CanteenProcurement.Wpf.Controls
{
    public partial class TopHeader : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(TopHeader), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
            nameof(Subtitle), typeof(string), typeof(TopHeader), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty MetaTitleProperty = DependencyProperty.Register(
            nameof(MetaTitle), typeof(string), typeof(TopHeader), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty MetaSubtitleProperty = DependencyProperty.Register(
            nameof(MetaSubtitle), typeof(string), typeof(TopHeader), new PropertyMetadata(string.Empty));

        public TopHeader()
        {
            InitializeComponent();
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public string MetaTitle
        {
            get => (string)GetValue(MetaTitleProperty);
            set => SetValue(MetaTitleProperty, value);
        }

        public string MetaSubtitle
        {
            get => (string)GetValue(MetaSubtitleProperty);
            set => SetValue(MetaSubtitleProperty, value);
        }
    }
}
