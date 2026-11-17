import AppKit

final class SnowView: NSView {
    override var isOpaque: Bool { return false }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { return true }

    override func draw(_ dirtyRect: NSRect) {
        guard let ctx = NSGraphicsContext.current?.cgContext, let win = window else { return }
        ctx.clear(bounds)
        let f = win.frame
        ctx.translateBy(x: -f.minX, y: -f.minY)   // simulation works in global screen coordinates
        Simulation.shared.draw(in: ctx, screenFrame: f)
    }

    // Mouse events arrive only while the cursor is over snow in snowball mode
    // (the simulation switches the overlay between click-through and interactive).
    override func mouseDown(with event: NSEvent) { Simulation.shared.mouseDown() }
    override func mouseUp(with event: NSEvent) { Simulation.shared.mouseUp() }
}
