using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GetHub.ViewModels
{
    public class LauncherPage : ObservableObject
    {
        public RepositoryNode Node
        {
            get => _node;
            set => SetProperty(ref _node, value);
        }

        public object Data
        {
            get => _data;
            set
            {
                var oldRepo = _data as Repository;
                if (SetProperty(ref _data, value))
                {
                    if (oldRepo != null)
                        oldRepo.PropertyChanged -= OnRepoPropertyChanged;
                    if (_data is Repository newRepo)
                    {
                        newRepo.PropertyChanged += OnRepoPropertyChanged;
                        RefreshTrackStatus(newRepo);
                    }
                    else
                    {
                        AheadCount = 0;
                        BehindCount = 0;
                        IsTrackStatusVisible = false;
                    }
                }
            }
        }

        public int AheadCount
        {
            get => _aheadCount;
            private set => SetProperty(ref _aheadCount, value);
        }

        public int BehindCount
        {
            get => _behindCount;
            private set => SetProperty(ref _behindCount, value);
        }

        public bool IsTrackStatusVisible
        {
            get => _isTrackStatusVisible;
            private set => SetProperty(ref _isTrackStatusVisible, value);
        }

        private void OnRepoPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Repository.CurrentBranch) && sender is Repository repo)
                RefreshTrackStatus(repo);
        }

        private void RefreshTrackStatus(Repository repo)
        {
            var branch = repo.CurrentBranch;
            AheadCount = branch?.Ahead?.Count ?? 0;
            BehindCount = branch?.Behind?.Count ?? 0;
            IsTrackStatusVisible = AheadCount > 0 || BehindCount > 0;
        }

        public Models.DirtyState DirtyState
        {
            get => _dirtyState;
            private set => SetProperty(ref _dirtyState, value);
        }

        public Popup Popup
        {
            get => _popup;
            set => SetProperty(ref _popup, value);
        }

        public bool IsInActiveGroup
        {
            get => _isInActiveGroup;
            set => SetProperty(ref _isInActiveGroup, value);
        }

        // Repo path Id of this page, or null for non-repo pages (welcome tab).
        public string RepoId => _node is { IsRepository: true } ? _node.Id : null;

        public AvaloniaList<Models.Notification> Notifications
        {
            get;
            set;
        } = new AvaloniaList<Models.Notification>();

        public LauncherPage()
        {
            _node = new RepositoryNode() { Id = Guid.NewGuid().ToString() };
            _data = Welcome.Instance;

            // New welcome page will clear the search filter before.
            Welcome.Instance.ClearSearchFilter();
        }

        public LauncherPage(RepositoryNode node, Repository repo)
        {
            _node = node;
            Data = repo;
        }

        public void ClearNotifications()
        {
            Notifications.Clear();
        }

        public void ChangeDirtyState(Models.DirtyState flag, bool remove)
        {
            var state = _dirtyState;
            if (remove)
            {
                if (state.HasFlag(flag))
                    state -= flag;
            }
            else
            {
                state |= flag;
            }

            DirtyState = state;
        }

        public bool CanCreatePopup()
        {
            return _popup is not { InProgress: true };
        }

        public async Task ProcessPopupAsync()
        {
            if (_popup is { InProgress: false } dump)
            {
                if (!dump.Check())
                    return;

                dump.InProgress = true;

                try
                {
                    var finished = await dump.Sure();
                    if (finished)
                    {
                        dump.Cleanup();
                        Popup = null;
                    }
                }
                catch (Exception e)
                {
                    Native.OS.LogException(e);
                }

                dump.InProgress = false;
            }
        }

        public void CancelPopup()
        {
            if (_popup == null || _popup.InProgress)
                return;

            _popup?.Cleanup();
            Popup = null;
        }

        private RepositoryNode _node = null;
        private object _data = null;
        private Models.DirtyState _dirtyState = Models.DirtyState.None;
        private Popup _popup = null;
        private bool _isInActiveGroup = true;
        private int _aheadCount;
        private int _behindCount;
        private bool _isTrackStatusVisible;
    }
}
