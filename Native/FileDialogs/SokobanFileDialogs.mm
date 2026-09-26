#import <AppKit/AppKit.h>
#include <stdlib.h>
#include <string.h>

// Called on Unity's main thread. The system owns navigation, filtering and overwrite confirmation.
extern "C" __attribute__((visibility("default"))) char *SokobanShowFilePanel(int save, const char *name, const char *directory) {
    @autoreleasepool {
        if (![NSThread isMainThread]) return NULL;
        NSSavePanel *panel;
        if (save) {
            panel = [NSSavePanel savePanel];
            panel.nameFieldStringValue = [NSString stringWithUTF8String:name ?: ""];
            panel.canCreateDirectories = YES;
        } else {
            NSOpenPanel *open = [NSOpenPanel openPanel];
            open.canChooseFiles = YES; open.canChooseDirectories = NO; open.allowsMultipleSelection = NO;
            panel = open;
        }
        panel.title = save ? @"导出推箱子关卡包" : @"导入推箱子关卡包";
        panel.message = @"推箱子关卡包（.sokopack.json）";
        panel.prompt = save ? @"保存" : @"打开";
        // Compound extensions are supported by the native panel; keep the entire document suffix.
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
        panel.allowedFileTypes = @[@"sokopack.json"];
#pragma clang diagnostic pop
        panel.allowsOtherFileTypes = NO;
        panel.extensionHidden = NO;
        if (directory && directory[0]) panel.directoryURL = [NSURL fileURLWithPath:[NSString stringWithUTF8String:directory] isDirectory:YES];
        [NSApp activateIgnoringOtherApps:YES];
        if ([panel runModal] != NSModalResponseOK) return NULL;
        return strdup(panel.URL.path.UTF8String);
    }
}
extern "C" __attribute__((visibility("default"))) void SokobanFreeFilePanelResult(char *result) { free(result); }
