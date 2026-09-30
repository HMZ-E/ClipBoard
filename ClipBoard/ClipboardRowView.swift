import SwiftUI

struct ClipboardRowView: View {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    let item: ClipboardItem
    var entranceDelay: Double = 0
    @State private var appeared = false
    @State private var copyPulse = 0
    var isSelected: Bool = false
    var externallyCopied: Bool = false
    var onCopy: () -> Bool
    var onPin: () -> Void
    var onDelete: () -> Void
    var onPreview: () -> Void
    var onSelect: () -> Void
    @State private var isHovered = false
    @State private var expanded = false
    @State private var copied = false
    @State private var copyFailed = false
    @State private var feedbackTask: Task<Void, Never>?

    private var presentation: ClipboardPresentation { ClipboardPresentation(item.content) }

    private var isImage: Bool {
        if case .image = item.content { return true }
        return false
    }

    private func copy() {
        feedbackTask?.cancel()
        copied = onCopy()
        copyFailed = !copied
        if copied { copyPulse += 1 }
        feedbackTask = Task { @MainActor in
            try? await Task.sleep(for: .seconds(1.5))
            guard !Task.isCancelled else { return }
            copied = false
            copyFailed = false
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 6) {
                Image(systemName: presentation.symbol)
                Text(presentation.title)
                if item.isPinned { Image(systemName: "pin.fill").transition(.scale.combined(with: .opacity)) }
                Spacer()
                if copied || externallyCopied {
                    Label("Copied", systemImage: "checkmark")
                        .foregroundStyle(.mint)
                        .transition(.scale(scale: 0.8).combined(with: .opacity))
                } else if copyFailed {
                    Text("Copy failed")
                } else {
                    Text(item.createdAt, style: .relative)
                }
            }
            .font(.system(size: 10, weight: .medium))
            .foregroundStyle(.white.opacity(0.65))

            Button(action: { onSelect(); if isImage { onPreview() } else { copy() } }) {
                Group {
                    switch item.content {
                    case .text(let text):
                        VStack(alignment: .leading, spacing: 6) {
                            if presentation.kind == .link, let domain = presentation.detail {
                                Text(domain).font(.system(size: 11, weight: .semibold)).foregroundStyle(.white.opacity(0.65))
                            }
                            HStack(alignment: .top, spacing: 10) {
                                if presentation.kind == .color {
                                    RoundedRectangle(cornerRadius: 7).fill(Color(hex: presentation.detail ?? "FFFFFF"))
                                        .frame(width: 32, height: 32)
                                        .overlay { RoundedRectangle(cornerRadius: 7).stroke(.white.opacity(0.25)) }
                                }
                                Text(text)
                                    .font(presentation.kind == .code ? .system(size: 12, design: .monospaced) : .system(size: 13))
                                    .lineLimit(expanded ? nil : 3)
                                    .multilineTextAlignment(.leading)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                            }
                        }
                    case .image(let data):
                        if let image = NSImage(data: data) {
                            Image(nsImage: image)
                                .resizable()
                                .scaledToFit()
                                .frame(maxWidth: .infinity)
                                .frame(height: 96)
                                .background(.black.opacity(0.2))
                                .clipShape(RoundedRectangle(cornerRadius: 9))
                        } else {
                            Label("Image unavailable", systemImage: "photo")
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }
                    }
                }
                .foregroundStyle(.white)
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .help(isImage ? "Preview image (Space)" : "Copy to clipboard (Enter)")
            .accessibilityLabel(isImage ? "Preview image" : "Copy text")

            HStack {
                if case .text = item.content {
                    Button(expanded ? "Show less" : "Expand") { onSelect(); expanded.toggle() }
                        .foregroundStyle(.white.opacity(0.65))
                }
                Spacer()
                HStack(spacing: 14) {
                    Button(action: onPreview) { Image(systemName: "arrow.up.left.and.arrow.down.right") }
                        .help("Preview (Space)").accessibilityLabel("Preview")
                    Button(action: copy) { Image(systemName: copied || externallyCopied ? "checkmark" : "doc.on.doc") }
                        .help("Copy")
                        .accessibilityLabel("Copy")
                    Button(action: onPin) { Image(systemName: item.isPinned ? "pin.slash" : "pin") }
                        .help(item.isPinned ? "Unpin" : "Pin")
                        .accessibilityLabel(item.isPinned ? "Unpin" : "Pin")
                    Button(action: onDelete) { Image(systemName: "trash") }
                        .help("Delete")
                        .accessibilityLabel("Delete")
                }
                .opacity(isHovered || isSelected ? 1 : 0)
                .allowsHitTesting(isHovered || isSelected)
                .offset(y: reduceMotion ? 0 : (isHovered || isSelected ? 0 : 4))
            }
            .font(.system(size: 11))
            .foregroundStyle(.white.opacity(0.8))
            .buttonStyle(.plain)
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(.black.opacity(isHovered ? 0.48 : 0.35))
        .background(.ultraThinMaterial)
        .clipShape(RoundedRectangle(cornerRadius: 14))
        .overlay {
            RoundedRectangle(cornerRadius: 14)
                .stroke(copied || externallyCopied || isSelected ? Color.mint.opacity(0.7) : Color.white.opacity(isHovered ? 0.3 : 0.1), lineWidth: isSelected ? 2 : 1)
        }
        .shadow(color: .black.opacity(isHovered ? 0.25 : 0), radius: isHovered ? 10 : 0, y: 5)
        .scaleEffect(reduceMotion ? 1 : (isHovered ? 1.012 : 1))
        .phaseAnimator([false, true, false], trigger: copyPulse) { content, pulse in
            content.scaleEffect(reduceMotion ? 1 : (pulse ? 1.025 : 1))
        } animation: { _ in
            reduceMotion ? .linear(duration: 0) : .spring(response: 0.25, dampingFraction: 0.65)
        }
        .opacity(appeared ? 1 : 0)
        .offset(y: reduceMotion || appeared ? 0 : 14)
        .task {
            if !reduceMotion {
                try? await Task.sleep(for: .seconds(entranceDelay))
                guard !Task.isCancelled else { return }
            }
            withAnimation(reduceMotion ? nil : .spring(response: 0.45, dampingFraction: 0.8)) {
                appeared = true
            }
        }
        .contentShape(RoundedRectangle(cornerRadius: 14))
        .onTapGesture { onSelect(); if isImage { onPreview() } else { copy() } }
        .onHover { isHovered = $0 }
        .animation(reduceMotion ? nil : .spring(response: 0.3, dampingFraction: 0.75), value: isHovered)
        .animation(reduceMotion ? nil : .spring(response: 0.35, dampingFraction: 0.85), value: expanded)
        .animation(reduceMotion ? nil : .easeInOut(duration: 0.2), value: copied)
        .animation(reduceMotion ? nil : .spring(response: 0.3), value: item.isPinned)
        .contextMenu {
            Button("Copy", action: copy)
            Button("Preview", action: onPreview)
            Button(item.isPinned ? "Unpin" : "Pin", action: onPin)
            Button("Delete", role: .destructive, action: onDelete)
        }
        .onDisappear { feedbackTask?.cancel() }
    }
}
