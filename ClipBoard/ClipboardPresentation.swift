import Foundation

/// Presentation hints never change the captured clipboard content.
struct ClipboardPresentation {
    enum Kind { case text, link, code, color, image }
    let kind: Kind
    let detail: String?

    init(_ content: ClipboardContent) {
        guard case .text(let text) = content else {
            kind = .image; detail = nil; return
        }
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        let hex = trimmed.hasPrefix("#") ? String(trimmed.dropFirst()) : trimmed
        if [3, 6].contains(hex.count), hex.range(of: "^[0-9a-fA-F]+$", options: .regularExpression) != nil,
           trimmed.hasPrefix("#") || hex.count == 6 {
            kind = .color
            detail = hex.count == 3 ? hex.map { "\($0)\($0)" }.joined() : hex
        } else if let url = URLComponents(string: trimmed),
                  ["http", "https"].contains(url.scheme?.lowercased() ?? ""),
                  let host = url.host, !host.isEmpty, !trimmed.contains(where: { $0.isWhitespace }) {
            kind = .link
            detail = host.hasPrefix("www.") ? String(host.dropFirst(4)) : host
        } else if trimmed.hasPrefix("```") ||
                    trimmed.range(of: "(?m)^\\s*(import [A-Za-z_]|from [A-Za-z_].* import |(let|var|const) \\w+\\s*[:=]|func \\w+\\(|def \\w+\\(|function[ (]|(struct|class|enum) \\w+.*\\{|SELECT .* FROM )", options: .regularExpression) != nil {
            kind = .code; detail = nil
        } else {
            kind = .text; detail = nil
        }
    }

    var title: String {
        switch kind {
        case .text: return "Text"
        case .link: return "Link"
        case .code: return "Code"
        case .color: return "Color"
        case .image: return "Image"
        }
    }
    var symbol: String {
        switch kind {
        case .text: return "text.alignleft"
        case .link: return "link"
        case .code: return "chevron.left.forwardslash.chevron.right"
        case .color: return "paintpalette"
        case .image: return "photo"
        }
    }
}

enum ScrollIntensity: String, CaseIterable {
    case soft = "Soft", balanced = "Balanced", bold = "Bold"
    var strength: Double {
        switch self {
        case .soft: return 0.37
        case .balanced: return 0.65
        case .bold: return 1
        }
    }
}
