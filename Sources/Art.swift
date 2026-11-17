import AppKit

/// Hand-drawn vector art: snowballs/snowmen and snow-plow vehicles (all original, drawn with Core Graphics).
enum Art {
    static let cs = CGColorSpaceCreateDeviceRGB()

    static func col(_ r: CGFloat, _ g: CGFloat, _ b: CGFloat, _ a: CGFloat = 1) -> CGColor {
        return CGColor(red: r, green: g, blue: b, alpha: a)
    }

    static func fill(_ ctx: CGContext, _ rect: CGRect, _ color: CGColor, radius: CGFloat = 0) {
        ctx.setFillColor(color)
        if radius > 0 {
            let rr = min(radius, min(rect.width, rect.height) / 2)
            ctx.addPath(CGPath(roundedRect: rect, cornerWidth: rr, cornerHeight: rr, transform: nil))
            ctx.fillPath()
        } else {
            ctx.fill(rect)
        }
    }

    static func circle(_ ctx: CGContext, _ cx: CGFloat, _ cy: CGFloat, _ r: CGFloat, _ color: CGColor) {
        ctx.setFillColor(color)
        ctx.fillEllipse(in: CGRect(x: cx - r, y: cy - r, width: r * 2, height: r * 2))
    }

    static func line(_ ctx: CGContext, _ a: CGPoint, _ b: CGPoint, width: CGFloat, _ color: CGColor) {
        ctx.setStrokeColor(color)
        ctx.setLineWidth(width)
        ctx.setLineCap(.round)
        ctx.move(to: a)
        ctx.addLine(to: b)
        ctx.strokePath()
    }

    static func triangle(_ ctx: CGContext, _ a: CGPoint, _ b: CGPoint, _ c: CGPoint, _ color: CGColor) {
        ctx.setFillColor(color)
        ctx.move(to: a)
        ctx.addLine(to: b)
        ctx.addLine(to: c)
        ctx.closePath()
        ctx.fillPath()
    }

    // MARK: - Snowball / snowman

    static func ball(_ ctx: CGContext, _ b: Ball) {
        let r = b.r
        let rect = CGRect(x: b.x - r, y: b.y - r, width: r * 2, height: r * 2)
        ctx.saveGState()
        ctx.addEllipse(in: rect)
        ctx.clip()
        let g = CGGradient(colorsSpace: cs,
                           colors: [col(1, 1, 1), col(0.80, 0.87, 0.97)] as CFArray,
                           locations: [0, 1])!
        ctx.drawRadialGradient(g,
                               startCenter: CGPoint(x: b.x - r * 0.3, y: b.y + r * 0.3), startRadius: 0,
                               endCenter: CGPoint(x: b.x, y: b.y), endRadius: r * 1.15,
                               options: [.drawsAfterEndLocation])
        ctx.restoreGState()
        ctx.setStrokeColor(col(0.55, 0.65, 0.82, 0.45))
        ctx.setLineWidth(1)
        ctx.strokeEllipse(in: rect)

        let coal = col(0.12, 0.12, 0.15)
        let hasAbove = b.above != nil
        let isHead = b.below != nil && b.above == nil

        // Buttons on every ball that has another ball above it
        if hasAbove && r > 9 {
            for i in 0..<3 {
                let by = b.y + r * 0.35 - CGFloat(i) * r * 0.38
                circle(ctx, b.x, by, max(1.2, r * 0.09), coal)
            }
        }
        // Arms on the ball right under the head
        if let up = b.above, up.above == nil, r > 8 {
            let brown = col(0.38, 0.24, 0.14)
            let w = max(1.5, r * 0.09)
            line(ctx, CGPoint(x: b.x - r * 0.85, y: b.y + r * 0.2), CGPoint(x: b.x - r * 1.6, y: b.y + r * 0.75), width: w, brown)
            line(ctx, CGPoint(x: b.x + r * 0.85, y: b.y + r * 0.2), CGPoint(x: b.x + r * 1.6, y: b.y + r * 0.75), width: w, brown)
        }
        // Face and hat
        if isHead && r > 5 {
            let er = max(1.2, r * 0.09)
            circle(ctx, b.x - r * 0.3, b.y + r * 0.2, er, coal)
            circle(ctx, b.x + r * 0.3, b.y + r * 0.2, er, coal)
            triangle(ctx,
                     CGPoint(x: b.x, y: b.y + r * 0.04),
                     CGPoint(x: b.x + r * 0.8, y: b.y - r * 0.1),
                     CGPoint(x: b.x, y: b.y - r * 0.16),
                     col(0.95, 0.45, 0.10))
            for i in -2...2 {
                let sx = b.x + CGFloat(i) * r * 0.17
                let sy = b.y - r * 0.42 + CGFloat(i * i) * r * 0.035
                circle(ctx, sx, sy, max(0.9, r * 0.05), coal)
            }
            fill(ctx, CGRect(x: b.x - r * 0.85, y: b.y + r * 0.86, width: r * 1.7, height: r * 0.14), coal)
            fill(ctx, CGRect(x: b.x - r * 0.5, y: b.y + r * 0.98, width: r * 1.0, height: r * 0.75), coal)
            fill(ctx, CGRect(x: b.x - r * 0.5, y: b.y + r * 0.98, width: r * 1.0, height: r * 0.14), col(0.80, 0.15, 0.18))
        }
    }

    // MARK: - Snow plow vehicles (drawn facing +x, origin = ground contact)

    static func wheel(_ ctx: CGContext, _ cx: CGFloat, _ cy: CGFloat, _ r: CGFloat, _ t: CGFloat) {
        circle(ctx, cx, cy, r, col(0.10, 0.11, 0.14))
        circle(ctx, cx, cy, r * 0.5, col(0.62, 0.65, 0.70))
        for i in 0..<4 {
            let a = t * 6 + CGFloat(i) * CGFloat.pi / 2
            line(ctx, CGPoint(x: cx, y: cy),
                 CGPoint(x: cx + cos(a) * r * 0.45, y: cy + sin(a) * r * 0.45),
                 width: 1.6, col(0.25, 0.27, 0.32))
        }
    }

    static func vehicle(_ ctx: CGContext, kind: PlowKind, t: CGFloat, image: CGImage?) {
        let gray = col(0.58, 0.63, 0.70)
        let dark = col(0.12, 0.13, 0.17)
        switch kind {
        case .tractor:
            let orange = col(0.86, 0.36, 0.22)
            fill(ctx, CGRect(x: -30, y: 14, width: 84, height: 22), orange, radius: 5)
            fill(ctx, CGRect(x: -26, y: 34, width: 34, height: 26), orange, radius: 4)
            fill(ctx, CGRect(x: -22, y: 38, width: 26, height: 18), col(0.75, 0.88, 0.98), radius: 3)
            fill(ctx, CGRect(x: 38, y: 34, width: 5, height: 16), dark)
            wheel(ctx, -14, 17, 17, t)
            wheel(ctx, 44, 10, 10, t)
            line(ctx, CGPoint(x: 52, y: 18), CGPoint(x: 66, y: 12), width: 4, gray)
            fill(ctx, CGRect(x: 64, y: 0, width: 7, height: 28), gray, radius: 2)
        case .snowmobile:
            fill(ctx, CGRect(x: -34, y: 0, width: 62, height: 12), dark, radius: 6)
            fill(ctx, CGRect(x: -30, y: 8, width: 72, height: 18), col(0.20, 0.45, 0.85), radius: 7)
            fill(ctx, CGRect(x: -24, y: 24, width: 30, height: 7), dark, radius: 3)
            triangle(ctx, CGPoint(x: 22, y: 26), CGPoint(x: 34, y: 26), CGPoint(x: 26, y: 38), col(0.75, 0.88, 0.98))
            line(ctx, CGPoint(x: 24, y: 5), CGPoint(x: 70, y: 9), width: 5, dark)
            line(ctx, CGPoint(x: 58, y: 12), CGPoint(x: 78, y: 12), width: 4, gray)
            fill(ctx, CGRect(x: 76, y: 0, width: 7, height: 24), gray, radius: 2)
        case .penguin:
            let bob = abs(sin(t * 9)) * 3
            let navy = col(0.10, 0.12, 0.20)
            let orange = col(0.96, 0.60, 0.15)
            fill(ctx, CGRect(x: -12 + bob, y: 0, width: 12, height: 5), orange, radius: 2)
            fill(ctx, CGRect(x: 2 - bob, y: 0, width: 12, height: 5), orange, radius: 2)
            ctx.setFillColor(navy)
            ctx.fillEllipse(in: CGRect(x: -14, y: 4 + bob, width: 28, height: 34))
            ctx.setFillColor(col(0.96, 0.97, 1.0))
            ctx.fillEllipse(in: CGRect(x: -8, y: 6 + bob, width: 19, height: 26))
            circle(ctx, 2, 42 + bob, 10, navy)
            circle(ctx, 5, 44 + bob, 2.2, col(1, 1, 1))
            circle(ctx, 5.6, 44 + bob, 1, navy)
            triangle(ctx, CGPoint(x: 10, y: 43 + bob), CGPoint(x: 19, y: 40 + bob), CGPoint(x: 10, y: 38 + bob), orange)
            line(ctx, CGPoint(x: 6, y: 22 + bob), CGPoint(x: 52, y: 6), width: 3, col(0.45, 0.30, 0.18))
            fill(ctx, CGRect(x: 52, y: 0, width: 7, height: 22), gray, radius: 2)
        case .custom:
            if let img = image {
                let h: CGFloat = 70
                let aspect = CGFloat(img.width) / max(1, CGFloat(img.height))
                let w = min(110, h * aspect)
                ctx.draw(img, in: CGRect(x: -w * 0.5, y: 0, width: w, height: h))
                line(ctx, CGPoint(x: w * 0.3, y: 14), CGPoint(x: 46, y: 12), width: 4, gray)
                fill(ctx, CGRect(x: 46, y: 0, width: 7, height: 26), gray, radius: 2)
            } else {
                vehicle(ctx, kind: .tractor, t: t, image: nil)
            }
        }
    }
}
