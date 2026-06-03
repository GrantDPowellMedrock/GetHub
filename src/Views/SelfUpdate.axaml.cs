using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;

namespace GetHub.Views
{
    public class UpdateInfoView : TextEditor
    {
        protected override Type StyleKeyOverride => typeof(TextEditor);

        public UpdateInfoView() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = true;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (_textMate == null)
            {
                _textMate = Models.TextMateHelper.CreateForEditor(this);
                Models.TextMateHelper.SetGrammarByFileName(_textMate, "README.md");
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
            }

            GC.Collect();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is Models.Version ver)
                Text = ver.Body;
        }

        private TextMate.Installation _textMate = null;
    }

    public partial class SelfUpdate : ChromelessWindow
    {
        public SelfUpdate()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close();
        }

        private void GotoDownload(object _, RoutedEventArgs e)
        {
            Native.OS.OpenBrowser("https://github.com/GrantDPowellMedrock/GetHub/releases/latest");
            e.Handled = true;
        }

        private async void DoSelfUpdate(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is not Button btn || btn.DataContext is not Models.Version ver)
                return;

            // Non-Windows: fall back to opening the releases page.
            if (!Models.SelfUpdater.IsSupported)
            {
                Native.OS.OpenBrowser("https://github.com/GrantDPowellMedrock/GetHub/releases/latest");
                return;
            }

            var status = new TextBlock
            {
                Text = "Starting update…",
                Foreground = Avalonia.Media.Brushes.White,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };

            btn.IsEnabled = false;
            btn.Content = status;

            try
            {
                ViewModels.Preferences.Instance.Save();
                await Models.SelfUpdater.RunAsync(
                    ver,
                    msg => Avalonia.Threading.Dispatcher.UIThread.Post(() => status.Text = msg),
                    System.Threading.CancellationToken.None);

                // On success (Windows) RunAsync exits the process; we won't get here.
            }
            catch (System.Exception ex)
            {
                Native.OS.LogException(ex);
                btn.IsEnabled = true;
                status.Text = $"Update failed: {ex.Message}";
            }
        }

        private void IgnoreThisVersion(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.Version ver })
                ViewModels.Preferences.Instance.IgnoreUpdateTag = ver.TagName;

            Close();
            e.Handled = true;
        }
    }
}
