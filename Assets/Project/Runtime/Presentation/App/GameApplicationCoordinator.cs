using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sokoban.Core.Data;
using Sokoban.Domain.Gameplay;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Views;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.App
{
    public sealed class GameApplicationCoordinator : MonoBehaviour
    {
        public ApplicationController App { get; private set; }
        public UserDataPaths DataPaths => paths;
        public IFileSystem Files { get; private set; }
        public ContentCatalog Catalog { get; private set; }
        public ProgressService Progress { get; private set; }
        public PackData ActivePack { get; private set; }
        public string ActiveLevelId { get; private set; }
        public GameplayView Gameplay { get; private set; }
        public bool ProgressSaved { get; private set; } = true;
        public string ProgressError { get; private set; }
        public Task LastSave { get; private set; } = Task.CompletedTask;
        public Task<TransactionResult> LastPackRemoval { get; private set; } = Task.FromResult<TransactionResult>(null);
        public event Action<string> ExtensionRequested;
        public event Action<CatalogEntry> EditRequested;
        private ProgressRepository repository;
        private UserDataPaths paths;
        private string builtInDirectory;
        private ResultsView results;
        private bool initialized;
        public bool Initialized => initialized && Progress != null && Catalog != null;
        private bool loadFailed;
        private int saveRevision;
        private bool pendingProgressFailure;
        public void Initialize(ApplicationController app, string dataRoot = null, string builtInRoot = null, IFileSystem files = null)
        {
            if (initialized) return;
            initialized = true;
            App = app;
            paths = new UserDataPaths(dataRoot ?? Application.persistentDataPath);
            builtInDirectory = builtInRoot ?? Path.Combine(Application.streamingAssetsPath, "BuiltInPacks");
            Files = files ?? new PhysicalFileSystem();
            repository = new ProgressRepository(paths, Files);
            var loaded = repository.Load();
            loadFailed = !loaded.Succeeded;
            Progress = new ProgressService(loaded.Snapshot);
            ProgressError = loaded.Error;
            ProgressSaved = !loadFailed;
            Catalog = new ContentCatalog();
            Catalog.Load(builtInDirectory, paths, Files);
            ActivePack = Catalog.Entries.FirstOrDefault(e => !e.IsDraft && e.PackId == Progress.RecentPackId)?.Pack ?? Catalog.Entries.FirstOrDefault(e => !e.IsDraft)?.Pack;
            App.NavigationRequested += Navigate;
            App.CommandRequested += Command;
            var workshop = GetComponent<Sokoban.Runtime.Presentation.Workshop.WorkshopApplicationCoordinator>() ?? gameObject.AddComponent<Sokoban.Runtime.Presentation.Workshop.WorkshopApplicationCoordinator>();
            workshop.Initialize(this);
            var persistence=GetComponent<Sokoban.Runtime.Presentation.Workshop.WorkshopPersistenceController>()??gameObject.AddComponent<Sokoban.Runtime.Presentation.Workshop.WorkshopPersistenceController>();
            persistence.Initialize(workshop,paths,Files);
            var analysis=GetComponent<Sokoban.Runtime.Presentation.Analysis.AnalysisMenuController>()??gameObject.AddComponent<Sokoban.Runtime.Presentation.Analysis.AnalysisMenuController>();
            analysis.Initialize(workshop);
            var apply=GetComponent<Sokoban.Runtime.Presentation.Controls.ApplyPackModal>()??gameObject.AddComponent<Sokoban.Runtime.Presentation.Controls.ApplyPackModal>();
            apply.Initialize(workshop);
            var exchange=GetComponent<Sokoban.Runtime.Presentation.Controls.ImportPreviewModal>()??gameObject.AddComponent<Sokoban.Runtime.Presentation.Controls.ImportPreviewModal>();
            exchange.Initialize(workshop);
            var settings=GetComponent<SettingsController>()??gameObject.AddComponent<SettingsController>();settings.Initialize(App,paths,Files);
            App.Navigate("MainMenu");
            if (loadFailed || Catalog.Errors.Count > 0)
                App.Modal.Show("本地内容读取提示", (ProgressError ?? "") + "\n" + string.Join("\n", Catalog.Errors));
        }
        public void ReloadCatalog()
        {
            Catalog.Load(builtInDirectory, paths, Files);
            var replacement = Catalog.Entries.FirstOrDefault(e => !e.IsDraft && e.PackId == ActivePack?.packId)?.Pack;
            // A running formal session retains its pack snapshot and order until the player leaves it.
            if (Gameplay == null || App.CurrentPage != "Gameplay")
                ActivePack = replacement ?? Catalog.Entries.FirstOrDefault(e => !e.IsDraft)?.Pack;
        }
        private T Page<T>(string name) where T : Component
        {
            var prefab = Resources.Load<GameObject>("UI/Pages/" + name);
            if (prefab != null) return Instantiate(prefab, App.pageRoot).GetComponent<T>();
            var root = UiFactory.Rect(name, App.pageRoot);
            UiFactory.Fill(root);
            return root.gameObject.AddComponent<T>();
        }
        private void Navigate(string page)
        {
            if (page == "Gameplay") return;
            Gameplay = null;
            results = null;
            App.ClearPage();
            switch (page)
            {
                case "MainMenu": Page<MainMenuView>(page).Build(App, ActivePack, Progress, Continue); break;
                case "PackLibrary":
                    ReloadCatalog();
                    Page<PackLibraryView>(page).Build(App, Catalog.Entries, entry => SelectPack(entry.Pack), entry =>
                    {
                        if (EditRequested != null) EditRequested(entry);
                        else ShowExtension("Workshop");
                    }, delete:ConfirmDeletePack);
                    break;
                case "LevelSelect":
                    if (ActivePack != null) Page<LevelSelectView>(page).Build(App, ActivePack, Progress, StartLevel);
                    else App.Navigate("PackLibrary");
                    break;
                default: ShowExtension(page); break;
            }
        }
        private void ShowExtension(string page)
        {
            if (ExtensionRequested != null) { ExtensionRequested(page); return; }
            var heading = UiFactory.Text("PageTitle", App.pageRoot, page == "Workshop" ? "关卡工坊" : page == "Settings" ? "设置" : "帮助", App.theme, 30);
            UiFactory.Place(heading.rectTransform, 40, 40, 650, 50);
            var message = UiFactory.Text("PageStatus", App.pageRoot, "此页面将在对应实现单元接入。当前可返回主菜单体验完整闯关流程。", App.theme, 15);
            UiFactory.Place(message.rectTransform, 40, 106, 900, 70);
            var back = App.Button(App.pageRoot, "返回主菜单", () => App.Navigate("MainMenu"));
            UiFactory.Place((RectTransform)back.transform, 40, 205, 180, 42);
        }
        private void Command(string command)
        {
            if(command=="ImportPack"){GetComponent<Sokoban.Runtime.Presentation.Controls.ImportPreviewModal>().BeginImport();return;}
            if(command=="PendingImports"){GetComponent<Sokoban.Runtime.Presentation.Controls.ImportPreviewModal>().ShowPending();return;}
            if (command == "Exit")
            {
                App.Modal.Show("退出推箱子", ProgressSaved ? "确认退出游戏？" : "正式进度尚未保存。退出后可能丢失本次成绩，可先重试保存。");
                if (!ProgressSaved) App.Modal.AddAction("重试保存", () => { LastSave = SaveProgressAsync(); });
                App.Modal.AddAction("退出", () => Application.Quit(), false);
            }
            else App.Navigate(command);
        }
        public void ConfirmDeletePack(CatalogEntry entry)
        {
            if(entry==null||entry.Source!=ContentSource.Installed)return;
            if(!LastPackRemoval.IsCompleted){App.Modal.Show("正在删除关卡集","请等待本次操作完成。");return;}
            App.Modal.Show("删除关卡集？","将从正式游玩列表中删除“"+entry.Pack.name+"”。\n工坊草稿和历史成绩会保留。之后可以从工坊重新加入，或重新导入。" );
            Sokoban.Runtime.Presentation.Controls.UnsavedChangesModal.RemoveDone(App);
            App.Modal.AddAction("取消",App.Modal.Close,false);
            App.Modal.AddAction("删除关卡集",()=>
            {
                if(!LastPackRemoval.IsCompleted)return;
                App.Modal.Show("正在删除关卡集","正在移除本机可玩版本…");
                Sokoban.Runtime.Presentation.Controls.UnsavedChangesModal.RemoveDone(App);
                LastPackRemoval=RemovePackAsync(entry);
            });
        }
        private async Task<TransactionResult> RemovePackAsync(CatalogEntry entry)
        {
            var result=await new InstalledPackRepository(paths,Files).RemoveAsync(entry.PackId);
            if(this==null)return result;
            if(result.Committed)
            {
                ReloadCatalog();
                if(Progress.RecentPackId==entry.PackId)
                {
                    Progress.RecentPackId=ActivePack?.packId??"";
                    LastSave=SaveProgressAsync();
                }
                if(App.CurrentPage=="PackLibrary")App.Navigate("PackLibrary");
            }
            else App.Modal.Show("删除未完成","关卡集仍然保留，可稍后重试。\n"+result.Error?.Message);
            return result;
        }
        public void SelectPack(PackData pack)
        {
            ContentCatalog.ValidatePlayable(pack);
            ActivePack = pack.DeepCopy();
            Progress.RecentPackId = pack.packId;
            LastSave = SaveProgressAsync();
            App.Navigate("LevelSelect");
        }
        public void Continue()
        {
            if (ActivePack == null) { App.Navigate("PackLibrary"); return; }
            string id = ActivePack.levelOrder.FirstOrDefault(x => UnlockPolicyEvaluator.CanEnter(ActivePack, x, Progress) && Progress.Best(ActivePack.packId, ActivePack.levels.Find(l => l.levelId == x)) == null) ?? ActivePack.levelOrder[0];
            StartLevel(id);
        }
        public void StartLevel(string levelId)
        {
            if (ActivePack == null || !UnlockPolicyEvaluator.CanEnter(ActivePack, levelId, Progress)) return;
            ActiveLevelId = levelId;
            Progress.RecentPackId = ActivePack.packId;
            App.Navigate("Gameplay");
            App.ClearPage();
            var session = new GameSession(ActivePack.levels.Find(l => l.levelId == levelId), SessionMode.Formal);
            Gameplay = Page<GameplayView>("Gameplay");
            Gameplay.Build(App, ActivePack, levelId, session, () => App.Navigate("LevelSelect"));
            Gameplay.CompletionCommitted += RecordCompletion;
            Gameplay.Completed += Complete;
            Gameplay.PauseRequested += Pause;
            Gameplay.RestartRequested += ConfirmRestart;
        }
        private void RecordCompletion()
        {
            Progress.Record(ActivePack.packId, Gameplay.Session);
            LastSave = SaveProgressAsync();
        }
        private void Complete()
        {
            pendingProgressFailure = false;
            var view = Gameplay;
            string next = UnlockPolicyEvaluator.Next(ActivePack, ActiveLevelId);
            results = new ResultsView(App, view.Session, UnlockPolicyEvaluator.AllComplete(ActivePack, Progress), next == null ? (Action)null : () => StartLevel(next), () => StartLevel(ActiveLevelId), () => App.Navigate("LevelSelect"), () => { App.Modal.Close(); view.Undo(); });
            if (LastSave.IsCompleted)
                results.SetSaveState(ProgressSaved, ProgressError, () => { LastSave = SaveProgressAsync(); });
        }
        public async Task SaveProgressAsync()
        {
            int revision = ++saveRevision;
            ProgressSaved = false;
            if (loadFailed)
            {
                ProgressError = "启动时读取进度失败，已暂停写入以保留原文件。请修复或从有效备份恢复后重新启动。";
                results?.SetSaveState(false, ProgressError, () => { LastSave = SaveProgressAsync(); });
                return;
            }
            var outcome = await repository.SaveAsync(Progress.Snapshot());
            if (this == null || revision != saveRevision) return;
            ProgressSaved = outcome.Committed;
            if (ProgressSaved) pendingProgressFailure = false;
            ProgressError = outcome.Committed ? null : outcome.Error?.Message ?? "写入未完成。";
            if (results != null && results.IsCurrent)
                results.SetSaveState(ProgressSaved, ProgressError, () => { LastSave = SaveProgressAsync(); });
            else if (!ProgressSaved) pendingProgressFailure = true;
        }
        private void Update()
        {
            if (!initialized || !pendingProgressFailure || App.Modal.IsOpen || Gameplay?.HasPendingCompletion == true) return;
            pendingProgressFailure = false;
            App.Modal.Show("成绩保存失败", "当前完成记录仍保留，可重试保存。\n" + ProgressError);
            App.Modal.AddAction("重试保存", () => { App.Modal.Close(); LastSave = SaveProgressAsync(); });
        }
        private void Pause()
        {
            if (Gameplay == null || Gameplay.Session.IsCompleted) return;
            App.Modal.Show("暂停", "休息一下，再继续下一次推动。");
            App.Modal.AddAction("继续", App.Modal.Close);
            App.Modal.AddAction("重开", ConfirmRestart, false);
            App.Modal.AddAction("返回选关", () => App.Navigate("LevelSelect"), false);
        }
        private void ConfirmRestart()
        {
            if (Gameplay == null) return;
            if (Gameplay.Session.Moves == 0) { App.Modal.Close(); Gameplay.Restart(); return; }
            App.Modal.Show("重开本关？", "本局移动记录将清空，已经保存的历史成绩会保留。");
            App.Modal.AddAction("重开", () => { App.Modal.Close(); Gameplay.Restart(); });
        }
        private void OnDestroy()
        {
            if (App == null) return;
            App.NavigationRequested -= Navigate;
            App.CommandRequested -= Command;
        }
    }
}
