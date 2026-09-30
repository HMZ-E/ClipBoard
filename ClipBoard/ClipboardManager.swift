import SwiftUI
import AppKit
import Combine

class ClipboardManager: ObservableObject {
    @Published var items: [ClipboardItem] = []

    private var timer: Timer?
    private var lastChangeCount: Int

    private let historyURL: URL
    private let pasteboard: NSPasteboard
    @Published private(set) var recentlyDeleted: ClipboardItem?
    private var deletedIndex = 0
    private var undoTask: Task<Void, Never>?

    init(historyURL: URL? = nil, pasteboard: NSPasteboard = .general, monitor: Bool = true) {
        self.historyURL = historyURL ?? FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
            .appendingPathComponent("clipboard_history.json")
        self.pasteboard = pasteboard
        self.lastChangeCount = self.pasteboard.changeCount
        loadHistory()
        if monitor { startMonitoring() }
    }

    deinit {
        timer?.invalidate()
        undoTask?.cancel()
    }

    private func startMonitoring() {
        timer = Timer.scheduledTimer(withTimeInterval: 1.5, repeats: true) { [weak self] _ in
            self?.checkPasteboard()
        }
    }

    private func checkPasteboard() {
        guard pasteboard.changeCount != lastChangeCount else { return }

        lastChangeCount = pasteboard.changeCount

        // 1. Check for Images first
        if let image = pasteboard.readObjects(forClasses: [NSImage.self], options: nil)?.first as? NSImage {
            if let tiff = image.tiffRepresentation,
               let bitmap = NSBitmapImageRep(data: tiff),
               let pngData = bitmap.representation(using: .png, properties: [:]) {
                
                let newItem = ClipboardItem(content: .image(pngData), createdAt: Date())
                add(newItem)
            }
        }
        // 2. Check for Text
        else if let text = pasteboard.string(forType: .string) {
            if !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                let newItem = ClipboardItem(content: .text(text), createdAt: Date())
                // Prevent duplicate consecutive copies
                if case .text(let lastText) = items.first?.content, lastText == text { return }
                add(newItem)
            }
        }
    }

    private func add(_ item: ClipboardItem) {
        DispatchQueue.main.async {
            self.items.insert(item, at: 0)
            self.lastChangeCount = self.pasteboard.changeCount
            while self.items.filter({ !$0.isPinned }).count > 50 {
                guard let index = self.items.lastIndex(where: { !$0.isPinned }) else { break }
                self.items.remove(at: index)
            }
            self.saveHistory()
        }
    }

    func copy(_ item: ClipboardItem) -> Bool {
        let success: Bool
        switch item.content {
        case .text(let text):
            pasteboard.clearContents()
            success = pasteboard.setString(text, forType: .string)
        case .image(let data):
            guard let image = NSImage(data: data) else { return false }
            pasteboard.clearContents()
            success = pasteboard.writeObjects([image])
        }
        lastChangeCount = pasteboard.changeCount
        return success
    }

    func togglePin(_ item: ClipboardItem) {
        guard let index = items.firstIndex(where: { $0.id == item.id }) else { return }
        items[index].isPinned.toggle()
        saveHistory()
    }

    func delete(_ item: ClipboardItem) {
        guard let index = items.firstIndex(where: { $0.id == item.id }) else { return }
        undoTask?.cancel()
        recentlyDeleted = items.remove(at: index)
        deletedIndex = index
        saveHistory()
        undoTask = Task { @MainActor [weak self] in
            try? await Task.sleep(for: .seconds(6))
            guard !Task.isCancelled else { return }
            self?.recentlyDeleted = nil
        }
    }

    func undoDelete() {
        guard let item = recentlyDeleted else { return }
        undoTask?.cancel()
        items.insert(item, at: min(deletedIndex, items.count))
        recentlyDeleted = nil
        saveHistory()
    }

    func clearHistory() {
        undoTask?.cancel()
        recentlyDeleted = nil
        items.removeAll()
        saveHistory()
    }

    private func saveHistory() {
        do {
            let data = try JSONEncoder().encode(items)
            try FileManager.default.createDirectory(at: historyURL.deletingLastPathComponent(), withIntermediateDirectories: true)
            try data.write(to: historyURL, options: .atomic)
        } catch {
            print("Failed to save history: \(error)")
        }
    }

    private func loadHistory() {
        do {
            if FileManager.default.fileExists(atPath: historyURL.path) {
                let data = try Data(contentsOf: historyURL)
                items = try JSONDecoder().decode([ClipboardItem].self, from: data)
            }
        } catch {
            print("Failed to load history: \(error)")
        }
    }
}
