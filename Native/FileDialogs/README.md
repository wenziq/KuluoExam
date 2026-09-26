# 桌面系统文件选择器

关卡工坊和关卡集的导入、导出统一通过 `RuntimeFileDialogService` 调用原生窗口。

- Unity 编辑器：`EditorUtility.OpenFilePanelWithFilters` / `SaveFilePanel`。
- Windows Player：系统 Common Item Dialog，通过 STA 线程调用 `IFileDialog`；系统负责文件导航、筛选和覆盖确认。
- macOS Player：AppKit `NSOpenPanel` / `NSSavePanel`，在 Unity 主线程执行。动态库包含 arm64、x86_64 两种架构，仅导入 macOS。
- 其他 Player 平台尚未提供适配，界面显示明确错误。

文件格式固定为 `.sokopack.json`。选择器只返回路径；读取、验证、导出和文件事务仍由既有 `ImportExportService` 处理。取消返回空结果，不改写数据。保存确认后不再修改路径，以免绕过系统对同名文件的覆盖确认。

## 重新构建 macOS 桥接库

在安装 Xcode Command Line Tools 的 Mac 上，从工程根目录执行：

```sh
sh Native/FileDialogs/build-macos.sh
```

产物：`Assets/Project/Plugins/macOS/libSokobanFileDialogs.dylib`。源文件和动态库一起保留；正常 Unity 构建直接使用已编译动态库，无需额外包或网络服务。

## 验证方式

`FilePickerTests` 的原生服务测试注入路径结果，覆盖取消、中文路径、目录记忆、错误、重复点击及对象销毁。旧 `FilePickerView` 仅由显式 `InGameFileDialogService` 适配器用于隔离 UI 回归，桌面产品默认不使用它；这些测试不能替代系统窗口实测。

验证版 U16 Player 探针可以加 `--sokoban-native-picker true`，保留人工操作系统窗口，后续导出/导入验证仍自动完成。测试数据必须使用独立目录。结果见 `Docs/TEST_REPORT.md` 的系统文件窗口章节。

接口参考：[Microsoft Common Item Dialog](https://learn.microsoft.com/en-us/windows/win32/shell/common-file-dialog)、[Apple NSSavePanel](https://developer.apple.com/documentation/appkit/nssavepanel)。
