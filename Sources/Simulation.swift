import AppKit

enum Intensity: Int {
    case light = 0, steady, blizzard
    var rate: CGFloat {
        switch self {
        case .light: return 35
        case .steady: return 110
        case .blizzard: return 380
        }
    }
}

enum Tool: Int {
    case mitten = 0, snowball, none
}

enum PlowKind: Int {
    case tractor = 0, snowmobile, penguin, custom
    var bladeOffset: CGFloat {
        switch self {
        case .tractor: return 70
        case .snowmobile: return 80
        case .penguin: return 58
        case .custom: return 52
        }
    }
}

enum BallState { case held, falling, resting }

struct Flake {
    var x: CGFloat
    var y: CGFloat
    var vx: CGFloat
    var vy: CGFloat
    var r: CGFloat
    var phase: CGFloat
    var free: CGFloat = 0   // seconds during which the flake cannot land (freshly shaken off)
}

struct Surface {
    let id: Int
    let rect: CGRect        // Cocoa coordinates (origin bottom-left); rect.maxY is the top edge
    let isGround: Bool
}

/// A snowball. Balls can be stacked into a snowman: `below` / `above` form the chain.
final class Ball {
    var x: CGFloat
    var y: CGFloat          // centre
    var r: CGFloat
    var vy: CGFloat = 0
    var state: BallState = .held
    var surfaceId: Int? = nil
    var localX: CGFloat = 0
    var grabDX: CGFloat = 0
    var grabDY: CGFloat = 0
    var below: Ball? = nil
    var above: Ball? = nil
    var removed = false
    init(x: CGFloat, y: CGFloat, r: CGFloat) { self.x = x; self.y = y; self.r = r }
}

/// Snow drift: a height-map along the top edge of a window / the screen bottom.
final class Pile {
    static let step: CGFloat = 3
    var heights: [CGFloat]
    let maxHeight: CGFloat

    init(width: CGFloat, maxHeight: CGFloat) {
        heights = Array(repeating: 0, count: max(2, Int(width / Pile.step)))
        self.maxHeight = maxHeight
    }

    func index(forLocalX lx: CGFloat) -> Int {
        return min(heights.count - 1, max(0, Int(lx / Pile.step)))
    }

    func height(atLocalX lx: CGFloat) -> CGFloat {
        return heights[index(forLocalX: lx)]
    }

    func add(atLocalX lx: CGFloat, amount: CGFloat) {
        let i = index(forLocalX: lx)
        for d in -2...2 {
            let j = i + d
            if j < 0 || j >= heights.count { continue }
            let w: CGFloat = 1 - CGFloat(abs(d)) / 3
            heights[j] = min(maxHeight, heights[j] + amount * w * 0.45)
        }
    }

    /// Makes the drift slump into a natural slope.
    func relax() {
        let n = heights.count
        if n < 3 { return }
        for i in 0..<(n - 1) {
            let diff = heights[i] - heights[i + 1]
            if abs(diff) > 1.8 {
                let m = (abs(diff) - 1.8) * 0.2
                if diff > 0 {
                    heights[i] -= m; heights[i + 1] += m
                } else {
                    heights[i] += m; heights[i + 1] -= m
                }
            }
        }
        heights[0] = min(heights[0], heights[1])
        heights[n - 1] = min(heights[n - 1], heights[n - 2])
    }

    func resize(toWidth w: CGFloat) {
        let n = max(2, Int(w / Pile.step))
        if n == heights.count { return }
        var new = [CGFloat](repeating: 0, count: n)
        for i in 0..<n { new[i] = heights[min(heights.count - 1, i * heights.count / n)] }
        heights = new
    }
}

final class Simulation {
    static let shared = Simulation()

    // Settings (changed from the menu)
    var intensity: Intensity = .steady
    var wind: CGFloat = 0
    var paused = false
    var tool: Tool = .mitten
    var accumulate = true
    var plowEnabled = true
    var plowKind: PlowKind = .tractor
    var customPlowImage: CGImage?
    var wallpaperOn = false

    // State
    var flakes: [Flake] = []
    var piles: [Int: Pile] = [:]
    var surfaces: [Surface] = []          // front-to-back windows, then ground surfaces
    var balls: [Ball] = []
    var views: [NSView] = [] { didSet { capturing = false } }
    var wallViews: [NSView] = []
    var onFullscreenChange: ((Set<Int>) -> Void)?

    private var lastRects: [Int: CGRect] = [:]
    private var fullscreenScreens = Set<Int>()
    private var unionFrame = CGRect.zero
    private var spawnAcc: CGFloat = 0
    private var lastTime = CACurrentMediaTime()
    private var lastMouse = NSEvent.mouseLocation
    private var lastBallMouse = NSEvent.mouseLocation
    private var tickCount = 0
    private var timer: Timer?
    private var capturing = false
    private let maxFlakes = 4500
    private let maxBalls = 40

    // Snow plow state, keyed by ground surface id
    private var plowX: [Int: CGFloat] = [:]
    private var plowDir: [Int: CGFloat] = [:]
    private var plowCooldown: [Int: CGFloat] = [:]

    // MARK: - Lifecycle

    func start() {
        updateFrame()
        prefill()
        refreshWindows()
        let t = Timer(timeInterval: 1.0 / 60.0, repeats: true) { [weak self] _ in self?.tick() }
        RunLoop.main.add(t, forMode: .common)
        timer = t
    }

    func updateFrame() {
        unionFrame = NSScreen.screens.reduce(CGRect.null) { $0.union($1.frame) }
        if unionFrame.isNull { unionFrame = CGRect(x: 0, y: 0, width: 1440, height: 900) }
    }

    private func prefill() {
        let count = Int(intensity.rate * (unionFrame.width / 1500) * 6)
        for _ in 0..<count {
            flakes.append(makeFlake(y: CGFloat.random(in: unionFrame.minY...unionFrame.maxY)))
        }
    }

    private func makeFlake(y: CGFloat) -> Flake {
        let r = CGFloat.random(in: 1.1...3.4)
        return Flake(x: CGFloat.random(in: (unionFrame.minX - 100)...(unionFrame.maxX + 100)),
                     y: y, vx: wind, vy: -(40 + r * 22), r: r,
                     phase: CGFloat.random(in: 0...6.28))
    }

    // MARK: - Main loop

    private func tick() {
        let now = CACurrentMediaTime()
        let dt = CGFloat(min(now - lastTime, 0.05))
        lastTime = now
        tickCount += 1
        if tickCount % 3 == 0 { refreshWindows() }
        if !paused {
            spawn(dt)
            step(dt)
            for p in piles.values { p.relax() }
            updatePlows(dt)
            mitten()
            updateBalls(dt)
        }
        updateCapture()
        for v in views { v.needsDisplay = true }
        if wallpaperOn && tickCount % 3 == 0 {
            for v in wallViews { v.needsDisplay = true }
        }
    }

    private func spawn(_ dt: CGFloat) {
        spawnAcc += intensity.rate * dt * (unionFrame.width / 1500)
        while spawnAcc >= 1 {
            spawnAcc -= 1
            if flakes.count >= maxFlakes { break }
            flakes.append(makeFlake(y: unionFrame.maxY + 8))
        }
    }

    private func step(_ dt: CGFloat) {
        let t = CGFloat(CACurrentMediaTime())
        var i = 0
        while i < flakes.count {
            var f = flakes[i]
            let oldY = f.y
            let targetVX = wind * (0.5 + f.r / 4) + sin(t * 1.3 + f.phase) * 12
            let targetVY = -(40 + f.r * 22)
            f.vx += (targetVX - f.vx) * min(1, dt * 1.5)
            f.vy += (targetVY - f.vy) * min(1, dt * 2.5)
            f.x += f.vx * dt
            f.y += f.vy * dt
            if f.free > 0 { f.free -= dt }

            var landed = false
            if accumulate && f.free <= 0 && f.vy < 0 {
                for (k, s) in surfaces.enumerated() {
                    if f.x < s.rect.minX || f.x > s.rect.maxX { continue }
                    guard let p = piles[s.id] else { continue }
                    let top = s.rect.maxY + p.height(atLocalX: f.x - s.rect.minX)
                    if oldY >= top && f.y < top {
                        let probe = CGPoint(x: f.x, y: s.rect.maxY + 0.5)
                        var hidden = false
                        for j in 0..<k where surfaces[j].rect.contains(probe) { hidden = true; break }
                        if hidden { continue }
                        p.add(atLocalX: f.x - s.rect.minX, amount: f.r * 0.9)
                        landed = true
                        break
                    }
                }
            }
            let gone = f.y < unionFrame.minY - 40 || f.x < unionFrame.minX - 150 || f.x > unionFrame.maxX + 150
            if landed || gone {
                flakes.remove(at: i)
            } else {
                flakes[i] = f
                i += 1
            }
        }
    }

    // MARK: - Windows

    private func refreshWindows() {
        let screens = NSScreen.screens
        guard let mainH = screens.first?.frame.height else { return }
        var wins: [Surface] = []
        var full = Set<Int>()

        let opts: CGWindowListOption = [.optionOnScreenOnly, .excludeDesktopElements]
        if let info = CGWindowListCopyWindowInfo(opts, kCGNullWindowID) as? [[String: Any]] {
            let me = Int(ProcessInfo.processInfo.processIdentifier)
            for w in info {
                guard let layer = w[kCGWindowLayer as String] as? Int, layer == 0,
                      let num = w[kCGWindowNumber as String] as? Int,
                      let pid = w[kCGWindowOwnerPID as String] as? Int, pid != me,
                      let bd = w[kCGWindowBounds as String] as? NSDictionary,
                      let b = CGRect(dictionaryRepresentation: bd as CFDictionary) else { continue }
                let alpha = (w[kCGWindowAlpha as String] as? Double) ?? 1
                if alpha < 0.1 || b.width < 120 || b.height < 60 { continue }
                let r = CGRect(x: b.minX, y: mainH - b.maxY, width: b.width, height: b.height)
                // A window exactly the size of a screen = a full-screen app: hide the snow there.
                if let idx = screens.firstIndex(where: {
                    abs($0.frame.width - r.width) < 1.5 && abs($0.frame.height - r.height) < 1.5 &&
                    abs($0.frame.minX - r.minX) < 1.5 && abs($0.frame.minY - r.minY) < 1.5 }) {
                    full.insert(idx)
                    continue
                }
                wins.append(Surface(id: num, rect: r, isGround: false))
            }
        }

        if full != fullscreenScreens {
            fullscreenScreens = full
            onFullscreenChange?(full)
        }

        if !accumulate {
            surfaces = []
            piles.removeAll()
            lastRects.removeAll()
            return
        }

        var newSurfaces = wins
        var seen = Set<Int>()
        for s in wins {
            seen.insert(s.id)
            if let p = piles[s.id] {
                p.resize(toWidth: s.rect.width)
                if let old = lastRects[s.id], abs(old.minX - s.rect.minX) + abs(old.maxY - s.rect.maxY) > 7 {
                    shed(p, rect: s.rect, fraction: 0.18)   // window is being dragged: snow slides off
                }
            } else {
                piles[s.id] = Pile(width: s.rect.width, maxHeight: 16)
            }
            lastRects[s.id] = s.rect
        }
        for (i, sc) in screens.enumerated() {
            let id = -(i + 1)
            let rect = CGRect(x: sc.frame.minX, y: sc.visibleFrame.minY, width: sc.frame.width, height: 1)
            if piles[id] == nil { piles[id] = Pile(width: rect.width, maxHeight: 26) }
            newSurfaces.append(Surface(id: id, rect: rect, isGround: true))
            seen.insert(id)
        }
        // Closed / minimised windows: their snow drops down.
        for id in Array(piles.keys) where !seen.contains(id) {
            if let p = piles[id], let r = lastRects[id] { shed(p, rect: r, fraction: 1.0) }
            piles[id] = nil
            lastRects[id] = nil
            plowX[id] = nil
        }
        surfaces = newSurfaces
    }

    /// Turns part of a drift back into falling flakes.
    private func shed(_ p: Pile, rect: CGRect, fraction: CGFloat) {
        for i in 0..<p.heights.count {
            let h = p.heights[i]
            if h < 0.6 { continue }
            let take = h * fraction
            p.heights[i] -= take
            let n = Int(take / 1.6)
            if n <= 0 { continue }
            for _ in 0..<n {
                if flakes.count >= maxFlakes { return }
                flakes.append(Flake(
                    x: rect.minX + CGFloat(i) * Pile.step + CGFloat.random(in: 0...Pile.step),
                    y: rect.maxY + h + CGFloat.random(in: 0...3),
                    vx: CGFloat.random(in: -25...25), vy: CGFloat.random(in: -70 ... -20),
                    r: CGFloat.random(in: 1.2...2.6), phase: CGFloat.random(in: 0...6.28), free: 0.6))
            }
        }
    }

    func clearAll() {
        for s in surfaces {
            if let p = piles[s.id] { shed(p, rect: s.rect, fraction: 1.0) }
        }
    }

    func clearBalls() {
        for b in balls { b.removed = true }
        balls.removeAll { b in
            b.above = nil
            b.below = nil
            return true
        }
    }

    // MARK: - Mitten (cursor sweeps the snow)

    private func mitten() {
        let m = NSEvent.mouseLocation
        let dx = m.x - lastMouse.x, dy = m.y - lastMouse.y
        lastMouse = m
        let speed = hypot(dx, dy)
        if tool != .mitten || speed < 1.5 { return }
        for s in surfaces {
            guard let p = piles[s.id] else { continue }
            let lx = m.x - s.rect.minX
            if lx < -30 || lx > s.rect.width + 30 { continue }
            if m.y < s.rect.maxY - 6 || m.y > s.rect.maxY + p.maxHeight + 40 { continue }
            let center = Int(lx / Pile.step)
            let radius = 9
            for d in -radius...radius {
                let j = center + d
                if j < 0 || j >= p.heights.count { continue }
                let w = 1 - CGFloat(abs(d)) / CGFloat(radius + 1)
                let take = min(p.heights[j], 2.2 * w * min(speed / 8, 2))
                if take < 0.05 { continue }
                p.heights[j] -= take
                if Int.random(in: 0..<3) == 0 && flakes.count < maxFlakes {
                    flakes.append(Flake(
                        x: s.rect.minX + CGFloat(j) * Pile.step,
                        y: s.rect.maxY + p.heights[j] + 2,
                        vx: dx * 6 + CGFloat.random(in: -30...30), vy: CGFloat.random(in: 20...90),
                        r: CGFloat.random(in: 1.0...2.2), phase: CGFloat.random(in: 0...6.28), free: 0.5))
                }
            }
        }
    }

    // MARK: - Snow plow

    private func updatePlows(_ dt: CGFloat) {
        for s in surfaces where s.isGround {
            guard let p = piles[s.id] else { continue }
            if let x = plowX[s.id] {
                let dir = plowDir[s.id] ?? 1
                let nx = x + dir * 150 * dt
                plowX[s.id] = nx
                let bladeX = nx + dir * plowKind.bladeOffset
                let c = Int((bladeX - s.rect.minX) / Pile.step)
                for d in -10...10 {
                    let j = c + d
                    if j < 0 || j >= p.heights.count { continue }
                    let h = p.heights[j]
                    if h < 0.4 { continue }
                    p.heights[j] = 0
                    if Int.random(in: 0..<2) == 0 && flakes.count < maxFlakes {
                        flakes.append(Flake(
                            x: bladeX + CGFloat.random(in: -6...6), y: s.rect.maxY + h,
                            vx: dir * CGFloat.random(in: 20...110), vy: CGFloat.random(in: 30...120),
                            r: CGFloat.random(in: 1.1...2.4), phase: CGFloat.random(in: 0...6.28), free: 0.5))
                    }
                }
                if (dir > 0 && nx > s.rect.maxX + 140) || (dir < 0 && nx < s.rect.minX - 140) {
                    plowX[s.id] = nil
                    plowCooldown[s.id] = 25
                }
            } else {
                if let cd = plowCooldown[s.id], cd > 0 {
                    plowCooldown[s.id] = cd - dt
                    continue
                }
                if !plowEnabled || tickCount % 30 != 0 { continue }
                let mean = p.heights.reduce(0, +) / CGFloat(p.heights.count)
                if mean > 6 {
                    let dir: CGFloat = Bool.random() ? 1 : -1
                    plowDir[s.id] = dir
                    plowX[s.id] = dir > 0 ? s.rect.minX - 120 : s.rect.maxX + 120
                }
            }
        }
    }

    // MARK: - Snowballs & snowmen

    private func baseOf(_ b: Ball) -> Ball {
        var c = b
        while let d = c.below { c = d }
        return c
    }

    private func inChain(_ base: Ball, _ target: Ball) -> Bool {
        var cur: Ball? = base
        while let c = cur {
            if c === target { return true }
            cur = c.above
        }
        return false
    }

    private func removeChain(from base: Ball) {
        var cur: Ball? = base
        while let c = cur {
            c.removed = true
            cur = c.above
        }
    }

    private func detach(_ b: Ball) {
        b.below?.above = nil
        b.below = nil
    }

    private func isHidden(_ point: CGPoint, behindSurfaceIndex k: Int) -> Bool {
        for j in 0..<k where surfaces[j].rect.contains(point) { return true }
        return false
    }

    /// While a ball is dragged along a drift it picks up snow and grows.
    private func gatherSnow(_ b: Ball) -> CGFloat {
        var taken: CGFloat = 0
        for s in surfaces {
            guard let p = piles[s.id] else { continue }
            let lx = b.x - s.rect.minX
            if lx < 0 || lx > s.rect.width { continue }
            let bottom = b.y - b.r
            let top = s.rect.maxY + p.height(atLocalX: lx)
            if bottom > top + 8 || bottom < s.rect.maxY - 12 { continue }
            let c = Int(lx / Pile.step)
            let rad = max(2, Int(b.r / Pile.step))
            for d in -rad...rad {
                let j = c + d
                if j < 0 || j >= p.heights.count { continue }
                let t = min(p.heights[j], 0.5)
                p.heights[j] -= t
                taken += t
            }
        }
        return taken
    }

    private func updateBalls(_ dt: CGFloat) {
        if balls.isEmpty { return }
        let m = NSEvent.mouseLocation
        let speed = hypot(m.x - lastBallMouse.x, m.y - lastBallMouse.y)
        lastBallMouse = m

        // Safety net: button released outside our events
        if NSEvent.pressedMouseButtons & 1 == 0 { mouseUp() }

        for b in balls where b.below == nil && !b.removed {
            switch b.state {
            case .held:
                b.x = m.x + b.grabDX
                b.y = m.y + b.grabDY
                if speed > 1 && b.above == nil {
                    let taken = gatherSnow(b)
                    b.r = min(30, b.r + taken * 0.03)
                }
            case .falling:
                let oldBottom = b.y - b.r
                b.vy = max(b.vy - 1400 * dt, -1200)
                b.y += b.vy * dt
                let newBottom = b.y - b.r
                var settled = false
                // Onto the top of another snowman / ball
                for t in balls where t !== b && t.above == nil && !t.removed && !inChain(b, t) {
                    if baseOf(t).state != .resting { continue }
                    if abs(b.x - t.x) < t.r * 0.85 && b.r <= t.r * 1.1 {
                        let topY = t.y + t.r
                        if newBottom <= topY && oldBottom >= t.y + t.r * 0.6 {
                            b.below = t
                            t.above = b
                            b.state = .resting
                            b.vy = 0
                            settled = true
                            break
                        }
                    }
                }
                // Onto a drift (window top / ground)
                if !settled {
                    for (k, s) in surfaces.enumerated() {
                        if b.x < s.rect.minX || b.x > s.rect.maxX { continue }
                        guard let p = piles[s.id] else { continue }
                        let top = s.rect.maxY + p.height(atLocalX: b.x - s.rect.minX)
                        if oldBottom >= top - 1 && newBottom <= top {
                            if isHidden(CGPoint(x: b.x, y: s.rect.maxY + 0.5), behindSurfaceIndex: k) { continue }
                            b.state = .resting
                            b.surfaceId = s.id
                            b.localX = b.x - s.rect.minX
                            b.y = top + b.r * 0.9
                            b.vy = 0
                            break
                        }
                    }
                }
                if b.y < unionFrame.minY - 120 { removeChain(from: b) }
            case .resting:
                if let sid = b.surfaceId,
                   let s = surfaces.first(where: { $0.id == sid }),
                   let p = piles[sid] {
                    b.x = s.rect.minX + b.localX
                    b.y = s.rect.maxY + p.height(atLocalX: b.localX) + b.r * 0.9
                } else {
                    b.state = .falling
                    b.vy = 0
                }
            }
            // Lay out the balls stacked on top
            var lower = b
            var cur = b.above
            while let u = cur {
                u.x = lower.x
                u.y = lower.y + lower.r + u.r * 0.85
                lower = u
                cur = u.above
            }
        }
        balls.removeAll { b in
            if b.removed {
                b.above = nil
                b.below = nil
                return true
            }
            return false
        }
    }

    // MARK: - Mouse (only delivered while the cursor is over snow, see updateCapture)

    private func pileHit(at m: CGPoint) -> Bool {
        for (k, s) in surfaces.enumerated() {
            guard let p = piles[s.id] else { continue }
            let lx = m.x - s.rect.minX
            if lx < 0 || lx > s.rect.width { continue }
            let h = p.height(atLocalX: lx)
            if h > 1.5 && m.y >= s.rect.maxY - 2 && m.y <= s.rect.maxY + h + 4 {
                if !isHidden(m, behindSurfaceIndex: k) { return true }
            }
        }
        return false
    }

    private func wantsMouse() -> Bool {
        if balls.contains(where: { $0.state == .held }) { return true }
        let m = NSEvent.mouseLocation
        for b in balls where hypot(b.x - m.x, b.y - m.y) < b.r + 4 { return true }
        return pileHit(at: m)
    }

    /// The overlay is click-through, except while the cursor is over snow in snowball mode.
    private func updateCapture() {
        let want = (tool == .snowball) && wantsMouse()
        if want != capturing {
            capturing = want
            for v in views { v.window?.ignoresMouseEvents = !want }
        }
    }

    func mouseDown() {
        guard tool == .snowball else { return }
        let m = NSEvent.mouseLocation
        for b in balls.reversed() where !b.removed && hypot(b.x - m.x, b.y - m.y) < b.r + 4 {
            detach(b)
            b.state = .held
            b.surfaceId = nil
            b.vy = 0
            b.grabDX = b.x - m.x
            b.grabDY = b.y - m.y
            return
        }
        if balls.count < maxBalls && pileHit(at: m) {
            let b = Ball(x: m.x, y: m.y + 3, r: 6)
            b.state = .held
            balls.append(b)
        }
    }

    func mouseUp() {
        for b in balls where b.state == .held {
            b.state = .falling
            b.vy = 0
        }
    }

    // MARK: - Drawing

    private func clipBehindFront(_ ctx: CGContext, _ k: Int, _ screenFrame: CGRect) {
        let clip = CGMutablePath()
        clip.addRect(screenFrame.insetBy(dx: -50, dy: -50))
        for j in 0..<k { clip.addRect(surfaces[j].rect) }
        ctx.addPath(clip)
        ctx.clip(using: .evenOdd)
    }

    func draw(in ctx: CGContext, screenFrame: CGRect) {
        // Drifts
        for (k, s) in surfaces.enumerated() {
            guard let p = piles[s.id], p.heights.contains(where: { $0 > 0.3 }) else { continue }
            if !s.rect.insetBy(dx: -40, dy: -80).intersects(screenFrame) { continue }
            ctx.saveGState()
            if k > 0 { clipBehindFront(ctx, k, screenFrame) }   // snow sits behind windows in front of it
            let base = s.rect.maxY
            let path = CGMutablePath()
            path.move(to: CGPoint(x: s.rect.minX, y: base - 1))
            for i in 0..<p.heights.count {
                path.addLine(to: CGPoint(x: min(s.rect.maxX, s.rect.minX + CGFloat(i) * Pile.step), y: base + p.heights[i]))
            }
            path.addLine(to: CGPoint(x: s.rect.maxX, y: base - 1))
            path.closeSubpath()
            ctx.addPath(path)
            ctx.setFillColor(CGColor(red: 0.96, green: 0.98, blue: 1.0, alpha: 0.97))
            ctx.fillPath()
            ctx.addPath(path)
            ctx.setStrokeColor(CGColor(red: 0.55, green: 0.65, blue: 0.82, alpha: 0.35))
            ctx.setLineWidth(1)
            ctx.strokePath()
            ctx.restoreGState()
        }

        // Snowballs and snowmen
        for b in balls where b.below == nil && !b.removed {
            if !screenFrame.insetBy(dx: -80, dy: -80).contains(CGPoint(x: b.x, y: b.y)) { continue }
            var k = -1
            if b.state == .resting, let sid = b.surfaceId {
                k = surfaces.firstIndex(where: { $0.id == sid }) ?? -1
            }
            ctx.saveGState()
            if k > 0 { clipBehindFront(ctx, k, screenFrame) }
            var cur: Ball? = b
            while let c = cur {
                Art.ball(ctx, c)
                cur = c.above
            }
            ctx.restoreGState()
        }

        // Snow plows
        let now = CGFloat(CACurrentMediaTime())
        for s in surfaces where s.isGround {
            guard let x = plowX[s.id] else { continue }
            if x < screenFrame.minX - 160 || x > screenFrame.maxX + 160 { continue }
            if s.rect.maxY < screenFrame.minY || s.rect.maxY > screenFrame.maxY { continue }
            let dir = plowDir[s.id] ?? 1
            ctx.saveGState()
            ctx.translateBy(x: x, y: s.rect.maxY - 2)
            if dir < 0 { ctx.scaleBy(x: -1, y: 1) }
            Art.vehicle(ctx, kind: plowKind, t: now, image: customPlowImage)
            ctx.restoreGState()
        }

        // Falling flakes (drawn over everything)
        ctx.setFillColor(CGColor(red: 1, green: 1, blue: 1, alpha: 0.93))
        let visible = screenFrame.insetBy(dx: -6, dy: -6)
        for f in flakes {
            if !visible.contains(CGPoint(x: f.x, y: f.y)) { continue }
            ctx.fillEllipse(in: CGRect(x: f.x - f.r, y: f.y - f.r, width: f.r * 2, height: f.r * 2))
        }
    }
}
