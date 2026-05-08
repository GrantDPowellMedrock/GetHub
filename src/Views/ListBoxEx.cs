using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace GetHub.Views
{
    public class ListBoxEx : ListBox
    {
        protected override Type StyleKeyOverride => typeof(ListBox);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
                return;

            base.OnKeyDown(e);
        }
    }
}
