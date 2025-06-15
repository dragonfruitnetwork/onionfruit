//
//  daemonloaderApp.swift
//  daemonloader
//
//  Created by Albie Spriddell on 04/06/2025.
//

import SwiftUI

class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }
}

@main
struct daemonloaderApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var appDelegate

    init() {
        UserDefaults.standard.set(false, forKey: "NSFullScreenMenuItemEverywhere")
    }

    var body: some Scene {
        WindowGroup {
            ContentView()
                .frame(width: 500, height: 150)
                .overlay(WindowAccessor())
        }
        .commands {
            CommandGroup(replacing: .newItem) { }
        }
    }
}

struct WindowAccessor: NSViewRepresentable {
    func makeNSView(context: Context) -> NSView {
        let view = NSView()
        DispatchQueue.main.async {
            if let window = view.window ?? NSApp.windows.first {
                let size = NSSize(width: 500, height: 150)
                window.minSize = size
                window.maxSize = size
                window.setContentSize(size)
                
                window.tabbingMode = .disallowed
                window.styleMask.remove([.resizable, .fullScreen])
            }
        }
        return view
    }
    func updateNSView(_ nsView: NSView, context: Context) {}
}
