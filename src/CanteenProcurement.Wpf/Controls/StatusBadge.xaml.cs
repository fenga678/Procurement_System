using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CanteenProcurement.Wpf.Controls
{
    public partial class StatusBadge : UserControl
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(StatusBadge), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
            nameof(Tone), typeof(string), typeof(StatusBadge), new PropertyMetadata("info", OnAppearanceChanged));

        public StatusBadge()
        {
            InitializeComponent();
            Loaded += (_, _) => ApplyTone();
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Tone
        {
            get => (string)GetValue(ToneProperty);
            set => SetValue(ToneProperty, value);
        }

        private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatusBadge badge)
            {
                badge.ApplyTone();
            }
        }

        private void ApplyTone()
        {
            var tone = (Tone ?? "info").ToLowerInvariant();
            BadgeBorder.Background = FindBrush(tone switch
            {
                "success" => "Brush.Success",
                "warning" => "Brush.Warning",
                "danger" => "Brush.Danger",
                _ => "Brush.Info"
            });

            BadgeText.Foreground = FindBrush("Brush.Text.Inverse");
        }

        private static Brush FindBrush(string key)
        {
            return (Brush)System.Windows.Application.Current.FindResource(key);
        }
    }
}
