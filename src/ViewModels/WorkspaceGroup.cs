using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GetHub.ViewModels
{
    public class WorkspaceGroup : ObservableObject
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

        // Repo path Ids (subset of the owning Workspace.Repositories).
        public List<string> RepositoryIds { get; set; } = [];

        private string _name = string.Empty;
        private int _bookmark = 0;
    }
}
