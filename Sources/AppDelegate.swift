import AppKit
import ServiceManagement

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuItemValidation {
    static let winds: [CGFloat] = [-90, -35, 0, 35, 90]

    private var statusItem: NSStatusItem!
    private var overlays: [NSWindow] = []
    private var wallWindows: [NSWindow] = []
    private let sim = Simulation.shared

    func applicationDidFinishLaunching(_ notification: Notification) {
        buildOverlays()
        buildMenu()
        sim.onFullscreenChange = { [weak self] set in self?.applyFullscreen(set) }
        sim.start()
        NotificationCenter.default.addObserver(self, selector: #selector(screensChanged),
                                               name: NSApplication.didChangeScreenParametersNotification, object: nil)
    }

    // MARK: - Overlays (one transparent click-through window per screen)

    private func buildOverlays() {
        for w in overlays { w.orderOut(nil) }
        overlays.removeAll()
        for screen in NSScreen.screens {
            // Non-activating panel: clicking snow never steals focus from the app you are using.
            let w = NSPanel(contentRect: screen.frame, styleMask: [.borderless, .nonactivatingPanel],
                            backing: .buffered, defer: false)
            w.isReleasedWhenClosed = false
            w.hidesOnDeactivate = false
            w.isOpaque = false
            w.backgroundColor = .clear
            w.hasShadow = false
            w.ignoresMouseEvents = true
            w.level = .statusBar
            w.collectionBehavior = [.canJoinAllSpaces, .stationary, .fullScreenAuxiliary, .ignoresCycle]
            let view = SnowView(frame: NSRect(origin: .zero, size: screen.frame.size))
            w.contentView = view
            w.setFrame(screen.frame, display: true)
            w.orderFrontRegardless()
            overlays.append(w)
        }
        sim.views = overlays.compactMap { $0.contentView }
    }

    // MARK: - Live wallpaper (cabin scene, sits above the system wallpaper, below desktop icons)

    private func buildWallpapers() {
        for w in wallWindows { w.orderOut(nil) }
        wallWindows.removeAll()
        guard sim.wallpaperOn else {
            sim.wallViews = []
            return
        }
        for screen in NSScreen.screens {
            let w = NSWindow(contentRect: screen.frame, styleMask: .borderless, backing: .buffered, defer: false)
            w.isReleasedWhenClosed = false
            w.isOpaque = true
            w.backgroundColor = .black
            w.hasShadow = false
            w.ignoresMouseEvents = true
            w.level = NSWindow.Level(rawValue: Int(CGWindowLevelForKey(.desktopWindow)) + 1)
            w.collectionBehavior = [.canJoinAllSpaces, .stationary, .ignoresCycle]
            w.contentView = WallpaperView(frame: NSRect(origin: .zero, size: screen.frame.size))
            w.setFrame(screen.frame, display: true)
            w.orderFrontRegardless()
            wallWindows.append(w)
        }
        sim.wallViews = wallWindows.compactMap { $0.contentView }
    }

    @objc private func screensChanged() {
        sim.updateFrame()
        buildOverlays()
        buildWallpapers()
    }

    private func applyFullscreen(_ set: Set<Int>) {
        for (i, w) in overlays.enumerated() {
            if set.contains(i) { w.orderOut(nil) } else { w.orderFrontRegardless() }
        }
    }

    // MARK: - Menu bar

    private func buildMenu() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        if let b = statusItem.button {
            if let img = NSImage(systemSymbolName: "snowflake", accessibilityDescription: "Qor") {
                b.image = img
            } else {
                b.title = "❄"
            }
        }
        let menu = NSMenu()

        func add(_ title: String, _ action: Selector, tag: Int = 0, to m: NSMenu) {
            let item = NSMenuItem(title: title, action: action, keyEquivalent: "")
            item.target = self
            item.tag = tag
            m.addItem(item)
        }
        func sub(_ title: String, _ m: NSMenu, in parent: NSMenu) {
            let item = NSMenuItem(title: title, action: nil, keyEquivalent: "")
            item.submenu = m
            parent.addItem(item)
        }

        let snowMenu = NSMenu()
        add("Yengil", #selector(setIntensity(_:)), tag: 0, to: snowMenu)
        add("Tekis", #selector(setIntensity(_:)), tag: 1, to: snowMenu)
        add("Bo'ron", #selector(setIntensity(_:)), tag: 2, to: snowMenu)
        sub("Qor yog'ishi", snowMenu, in: menu)

        let windMenu = NSMenu()
        add("Chapga kuchli", #selector(setWind(_:)), tag: 0, to: windMenu)
        add("Chapga", #selector(setWind(_:)), tag: 1, to: windMenu)
        add("Shamolsiz", #selector(setWind(_:)), tag: 2, to: windMenu)
        add("O'ngga", #selector(setWind(_:)), tag: 3, to: windMenu)
        add("O'ngga kuchli", #selector(setWind(_:)), tag: 4, to: windMenu)
        sub("Shamol", windMenu, in: menu)

        let toolMenu = NSMenu()
        add("Qo'lqop (kursor bilan supurish)", #selector(setTool(_:)), tag: 0, to: toolMenu)
        add("Qor shari va qorbobo yasash", #selector(setTool(_:)), tag: 1, to: toolMenu)
        add("Asbobsiz", #selector(setTool(_:)), tag: 2, to: toolMenu)
        toolMenu.addItem(.separator())
        add("Qorbobolarni o'chirish", #selector(clearBalls(_:)), to: toolMenu)
        sub("Asbob", toolMenu, in: menu)

        let plowMenu = NSMenu()
        add("Qor tozalagich yoqilgan", #selector(togglePlow(_:)), to: plowMenu)
        plowMenu.addItem(.separator())
        add("Traktor", #selector(setPlowKind(_:)), tag: PlowKind.tractor.rawValue, to: plowMenu)
        add("Qor skuteri", #selector(setPlowKind(_:)), tag: PlowKind.snowmobile.rawValue, to: plowMenu)
        add("Belkurakli pingvin", #selector(setPlowKind(_:)), tag: PlowKind.penguin.rawValue, to: plowMenu)
        add("O'z rasmim...", #selector(pickPlowImage(_:)), to: plowMenu)
        sub("Qor tozalagich", plowMenu, in: menu)

        menu.addItem(.separator())
        add("Jonli oboy (qishki kulba)", #selector(toggleWallpaper(_:)), to: menu)
        add("Pauza", #selector(togglePause(_:)), to: menu)
        add("Oynalarda qor yig'ilsin", #selector(toggleAccumulate(_:)), to: menu)
        add("Qorni tozalash", #selector(clearSnow(_:)), to: menu)
        menu.addItem(.separator())
        add("Kompyuter yoqilganda ishga tushsin", #selector(toggleLogin(_:)), to: menu)
        add("Chiqish", #selector(quit(_:)), to: menu)
        statusItem.menu = menu
    }

    @objc func setIntensity(_ item: NSMenuItem) {
        sim.intensity = Intensity(rawValue: item.tag) ?? .steady
    }

    @objc func setWind(_ item: NSMenuItem) {
        sim.wind = AppDelegate.winds[max(0, min(item.tag, AppDelegate.winds.count - 1))]
    }

    @objc func setTool(_ item: NSMenuItem) { sim.tool = Tool(rawValue: item.tag) ?? .mitten }
    @objc func clearBalls(_ item: NSMenuItem) { sim.clearBalls() }
    @objc func togglePlow(_ item: NSMenuItem) { sim.plowEnabled.toggle() }
    @objc func setPlowKind(_ item: NSMenuItem) { sim.plowKind = PlowKind(rawValue: item.tag) ?? .tractor }

    @objc func pickPlowImage(_ item: NSMenuItem) {
        NSApp.activate(ignoringOtherApps: true)
        let panel = NSOpenPanel()
        panel.allowedFileTypes = ["png", "jpg", "jpeg"]
        panel.allowsMultipleSelection = false
        panel.message = "Qor tozalagich sifatida ishlatiladigan rasmni tanlang"
        if panel.runModal() == .OK, let url = panel.url,
           let img = NSImage(contentsOf: url),
           let cg = img.cgImage(forProposedRect: nil, context: nil, hints: nil) {
            sim.customPlowImage = cg
            sim.plowKind = .custom
        }
    }

    @objc func toggleWallpaper(_ item: NSMenuItem) {
        sim.wallpaperOn.toggle()
        buildWallpapers()
    }

    @objc func togglePause(_ item: NSMenuItem) { sim.paused.toggle() }
    @objc func toggleAccumulate(_ item: NSMenuItem) {
        if sim.accumulate { sim.clearAll() }
        sim.accumulate.toggle()
    }
    @objc func clearSnow(_ item: NSMenuItem) { sim.clearAll() }
    @objc func quit(_ item: NSMenuItem) { NSApp.terminate(nil) }

    @objc func toggleLogin(_ item: NSMenuItem) {
        if #available(macOS 13.0, *) {
            do {
                if SMAppService.mainApp.status == .enabled {
                    try SMAppService.mainApp.unregister()
                } else {
                    try SMAppService.mainApp.register()
                }
            } catch {
                NSSound.beep()
            }
        }
    }

    @objc func validateMenuItem(_ item: NSMenuItem) -> Bool {
        guard let a = item.action else { return true }
        switch a {
        case #selector(setIntensity(_:)):
            item.state = (sim.intensity.rawValue == item.tag) ? .on : .off
        case #selector(setWind(_:)):
            item.state = (sim.wind == AppDelegate.winds[item.tag]) ? .on : .off
        case #selector(setTool(_:)):
            item.state = (sim.tool.rawValue == item.tag) ? .on : .off
        case #selector(togglePlow(_:)):
            item.state = sim.plowEnabled ? .on : .off
        case #selector(setPlowKind(_:)):
            item.state = (sim.plowKind.rawValue == item.tag) ? .on : .off
        case #selector(toggleWallpaper(_:)):
            item.state = sim.wallpaperOn ? .on : .off
        case #selector(togglePause(_:)):
            item.title = sim.paused ? "Davom ettirish" : "Pauza"
        case #selector(toggleAccumulate(_:)):
            item.state = sim.accumulate ? .on : .off
        case #selector(toggleLogin(_:)):
            if #available(macOS 13.0, *) {
                item.state = (SMAppService.mainApp.status == .enabled) ? .on : .off
            }
        default:
            break
        }
        return true
    }
}
