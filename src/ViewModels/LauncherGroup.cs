using CommunityToolkit.Mvvm.ComponentModel;

namespace GetHub.ViewModels
{
    public class LauncherGroup : ObservableObject
    {
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Bookmark
        {
            get => _bookmark;
            set => SetProperty(ref _bookmark, value);
        }

        public bool IsPseudo { get; init; }

        public LauncherGroup(string name, int bookmark, bool isPseudo)
        {
            _name = name;
            _bookmark = bookmark;
            IsPseudo = isPseudo;
        }

        private string _name;
        private int _bookmark;
    }
}
