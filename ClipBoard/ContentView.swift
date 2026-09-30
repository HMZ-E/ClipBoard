import SwiftUI
import AppKit

struct ContentView: View {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @StateObject private var manager: ClipboardManager
    @State private var searchText = ""
    @State private var showSettings = false
    @State private var selectedID: UUID?
    @State private var navigationStep = 0
    @State private var previewItem: ClipboardItem?
    @State private var copiedID: UUID?
    @State private var copyFailed = false
    @State private var feedbackTask: Task<Void, Never>?
    @FocusState private var searchFocused: Bool
    @AppStorage("meshPreset") private var meshPreset = "Chrome"
    @AppStorage("scrollIntensity") private var scrollIntensity = ScrollIntensity.bold.rawValue
    @AppStorage("customMoodColor1") private var customColor1 = "8080FF"
    @AppStorage("customMoodColor2") private var customColor2 = "FF80BF"
    @AppStorage("customMoodColor3") private var customColor3 = "182038"

    init(manager: ClipboardManager = ClipboardManager()) {
        _manager = StateObject(wrappedValue: manager)
    }

    private var customColors: [Color] {
        [Color(hex: customColor1), Color(hex: customColor2), Color(hex: customColor3)]
    }
    private var visibleItems: [ClipboardItem] {
        manager.items.filter { item in
            guard !searchText.isEmpty else { return true }
            if case .text(let text) = item.content { return text.localizedCaseInsensitiveContains(searchText) }
            return "image screenshot".localizedCaseInsensitiveContains(searchText)
        }.sorted {
            if $0.isPinned != $1.isPinned { return $0.isPinned }
            return $0.createdAt > $1.createdAt
        }
    }
    private var pinnedItems: [ClipboardItem] { visibleItems.filter(\.isPinned) }
    private var historyItems: [ClipboardItem] { visibleItems.filter { !$0.isPinned } }
    private var selectedItem: ClipboardItem? { visibleItems.first { $0.id == selectedID } }

    private func colorBinding(_ hex: Binding<String>) -> Binding<Color> {
        Binding(get: { Color(hex: hex.wrappedValue) }, set: { color in
            guard let rgb = NSColor(color).usingColorSpace(.sRGB) else { return }
            hex.wrappedValue = String(format: "%02X%02X%02X",
                Int((rgb.redComponent * 255).rounded()), Int((rgb.greenComponent * 255).rounded()), Int((rgb.blueComponent * 255).rounded()))
        })
    }

    @discardableResult private func copy(_ item: ClipboardItem) -> Bool {
        selectedID = item.id
        let success = manager.copy(item)
        feedbackTask?.cancel()
        copiedID = success ? item.id : nil
        copyFailed = !success
        feedbackTask = Task { @MainActor in
            try? await Task.sleep(for: .seconds(1.5))
            guard !Task.isCancelled else { return }
            copiedID = nil
            copyFailed = false
        }
        return success
    }

    private func preview(_ item: ClipboardItem) {
        selectedID = item.id
        searchFocused = false
        withAnimation(reduceMotion ? nil : .spring(response: 0.4, dampingFraction: 0.85)) { previewItem = item }
    }
    private func closePreview() {
        withAnimation(reduceMotion ? nil : .spring(response: 0.35, dampingFraction: 0.85)) { previewItem = nil }
    }
    private func navigate(_ direction: Int) {
        guard !visibleItems.isEmpty else { return }
        navigationStep += 1
        searchFocused = false
        if let index = visibleItems.firstIndex(where: { $0.id == selectedID }) {
            selectedID = visibleItems[min(max(index + direction, 0), visibleItems.count - 1)].id
        } else {
            selectedID = direction > 0 ? visibleItems.first?.id : visibleItems.last?.id
        }
    }
    private func handleKey(_ event: NSEvent, editing: Bool) -> Bool {
        let modifiers = event.modifierFlags.intersection([.command, .control, .option, .shift])
        if modifiers == .command, event.charactersIgnoringModifiers?.lowercased() == "z",
           !editing, !showSettings, previewItem == nil, manager.recentlyDeleted != nil {
            manager.undoDelete(); return true
        }
        guard modifiers.isEmpty else { return false }
        if event.keyCode == 53 {
            if previewItem != nil { closePreview(); return true }
            if showSettings { showSettings = false; return true }
            if !searchText.isEmpty { searchText = ""; return true }
            return false
        }
        guard !showSettings else { return false }
        if let previewItem {
            if event.keyCode == 36, !editing { copy(previewItem); return true }
            return false
        }
        if editing {
            // Down exits the single-line search field; typing and caret movement stay native.
            if searchFocused, event.keyCode == 125 { navigate(1); return true }
            return false
        }
        switch event.keyCode {
        case 125: navigate(1); return true
        case 126: navigate(-1); return true
        case 36: if let selectedItem { copy(selectedItem); return true }; return false
        case 49: if let selectedItem { preview(selectedItem); return true }; return false
        default: return false
        }
    }

    private var pinnedShelf: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("PINNED · \(pinnedItems.count)").font(.system(size: 10, weight: .semibold)).foregroundStyle(.white.opacity(0.55))
                .padding(.horizontal, 20)
            ScrollViewReader { reader in
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 8) {
                        ForEach(pinnedItems) { item in
                            PinnedClipboardCard(item: item, isSelected: selectedID == item.id,
                                copied: copiedID == item.id,
                                onCopy: { copy(item) }, onPreview: { preview(item) }, onSelect: { selectedID = item.id },
                                onUnpin: { manager.togglePin(item) }, onDelete: { manager.delete(item) })
                                .id(item.id)
                        }
                    }
                    .padding(.horizontal, 20).padding(.vertical, 2)
                }
                .onChange(of: navigationStep) { _, _ in
                    if let id = selectedID, pinnedItems.contains(where: { $0.id == id }) {
                        withAnimation(reduceMotion ? nil : .easeInOut(duration: 0.2)) { reader.scrollTo(id, anchor: .center) }
                    }
                }
            }
        }
        .padding(.bottom, 10)
    }

    private func historyCard(_ item: ClipboardItem, index: Int, viewportHeight: CGFloat) -> some View {
        ClipboardRowView(item: item, entranceDelay: Double(min(index, 7)) * 0.035,
            isSelected: selectedID == item.id, externallyCopied: copiedID == item.id,
            onCopy: { copy(item) }, onPin: { manager.togglePin(item) }, onDelete: { manager.delete(item) },
            onPreview: { preview(item) }, onSelect: { selectedID = item.id })
            .modifier(ClipboardScrollDepth(viewportHeight: viewportHeight, reduceMotion: reduceMotion,
                strength: (ScrollIntensity(rawValue: scrollIntensity) ?? .bold).strength))
            .zIndex(Double(historyItems.count - index))
            .id(item.id)
            .transition(reduceMotion ? .opacity : .asymmetric(insertion: .opacity.combined(with: .move(edge: .top)),
                removal: .opacity.combined(with: .scale(scale: 0.92))))
    }

    private var clipboardList: some View {
        GeometryReader { viewport in
            ScrollViewReader { reader in
                ScrollView {
                    LazyVStack(spacing: 10) {
                        ForEach(Array(historyItems.enumerated()), id: \.element.id) { index, item in
                            historyCard(item, index: index, viewportHeight: viewport.size.height)
                        }
                        if historyItems.isEmpty {
                            Text(visibleItems.isEmpty ? (searchText.isEmpty ? "Copy something to start your history." : "No matches.") : "Your favorites are in the pinned shelf.")
                                .font(.caption).foregroundStyle(.white.opacity(0.55))
                                .frame(maxWidth: .infinity).padding(.top, 24)
                        }
                    }
                    .animation(reduceMotion ? nil : .spring(response: 0.4, dampingFraction: 0.85), value: historyItems.map(\.id))
                    .padding(.top, 6).padding(.horizontal, 20).padding(.bottom, 28)
                }
                .coordinateSpace(name: "clipboardScroll")
                .mask {
                    LinearGradient(stops: [.init(color: .clear, location: 0), .init(color: .black, location: 0.018),
                        .init(color: .black, location: 0.97), .init(color: .clear, location: 1)], startPoint: .top, endPoint: .bottom)
                }
                .onChange(of: navigationStep) { _, _ in
                    if let id = selectedID, historyItems.contains(where: { $0.id == id }) {
                        withAnimation(reduceMotion ? nil : .easeInOut(duration: 0.2)) { reader.scrollTo(id, anchor: .center) }
                    }
                }
            }
        }
    }

    private var header: some View {
        HStack {
            if NSImage(named: "ClipboardLogo") != nil {
                Image("ClipboardLogo").resizable().scaledToFit().frame(height: 35)
            } else { Text("CLIPBOARD").font(.system(size: 24, weight: .heavy, design: .rounded)).foregroundStyle(.white) }
            Spacer()
            Button { showSettings = true } label: {
                Image(systemName: "slider.horizontal.3").font(.system(size: 18)).foregroundStyle(.white.opacity(0.7))
            }.buttonStyle(.plain).help("Mood and settings")
        }
        .padding(.horizontal, 20).padding(.top, 20).padding(.bottom, 10)
    }
    private var search: some View {
        HStack {
            Image(systemName: "magnifyingglass").foregroundStyle(.gray)
            TextField("Search...", text: $searchText).textFieldStyle(.plain).foregroundStyle(.white).focused($searchFocused)
        }
        .padding(10).background(.black.opacity(0.3)).clipShape(RoundedRectangle(cornerRadius: 10))
        .overlay { RoundedRectangle(cornerRadius: 10).stroke(.white.opacity(0.1)) }
        .padding(.horizontal, 20).padding(.bottom, 12)
    }
    private var footer: some View {
        HStack(spacing: 8) {
            if manager.recentlyDeleted != nil {
                Label("Item deleted", systemImage: "trash").font(.caption)
                Spacer()
                Button("Undo") { manager.undoDelete() }.buttonStyle(.plain).foregroundStyle(.mint)
                Text("⌘Z").foregroundStyle(.white.opacity(0.4))
            } else {
                Spacer(minLength: 0)
                Text(copyFailed ? "Couldn’t copy this item" : "↑↓ Select   ↵ Copy   Space Preview")
                    .foregroundStyle(.white.opacity(0.5))
                Spacer(minLength: 0)
            }
        }
        .font(.system(size: 10)).foregroundStyle(.white)
        .padding(.horizontal, 20).frame(height: 30)
        .background(.black.opacity(manager.recentlyDeleted != nil ? 0.3 : 0))
        .animation(reduceMotion ? nil : .easeInOut(duration: 0.2), value: manager.recentlyDeleted?.id)
    }

    private var settings: some View {
        SettingsView(meshPreset: $meshPreset, scrollIntensity: $scrollIntensity,
            color1: colorBinding($customColor1), color2: colorBinding($customColor2), color3: colorBinding($customColor3),
            onClose: { showSettings = false })
    }

    var body: some View {
        ZStack {
            AnimatedMeshView(colors: MeshGradientHelper.colors(for: meshPreset, custom: customColors))
                .animation(reduceMotion ? nil : .easeInOut(duration: 0.6), value: meshPreset)
                .animation(reduceMotion ? nil : .easeInOut(duration: 0.3), value: customColor1)
                .animation(reduceMotion ? nil : .easeInOut(duration: 0.3), value: customColor2)
                .animation(reduceMotion ? nil : .easeInOut(duration: 0.3), value: customColor3)
            Rectangle().fill(.black.opacity(0.4)).background(.ultraThinMaterial).ignoresSafeArea()
            VStack(spacing: 0) {
                header
                search
                if !pinnedItems.isEmpty { pinnedShelf }
                clipboardList
                footer
            }
            .allowsHitTesting(!showSettings && previewItem == nil)
            if showSettings {
                settings.transition(reduceMotion ? .opacity : .opacity.combined(with: .scale(scale: 0.95)))
            }
            if let previewItem {
                ClipboardPreviewView(item: previewItem, onCopy: { copy(previewItem) }, onClose: closePreview)
                    .id(previewItem.id)
                    .transition(reduceMotion ? .opacity : .opacity.combined(with: .scale(scale: 0.82)))
            }
        }
        .frame(width: 380, height: 600)
        .background { ClipboardKeyboardHandler(onKey: handleKey).frame(width: 0, height: 0) }
        .animation(reduceMotion ? nil : .spring(response: 0.4, dampingFraction: 0.85), value: showSettings)
        .onChange(of: visibleItems.map(\.id)) { _, ids in
            if let selectedID, !ids.contains(selectedID) { self.selectedID = ids.first }
        }
        .onDisappear { feedbackTask?.cancel(); copiedID = nil }
    }
}

private struct ClipboardScrollDepth: ViewModifier {
    let viewportHeight: CGFloat
    let reduceMotion: Bool
    let strength: Double

    func body(content: Content) -> some View {
        content.visualEffect { effect, geometry in
            let frame = geometry.frame(in: .named("clipboardScroll"))
            let height = max(frame.height, 1)
            let inset = 28 * strength
            let above = max(0, inset - frame.minY)
            let below = max(0, frame.maxY - (viewportHeight - inset))
            let progress = min(max(above, below) / height, 1)
            let depth = progress * progress * (3 - 2 * progress)
            let bottomOverflow = max(0, frame.minY - (viewportHeight - 54 * strength))
            let topOverflow = max(0, -frame.maxY + 42 * strength)
            let tuck = below > above ? -bottomOverflow + min(bottomOverflow * 0.22, 24 * strength)
                : topOverflow - min(topOverflow * 0.22, 20 * strength)
            let scale: CGFloat = reduceMotion ? 1 : 1 - depth * 0.15 * strength
            let opacity: Double = reduceMotion ? 1 : Double(1 - depth * 0.45 * strength)
            return effect.scaleEffect(scale, anchor: below > above ? .top : .bottom)
                .opacity(opacity).offset(y: reduceMotion ? 0 : tuck)
        }
    }
}
