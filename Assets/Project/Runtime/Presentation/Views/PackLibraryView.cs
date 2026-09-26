using System;
using System.Collections.Generic;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Board;
using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Views
{
    public sealed class PackLibraryView : PageView
    {
        public void Build(ApplicationController app, IReadOnlyList<CatalogEntry> entries, Action<CatalogEntry> select, Action<CatalogEntry> edit, ContentSource? filter = null, bool draftsOnly = false, Action<CatalogEntry> delete = null, Action resume = null)
        {
            Begin(app);
            PageHeading(draftsOnly ? "WORKSHOP COLLECTION" : "YOUR PUZZLE COLLECTION",
                draftsOnly ? "工坊关卡集" : "关卡集",
                draftsOnly ? "制作内容保存在这里。加入可玩关卡集后，才会出现在正式游玩列表。" : "选择已准备好的关卡开始游玩。制作草稿保存在关卡工坊。" );
            if (draftsOnly)
            {
                Right(Action("NewPack", "+ 新建草稿",0,157,130,()=>app.Command("NewPack"),true),40,157);
                Right(Action("CopyExample", "从示例复制",0,157,120,()=>app.Command("CopyExample")),180,157);
                if (resume != null) Action("ResumeDraft", "继续编辑当前草稿",40,157,175,resume);
            }
            else
            {
                Right(Action("Import", "导入关卡包",0,157,125,()=>app.Command("ImportPack")),40,157);
                Right(Action("PendingImports", "待验证导入",0,157,125,()=>app.Command("PendingImports")),175,157);
                Right(Action("OpenWorkshopLibrary", "工坊草稿",0,157,115,()=>app.Navigate("WorkshopLibrary")),310,157);
                string[] labels = { "全部可玩关卡", "内置示例", "我的可玩关卡" };
                ContentSource?[] filters = { null, ContentSource.BuiltIn, ContentSource.Installed };
                for (int i = 0; i < filters.Length; i++)
                {
                    ContentSource? selected = filters[i];
                    Action("Filter" + i, labels[i], 40 + i * 145, 157, 135, () =>
                    {
                        for (int c = Root.childCount - 1; c >= 0; c--)
                        {
                            Root.GetChild(c).gameObject.SetActive(false);
                            Destroy(Root.GetChild(c).gameObject);
                        }
                        Build(app, entries, select, edit, selected, draftsOnly, delete, resume);
                    }, selected == filter);
                }
            }
            var content = ScrollBody(240,24,366);
            int count = 0;
            foreach (var entry in entries)
            {
                if (entry.IsDraft != draftsOnly) continue;
                if (filter.HasValue && filter.Value != entry.Source) continue;
                count++;
                var pack = entry.Pack;
                var card = UiFactory.Panel("PackCard", content, Theme.panel, true).rectTransform;
                UiFactory.Round(card.GetComponent<UnityEngine.UI.Image>());

                var board = BoardView.Create(card);
                UiFactory.Fill((RectTransform)board.transform,16,179,16,16);
                board.fitPadding = new Vector2(25,25);board.maxCellSize=23;
                if (pack.levels.Count > 0) board.Show(pack.levels[0]);
                var name = UiFactory.Text("Name", card, pack.name, Theme, 20);
                UiFactory.Fill(name.rectTransform,20,119,20,205);
                var description = UiFactory.Text("Description", card, pack.description, Theme, 14, Theme.secondary);
                UiFactory.Fill(description.rectTransform,20,80,20,246);
                var meta = UiFactory.Text("Meta", card, entry.CategoryLabel + "  ·  " + pack.levels.Count + " 个关卡  ·  " + (entry.IsDraft ? "草稿，不直接用于正式游戏" : pack.unlockPolicy == Sokoban.Core.Data.UnlockPolicy.Sequential ? "顺序解锁" : "全部可选"), Theme, 13, Theme.secondary);
                UiFactory.Fill(meta.rectTransform,20,58,20,284);
                var play = UiFactory.Button("SelectPack", card, entry.IsDraft ? "打开草稿  →" : "选择关卡  →", Theme, () => { if (entry.IsDraft) edit(entry); else select(entry); }, true);
                var pr=(RectTransform)play.transform;pr.anchorMin=new Vector2(0,0);pr.anchorMax=new Vector2(entry.IsDraft?1:.57f,0);pr.offsetMin=new Vector2(20,20);pr.offsetMax=new Vector2(-8,56);
                if (!entry.IsDraft)
                {
                    var copy = UiFactory.Button("CopyPack", card, "创建编辑副本", Theme, () => edit(entry));
                    var cr=(RectTransform)copy.transform;cr.anchorMin=new Vector2(.57f,0);cr.anchorMax=new Vector2(1,0);cr.offsetMin=new Vector2(0,20);cr.offsetMax=new Vector2(-20,56);
                }
                if ((entry.IsDraft || entry.Source == ContentSource.Installed) && delete != null)
                {
                    var remove = UiFactory.Button("DeletePack", card, "删除关卡集", Theme, () => delete(entry));
                    var rr = (RectTransform)remove.transform;
                    rr.anchorMin = rr.anchorMax = Vector2.one; rr.pivot = Vector2.one;
                    rr.anchoredPosition = new Vector2(-12,-12); rr.sizeDelta = new Vector2(105,32);
                    app.Tooltip(remove, entry.IsDraft ? "删除这份工坊草稿，保留已加入的可玩版本和历史成绩" : "删除本机可玩版本，保留工坊草稿和历史成绩");
                }
                app.Tooltip(play, pack.name);
            }
            if (count == 0)
            {
                var empty = UiFactory.Text("Empty", content, draftsOnly ? "这里还没有制作草稿。\n新建一份草稿，或从示例复制开始。" : "这里还没有可玩的关卡集。\n在工坊完成制作后，选择“加入可玩关卡集”。", Theme, 20);
                UiFactory.Preferred(empty.gameObject, 100);
                var create = UiFactory.Button("EmptyCreate", content, draftsOnly ? "新建草稿" : "前往工坊草稿", Theme, () => { if (draftsOnly) app.Command("NewPack"); else app.Navigate("WorkshopLibrary"); }, true);
                UiFactory.Preferred(create.gameObject, 44);
                var copy = UiFactory.Button("EmptyCopy", content, "从示例复制", Theme, () => app.Command("CopyExample"));
                UiFactory.Preferred(copy.gameObject, 44);
            }
        }
        static void Right(UnityEngine.UI.Button button,float right,float top)
        {var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=Vector2.one;rect.pivot=Vector2.one;rect.anchoredPosition=new Vector2(-right,-top);}
    }
}
