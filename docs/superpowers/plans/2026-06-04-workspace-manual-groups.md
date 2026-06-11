# Per-Workspace Manual Group Tabs — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the global, tree-derived group-tab strip with manual groups owned by each workspace, plus an "Open as group" action that populates a group from a sidebar folder/container.

**Architecture:** Groups become a serialized `List<WorkspaceGroup>` on `Workspace`. The `Launcher` view-model gains explicit group operations (create/rename/delete/color/reorder/assign) and an "Open as group" action; filtering keys off per-group repo-Id membership instead of a tree lookup. The Welcome sidebar tree (global) is untouched.

**Tech Stack:** C# / .NET 10, Avalonia 11.3, CommunityToolkit.Mvvm. No automated test project — verification is `dotnet build -c Release` via fork CI (no local SDK) + manual smoke.

**Spec:** `docs/superpowers/specs/2026-06-04-workspace-manual-groups-design.md`

**Base branch:** `feat/workspace-manual-groups` (off `master`; supersedes the still-open PR #3).

---

## Verification model (read first)

This repo has no unit tests and no local .NET SDK. Each code task ends with a **commit**; compilation is verified once in Task 11 by pushing the branch and running the fork CI (`gh workflow run ci.yml --repo shahaanf/GetHub --ref feat/workspace-manual-groups`). Do not claim "builds" until that run is green. Manual smoke steps are listed in Task 11.

---

## File Structure

- Create `src/ViewModels/WorkspaceGroup.cs` — the per-workspace group model.
- Modify `src/ViewModels/Workspace.cs` — add `Groups`.
- Modify `src/ViewModels/LauncherPage.cs` — add `RepoId`, remove `GetGroupName`.
- Modify `src/ViewModels/Preferences.cs` — remove `FindGroupRoot`/`ContainsRecursive`, sanitize group membership on load.
- Modify `src/ViewModels/Launcher.cs` — rewrite group operations, filtering, "Open as group", cleanup hooks.
- Modify `src/Views/Launcher.axaml` — always-visible strip + `+` button.
- Modify `src/Views/Launcher.axaml.cs` — `+` handler, group context menu (rename/delete/color), repo→group drop, assign menus.
- Modify `src/Views/Welcome.axaml.cs` — "Open as group" context entry.
- Modify `src/Resources/Locales/en_US.axaml` — new UI strings.

---

### Task 1: WorkspaceGroup model + Workspace.Groups

**Files:**
- Create: `src/ViewModels/WorkspaceGroup.cs`
- Modify: `src/ViewModels/Workspace.cs`

- [ ] **Step 1: Create the model**

```csharp
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
```

- [ ] **Step 2: Add `Groups` to `Workspace`** — after the `Repositories` property in `src/ViewModels/Workspace.cs`:

```csharp
        public List<WorkspaceGroup> Groups
        {
            get;
            set;
        } = [];
```

Ensure `using System.Collections.Generic;` is present (it already is, for `Repositories`).

- [ ] **Step 3: Commit**

```bash
git add src/ViewModels/WorkspaceGroup.cs src/ViewModels/Workspace.cs
git commit -m "feat(groups): add per-workspace WorkspaceGroup model"
```

---

### Task 2: LauncherPage.RepoId, remove GetGroupName

**Files:**
- Modify: `src/ViewModels/LauncherPage.cs:92-98`

- [ ] **Step 1: Replace `GetGroupName()` with `RepoId`** — delete the whole method:

```csharp
        public string GetGroupName()
        {
            if (_node == null || !_node.IsRepository)
                return Launcher.GroupAll;
            var group = Preferences.Instance.FindGroupRoot(_node.Id);
            return group?.Name ?? Launcher.GroupUngrouped;
        }
```

and replace with:

```csharp
        // Repo path Id of this page, or null for non-repo pages (welcome tab).
        public string RepoId => _node is { IsRepository: true } ? _node.Id : null;
```

- [ ] **Step 2: Commit**

```bash
git add src/ViewModels/LauncherPage.cs
git commit -m "feat(groups): expose LauncherPage.RepoId, drop tree-derived GetGroupName"
```

---

### Task 3: Preferences — remove tree-derivation, sanitize membership on load

**Files:**
- Modify: `src/ViewModels/Preferences.cs` (remove `FindGroupRoot` + `ContainsRecursive` around 573-597; extend `PrepareWorkspaces` ~742)

- [ ] **Step 1: Delete `FindGroupRoot` and `ContainsRecursive`** (both now unused):

```csharp
        public RepositoryNode FindGroupRoot(string id)
        {
            foreach (var root in RepositoryNodes)
            {
                if (root.IsRepository)
                    continue;

                if (ContainsRecursive(root, id))
                    return root;
            }
            return null;
        }

        private bool ContainsRecursive(RepositoryNode node, string id)
        {
            if (node.Id == id)
                return true;
            foreach (var sub in node.SubNodes)
            {
                if (ContainsRecursive(sub, id))
                    return true;
            }
            return false;
        }
```

Delete both methods entirely.

- [ ] **Step 2: Sanitize group membership in `PrepareWorkspaces`** — inside the existing `foreach (var workspace in Workspaces)` loop body in `PrepareWorkspaces()`, add:

```csharp
                if (workspace.Groups.Count > 0)
                {
                    var valid = new HashSet<string>(workspace.Repositories);
                    foreach (var group in workspace.Groups)
                        group.RepositoryIds.RemoveAll(id => !valid.Contains(id));
                }
```

Add `using System.Collections.Generic;` if not already imported (it is).

- [ ] **Step 3: Commit**

```bash
git add src/ViewModels/Preferences.cs
git commit -m "feat(groups): drop FindGroupRoot, prune stale group membership on load"
```

---

### Task 4: Launcher — rewrite strip build, filter, open, color, reorder

**Files:**
- Modify: `src/ViewModels/Launcher.cs` (const block ~15-16; `MoveGroup` 66-94; `SetGroupColor` 96-118; `ActiveGroup` 120-131; `RefreshGroups` 558-590; `ApplyGroupFilter` 592-597; `OpenGroupRepositories` 599-625; `OpenRepoDescendants` 627-646)

- [ ] **Step 1: Drop the `GroupUngrouped` constant** — line 16. Change:

```csharp
        public const string GroupAll = "All";
        public const string GroupUngrouped = "Ungrouped";
```

to:

```csharp
        public const string GroupAll = "All";
```

- [ ] **Step 2: Replace `MoveGroup`** (66-94) with the workspace-scoped version:

```csharp
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
```

- [ ] **Step 3: Replace `SetGroupColor`** (96-118):

```csharp
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
```

- [ ] **Step 4: Replace `RefreshGroups`** (558-590) — build the strip from the active workspace's groups:

```csharp
        public void RefreshGroups()
        {
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
```

- [ ] **Step 5: Replace `ApplyGroupFilter`** (592-597) — membership-based:

```csharp
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
```

- [ ] **Step 6: Replace `OpenGroupRepositories` and delete `OpenRepoDescendants`** (599-646) — open members by Id:

```csharp
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
```

(Delete the old `OpenRepoDescendants` method body entirely.)

- [ ] **Step 7: Commit**

```bash
git add src/ViewModels/Launcher.cs
git commit -m "feat(groups): build strip + filter + open from workspace groups"
```

---

### Task 5: Launcher — create / rename / delete / assign operations

**Files:**
- Modify: `src/ViewModels/Launcher.cs` (add public methods near the other group ops, e.g. after `SetGroupColor`)

- [ ] **Step 1: Add the group-management methods:**

```csharp
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
```

Confirm `using System;` is present (for `StringComparison`) — it is at the top of `Launcher.cs`.

- [ ] **Step 2: Commit**

```bash
git add src/ViewModels/Launcher.cs
git commit -m "feat(groups): create/rename/delete/assign workspace group ops"
```

---

### Task 6: Launcher — "Open as group", cleanup hooks, page-sync, switch reset

**Files:**
- Modify: `src/ViewModels/Launcher.cs` (`SwitchWorkspace` 216-261; `CloseRepositoryInTab` 516-529; `PostActivePageChanged` 550-555; add `OpenAsGroup` + `CollectDescendantRepoIds`)

- [ ] **Step 1: Add `OpenAsGroup` + `CollectDescendantRepoIds`:**

```csharp
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
```

- [ ] **Step 2: Strip closed repos from groups** — in `CloseRepositoryInTab`, change the `removeFromWorkspace` block:

```csharp
                if (removeFromWorkspace)
                    _activeWorkspace.Repositories.Remove(repo.FullPath);
```

to:

```csharp
                if (removeFromWorkspace)
                {
                    _activeWorkspace.Repositories.Remove(repo.FullPath);
                    foreach (var g in _activeWorkspace.Groups)
                        g.RepositoryIds.Remove(repo.FullPath);
                }
```

- [ ] **Step 3: Fix page→group auto-sync** — in `PostActivePageChanged`, replace:

```csharp
            if (_activeGroup != GroupAll && _activePage != null)
            {
                var pageGroup = _activePage.GetGroupName();
                if (pageGroup != _activeGroup)
                    ActiveGroup = pageGroup;
            }
```

with:

```csharp
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
```

- [ ] **Step 4: Reset + rebuild strip on workspace switch** — in `SwitchWorkspace`, immediately after `to.IsActive = true;` (line ~228) add:

```csharp
            _activeGroup = GroupAll;
```

and immediately before the closing `GC.Collect();` (line ~260) add:

```csharp
            RefreshGroups();
```

- [ ] **Step 5: Commit**

```bash
git add src/ViewModels/Launcher.cs
git commit -m "feat(groups): Open as group, close/switch cleanup, page sync"
```

---

### Task 7: Launcher.axaml — always-visible strip + `+` button

**Files:**
- Modify: `src/Views/Launcher.axaml:137-141` (strip Border visibility) and the strip body (~148-153, after the `ItemsControl`)

- [ ] **Step 1: Make the strip always visible** — remove the count condition so the `+` is always reachable. Change the strip `Border` open tag (137-141):

```xml
    <Border Grid.Row="1"
            Background="{DynamicResource Brush.TitleBar}"
            BorderThickness="0,0,0,1"
            BorderBrush="{DynamicResource Brush.Border0}"
            IsVisible="{Binding Groups.Count, Converter={x:Static c:IntConverters.IsGreaterThanOne}}">
```

to (drop the `IsVisible`):

```xml
    <Border Grid.Row="1"
            Background="{DynamicResource Brush.TitleBar}"
            BorderThickness="0,0,0,1"
            BorderBrush="{DynamicResource Brush.Border0}">
```

- [ ] **Step 2: Add a `+` button** — wrap the existing `ItemsControl` and a new `Button` in a horizontal `StackPanel`. Replace the `ItemsControl` opening line `<ItemsControl ItemsSource="{Binding Groups}" Height="30" VerticalAlignment="Bottom">` with a `StackPanel` wrapper, and add the button after the `</ItemsControl>`:

```xml
        <StackPanel Orientation="Horizontal" VerticalAlignment="Bottom">
          <ItemsControl ItemsSource="{Binding Groups}" Height="30" VerticalAlignment="Bottom">
```

…(existing `ItemsControl` content unchanged)… and after `</ItemsControl>`:

```xml
          <Button Classes="icon_button"
                  Width="22" Height="22"
                  Margin="2,0,0,4"
                  VerticalAlignment="Bottom"
                  ToolTip.Tip="{DynamicResource Text.Welcome.NewGroup}"
                  Click="OnAddGroup">
            <Path Width="10" Height="10"
                  Data="{StaticResource Icons.Add}"
                  Fill="{DynamicResource Brush.FG2}"/>
          </Button>
        </StackPanel>
```

(If `Icons.Add` is not a defined resource, use the same icon key the toolbar's "new tab" button uses — grep `Icons.` in `Launcher.axaml` and reuse an existing "plus"/"add" geometry.)

- [ ] **Step 3: Commit**

```bash
git add src/Views/Launcher.axaml
git commit -m "feat(groups): always-show strip with a + add-group button"
```

---

### Task 8: Launcher.axaml.cs — add handler, group menu, repo→group drop, assign menus

**Files:**
- Modify: `src/Views/Launcher.axaml.cs` (`OnGroupDrop` ~469-481; `OnGroupContextRequested` ~484-525; add `OnAddGroup`; the page-tab context menu builder; `_dndGroupFormat` declaration region)

- [ ] **Step 1: Find the repo drag data format.** Grep `_dnd` in `Launcher.axaml.cs` to find the existing tab-drag format constant (e.g. `_dndTabFormat` carrying a `LauncherPage`/repo path). Note its name; call it `<REPO_DND>` below. If page tabs are dragged with a `LauncherPage` reference, read `page.Node.Id`; if with a path string, use it directly.

- [ ] **Step 2: Add `OnAddGroup`** — a minimal name prompt via a `Flyout` with a `TextBox`. Add to the `Welcome`-less `Launcher` partial class:

```csharp
        private void OnAddGroup(object sender, RoutedEventArgs e)
        {
            if (sender is not Control anchor || DataContext is not ViewModels.Launcher vm)
                return;

            var box = new TextBox
            {
                Width = 180,
                Watermark = App.Text("Welcome.NewGroup"),
            };
            var flyout = new Flyout { Content = box, Placement = PlacementMode.Bottom };

            box.KeyDown += (_, ke) =>
            {
                if (ke.Key == Key.Enter)
                {
                    vm.CreateGroup(box.Text);
                    flyout.Hide();
                    ke.Handled = true;
                }
                else if (ke.Key == Key.Escape)
                {
                    flyout.Hide();
                    ke.Handled = true;
                }
            };

            flyout.ShowAt(anchor);
            box.Focus();
        }
```

Add `using Avalonia.Controls.Primitives;` (for `PlacementMode`) and confirm `Avalonia.Input` (`Key`) and `Avalonia.Interactivity` (`RoutedEventArgs`) usings exist; add if missing.

- [ ] **Step 3: Accept a repo dropped onto a group** — extend `OnGroupDrop` (after the existing group-reorder branch):

```csharp
        private void OnGroupDrop(object sender, DragEventArgs e)
        {
            if (sender is not Border bd || bd.DataContext is not ViewModels.LauncherGroup to || to.IsPseudo)
                return;
            if (DataContext is not ViewModels.Launcher vm)
                return;

            // Existing: reorder when a group name is dragged.
            if (e.DataTransfer.TryGetValue(_dndGroupFormat) is { Length: > 0 } fromName)
            {
                vm.MoveGroup(fromName, to.Name);
            }
            // New: assign when a repo tab is dragged onto the group.
            else if (e.DataTransfer.TryGetValue(<REPO_DND>) is { } dropped)
            {
                var repoId = ResolveDroppedRepoId(dropped); // page.Node.Id or path string
                if (!string.IsNullOrEmpty(repoId))
                    vm.AddRepoToGroup(to.Name, repoId);
            }

            _pressedGroup = false;
            _startDragGroup = false;
        }
```

Implement `ResolveDroppedRepoId` to match the existing tab-drag payload type discovered in Step 1 (return the repo path Id, or null for the welcome tab). If the existing payload is already a path string, `ResolveDroppedRepoId` just returns it.

- [ ] **Step 4: Add Rename/Delete to the group context menu** — in `OnGroupContextRequested`, after the existing color items are added to `menu`, append:

```csharp
            menu.Items.Add(new MenuItem { Header = "-" });

            var rename = new MenuItem { Header = App.Text("Welcome.RenameGroup") };
            rename.Click += (_, ev) =>
            {
                ShowRenameGroupFlyout(bd, group.Name);
                ev.Handled = true;
            };
            menu.Items.Add(rename);

            var delete = new MenuItem { Header = App.Text("Welcome.DeleteGroup") };
            delete.Click += (_, ev) =>
            {
                if (DataContext is ViewModels.Launcher vm)
                    vm.DeleteGroup(group.Name);
                ev.Handled = true;
            };
            menu.Items.Add(delete);
```

and add the rename flyout helper:

```csharp
        private void ShowRenameGroupFlyout(Control anchor, string oldName)
        {
            if (DataContext is not ViewModels.Launcher vm)
                return;

            var box = new TextBox { Width = 180, Text = oldName };
            var flyout = new Flyout { Content = box, Placement = PlacementMode.Bottom };
            box.KeyDown += (_, ke) =>
            {
                if (ke.Key == Key.Enter)
                {
                    vm.RenameGroup(oldName, box.Text);
                    flyout.Hide();
                    ke.Handled = true;
                }
                else if (ke.Key == Key.Escape)
                {
                    flyout.Hide();
                    ke.Handled = true;
                }
            };
            flyout.ShowAt(anchor);
            box.Focus();
            box.SelectAll();
        }
```

- [ ] **Step 5: Add "Add/Remove from group" to the page-tab context menu.** Find the handler that builds the page-tab (repo tab) context menu (grep `ContextRequested` in `Launcher.axaml.cs` for the tab strip, distinct from `OnGroupContextRequested`). For a tab whose `Node.IsRepository`, append a submenu:

```csharp
            if (page.Node is { IsRepository: true } && DataContext is ViewModels.Launcher vm && vm.ActiveWorkspace.Groups.Count > 0)
            {
                var addTo = new MenuItem { Header = App.Text("Welcome.AddToGroup") };
                foreach (var g in vm.ActiveWorkspace.Groups)
                {
                    var gname = g.Name;
                    var item = new MenuItem { Header = gname };
                    item.Click += (_, ev) => { vm.AddRepoToGroup(gname, page.Node.Id); ev.Handled = true; };
                    addTo.Items.Add(item);
                }
                menu.Items.Add(addTo);

                var removeFrom = new MenuItem { Header = App.Text("Welcome.RemoveFromGroup") };
                foreach (var g in vm.ActiveWorkspace.Groups)
                {
                    var gname = g.Name;
                    var item = new MenuItem { Header = gname };
                    item.Click += (_, ev) => { vm.RemoveRepoFromGroup(gname, page.Node.Id); ev.Handled = true; };
                    removeFrom.Items.Add(item);
                }
                menu.Items.Add(removeFrom);
            }
```

(Adjust the local variable name `page`/`menu` to match the actual handler. `ActiveWorkspace` is already a public property on `Launcher`.)

- [ ] **Step 6: Commit**

```bash
git add src/Views/Launcher.axaml.cs
git commit -m "feat(groups): + button, rename/delete menu, repo->group assign"
```

---

### Task 9: Welcome.axaml.cs — "Open as group" context entry

**Files:**
- Modify: `src/Views/Welcome.axaml.cs` (`OnTreeNodeContextRequested`, ~98-160)

- [ ] **Step 1: Add the menu item** — for any container node (folder OR repo-with-subnodes), offer "Open as group". Inside `OnTreeNodeContextRequested`, near the top of the menu build (before the existing repo/group items):

```csharp
                if (node.IsContainer && node.SubNodes.Count > 0)
                {
                    var openAsGroup = new MenuItem();
                    openAsGroup.Header = App.Text("Welcome.OpenAsGroup");
                    openAsGroup.Icon = this.CreateMenuIcon("Icons.Folder.Open");
                    openAsGroup.Click += (_, e) =>
                    {
                        var launcher = App.GetLauncher();
                        launcher?.OpenAsGroup(node);
                        e.Handled = true;
                    };
                    menu.Items.Add(openAsGroup);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
```

Confirm `App.GetLauncher()` returns the active `Launcher` (it is used elsewhere, e.g. `RepositoryNode.Open`). If it returns a different wrapper, route through the same path `RepositoryNode.Open` uses to reach the `Launcher` instance.

- [ ] **Step 2: Commit**

```bash
git add src/Views/Welcome.axaml.cs
git commit -m "feat(groups): Open as group action in the sidebar tree"
```

---

### Task 10: Localization strings

**Files:**
- Modify: `src/Resources/Locales/en_US.axaml`

- [ ] **Step 1: Add strings** — alongside the existing `Welcome.*` keys (grep `Welcome.OpenAllInNode` to find the block):

```xml
  <x:String x:Key="Text.Welcome.NewGroup" xml:space="preserve">New group</x:String>
  <x:String x:Key="Text.Welcome.RenameGroup" xml:space="preserve">Rename group</x:String>
  <x:String x:Key="Text.Welcome.DeleteGroup" xml:space="preserve">Delete group</x:String>
  <x:String x:Key="Text.Welcome.AddToGroup" xml:space="preserve">Add to group</x:String>
  <x:String x:Key="Text.Welcome.RemoveFromGroup" xml:space="preserve">Remove from group</x:String>
  <x:String x:Key="Text.Welcome.OpenAsGroup" xml:space="preserve">Open as group</x:String>
```

Match the exact key prefix convention used by neighbouring entries (e.g. `Text.Welcome.OpenAllInNode` → so `App.Text("Welcome.X")` resolves `Text.Welcome.X`). Verify by reading one existing `Welcome.*` entry and mirroring its key shape.

- [ ] **Step 2: Commit**

```bash
git add src/Resources/Locales/en_US.axaml
git commit -m "feat(groups): localization strings for group actions"
```

---

### Task 11: Build verification + manual smoke + close PR #3

- [ ] **Step 1: Push and run fork CI**

```bash
git push fork feat/workspace-manual-groups
gh workflow run ci.yml --repo shahaanf/GetHub --ref feat/workspace-manual-groups
```

Watch the run; it MUST be green (build + package on Windows + macOS) before proceeding. Fix any compile errors, commit, re-push, re-run.

- [ ] **Step 2: Download + launch the win-x64 artifact** (per the established workflow) and smoke-test:

- Create a group via `+`; it appears; persists after restart.
- Rename, color (right-click), delete a group.
- Drag a repo tab onto a group; right-click tab → Add to / Remove from group.
- Click a group tab → its member repos open + the strip filters to them; `All` shows everything.
- Multi-membership: add one repo to two groups; each filters it in.
- Two workspaces: groups differ; switching workspace swaps the strip; group members are workspace-scoped.
- Sidebar right-click a folder and a repo-container → "Open as group" opens its repos + creates/【merges】the group + selects it.
- Close a repo from the workspace → it drops out of its groups.

- [ ] **Step 3: Open PR + close the superseded one**

```bash
gh pr create --repo GrantDPowellMedrock/GetHub --base master \
  --head shahaanf:feat/workspace-manual-groups \
  --title "Per-workspace manual group tabs" --body "<summary + spec link + CI link>"
gh pr close 3 --repo GrantDPowellMedrock/GetHub \
  --comment "Superseded by per-workspace manual groups (#<new>)."
```

---

## Self-Review

- **Spec coverage:** data model (Task 1), strip per-workspace (Task 4), create/rename/delete/color/reorder (Tasks 4–5, 8), multi-membership + assign drag/menu (Tasks 5, 8), Open-as-group merge (Tasks 6, 9), filter/open (Task 4), cleanup on close/switch + load sanitize (Tasks 3, 6), supersede PR #3 / remove FindGroupRoot (Tasks 2–4, 11), no Ungrouped (Task 4 drops the const), strings (Task 10). All spec sections mapped.
- **Type consistency:** `WorkspaceGroup { Name, Bookmark, RepositoryIds }`, `Workspace.Groups`, `LauncherPage.RepoId`, `Launcher.ActiveWorkspace`, and method names (`CreateGroup`/`RenameGroup`/`DeleteGroup`/`AddRepoToGroup`/`RemoveRepoFromGroup`/`OpenAsGroup`/`OpenRepositoryById`/`CollectDescendantRepoIds`) are used consistently across tasks.
- **Open placeholders to resolve during execution (not plan gaps):** the repo drag-data format name (`<REPO_DND>`/`ResolveDroppedRepoId`, Task 8 Step 1) and the `Icons.Add` geometry key (Task 7) must be read from the existing code — each task says where to look and what to mirror.
