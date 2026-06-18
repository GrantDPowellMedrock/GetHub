using System;
using System.IO;
using System.Reflection;
using System.Text;

using Avalonia.Collections;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GetHub.ViewModels
{
    public class Launcher : ObservableObject
    {
        public const string GroupAll = "All";

        private static readonly string AppVersion = GetAppVersion();

        private static string GetAppVersion()
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            return ver != null ? $"{ver.Major}.{ver.Minor:D2}" : string.Empty;
        }

        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        public AvaloniaList<LauncherPage> Pages
        {
            get;
            private set;
        }

        public Workspace ActiveWorkspace
        {
            get => _activeWorkspace;
            private set => SetProperty(ref _activeWorkspace, value);
        }

        public LauncherPage ActivePage
        {
            get => _activePage;
            set
            {
                if (SetProperty(ref _activePage, value))
                    PostActivePageChanged();
            }
        }

        public ICommandPalette CommandPalette
        {
            get => _commandPalette;
            set => SetProperty(ref _commandPalette, value);
        }

        public AvaloniaList<LauncherGroup> Groups
        {
            get;
            private set;
        } = new AvaloniaList<LauncherGroup>();

        public void MoveGroup(string fromName, string toName)
        {
            if (string.IsNullOrEmpty(fromName) || string.IsNullOrEmpty(toName) || fromName == toName)
                return;
            if (fromName == GroupAll || toName == GroupAll)
                return;

            var list = _activeWorkspace.Groups;
            int fromIdx = -1, toIdx = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Name == fromName) fromIdx = i;
                else if (list[i].Name == toName) toIdx = i;
            }
            if (fromIdx < 0 || toIdx < 0)
                return;

            var moved = list[fromIdx];
            list.RemoveAt(fromIdx);
            list.Insert(toIdx, moved);

            Preferences.Instance.Save();
            RefreshGroups();
        }

        public void SetGroupColor(string groupName, int bookmark)
        {
            if (string.IsNullOrEmpty(groupName) || groupName == GroupAll)
                return;

            foreach (var g in _activeWorkspace.Groups)
            {
                if (g.Name == groupName)
                {
                    g.Bookmark = bookmark;
                    break;
                }
            }
            foreach (var g in Groups)
            {
                if (g.Name == groupName)
                {
                    g.Bookmark = bookmark;
                    break;
                }
            }
            Preferences.Instance.Save();
        }

        public bool CreateGroup(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || string.Equals(name, GroupAll, StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (var g in _activeWorkspace.Groups)
            {
                if (string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            _activeWorkspace.Groups.Add(new WorkspaceGroup { Name = name });
            Preferences.Instance.Save();
            RefreshGroups();
            return true;
        }

        public bool RenameGroup(string oldName, string newName)
        {
            newName = newName?.Trim();
            if (string.IsNullOrEmpty(newName) || string.Equals(newName, GroupAll, StringComparison.OrdinalIgnoreCase))
                return false;

            WorkspaceGroup target = null;
            foreach (var g in _activeWorkspace.Groups)
            {
                if (!string.Equals(g.Name, oldName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(g.Name, newName, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (g.Name == oldName)
                    target = g;
            }
            if (target == null)
                return false;

            var wasActive = _activeGroup == oldName;
            target.Name = newName;
            if (wasActive)
                _activeGroup = newName;

            Preferences.Instance.Save();
            RefreshGroups();
            return true;
        }

        public void DeleteGroup(string name)
        {
            if (string.IsNullOrEmpty(name) || name == GroupAll)
                return;

            _activeWorkspace.Groups.RemoveAll(g => g.Name == name);
            if (_activeGroup == name)
                _activeGroup = GroupAll;

            Preferences.Instance.Save();
            RefreshGroups();
        }

        public void AddRepoToGroup(string groupName, string repoId)
        {
            if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(repoId) || groupName == GroupAll)
                return;
            if (!_activeWorkspace.Repositories.Contains(repoId))
                return;

            foreach (var g in _activeWorkspace.Groups)
            {
                if (g.Name == groupName)
                {
                    if (!g.RepositoryIds.Contains(repoId))
                        g.RepositoryIds.Add(repoId);
                    break;
                }
            }

            Preferences.Instance.Save();
            ApplyGroupFilter();
        }

        public void RemoveRepoFromGroup(string groupName, string repoId)
        {
            if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(repoId))
                return;

            foreach (var g in _activeWorkspace.Groups)
            {
                if (g.Name == groupName)
                {
                    g.RepositoryIds.Remove(repoId);
                    break;
                }
            }

            Preferences.Instance.Save();
            ApplyGroupFilter();
        }

        public void OpenAsGroup(RepositoryNode node)
        {
            if (node == null)
                return;

            var ids = new System.Collections.Generic.List<string>();
            CollectDescendantRepoIds(node, ids);
            if (ids.Count == 0)
                return;

            _ignoreIndexChange = true;
            try
            {
                foreach (var id in ids)
                    OpenRepositoryById(id);
            }
            finally
            {
                _ignoreIndexChange = false;
            }

            WorkspaceGroup group = null;
            foreach (var g in _activeWorkspace.Groups)
            {
                if (string.Equals(g.Name, node.Name, StringComparison.OrdinalIgnoreCase))
                {
                    group = g;
                    break;
                }
            }
            if (group == null)
            {
                group = new WorkspaceGroup { Name = node.Name };
                _activeWorkspace.Groups.Add(group);
            }

            foreach (var id in ids)
            {
                if (_activeWorkspace.Repositories.Contains(id) && !group.RepositoryIds.Contains(id))
                    group.RepositoryIds.Add(id);
            }

            Preferences.Instance.Save();
            RefreshGroups();
            ActiveGroup = group.Name;
        }

        private void CollectDescendantRepoIds(RepositoryNode node, System.Collections.Generic.List<string> outIds)
        {
            if (node.IsRepository && !outIds.Contains(node.Id))
                outIds.Add(node.Id);

            foreach (var sub in node.SubNodes)
                CollectDescendantRepoIds(sub, outIds);
        }

        public string ActiveGroup
        {
            get => _activeGroup;
            set
            {
                if (SetProperty(ref _activeGroup, value))
                {
                    OpenGroupRepositories(value);
                    ApplyGroupFilter();
                }
            }
        }

        public Launcher(string startupRepo)
        {
            Models.Notification.Raised += DispatchNotification;
            _ignoreIndexChange = true;

            Pages = new AvaloniaList<LauncherPage>();
            Pages.CollectionChanged += (_, _) => RefreshGroups();
            AddNewTab();

            var pref = Preferences.Instance;
            ActiveWorkspace = pref.GetActiveWorkspace();

            var repos = ActiveWorkspace.Repositories.ToArray();
            foreach (var repo in repos)
            {
                var node = pref.FindNode(repo) ??
                    new RepositoryNode
                    {
                        Id = repo,
                        Name = Path.GetFileName(repo),
                        Bookmark = 0,
                        IsRepository = true,
                    };

                OpenRepositoryInTab(node, null);
            }

            _ignoreIndexChange = false;

            if (TryOpenRepositoryFromPath(startupRepo))
                return;

            if (!string.IsNullOrEmpty(startupRepo))
            {
                var test = new Commands.QueryRepositoryRootPath(startupRepo).GetResult();
                if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
                {
                    var node = pref.FindOrAddNodeByRepositoryPath(test.StdOut.Trim(), null, false);
                    Welcome.Instance.Refresh();

                    OpenRepositoryInTab(node, null);
                    return;
                }
            }

            var activeIdx = ActiveWorkspace.ActiveIdx;
            if (activeIdx > 0 && activeIdx < Pages.Count)
            {
                ActivePage = Pages[activeIdx];
                return;
            }

            ActivePage = Pages[0];
            PostActivePageChanged();
        }

        public bool TryOpenRepositoryFromPath(string repo)
        {
            if (!string.IsNullOrEmpty(repo) && Directory.Exists(repo))
            {
                var test = new Commands.QueryRepositoryRootPath(repo).GetResult();
                if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
                {
                    var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(test.StdOut.Trim(), null, false);
                    Welcome.Instance.Refresh();
                    OpenRepositoryInTab(node, null);
                    return true;
                }
            }

            return false;
        }

        public void CloseAll()
        {
            _ignoreIndexChange = true;

            foreach (var one in Pages)
                CloseRepositoryInTab(one, false);

            _ignoreIndexChange = false;
        }

        public void SwitchWorkspace(Workspace to)
        {
            if (to == null || to.IsActive)
                return;

            _ignoreIndexChange = true;

            var pref = Preferences.Instance;
            foreach (var w in pref.Workspaces)
                w.IsActive = false;

            ActiveWorkspace = to;
            to.IsActive = true;
            _activeGroup = GroupAll;

            foreach (var one in Pages)
                CloseRepositoryInTab(one, false);

            Pages.Clear();
            AddNewTab();

            var repos = to.Repositories.ToArray();
            foreach (var repo in repos)
            {
                var node = pref.FindNode(repo) ??
                    new RepositoryNode
                    {
                        Id = repo,
                        Name = Path.GetFileName(repo),
                        Bookmark = 0,
                        IsRepository = true,
                    };

                OpenRepositoryInTab(node, null);
            }

            var activeIdx = to.ActiveIdx;
            if (activeIdx >= 0 && activeIdx < Pages.Count)
                ActivePage = Pages[activeIdx];
            else
                ActivePage = Pages[0];

            _ignoreIndexChange = false;
            PostActivePageChanged();
            Preferences.Instance.Save();
            RefreshGroups();
            GC.Collect();
        }

        public void AddNewTab()
        {
            var page = new LauncherPage();
            Pages.Add(page);
            ActivePage = page;
        }

        public void MoveTab(LauncherPage from, LauncherPage to)
        {
            _ignoreIndexChange = true;

            var fromIdx = Pages.IndexOf(from);
            var toIdx = Pages.IndexOf(to);
            Pages.Move(fromIdx, toIdx);

            _activeWorkspace.Repositories.Clear();
            foreach (var p in Pages)
            {
                if (p.Data is Repository r)
                    _activeWorkspace.Repositories.Add(r.FullPath);
            }

            _ignoreIndexChange = false;
            ActivePage = from;
        }

        public void GotoNextTab()
        {
            if (Pages.Count == 1)
                return;

            var activeIdx = Pages.IndexOf(_activePage);
            var nextIdx = (activeIdx + 1) % Pages.Count;
            ActivePage = Pages[nextIdx];
        }

        public void GotoPrevTab()
        {
            if (Pages.Count == 1)
                return;

            var activeIdx = Pages.IndexOf(_activePage);
            var prevIdx = activeIdx == 0 ? Pages.Count - 1 : activeIdx - 1;
            ActivePage = Pages[prevIdx];
        }

        public void CloseTab(LauncherPage page)
        {
            if (Pages.Count == 1)
            {
                var last = Pages[0];
                if (last.Data is Repository repo)
                {
                    _activeWorkspace.Repositories.Clear();
                    _activeWorkspace.ActiveIdx = 0;

                    repo.Close();

                    Welcome.Instance.ClearSearchFilter();
                    last.Node = new RepositoryNode() { Id = Guid.NewGuid().ToString() };
                    last.Data = Welcome.Instance;
                    last.Popup?.Cleanup();
                    last.Popup = null;

                    PostActivePageChanged();
                    GC.Collect();
                }
                else
                {
                    App.Quit(0);
                }

                return;
            }

            page ??= _activePage;

            var removeIdx = Pages.IndexOf(page);
            var activeIdx = Pages.IndexOf(_activePage);
            if (removeIdx == activeIdx)
                ActivePage = Pages[removeIdx > 0 ? removeIdx - 1 : removeIdx + 1];

            CloseRepositoryInTab(page);
            Pages.RemoveAt(removeIdx);
            GC.Collect();
        }

        public void CloseOtherTabs()
        {
            if (Pages.Count == 1)
                return;

            _ignoreIndexChange = true;

            var id = ActivePage.Node.Id;
            foreach (var one in Pages)
            {
                if (one.Node.Id != id)
                    CloseRepositoryInTab(one);
            }

            Pages = new AvaloniaList<LauncherPage> { ActivePage };
            OnPropertyChanged(nameof(Pages));

            _activeWorkspace.ActiveIdx = 0;
            _ignoreIndexChange = false;
            GC.Collect();
        }

        public void CloseRightTabs()
        {
            _ignoreIndexChange = true;

            var endIdx = Pages.IndexOf(ActivePage);
            for (var i = Pages.Count - 1; i > endIdx; i--)
            {
                CloseRepositoryInTab(Pages[i]);
                Pages.Remove(Pages[i]);
            }

            _ignoreIndexChange = false;
            GC.Collect();
        }

        public void OpenRepositoryInTab(RepositoryNode node, LauncherPage page)
        {
            foreach (var one in Pages)
            {
                if (one.Node.Id == node.Id)
                {
                    ActivePage = one;
                    return;
                }
            }

            if (!Directory.Exists(node.Id))
            {
                ActivePage.Notifications.Add(new Models.Notification
                {
                    Group = node.Id,
                    Message = "Repository does NOT exist any more. Please remove it.",
                    IsError = true,
                });
                return;
            }

            var isBare = new Commands.IsBareRepository(node.Id).GetResult();
            var gitDir = isBare ? node.Id : GetRepositoryGitDir(node.Id);
            if (string.IsNullOrEmpty(gitDir))
            {
                ActivePage.Notifications.Add(new Models.Notification
                {
                    Group = node.Id,
                    Message = "Given path is not a valid git repository!",
                    IsError = true,
                });
                return;
            }

            var repo = new Repository(isBare, node.Id, gitDir);
            repo.Open();

            if (page == null)
            {
                if (_activePage == null || _activePage.Node.IsRepository)
                {
                    page = new LauncherPage(node, repo);
                    Pages.Add(page);
                }
                else
                {
                    page = _activePage;
                    page.Node = node;
                    page.Data = repo;
                }
            }
            else
            {
                page.Node = node;
                page.Data = repo;
            }

            _activeWorkspace.Repositories.Clear();
            foreach (var p in Pages)
            {
                if (p.Data is Repository r)
                    _activeWorkspace.Repositories.Add(r.FullPath);
            }

            if (_activePage == page)
                PostActivePageChanged();
            else
                ActivePage = page;
        }

        private void DispatchNotification(Models.Notification notification)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => DispatchNotification(notification));
                return;
            }

            if (string.IsNullOrEmpty(notification.Group))
            {
                _activePage?.Notifications.Add(notification);
                return;
            }

            foreach (var page in Pages)
            {
                var id = page.Node.Id.Replace('\\', '/').TrimEnd('/');
                if (id.Equals(notification.Group, StringComparison.OrdinalIgnoreCase))
                {
                    page.Notifications.Add(notification);
                    return;
                }
            }

            _activePage?.Notifications.Add(notification);
        }

        private string GetRepositoryGitDir(string repo)
        {
            var fullpath = Path.Combine(repo, ".git");
            if (Directory.Exists(fullpath))
            {
                if (Directory.Exists(Path.Combine(fullpath, "refs")) &&
                    Directory.Exists(Path.Combine(fullpath, "objects")) &&
                    File.Exists(Path.Combine(fullpath, "HEAD")))
                    return fullpath;

                return null;
            }

            if (File.Exists(fullpath))
            {
                var redirect = File.ReadAllText(fullpath).Trim();
                if (redirect.StartsWith("gitdir: ", StringComparison.Ordinal))
                    redirect = redirect.Substring(8);

                if (!Path.IsPathRooted(redirect))
                    redirect = Path.GetFullPath(Path.Combine(repo, redirect));

                if (Directory.Exists(redirect))
                    return redirect;

                return null;
            }

            return new Commands.QueryGitDir(repo).GetResult();
        }

        private void CloseRepositoryInTab(LauncherPage page, bool removeFromWorkspace = true)
        {
            if (page.Data is Repository repo)
            {
                if (removeFromWorkspace)
                {
                    _activeWorkspace.Repositories.Remove(repo.FullPath);
                    foreach (var g in _activeWorkspace.Groups)
                        g.RepositoryIds.Remove(repo.FullPath);
                }

                repo.Close();
            }

            page.Popup?.Cleanup();
            page.Popup = null;
            page.Data = null;
        }

        private void PostActivePageChanged()
        {
            if (_ignoreIndexChange)
                return;

            if (_activePage is { Data: Repository repo })
                _activeWorkspace.ActiveIdx = _activeWorkspace.Repositories.IndexOf(repo.FullPath);

            var builder = new StringBuilder(512);
            builder.Append("GetHub ").Append(AppVersion).Append(" - ");
            builder.Append(string.IsNullOrEmpty(_activePage.Node.Name) ? "Repositories" : _activePage.Node.Name);

            var workspaces = Preferences.Instance.Workspaces;
            if (workspaces.Count == 0 || workspaces.Count > 1 || workspaces[0] != _activeWorkspace)
                builder.Append(" - ").Append(_activeWorkspace.Name);

            Title = builder.ToString();
            CommandPalette = null;

            if (_activeGroup != GroupAll && _activePage != null)
            {
                var rid = _activePage.RepoId;
                var inGroup = false;
                foreach (var g in _activeWorkspace.Groups)
                {
                    if (g.Name == _activeGroup && rid != null && g.RepositoryIds.Contains(rid))
                    {
                        inGroup = true;
                        break;
                    }
                }
                if (!inGroup)
                    ActiveGroup = GroupAll;
            }
        }

        public void RefreshGroups()
        {
            // Fired via Pages.CollectionChanged, which can run from AddNewTab() in
            // the constructor BEFORE ActiveWorkspace is assigned. Bail out safely;
            // it runs again once repos open with the workspace set.
            if (_activeWorkspace == null)
                return;

            var built = new System.Collections.Generic.List<LauncherGroup>
            {
                new LauncherGroup(GroupAll, 0, true)
            };
            foreach (var g in _activeWorkspace.Groups)
                built.Add(new LauncherGroup(g.Name, g.Bookmark, false));

            Groups.Clear();
            foreach (var g in built)
                Groups.Add(g);

            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var g in Groups) names.Add(g.Name);
            if (!names.Contains(_activeGroup))
                _activeGroup = GroupAll;

            ApplyGroupFilter();
            OnPropertyChanged(nameof(ActiveGroup));
        }

        private void ApplyGroupFilter()
        {
            var all = _activeGroup == GroupAll;

            WorkspaceGroup group = null;
            if (!all)
            {
                foreach (var g in _activeWorkspace.Groups)
                {
                    if (g.Name == _activeGroup)
                    {
                        group = g;
                        break;
                    }
                }
            }

            foreach (var page in Pages)
            {
                var rid = page.RepoId;
                page.IsInActiveGroup = all || (rid != null && group != null && group.RepositoryIds.Contains(rid));
            }
        }

        private void OpenGroupRepositories(string groupName)
        {
            if (string.IsNullOrEmpty(groupName) || groupName == GroupAll)
                return;

            WorkspaceGroup group = null;
            foreach (var g in _activeWorkspace.Groups)
            {
                if (g.Name == groupName)
                {
                    group = g;
                    break;
                }
            }
            if (group == null)
                return;

            _ignoreIndexChange = true;
            try
            {
                foreach (var id in group.RepositoryIds.ToArray())
                    OpenRepositoryById(id);
            }
            finally
            {
                _ignoreIndexChange = false;
            }
        }

        private void OpenRepositoryById(string id)
        {
            if (string.IsNullOrEmpty(id) || !Directory.Exists(id))
                return;

            foreach (var p in Pages)
            {
                if (p.Node.Id == id)
                    return;
            }

            var node = Preferences.Instance.FindNode(id) ??
                new RepositoryNode
                {
                    Id = id,
                    Name = Path.GetFileName(id),
                    Bookmark = 0,
                    IsRepository = true,
                };

            OpenRepositoryInTab(node, null);
        }

        public void OpenGroupInZed(string groupName)
        {
            var zed = Native.OS.ExternalTools.Find(x => x.Name.Equals("Zed", StringComparison.Ordinal));
            if (zed == null)
            {
                Models.Notification.Send(string.Empty, "Zed was not found on this system.", true);
                return;
            }

            WorkspaceGroup group = null;
            foreach (var g in _activeWorkspace.Groups)
            {
                if (g.Name == groupName)
                {
                    group = g;
                    break;
                }
            }
            if (group == null)
                return;

            var paths = new System.Collections.Generic.List<string>();
            foreach (var id in group.RepositoryIds)
            {
                if (Directory.Exists(id) && !paths.Contains(id))
                    paths.Add(id);
            }
            if (paths.Count == 0)
                return;

            // Open ONE Zed window with every repo added as a workspace root folder.
            var args = string.Join(" ", paths.ConvertAll(p => p.Quoted()));
            zed.Launch(args);
        }

        private Workspace _activeWorkspace;
        private LauncherPage _activePage;
        private bool _ignoreIndexChange;
        private string _title = string.Empty;
        private ICommandPalette _commandPalette;
        private string _activeGroup = GroupAll;
    }
}
