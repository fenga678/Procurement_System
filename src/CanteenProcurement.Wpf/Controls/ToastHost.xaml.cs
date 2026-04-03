using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CanteenProcurement.Wpf.Controls
{
    public partial class ToastHost : UserControl
    {
        public ObservableCollection<ToastMessage> Notifications { get; } = new();

        public ToastHost()
        {
            InitializeComponent();
        }

        public async void ShowToast(string title, string message, string tone = "info")
        {
            var item = new ToastMessage(title, message, tone);
            Notifications.Add(item);
            await Task.Delay(3200);
            await Dispatcher.InvokeAsync(() => Notifications.Remove(item));
        }
    }

    public sealed class ToastMessage
    {
        public ToastMessage(string title, string message, string tone)
        {
            Title = title;
            Message = message;
            Tone = tone;
        }

        public string Title { get; }
        public string Message { get; }
        public string Tone { get; }
    }
}
