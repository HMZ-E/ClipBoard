import SwiftUI
import AppKit
import Combine

class ClipboardManager: ObservableObject {
    @Published var items: [ClipboardItem] = []

    private var timer: Timer?
    private var lastChangeCount: Int

    // Saves to a file instead of UserDefaults (Much safer!)
    private let historyURL = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        .appendingPathComponent("clipboard_history.json")

    init() {
        self.lastChangeCount = NSPasteboard.general.changeCount
        loadHistory()
        startMonitoring()
    }

    private func startMonitoring() {
        timer = Timer.scheduledTimer(withTimeInterval: 1.5, repeats: true) { [weak self] _ in
            self?.checkPasteboard()
        }
    }

    private func checkPasteboard() {
        guard NSPasteboard.general.changeCount != lastChangeCount else { return }

        // 1. Check for Images first
        if let image = NSPasteboard.general.readObjects(forClasses: [NSImage.self], options: nil)?.first as? NSImage {
            if let tiff = image.tiffRepresentation,
               let bitmap = NSBitmapImageRep(data: tiff),
               let pngData = bitmap.representation(using: .png, properties: [:]) {
                
                let newItem = ClipboardItem(content: .image(pngData), createdAt: Date())
                add(newItem)
            }
        }
        // 2. Check for Text
        else if let text = NSPasteboard.general.string(forType: .string) {
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
            self.lastChangeCount = NSPasteboard.general.changeCount
            if self.items.count > 50 { self.items.removeLast() } // Keep last 50 items
            self.saveHistory()
        }
    }

    func clearHistory() {
        items.removeAll()
        saveHistory()
    }

    private func saveHistory() {
        do {
            let data = try JSONEncoder().encode(items)
            try FileManager.default.createDirectory(at: historyURL.deletingLastPathComponent(), withIntermediateDirectories: true)
            try data.write(to: historyURL)
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
