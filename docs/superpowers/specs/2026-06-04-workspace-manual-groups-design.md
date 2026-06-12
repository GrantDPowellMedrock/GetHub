# Per-Workspace Manual Group Tabs — Design

Date: 2026-06-04
Status: Approved (pending written-spec review)

## Context

GetHub shows a group-tab strip above the page tabs. Today the strip is **derived
automatically** from the global `RepositoryNodes` sidebar tree: every top-level
folder node becomes a group tab. This has two problems:

1. **Confusing overlap with workspaces.** A workspace already is "a set of repos
   you work with"; a derived group is also "a set of repos," on a different axis,
   with no clear relationship. Group content is global, so a group can reference
   repos that are not part of the active workspace.
2. **Fragile / invisible.** Group tabs only appear if the tree happens to be
   structured a certain way. After the nested-subrepo work, a repository can also
   be a container, and the derivation did not reliably surface those as tabs.

This redesign makes group tabs **manual and scoped to the workspace**, with an
explicit action to populate a group from an existing sidebar folder/container.

## Goals

- Group tabs are owned by the active workspace; switching workspace shows that
  workspace's groups.
- The user creates, renames, deletes, colors, and reorders groups directly in the
  topbar.
- A repo can belong to 0..N groups within its workspace (multi-membership).
- Assign repos to a group by drag (open repo tab → group tab) or via right-click.
- An "Open as group" action on a sidebar folder/container autopopulates a
  workspace group with that node's repos and opens them.

## Non-Goals

- No global/shared groups.
- No automatic seeding of groups on load or on scan.
- No change to the Welcome sidebar tree (the global library, including nested
  sub-repos from the prior PRs, is untouched).
- No "Ungrouped" pseudo-tab.

## Data Model

Groups live on the `Workspace`, persisted in `preferences.json` with the rest of
the workspace list.

```csharp
public class WorkspaceGroup : ObservableObject
{
    public string Name { get; set; }            // unique within the workspace
    public int Bookmark { get; set; }           // color index, 0 = none
    public List<string> RepositoryIds { get; set; } = [];   // repo paths (Id)
}

// on Workspace:
public List<WorkspaceGroup> Groups { get; set; } = [];
```

Invariant: every entry in `WorkspaceGroup.RepositoryIds` is also present in
`Workspace.Repositories`. Membership references repos by normalized path Id (the
same Id used by `RepositoryNode`).

`LauncherGroup` (the existing view-model row for the strip) is kept as the
display item: `{ Name, Bookmark, IsPseudo }`. Only `All` is pseudo now.

## Strip Composition

`RefreshGroups()` rebuilds `Groups` (the `AvaloniaList<LauncherGroup>`) as:

1. `All` (pseudo, always first)
2. one `LauncherGroup` per `ActiveWorkspace.Groups`, in stored order
3. a trailing `+` affordance (rendered by the view, not a `LauncherGroup`)

The strip is always visible so the `+` is reachable even with zero groups.
`RefreshGroups()` is triggered on: workspace switch, any group mutation, and
page-collection changes (to keep `All` filtering correct).

## Operations (on `Launcher`, each persists + refreshes)

All operate on `ActiveWorkspace.Groups`. Names are unique within the workspace
(case-insensitive); creation/rename rejects duplicates and empty names.

- `CreateGroup(name)` — append empty group.
- `RenameGroup(oldName, newName)` — rename; reject duplicate/empty.
- `DeleteGroup(name)` — remove group; member repos simply lose this membership
  (repos themselves and their tabs are unaffected).
- `SetGroupColor(name, bookmark)` — set `Bookmark` (existing method, retargeted
  from tree nodes to `WorkspaceGroup`).
- `MoveGroup(fromName, toName)` — reorder within `ActiveWorkspace.Groups`
  (existing method, retargeted).
- `AddRepoToGroup(groupName, repoId)` — add if `repoId ∈ Workspace.Repositories`
  and not already a member.
- `RemoveRepoFromGroup(groupName, repoId)`.

## Assigning Repos

Two paths, both call `AddRepoToGroup`:

1. **Drag**: drag an open page tab (repo) onto a group tab. `OnGroupDrop` gains a
   second data format (repo Id) alongside the existing group-reorder format.
2. **Right-click** a page tab or a sidebar repo node → **Add to group ▸ [list]**
   and **Remove from group ▸ [list]** built from `ActiveWorkspace.Groups`.

## "Open as group" (autopopulate)

Right-click a sidebar folder node **or** a repo-container (a repository that has
sub-nodes) → **Open as group**:

1. Collect all descendant repository Ids under the node (depth-first; include the
   node itself if it is a repository).
2. For each Id: ensure it is in `ActiveWorkspace.Repositories` and open its tab
   (reuse `OpenRepositoryInTab`; skip already-open).
3. Find a workspace group whose `Name` equals the node `Name`:
   - exists → merge the collected Ids into its `RepositoryIds` (dedupe).
   - none → create a new `WorkspaceGroup { Name = node.Name, RepositoryIds = ids }`.
4. `RefreshGroups()` and set `ActiveGroup = node.Name`.

This is the only path that pre-fills a group; it is explicit and user-initiated.

## Filtering & Opening

- `ActiveGroup` setter → `OpenGroupRepositories(name)` then `ApplyGroupFilter()`.
- `OpenGroupRepositories(name)`: for `All`, no-op; otherwise open each member
  `repoId` not already open.
- `ApplyGroupFilter()`: for each page, `IsInActiveGroup = (ActiveGroup == All) ||
  activeGroup.RepositoryIds.Contains(page.RepoId)`.
- Replace `LauncherPage.GetGroupName()` (single-group, tree-derived) with a repo
  Id accessor `RepoId` used by membership checks. Page→active-group auto-sync
  (when selecting a page outside the active group) falls back to `All`.

## Cleanup / Consistency

- Removing a repo from the workspace (permanent close / remove) → strip its Id
  from every group in that workspace.
- Deleting a workspace removes its groups with it (they are owned by it).
- On load, drop any `RepositoryId` not present in `Workspace.Repositories`
  (defensive against hand-edited prefs).

## Supersedes

This removes the tree-derived tab logic added in PR #3
(`feat/group-tabs-repo-containers`): `RefreshGroups`, `OpenGroupRepositories`,
`SetGroupColor`, `MoveGroup`, and `Preferences.FindGroupRoot` stop scanning
`RepositoryNodes` for tabs. PR #3 should be closed in favor of this. The
`RepositoryNode.IsContainer` property (from PR #1–#2) stays — it still drives
sidebar tree expansion and identifies repo-containers for "Open as group."

## Files Touched

- `src/ViewModels/Workspace.cs` — add `Groups`.
- `src/ViewModels/WorkspaceGroup.cs` — new model.
- `src/ViewModels/Launcher.cs` — group operations, filter/open, "Open as group",
  workspace-switch refresh, cleanup hooks.
- `src/ViewModels/LauncherPage.cs` — `RepoId`; remove `GetGroupName`.
- `src/ViewModels/Preferences.cs` — remove tab-derivation use of `FindGroupRoot`;
  workspace-removal cleanup.
- `src/Views/Launcher.axaml(.cs)` — `+` affordance, group context menu
  (rename/delete/color), repo-onto-group drop, add/remove-from-group menus.
- `src/Views/Welcome.axaml.cs` — "Open as group" context-menu entry.
- `src/Resources/Locales/en_US.axaml` — new strings.

## Testing

GetHub has no automated test project; verification is a build + manual smoke test
via fork CI artifact (per established workflow). Manual checks:

- Create/rename/delete/color/reorder a group; persists across restart.
- Multi-membership: a repo in two groups; filter by each shows it.
- Drag a repo tab onto a group; right-click add/remove.
- Two workspaces have independent group sets; switching swaps the strip.
- "Open as group" on a folder and on a repo-container populates + opens + selects.
- Same-name "Open as group" merges rather than duplicating.
- Remove a repo from the workspace → it disappears from its groups.
- Build passes on fork CI (Windows + macOS).
