import AppKit

final class WallpaperView: NSView {
    override var isOpaque: Bool { return true }

    override func draw(_ dirtyRect: NSRect) {
        guard let ctx = NSGraphicsContext.current?.cgContext else { return }
        Scene.draw(ctx, size: bounds.size, t: CGFloat(CACurrentMediaTime()))
    }
}

/// Animated winter-night scene: moon, twinkling stars, pines, a log cabin with glowing windows,
/// blinking string lights and chimney smoke. Original artwork drawn with Core Graphics.
enum Scene {
    static let cs = CGColorSpaceCreateDeviceRGB()

    static func c(_ r: CGFloat, _ g: CGFloat, _ b: CGFloat, _ a: CGFloat = 1) -> CGColor {
        return CGColor(red: r, green: g, blue: b, alpha: a)
    }

    static func frac(_ x: CGFloat) -> CGFloat { return x - floor(x) }

    static func linear(_ ctx: CGContext, _ rect: CGRect, top: CGColor, bottom: CGColor) {
        let g = CGGradient(colorsSpace: cs, colors: [top, bottom] as CFArray, locations: [0, 1])!
        ctx.saveGState()
        ctx.clip(to: rect)
        ctx.drawLinearGradient(g, start: CGPoint(x: rect.midX, y: rect.maxY),
                               end: CGPoint(x: rect.midX, y: rect.minY), options: [])
        ctx.restoreGState()
    }

    static func glow(_ ctx: CGContext, _ center: CGPoint, _ radius: CGFloat, _ color: CGColor, _ alpha: CGFloat) {
        let inner = color.copy(alpha: alpha)!
        let outer = color.copy(alpha: 0)!
        let g = CGGradient(colorsSpace: cs, colors: [inner, outer] as CFArray, locations: [0, 1])!
        ctx.drawRadialGradient(g, startCenter: center, startRadius: 0, endCenter: center,
                               endRadius: radius, options: [])
    }

    static func poly(_ ctx: CGContext, _ pts: [CGPoint], _ color: CGColor) {
        guard let first = pts.first else { return }
        ctx.setFillColor(color)
        ctx.move(to: first)
        for p in pts.dropFirst() { ctx.addLine(to: p) }
        ctx.closePath()
        ctx.fillPath()
    }

    static func rect(_ ctx: CGContext, _ x: CGFloat, _ y: CGFloat, _ w: CGFloat, _ h: CGFloat, _ color: CGColor) {
        ctx.setFillColor(color)
        ctx.fill(CGRect(x: x, y: y, width: w, height: h))
    }

    static func tree(_ ctx: CGContext, x: CGFloat, base: CGFloat, u: CGFloat) {
        let dark = c(0.06, 0.13, 0.24)
        let snow = c(0.93, 0.96, 1.0)
        rect(ctx, x - 5 * u, base - 2 * u, 10 * u, 16 * u, c(0.18, 0.11, 0.09))
        let tiers: [(CGFloat, CGFloat, CGFloat)] = [(72, 10, 52), (56, 42, 48), (40, 72, 44)]
        for (width, y0, height) in tiers {
            let hw = width / 2 * u
            let yb = base + y0 * u
            let yt = base + (y0 + height) * u
            poly(ctx, [CGPoint(x: x - hw, y: yb), CGPoint(x: x + hw, y: yb), CGPoint(x: x, y: yt)], dark)
            let capY = base + (y0 + height * 0.52) * u
            let capHW = hw * 0.48
            poly(ctx, [CGPoint(x: x - capHW, y: capY), CGPoint(x: x + capHW, y: capY),
                       CGPoint(x: x, y: yt)], snow)
        }
    }

    static func draw(_ ctx: CGContext, size: CGSize, t: CGFloat) {
        let w = size.width, h = size.height
        let s = h / 900
        let groundY = h * 0.17

        // Sky
        linear(ctx, CGRect(x: 0, y: 0, width: w, height: h), top: c(0.03, 0.05, 0.16), bottom: c(0.16, 0.22, 0.46))

        // Stars
        for i in 0..<110 {
            let fx = frac(CGFloat(i) * 0.6180339887)
            let fy = frac(CGFloat(i) * 0.7548776662)
            let x = fx * w
            let y = h * (0.30 + 0.70 * fy)
            let speed = 0.7 + CGFloat(i % 5) * 0.35
            let tw = 0.5 + 0.5 * sin(t * speed + CGFloat(i))
            let r = (0.6 + CGFloat(i % 3) * 0.45) * s * 1.2
            ctx.setFillColor(c(1, 1, 0.95, 0.25 + 0.75 * tw))
            ctx.fillEllipse(in: CGRect(x: x - r, y: y - r, width: r * 2, height: r * 2))
        }

        // Moon
        let moon = CGPoint(x: w * 0.8, y: h * 0.78)
        glow(ctx, moon, 170 * s, c(0.85, 0.90, 1.0), 0.35)
        ctx.setFillColor(c(1.0, 0.97, 0.80))
        ctx.fillEllipse(in: CGRect(x: moon.x - 36 * s, y: moon.y - 36 * s, width: 72 * s, height: 72 * s))

        // Hills
        let far = CGMutablePath()
        far.move(to: CGPoint(x: 0, y: 0))
        var x: CGFloat = 0
        while x <= w + 12 {
            let a = 38 * s * sin(x / (210 * s))
            let b = 22 * s * sin(x / (97 * s) + 1.3)
            far.addLine(to: CGPoint(x: x, y: groundY + 55 * s + a + b))
            x += 12
        }
        far.addLine(to: CGPoint(x: w + 12, y: 0))
        far.closeSubpath()
        ctx.addPath(far)
        ctx.setFillColor(c(0.11, 0.15, 0.34))
        ctx.fillPath()

        let near = CGMutablePath()
        near.move(to: CGPoint(x: 0, y: 0))
        x = 0
        while x <= w + 12 {
            near.addLine(to: CGPoint(x: x, y: groundY + 18 * s + 14 * s * sin(x / (160 * s) + 2)))
            x += 12
        }
        near.addLine(to: CGPoint(x: w + 12, y: 0))
        near.closeSubpath()
        ctx.addPath(near)
        ctx.setFillColor(c(0.17, 0.22, 0.45))
        ctx.fillPath()

        // Trees (back row, then front row)
        let backTrees: [(CGFloat, CGFloat)] = [(0.06, 1.0), (0.15, 1.2), (0.30, 0.9), (0.70, 0.9), (0.84, 1.2), (0.94, 1.0)]
        for (fx, sc) in backTrees { tree(ctx, x: fx * w, base: groundY + 10 * s, u: sc * s * 1.1) }

        // Snowy ground
        let ground = CGMutablePath()
        ground.move(to: CGPoint(x: 0, y: 0))
        x = 0
        while x <= w + 12 {
            ground.addLine(to: CGPoint(x: x, y: groundY + 6 * s * sin(x / (140 * s))))
            x += 12
        }
        ground.addLine(to: CGPoint(x: w + 12, y: 0))
        ground.closeSubpath()
        ctx.saveGState()
        ctx.addPath(ground)
        ctx.clip()
        linear(ctx, CGRect(x: 0, y: 0, width: w, height: groundY + 20 * s), top: c(0.94, 0.96, 1.0), bottom: c(0.76, 0.83, 0.95))
        ctx.restoreGState()

        let frontTrees: [(CGFloat, CGFloat)] = [(0.22, 1.5), (0.78, 1.6), (0.03, 1.7), (0.97, 1.5)]
        for (fx, sc) in frontTrees { tree(ctx, x: fx * w, base: groundY - 6 * s, u: sc * s * 1.1) }

        // Cabin geometry
        let cx = w * 0.5
        let cw = 330 * s, ch = 140 * s
        let baseY = groundY - 6 * s
        let wallTop = baseY + ch
        let eave = 34 * s, peakH = 92 * s
        let L = CGPoint(x: cx - cw / 2 - eave, y: wallTop - 4 * s)
        let R = CGPoint(x: cx + cw / 2 + eave, y: wallTop - 4 * s)
        let P = CGPoint(x: cx, y: wallTop + peakH)

        // Warm light spilling on the snow
        let flick = 0.5 + 0.5 * sin(t * 3.1)
        let winX: [CGFloat] = [cx - cw * 0.28, cx + cw * 0.28]
        for wx in winX {
            glow(ctx, CGPoint(x: wx, y: baseY - 4 * s), 110 * s, c(1.0, 0.75, 0.30), 0.22 + 0.06 * flick)
        }
        // Shadow under the cabin
        ctx.setFillColor(c(0.25, 0.30, 0.55, 0.25))
        ctx.fillEllipse(in: CGRect(x: cx - cw * 0.6, y: baseY - 14 * s, width: cw * 1.2, height: 26 * s))

        // Chimney (behind the roof)
        let chX = cx + 72 * s, chW = 26 * s
        let chTop = wallTop + 100 * s
        rect(ctx, chX, wallTop, chW, 100 * s, c(0.35, 0.20, 0.16))
        rect(ctx, chX - 4 * s, chTop - 2 * s, chW + 8 * s, 9 * s, c(0.94, 0.96, 1.0))

        // Walls
        rect(ctx, cx - cw / 2, baseY, cw, ch, c(0.33, 0.20, 0.13))
        var py = baseY + 18 * s
        while py < wallTop {
            rect(ctx, cx - cw / 2, py, cw, 1.6 * s, c(0.21, 0.12, 0.09))
            py += 18 * s
        }
        rect(ctx, cx - cw / 2, baseY, 8 * s, ch, c(0.27, 0.16, 0.10))
        rect(ctx, cx + cw / 2 - 8 * s, baseY, 8 * s, ch, c(0.27, 0.16, 0.10))

        // Door
        rect(ctx, cx - 20 * s, baseY, 40 * s, 78 * s, c(0.16, 0.09, 0.07))
        circle(ctx, cx + 11 * s, baseY + 38 * s, 2.4 * s, c(0.95, 0.80, 0.35))

        // Windows
        for wx in winX {
            let ww = 64 * s, wh = 56 * s
            let y0 = baseY + 50 * s
            rect(ctx, wx - ww / 2 - 3 * s, y0 - 3 * s, ww + 6 * s, wh + 6 * s, c(0.20, 0.12, 0.08))
            rect(ctx, wx - ww / 2, y0, ww, wh, c(1.0, 0.75 + 0.06 * flick, 0.36, 0.97))
            rect(ctx, wx - 1.5 * s, y0, 3 * s, wh, c(0.20, 0.12, 0.08))
            rect(ctx, wx - ww / 2, y0 + wh / 2 - 1.5 * s, ww, 3 * s, c(0.20, 0.12, 0.08))
            rect(ctx, wx - ww / 2 - 6 * s, y0 - 8 * s, ww + 12 * s, 7 * s, c(0.94, 0.96, 1.0))
        }

        // Roof
        poly(ctx, [L, P, R], c(0.22, 0.12, 0.10))
        ctx.setStrokeColor(c(0.95, 0.97, 1.0))
        ctx.setLineWidth(16 * s)
        ctx.setLineCap(.round)
        ctx.setLineJoin(.round)
        ctx.move(to: CGPoint(x: L.x, y: L.y + 6 * s))
        ctx.addLine(to: CGPoint(x: P.x, y: P.y + 6 * s))
        ctx.addLine(to: CGPoint(x: R.x, y: R.y + 6 * s))
        ctx.strokePath()

        // Blinking string lights along both eaves
        let palette: [CGColor] = [c(1.0, 0.35, 0.35), c(1.0, 0.82, 0.30), c(0.40, 0.85, 0.50), c(0.40, 0.65, 1.0), c(0.90, 0.50, 1.0)]
        let n = 13
        for side in 0..<2 {
            let a = side == 0 ? L : R
            for i in 0..<n {
                let f = (CGFloat(i) + 0.5) / CGFloat(n)
                let px = a.x + (P.x - a.x) * f
                let py2 = a.y + (P.y - a.y) * f - 11 * s
                let phase = CGFloat(i + side * 3)
                let on = max(0, sin(t * 2.2 + phase * 0.9))
                let color = palette[(i + side) % palette.count]
                glow(ctx, CGPoint(x: px, y: py2), 12 * s, color, 0.45 * on)
                ctx.setFillColor(color.copy(alpha: 0.35 + 0.65 * on)!)
                ctx.fillEllipse(in: CGRect(x: px - 3.2 * s, y: py2 - 3.2 * s, width: 6.4 * s, height: 6.4 * s))
            }
        }

        // Chimney smoke
        for i in 0..<7 {
            let p = frac(t * 0.10 + CGFloat(i) / 7)
            let sx = chX + chW / 2 + sin(p * 6 + CGFloat(i)) * 14 * s + p * 34 * s
            let sy = chTop + 8 * s + p * 190 * s
            let r = (7 + p * 24) * s
            ctx.setFillColor(c(0.86, 0.89, 0.98, 0.38 * (1 - p)))
            ctx.fillEllipse(in: CGRect(x: sx - r, y: sy - r, width: r * 2, height: r * 2))
        }

        // Doorstep snow
        rect(ctx, cx - 34 * s, baseY - 4 * s, 68 * s, 8 * s, c(0.94, 0.96, 1.0))
    }

    static func circle(_ ctx: CGContext, _ cx: CGFloat, _ cy: CGFloat, _ r: CGFloat, _ color: CGColor) {
        ctx.setFillColor(color)
        ctx.fillEllipse(in: CGRect(x: cx - r, y: cy - r, width: r * 2, height: r * 2))
    }
}
