import SwiftUI
import AppKit

struct ClipboardPreviewView: View {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    let item: ClipboardItem
    let onCopy: () -> Bool
    let onClose: () -> Void
    @State private var copied = false
    @State private var copyFailed = false

    var body: some View {
        VStack(spacing: 16) {
            HStack {
                Label(ClipboardPresentation(item.content).title + " preview", systemImage: "arrow.up.left.and.arrow.down.right")
                    .font(.headline)
                Spacer()
                Button(action: onClose) { Image(systemName: "xmark.circle.fill").font(.title2) }
                    .help("Close preview (Esc)")
                    .accessibilityLabel("Close preview")
            }
            previewContent
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            HStack {
                Text("Esc to close").font(.caption).foregroundStyle(.white.opacity(0.55))
                Spacer()
                Button {
                    copied = onCopy()
                    copyFailed = !copied
                } label: {
                    Label(copied ? "Copied" : (copyFailed ? "Copy failed" : "Copy"), systemImage: copied ? "checkmark" : "doc.on.doc")
                }
                .buttonStyle(.borderedProminent)
                .tint(.white.opacity(0.2))
            }
        }
        .padding(20)
        .foregroundStyle(.white)
        .background(Color(white: 0.06))
        .background(.ultraThinMaterial)
        .clipShape(RoundedRectangle(cornerRadius: 18))
        .overlay { RoundedRectangle(cornerRadius: 18).stroke(.white.opacity(0.15)) }
        .padding(12)
        .animation(reduceMotion ? nil : .easeInOut(duration: 0.2), value: copied)
    }

    @ViewBuilder private var previewContent: some View {
        switch item.content {
        case .image(let data):
            if let image = NSImage(data: data) {
                Image(nsImage: image)
                    .resizable().scaledToFit()
            } else {
                ContentUnavailableView("Image unavailable", systemImage: "photo")
            }
        case .text(let text):
            ScrollView {
                Text(text)
                    .font(ClipboardPresentation(item.content).kind == .code ? .system(size: 13, design: .monospaced) : .system(size: 14))
                    .textSelection(.enabled)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(12)
            }
            .background(.white.opacity(0.04))
            .clipShape(RoundedRectangle(cornerRadius: 10))
        }
    }
}

struct PinnedClipboardCard: View {
    let item: ClipboardItem
    let isSelected: Bool
    let copied: Bool
    let onCopy: () -> Void
    let onPreview: () -> Void
    let onSelect: () -> Void
    let onUnpin: () -> Void
    let onDelete: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 5) {
                Image(systemName: "pin.fill")
                Text(copied ? "Copied ✓" : ClipboardPresentation(item.content).title)
                Spacer(minLength: 0)
                Button(action: onCopy) { Image(systemName: "doc.on.doc") }
                    .help("Copy pinned item").accessibilityLabel("Copy pinned item")
            }
            .font(.system(size: 10, weight: .medium))
            .foregroundStyle(copied ? Color.mint : Color.white.opacity(0.65))
            Button {
                onSelect()
                if case .image = item.content { onPreview() } else { onCopy() }
            } label: {
                thumbnail.frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
                    .contentShape(Rectangle())
            }
            .help("Click text to copy; click image to preview")
        }
        .buttonStyle(.plain)
        .padding(10)
        .frame(width: 136, height: 88)
        .foregroundStyle(.white)
        .background(.black.opacity(0.3))
        .background(.ultraThinMaterial)
        .clipShape(RoundedRectangle(cornerRadius: 12))
        .overlay { RoundedRectangle(cornerRadius: 12).stroke(isSelected ? .mint.opacity(0.8) : .white.opacity(0.12), lineWidth: isSelected ? 2 : 1) }
        .contextMenu {
            Button("Copy", action: onCopy)
            Button("Preview", action: onPreview)
            Button("Unpin", action: onUnpin)
            Button("Delete", role: .destructive, action: onDelete)
        }
    }

    @ViewBuilder private var thumbnail: some View {
        switch item.content {
        case .image(let data):
            if let image = NSImage(data: data) {
                Image(nsImage: image).resizable().scaledToFit()
            } else { Image(systemName: "photo") }
        case .text(let text):
            HStack(spacing: 6) {
                if ClipboardPresentation(item.content).kind == .color {
                    RoundedRectangle(cornerRadius: 5).fill(Color(hex: ClipboardPresentation(item.content).detail ?? "FFFFFF"))
                        .frame(width: 22, height: 22)
                }
                Text(text).font(ClipboardPresentation(item.content).kind == .code ? .system(size: 11, design: .monospaced) : .system(size: 11))
                    .lineLimit(2).multilineTextAlignment(.leading)
            }
        }
    }
}

/// Only intercept keys in this popover's window, and unregister on detach.
struct ClipboardKeyboardHandler: NSViewRepresentable {
    var onKey: (NSEvent, Bool) -> Bool

    func makeNSView(context: Context) -> KeyView { KeyView(onKey: onKey) }
    func updateNSView(_ view: KeyView, context: Context) { view.onKey = onKey }

    final class KeyView: NSView {
        var onKey: (NSEvent, Bool) -> Bool
        private var monitor: Any?
        init(onKey: @escaping (NSEvent, Bool) -> Bool) {
            self.onKey = onKey
            super.init(frame: .zero)
        }
        required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            if let monitor { NSEvent.removeMonitor(monitor); self.monitor = nil }
            guard window != nil else { return }
            monitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
                guard let self, let window = self.window, event.window === window else { return event }
                let editing = window.firstResponder is NSTextView || window.firstResponder is NSTextField
                return self.onKey(event, editing) ? nil : event
            }
        }
        deinit { if let monitor { NSEvent.removeMonitor(monitor) } }
    }
}
